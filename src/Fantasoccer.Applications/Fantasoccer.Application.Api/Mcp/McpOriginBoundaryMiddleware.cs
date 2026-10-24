using OpenIddict.Abstractions;

namespace Fantasoccer.Application.Api.Mcp;

public sealed class McpOriginBoundaryMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, McpOptions settings,
        IOpenIddictAuthorizationManager authorizations, IOpenIddictApplicationManager applications)
    {
        if (context.Request.Path.StartsWithSegments("/mcp"))
        {
            var bodyLimit = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if (bodyLimit is { IsReadOnly: false }) bodyLimit.MaxRequestBodySize = 8 * 1024 * 1024;
            if (context.Request.ContentLength > 8 * 1024 * 1024)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return;
            }
            var origin = context.Request.Headers.Origin.ToString();
            var allowed = origin.Length == 0 || origin == new Uri(settings.FrontendUrl).GetLeftPart(UriPartial.Authority) ||
                          origin == new Uri(settings.Issuer).GetLeftPart(UriPartial.Authority);
            if (!allowed && context.User.GetAuthorizationId() is { } id &&
                await authorizations.FindByIdAsync(id, context.RequestAborted) is { } grant &&
                await authorizations.GetApplicationIdAsync(grant, context.RequestAborted) is { } clientId &&
                await applications.FindByIdAsync(clientId, context.RequestAborted) is { } client)
                allowed = (await applications.GetRedirectUrisAsync(client, context.RequestAborted))
                    .Any(x => new Uri(x).GetLeftPart(UriPartial.Authority) == origin);
            if (!allowed)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }
        await next(context);
    }
}
