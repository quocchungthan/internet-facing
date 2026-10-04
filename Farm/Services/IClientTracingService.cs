namespace Farm.Services;

public record ClientRateLimitResult(
    bool IsAllowed,
    string ClientId,
    int Limit,
    int Remaining,
    int ResetSeconds,
    long TotalRequests,
    long EndpointRequests,
    int UniqueClients,
    int EndpointUniqueClients,
    string Status
);

public record EndpointMetrics(
    long TotalRequests,
    int UniqueClients
);

public record TracingMetricsResponse(
    long TotalRequests,
    int UniqueClients,
    Dictionary<string, EndpointMetrics> Endpoints,
    ClientRateLimitResult CurrentClient
);

public interface IClientTracingService
{
    ClientRateLimitResult ProcessRequest(HttpContext context, string endpoint);
    TracingMetricsResponse GetMetrics(HttpContext context);
}
