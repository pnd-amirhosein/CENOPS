namespace NTSW_CENOPS.Application.Monitoring;

public sealed record HealthProbeResult(
    bool IsSuccess,
    int? StatusCode,
    long DurationMs,
    string? ErrorMessage);
