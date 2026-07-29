using System.Net;
using BotFramework.Host.Admin.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CasinoShiz.Tests;

public sealed class ChatAdministrationAdminPageE2EApplication(WebApplication app, Uri address) : IAsyncDisposable
{
    public HttpClient CreateClient() => new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        BaseAddress = address,
    };

    public async ValueTask DisposeAsync()
    {
        await app.StopAsync();
        await app.DisposeAsync();
    }
}
