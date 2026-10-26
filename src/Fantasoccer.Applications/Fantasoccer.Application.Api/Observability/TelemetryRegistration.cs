using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Fantasoccer.Application.Api.Observability;

public static class TelemetryRegistration
{
    public static void AddFantasoccerTelemetry(this WebApplicationBuilder builder)
    {
        var provider = builder.Configuration["Telemetry:Provider"] ?? "ApplicationInsights";
        if (provider.Equals("None", StringComparison.OrdinalIgnoreCase)) return;
        if (provider.Equals("ApplicationInsights", StringComparison.OrdinalIgnoreCase))
        {
            var connection = builder.Configuration["ConnectionStrings:AppInsights"];
            if (!string.IsNullOrWhiteSpace(connection))
            {
                // Application Insights 3.x uses the OpenTelemetry activity pipeline.
                builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddProcessor(new SensitiveSpanProcessor()));
                builder.Services.AddApplicationInsightsTelemetry(options => options.ConnectionString = connection);
            }
            return;
        }
        if (!provider.Equals("OpenTelemetry", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unknown Telemetry:Provider. Use ApplicationInsights, OpenTelemetry or None.");

        var resource = ResourceBuilder.CreateDefault().AddService("fantasoccer-api");
        builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
        builder.Logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(resource);
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = false;
            options.AddOtlpExporter();
        });
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("fantasoccer-api"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddSource("Npgsql")
                .AddProcessor(new SensitiveSpanProcessor())
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation().AddOtlpExporter());
    }
}

// Login legacy and signed Blob URLs can contain secrets in their query string.
// Never export those query strings, userinfo or SQL text to a diagnostics backend.
public sealed class SensitiveSpanProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        foreach (var name in new[] { "url.query", "http.target", "db.statement", "db.query.text" })
            activity.SetTag(name, null);
        foreach (var name in new[] { "url.full", "http.url" })
        {
            if (activity.GetTagItem(name) is not string value) continue;
            activity.SetTag(name, Uri.TryCreate(value, UriKind.Absolute, out var uri)
                ? new UriBuilder(uri) { Path = uri.Host.Contains("googleapis.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Contains("push", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".notify.windows.com", StringComparison.OrdinalIgnoreCase) ? "/[redacted-provider-endpoint]" : uri.AbsolutePath,
                    Query = "", Fragment = "", UserName = "", Password = "" }.Uri.GetLeftPart(UriPartial.Path)
                : null);
        }
    }
}
