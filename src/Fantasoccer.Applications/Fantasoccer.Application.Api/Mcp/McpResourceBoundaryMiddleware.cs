using System.Text.Json;

namespace Fantasoccer.Application.Api.Mcp;

// Membership/identity documents must not remain writable through an unguarded
// legacy URL: they are the authorization anchors used by both surfaces, even
// while MCP is disabled.
public sealed class McpResourceBoundaryMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, McpEndpointCatalog catalog, McpAccessGuard guard)
    {
        if (context.User.Identity?.IsAuthenticated != true) { await next(context); return; }
        if (context.User.HasClaim(x => x.Type == McpOptions.ResourceClaim) && !context.Request.Path.StartsWithSegments("/mcp"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        var path = context.Request.Path;
        if (!(path.StartsWithSegments("/api/AppIdentity") || path.StartsWithSegments("/api/Group") || path.StartsWithSegments("/LeagueManager")))
        {
            await next(context);
            return;
        }
        var template = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText?.TrimStart('/');
        var operation = catalog.Operations.Values.FirstOrDefault(x => x.Api.HttpMethod == context.Request.Method && x.Api.RelativePath == template);
        if (operation == null) { await next(context); return; } // Existing routing handles unknown paths.
        var arguments = new Dictionary<string, JsonElement>
        {
            ["query"] = JsonSerializer.SerializeToElement(context.Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString()))
        };
        // The JS repository client sets JSON Content-Type even on bodyless GETs.
        // A media type alone is not evidence of a payload; parsing an empty stream
        // turns valid group/identity reads into 400 responses.
        var canHaveBody = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpRequestBodyDetectionFeature>()?.CanHaveBody
            ?? context.Request.ContentLength > 0;
        if (context.Request.HasJsonContentType() && canHaveBody)
        {
            context.Request.EnableBuffering(bufferThreshold: 30 * 1024, bufferLimit: 4 * 1024 * 1024);
            try
            {
                arguments["body"] = await JsonSerializer.DeserializeAsync<JsonElement>(context.Request.Body, cancellationToken: context.RequestAborted);
            }
            catch (JsonException) { context.Response.StatusCode = StatusCodes.Status400BadRequest; return; }
            catch (IOException) { context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge; return; }
            finally { context.Request.Body.Position = 0; }
        }
        bool allowed;
        try { allowed = await guard.AllowsAsync(operation, arguments, context.User, context.RequestAborted); }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        if (!allowed)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        await next(context);
    }
}
