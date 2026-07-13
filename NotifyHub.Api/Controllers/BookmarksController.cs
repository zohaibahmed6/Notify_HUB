using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotifyHub.Api.Bookmarks.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Api.Controllers;

/// §5: admin-managed snippet library, consumed by the template create/edit dropdown.
/// Listing is open to any authenticated user (Staff also builds templates, same
/// reasoning as TemplatesController); mutations are Admin-only.
[ApiController]
[Route("api/bookmarks")]
public class BookmarksController(NotifyHubDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BookmarkDto>>> List(CancellationToken ct)
    {
        var bookmarks = await db.Bookmarks.OrderBy(b => b.Label).ToListAsync(ct);
        return Ok(bookmarks.Select(ToDto).ToList());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BookmarkDto>> Create(CreateBookmarkRequest request, CancellationToken ct)
    {
        var bookmark = new Bookmark { Label = request.Label, Description = request.Description, InsertText = request.InsertText };
        db.Bookmarks.Add(bookmark);
        await db.SaveChangesAsync(ct);

        return Created($"/api/bookmarks/{bookmark.Id}", ToDto(bookmark));
    }

    [HttpPatch("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BookmarkDto>> Update(long id, UpdateBookmarkRequest request, CancellationToken ct)
    {
        var bookmark = await db.Bookmarks.SingleOrDefaultAsync(b => b.Id == id, ct);
        if (bookmark is null)
            return NotFound();

        if (request.Label is not null)
            bookmark.Label = request.Label;

        if (request.Description is not null)
            bookmark.Description = request.Description;

        if (request.InsertText is not null)
            bookmark.InsertText = request.InsertText;

        await db.SaveChangesAsync(ct);
        return Ok(ToDto(bookmark));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Delete(long id, CancellationToken ct)
    {
        var bookmark = await db.Bookmarks.SingleOrDefaultAsync(b => b.Id == id, ct);
        if (bookmark is null)
            return NotFound();

        db.Bookmarks.Remove(bookmark);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static BookmarkDto ToDto(Bookmark b) => new()
    {
        Id = b.Id,
        Label = b.Label,
        Description = b.Description,
        InsertText = b.InsertText,
    };
}
