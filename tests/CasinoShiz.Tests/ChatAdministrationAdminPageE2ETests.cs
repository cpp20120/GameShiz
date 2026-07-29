using System.Net;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(ChatAdministrationAdminPageE2ETestCollection.Name)]
public sealed class ChatAdministrationAdminPageE2ETests(ChatAdministrationAdminPageE2EFixture fixture)
{
    [Fact]
    public async Task AdminPage_RendersChatAdministrationReadModelFromPostgres()
    {
        using var client = fixture.Application.CreateClient();

        using var login = await client.GetAsync("/e2e-login");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var response = await client.GetAsync(
            "/admin/chat-administration?chatId=770001");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.Contains("E2E Moderation Group", StringComparison.Ordinal), body);
        Assert.Contains("Moderation cases", body, StringComparison.Ordinal);
        Assert.Contains("flood from integration test", body, StringComparison.Ordinal);
        Assert.Contains("RestrictMember", body, StringComparison.Ordinal);
        Assert.Contains("moderation.case.applied", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminPage_RequiresAdminSession()
    {
        using var client = fixture.Application.CreateClient();

        using var response = await client.GetAsync("/admin/chat-administration");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/admin/login", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
