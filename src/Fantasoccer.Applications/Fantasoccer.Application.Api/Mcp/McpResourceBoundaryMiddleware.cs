namespace Fantasoccer.Application.Api.Mcp;

// A token issued to an MCP connection is valid for the MCP resource only: everywhere else in the API it is refused,
// even while MCP is disabled and whatever the account could otherwise do.
public sealed class McpResourceBoundaryMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true && context.User.HasClaim(x => x.Type == McpOptions.ResourceClaim) &&
            !context.Request.Path.StartsWithSegments("/mcp"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        await next(context);
    }
}
