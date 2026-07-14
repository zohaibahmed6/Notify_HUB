using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NotifyHub.Api.Common;
using NotifyHub.Api.Inbox;
using NotifyHub.Api.Users.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Domain.Validation;
using NotifyHub.Infrastructure.Auditing;
using NotifyHub.Infrastructure.Persistence;
using NotifyHub.Infrastructure.Users;

namespace NotifyHub.Api.Controllers;

/// User management (create/list/status). List/Create/status changes are Admin-only;
/// `assignable` is open to any authenticated user since every assignee-picker in the app
/// (task forward, thread assign, task assign) needs to source its options from here.
[ApiController]
[Route("api/users")]
public class UsersController(NotifyHubDbContext db, IPasswordHasher<User> passwordHasher, IHubContext<InboxHub> inboxHub) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PagedResult<UserDto>>> List(
        string? role, string? status, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        (page, pageSize) = PagedResult<UserDto>.Clamp(page, pageSize);

        var query = db.Users.AsQueryable();

        if (role is not null)
        {
            if (!Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsedRole))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: $"Invalid role '{role}'. Valid values: {string.Join(", ", Enum.GetNames<UserRole>())}.");
            }

            query = query.Where(u => u.Role == parsedRole);
        }

        if (status is not null)
        {
            if (!Enum.TryParse<UserStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: $"Invalid status '{status}'. Valid values: {string.Join(", ", Enum.GetNames<UserStatus>())}.");
            }

            query = query.Where(u => u.Status == parsedStatus);
        }

        query = query.OrderBy(u => u.Id);

        var totalCount = await query.CountAsync(ct);
        var users = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return Ok(new PagedResult<UserDto> { Items = users.Select(ToDto).ToList(), Page = page, PageSize = pageSize, TotalCount = totalCount });
    }

    /// Excludes Inactive/OnLeave users (§7): they must not receive new tasks or messages,
    /// so they're never offered as an assignment target.
    [HttpGet("assignable")]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> Assignable(CancellationToken ct)
    {
        var users = await db.Users
            .Where(u => u.Status == UserStatus.Active)
            .OrderBy(u => u.FullName ?? u.Username)
            .ToListAsync(ct);

        return Ok(users.Select(ToDto).ToList());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Username is required.");

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: $"Invalid role '{request.Role}'. Valid values: {string.Join(", ", Enum.GetNames<UserRole>())}.");
        }

        if (!PasswordPolicy.IsValid(request.Password, out var failures))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: $"Password does not meet the password policy: {string.Join(" ", failures)}");
        }

        if (await db.Users.AnyAsync(u => u.Username == request.Username, ct))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: $"Username '{request.Username}' is already taken.");

        var user = new User { Username = request.Username, FullName = request.FullName, Role = role, Status = UserStatus.Active };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return Created($"/api/users/{user.Id}", ToDto(user));
    }

    /// §7: transitioning to Inactive/OnLeave auto-forwards this user's open (non-terminal)
    /// tasks to a fallback Active Admin, atomically with the status change itself — the
    /// admin performing the deactivation gets immediate, deterministic feedback rather than
    /// a polling-delay race with EscalationJob (see CODEBASE_MAP.md's documented pre-existing
    /// no-optimistic-concurrency limitation, unaffected by this change).
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDto>> UpdateStatus(long id, UpdateUserStatusRequest request, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
            return NotFound();

        if (!Enum.TryParse<UserStatus>(request.Status, ignoreCase: true, out var newStatus))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: $"Invalid status '{request.Status}'. Valid values: {string.Join(", ", Enum.GetNames<UserStatus>())}.");
        }

        // P9-12: both required together when marking OnLeave.
        if (newStatus == UserStatus.OnLeave && (request.LeaveFrom is null || request.LeaveTo is null))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "LeaveFrom and LeaveTo are both required when Status is OnLeave.");
        }

        if (request.LeaveFrom is not null && request.LeaveTo is not null && request.LeaveFrom > request.LeaveTo)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "LeaveFrom must not be after LeaveTo.");
        }

        var previousStatus = user.Status;
        user.Status = newStatus;

        if (newStatus == UserStatus.OnLeave)
        {
            user.LeaveFrom = request.LeaveFrom;
            user.LeaveTo = request.LeaveTo;
        }

        var forwardedTaskIds = new List<long>();
        long? forwardedToAdminId = null;

        if (previousStatus == UserStatus.Active && newStatus is UserStatus.Inactive or UserStatus.OnLeave)
        {
            forwardedToAdminId = await FallbackUserResolver.ResolveFallbackAdminIdAsync(db, ct, excludeUserId: id);

            if (forwardedToAdminId is not null)
            {
                var openTasks = await db.Tasks
                    .Where(t => t.AssignedStaffId == id
                        && t.Status != NotifyHubTaskStatus.Completed
                        && t.Status != NotifyHubTaskStatus.Cancelled)
                    .ToListAsync(ct);

                foreach (var task in openTasks)
                {
                    task.AssignedStaffId = forwardedToAdminId;
                    AuditLogger.Add(db, actor: "system", action: "forward", entityType: "TaskItem", entityId: task.Id,
                        detail: $"auto-forwarded: assignee marked {newStatus}");
                    forwardedTaskIds.Add(task.Id);
                }
            }
        }

        await db.SaveChangesAsync(ct);

        foreach (var taskId in forwardedTaskIds)
        {
            await inboxHub.Clients.All.SendAsync("taskAssignmentChanged", new { taskId, assignedStaffId = forwardedToAdminId }, ct);
        }

        return Ok(ToDto(user));
    }

    private static UserDto ToDto(User u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        FullName = u.FullName,
        Role = u.Role.ToString(),
        Status = u.Status.ToString(),
        LeaveFrom = u.LeaveFrom,
        LeaveTo = u.LeaveTo,
    };
}
