using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace BotFramework.Rest;

public static class RestHttpContextExtensions
{
    public static RestRequestContext GetRestRequestContext(this HttpContext context) =>
        context.RequestServices.GetRequiredService<RestRequestContext>();
}