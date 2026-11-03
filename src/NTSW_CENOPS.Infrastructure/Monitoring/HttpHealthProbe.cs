using System.Diagnostics;
using NTSW_CENOPS.Application.Monitoring;
using NTSW_CENOPS.Domain.Monitoring;

namespace NTSW_CENOPS.Infrastructure.Monitoring;

public sealed class HttpHealthProbe : IHealthProbe
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HttpHealthProbe(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<HealthProbeResult> CheckAsync(
        HealthApi api,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("health");
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(api.TimeoutSeconds, 1, 60));

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, api.Url);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            return new HealthProbeResult(
                IsSuccess: (int)response.StatusCode is >= 200 and <= 299,
                StatusCode: (int)response.StatusCode,
                DurationMs: stopwatch.ElapsedMilliseconds,
                ErrorMessage: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new HealthProbeResult(
                IsSuccess: false,
                StatusCode: null,
                DurationMs: stopwatch.ElapsedMilliseconds,
                ErrorMessage: $"{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
        }
    }
}
