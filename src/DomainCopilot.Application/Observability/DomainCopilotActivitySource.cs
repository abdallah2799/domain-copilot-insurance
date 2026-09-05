using System.Diagnostics;

namespace DomainCopilot.Application.Observability;

/// <summary>FR-9's correlation-id/tracing spans for the parts of a request OpenTelemetry's
/// AspNetCore/HttpClient auto-instrumentation can't see on its own: an adjudication stage, an
/// agent's completion call. Every span started here nests under the same request's Activity
/// (ASP.NET Core already starts one per request, W3C trace-context format, since .NET Core 3) via
/// ambient AsyncLocal propagation -- no manual correlation-id plumbing needed for that nesting.
/// <see cref="ActivitySource"/>/<see cref="Activity"/> are BCL types (System.Diagnostics), not an
/// OpenTelemetry SDK dependency, so Application can use them directly per CLAUDE.md's layering
/// rule -- only the exporter wiring (Infrastructure/Api) touches an actual OTel package.</summary>
public static class DomainCopilotActivitySource
{
    public const string Name = "DomainCopilot";

    public static readonly ActivitySource Instance = new(Name);
}
