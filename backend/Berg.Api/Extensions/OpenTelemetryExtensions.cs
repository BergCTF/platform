using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using Berg.Api.Configuration;
using OpenTelemetry.Resources;
using Npgsql;

namespace Berg.Api.Extensions;

public static class OpenTelemetryExtensions
{
    public static void AddOpenTelemetryExporters(this WebApplicationBuilder builder, InfraConfig infraConfig)
    {
        var openTelemetry = builder.Services.AddOpenTelemetry();
        var resourceBuilder = ResourceBuilder.CreateDefault().AddService("Berg.Api");
        if(!string.IsNullOrEmpty(infraConfig.OpenTelemetryGrpcTracingEndpoint))
        {
            openTelemetry.WithTracing(builder => builder
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddSource(Constants.BergActivitySource.Name)
                .AddOtlpExporter("tracing", tracing => {
                    tracing.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    tracing.Endpoint = new Uri(infraConfig.OpenTelemetryGrpcTracingEndpoint);
                }));
        }
        if(!string.IsNullOrEmpty(infraConfig.OpenTelemetryGrpcMetricsEndpoint))
        {
            openTelemetry.WithMetrics(builder => builder
                .SetResourceBuilder(resourceBuilder)
                .AddMeter("Berg.Api")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsqlInstrumentation()
                .AddOtlpExporter("metrics", metrics => {
                    metrics.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    metrics.Endpoint = new Uri(infraConfig.OpenTelemetryGrpcMetricsEndpoint);
                }));
        }
        if(!string.IsNullOrEmpty(infraConfig.OpenTelemetryGrpcLoggingEndpoint))
        {
            openTelemetry.WithLogging(builder => builder
                .SetResourceBuilder(resourceBuilder)
                .AddOtlpExporter("logging", logging => {
                    logging.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    logging.Endpoint = new Uri(infraConfig.OpenTelemetryGrpcLoggingEndpoint);
                }));
        }
    }
}
