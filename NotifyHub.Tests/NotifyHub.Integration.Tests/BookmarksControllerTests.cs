using System.Net;
using System.Net.Http.Json;
using NotifyHub.Api.Bookmarks.Dtos;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// §5: admin-managed snippet library consumed by the template create/edit dropdown.
public class BookmarksControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task List_AnyAuthenticatedUser_SeesSeededBookmarks()
    {
        var (client, _) = await _client.AsStaffAsync();

        var bookmarks = await client.GetFromJsonAsync<List<BookmarkDto>>("/api/bookmarks");

        Assert.Contains(bookmarks!, b => b.InsertText == "{{patient_name}}");
    }

    [Fact]
    public async Task Create_AsAdmin_Succeeds()
    {
        var (client, _) = await _client.AsAdminAsync();

        var response = await client.PostAsJsonAsync("/api/bookmarks", new CreateBookmarkRequest
        {
            Label = "Test Snippet",
            Description = "A test snippet",
            InsertText = "Thanks for reaching out!",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<BookmarkDto>();
        Assert.Equal("Test Snippet", dto!.Label);
    }

    [Fact]
    public async Task Create_AsStaff_Forbidden()
    {
        var (client, _) = await _client.AsStaffAsync();

        var response = await client.PostAsJsonAsync("/api/bookmarks", new CreateBookmarkRequest
        {
            Label = "Should not be created",
            Description = "x",
            InsertText = "x",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsAdmin_RemovesBookmark()
    {
        var (client, _) = await _client.AsAdminAsync();

        var created = await client.PostAsJsonAsync("/api/bookmarks", new CreateBookmarkRequest
        {
            Label = "Delete-test",
            Description = "x",
            InsertText = "x",
        });
        var dto = await created.Content.ReadFromJsonAsync<BookmarkDto>();

        var response = await client.DeleteAsync($"/api/bookmarks/{dto!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var bookmarks = await client.GetFromJsonAsync<List<BookmarkDto>>("/api/bookmarks");
        Assert.DoesNotContain(bookmarks!, b => b.Id == dto.Id);
    }
}
