using CasinoShiz.Identity.Transport.Grpc;
using CasinoShiz.AdminBff.Pages;
using CasinoShiz.Wallet.Transport.Grpc;
using Games.Admin.Transport.Grpc;
using CasinoShiz.Operations.Transport.Grpc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "casinoshiz.admin";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.IdleTimeout = TimeSpan.FromHours(8);
});

var backend = new Uri(builder.Configuration["Services:Backend:Address"] ?? "http://localhost:5081");
var wallet = new Uri(builder.Configuration["Services:Wallet:Address"] ?? backend.ToString());
var identity = new Uri(builder.Configuration["Services:Identity:Address"] ?? backend.ToString());
builder.Services.AddHttpClient("legacy-admin", client => client.BaseAddress = backend);
builder.Services.AddAdminGrpcClients(backend);
builder.Services.AddWalletGrpcClients(wallet);
builder.Services.AddIdentityGrpcClient(identity);
builder.Services.AddOperationsGrpcClient(backend, builder.Configuration["Services:Operations:ApiKey"] ?? "");

var app = builder.Build();
app.UseStaticFiles();
app.UseSession();
app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/admin")
        || context.Request.Path.StartsWithSegments("/admin/login")
        || context.Request.Path.StartsWithSegments("/admin/logout"))
    {
        await next();
        return;
    }

    if (!context.Session.IsAuthenticated())
    {
        context.Response.Redirect("/admin/login");
        return;
    }

    var target = context.Request.Path + context.Request.QueryString;
    using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), target);
    if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
        request.Content = new StreamContent(context.Request.Body);
    foreach (var header in context.Request.Headers)
    {
        if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase)
            || string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
        if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
    }
    request.Headers.TryAddWithoutValidation("x-admin-api-key", builder.Configuration["Services:Operations:ApiKey"] ?? "");
    request.Headers.TryAddWithoutValidation("x-admin-actor-id", context.Session.ActorId().ToString(System.Globalization.CultureInfo.InvariantCulture));
    request.Headers.TryAddWithoutValidation("x-admin-actor-name", context.Session.ActorName());
    request.Headers.TryAddWithoutValidation("x-admin-role", context.Session.ActorRole());

    var client = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("legacy-admin");
    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
    context.Response.StatusCode = (int)response.StatusCode;
    foreach (var header in response.Headers.Concat(response.Content.Headers))
    {
        if (string.Equals(header.Key, "transfer-encoding", StringComparison.OrdinalIgnoreCase)) continue;
        context.Response.Headers[header.Key] = header.Value.ToArray();
    }
    await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
});
app.MapRazorPages();
app.MapGet("/", () => Results.Redirect("/admin"));
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy", service = "casinoshiz-admin-bff" }));
await app.RunAsync();
