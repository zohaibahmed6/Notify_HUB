using System.Net;
using System.Net.Http.Json;
using NotifyHub.Api.Templates.Dtos;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// §6b: Templates & reminder rules screen needs edit, not just create — covers the new
/// PATCH /api/templates/{id} endpoint added in step 6.
public class TemplatesControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Update_AppliesOnlyProvidedFields()
    {
        var (client, _) = await _client.AsStaffAsync();

        var created = await client.PostAsJsonAsync("/api/templates", new CreateTemplateRequest
        {
            Name = "Edit-test template",
            Body = "Original body",
            TriggerType = "AppointmentReminder",
            OffsetHours = 48,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var original = await created.Content.ReadFromJsonAsync<TemplateDto>();

        var patched = await client.PatchAsJsonAsync($"/api/templates/{original!.Id}", new UpdateTemplateRequest
        {
            Body = "Updated body",
            OffsetHours = 24,
        });

        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
        var updated = await patched.Content.ReadFromJsonAsync<TemplateDto>();

        Assert.Equal("Edit-test template", updated!.Name); // untouched field preserved
        Assert.Equal("Updated body", updated.Body);
        Assert.Equal(24, updated.OffsetHours);
        Assert.Equal("AppointmentReminder", updated.TriggerType);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_ForUnknownId()
    {
        var (client, _) = await _client.AsStaffAsync();

        var response = await client.PatchAsJsonAsync("/api/templates/9999999", new UpdateTemplateRequest { Name = "Nope" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_DefaultsToActive()
    {
        var (client, _) = await _client.AsStaffAsync();

        var created = await client.PostAsJsonAsync("/api/templates", new CreateTemplateRequest
        {
            Name = "Default-active template",
            Body = "Body",
            TriggerType = "MedicationAlert",
            OffsetHours = 1,
        });
        var dto = await created.Content.ReadFromJsonAsync<TemplateDto>();

        Assert.True(dto!.IsActive);
    }

    [Fact]
    public async Task Update_SetsIsActive_AndListFilterRespectsIt()
    {
        var (client, _) = await _client.AsStaffAsync();

        var created = await client.PostAsJsonAsync("/api/templates", new CreateTemplateRequest
        {
            Name = "Deactivate-test template",
            Body = "Body",
            TriggerType = "MedicationAlert",
            OffsetHours = 1,
        });
        var original = await created.Content.ReadFromJsonAsync<TemplateDto>();

        var patched = await client.PatchAsJsonAsync($"/api/templates/{original!.Id}", new UpdateTemplateRequest { IsActive = false });
        var updated = await patched.Content.ReadFromJsonAsync<TemplateDto>();
        Assert.False(updated!.IsActive);

        var activeOnly = await client.GetFromJsonAsync<List<TemplateDto>>("/api/templates?isActive=true");
        Assert.DoesNotContain(activeOnly!, t => t.Id == original.Id);

        var inactiveOnly = await client.GetFromJsonAsync<List<TemplateDto>>("/api/templates?isActive=false");
        Assert.Contains(inactiveOnly!, t => t.Id == original.Id);
    }

    [Fact]
    public async Task Update_RejectsInvalidTriggerType()
    {
        var (client, _) = await _client.AsStaffAsync();

        var created = await client.PostAsJsonAsync("/api/templates", new CreateTemplateRequest
        {
            Name = "Invalid-trigger-test template",
            Body = "Body",
            TriggerType = "MedicationAlert",
            OffsetHours = 1,
        });
        var original = await created.Content.ReadFromJsonAsync<TemplateDto>();

        var response = await client.PatchAsJsonAsync($"/api/templates/{original!.Id}", new UpdateTemplateRequest
        {
            TriggerType = "NotARealType",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
