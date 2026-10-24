using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using ModelContextProtocol.Protocol;

namespace Fantasoccer.Application.Api.Mcp;

// Dispatches to the real endpoints IN PROCESS. No loopback HTTP, no caller-selected URL,
// no controller reflection calls, no repository calls that skip the HTTP authorization layer.
public sealed class McpApiFacade(McpEndpointCatalog catalog, IServiceScopeFactory scopes,
    IHttpContextAccessor contexts, ILogger<McpApiFacade> logger)
{
    private RequestDelegate? _pipeline;

    public void Initialize(WebApplication app)
    {
        var pipeline = ((IApplicationBuilder)app).New();
        pipeline.UseRouting();
        pipeline.UseAuthorization();
        pipeline.UseMiddleware<TeamRepositoryBoundaryMiddleware>();
        pipeline.UseEndpoints(endpoints =>
        {
            foreach (var source in ((IEndpointRouteBuilder)app).DataSources) endpoints.DataSources.Add(source);
        });
        _pipeline = pipeline.Build();
    }

    public async Task<CallToolResult> CallAsync(CallToolRequestParams request, CancellationToken cancellationToken)
    {
        var parent = contexts.HttpContext;
        if (parent?.User.Identity?.IsAuthenticated != true) return Error("Authentication required.");
        if (!catalog.Operations.TryGetValue(request.Name, out var operation)) return Error("Unknown tool.");
        var args = request.Arguments ?? new Dictionary<string, JsonElement>();
        if (args.Keys.Any(x => x is not ("query" or "route" or "body" or "files"))) return Error("Unknown argument.");
        if (args.Any(x => x.Key != "body" && x.Value.ValueKind != JsonValueKind.Object)) return Error("query, route and files must be objects.");
        await using var scope = scopes.CreateAsyncScope();
        try
        {
            if (!await scope.ServiceProvider.GetRequiredService<McpAccessGuard>().AllowsAsync(operation, args, parent.User, cancellationToken))
                return Error("Forbidden: this operation or resource is not permitted for your account.");

            var context = new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider,
                User = parent.User.Clone(),
                RequestAborted = cancellationToken
            };
            context.Request.Method = operation.Api.HttpMethod!;
            context.Request.Scheme = "https";
            context.Request.Host = parent.Request.Host;
            var path = "/" + operation.Api.RelativePath!.Split('?')[0];
            if (args.TryGetValue("route", out var route))
                foreach (var parameter in route.EnumerateObject())
                {
                    var value = AsQuery(parameter.Value);
                    if (value.Contains('/') || value.Contains('\\') || value is "." or "..") return Error("Invalid route value.");
                    path = Regex.Replace(path, "\\{" + Regex.Escape(parameter.Name) + "(?::[^}]+)?\\}", Uri.EscapeDataString(value));
                }
            if (path.Contains('{')) return Error("Missing route parameter.");
            context.Request.Path = path;
            if (args.TryGetValue("query", out var query))
            {
                if (query.EnumerateObject().Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != query.EnumerateObject().Count())
                    return Error("Duplicate query parameter.");
                var allowed = operation.Api.ParameterDescriptions.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (query.EnumerateObject().Any(x => !allowed.Contains(x.Name))) return Error("Unknown query parameter.");
                context.Request.QueryString = new QueryString(QueryHelpers.AddQueryString("", query.EnumerateObject()
                    .ToDictionary(x => x.Name, x => (string?)AsQuery(x.Value))));
            }
            using var body = new MemoryStream();
            if (args.TryGetValue("files", out var files))
            {
                using var multipart = new MultipartFormDataContent();
                foreach (var file in files.EnumerateObject())
                {
                    var bytes = Convert.FromBase64String(file.Value.GetProperty("base64").GetString()!);
                    if (bytes.Length > 2 * 1024 * 1024) return Error("File exceeds 2 MiB.");
                    var content = new ByteArrayContent(bytes);
                    content.Headers.ContentType = MediaTypeHeaderValue.Parse(file.Value.GetProperty("contentType").GetString()!);
                    multipart.Add(content, file.Name, Path.GetFileName(file.Value.GetProperty("fileName").GetString()!));
                }
                await multipart.CopyToAsync(body, cancellationToken);
                context.Request.ContentType = multipart.Headers.ContentType!.ToString();
            }
            else if (args.TryGetValue("body", out var jsonBody))
            {
                await body.WriteAsync(Encoding.UTF8.GetBytes(jsonBody.GetRawText()), cancellationToken);
                context.Request.ContentType = "application/json";
            }
            if (body.Length > 4 * 1024 * 1024) return Error("Request exceeds 4 MiB.");
            body.Position = 0;
            context.Request.Body = body;
            context.Request.ContentLength = body.Length;
            using var output = new LimitedResponseStream();
            context.Response.Body = output;
            contexts.HttpContext = context;
            await (_pipeline ?? throw new InvalidOperationException("MCP pipeline not initialized."))(context);
            output.Position = 0;
            var textResponse = output.Length == 0 || context.Response.ContentType == null ||
                context.Response.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
                context.Response.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
                context.Response.ContentType.Contains("xml", StringComparison.OrdinalIgnoreCase);
            var response = textResponse
                ? await new StreamReader(output).ReadToEndAsync(cancellationToken)
                : Convert.ToBase64String(output.ToArray());
            // Redirects never cause another request. Neither cookies nor authentication headers leave the facade.
            var payload = JsonSerializer.Serialize(new { statusCode = context.Response.StatusCode, contentType = context.Response.ContentType, encoding = textResponse ? "utf-8" : "base64", body = response });
            logger.LogInformation("MCP tool {Tool} completed with HTTP {StatusCode}; request {TraceId}.", request.Name, context.Response.StatusCode, parent.TraceIdentifier);
            return new CallToolResult
            {
                IsError = context.Response.StatusCode >= 300,
                Content = [new TextContentBlock { Text = payload }]
            };
        }
        catch (Exception exception) when (exception is JsonException or FormatException or ArgumentException)
        {
            return Error("Invalid tool arguments.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError("MCP tool {Tool} failed ({ExceptionType}); request {TraceId}. No arguments or credentials logged.",
                request.Name, exception.GetType().Name, parent.TraceIdentifier);
            return Error("Operation failed. Its outcome may be uncertain; do not retry writes automatically. Reference: " + parent.TraceIdentifier);
        }
        finally { contexts.HttpContext = parent; }
    }

    private static string AsQuery(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
    private static CallToolResult Error(string message) => new() { IsError = true, Content = [new TextContentBlock { Text = message }] };

    private sealed class LimitedResponseStream : MemoryStream
    {
        private const int Limit = 4 * 1024 * 1024;
        public override void Write(byte[] buffer, int offset, int count)
        {
            Check(count); base.Write(buffer, offset, count);
        }
        public override void Write(ReadOnlySpan<byte> buffer) { Check(buffer.Length); base.Write(buffer); }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Check(buffer.Length); return base.WriteAsync(buffer, cancellationToken);
        }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Check(count); return base.WriteAsync(buffer, offset, count, cancellationToken);
        }
        private void Check(int count)
        {
            if (Length + count > Limit) throw new IOException("MCP response limit exceeded.");
        }
    }
}
