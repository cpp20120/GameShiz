using Microsoft.AspNetCore.Http;

namespace BotFramework.Rest;

internal sealed class RestRequestContextEndpointFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        _ = context.HttpContext.GetRestRequestContext();
        return next(context);
    }
}