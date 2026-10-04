using System.Collections.Concurrent;

namespace Farm.Services;

public class ClientTracingService : IClientTracingService
{
    private readonly int _limit;
    private readonly TimeSpan _window;

    private long _totalRequests;
    private readonly ConcurrentDictionary<string, byte> _uniqueClients = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, EndpointStats> _endpointStats = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ClientWindow> _clientWindows = new(StringComparer.OrdinalIgnoreCase);

    public ClientTracingService(int limit = 60, int windowSeconds = 60)
    {
        _limit = limit;
        _window = TimeSpan.FromSeconds(windowSeconds);
    }

    public ClientRateLimitResult ProcessRequest(HttpContext context, string endpoint)
    {
        var clientId = ResolveClientId(context);
        var now = DateTime.UtcNow;

        // Update global unique clients and total requests
        Interlocked.Increment(ref _totalRequests);
        _uniqueClients.TryAdd(clientId, 0);

        // Update endpoint stats
        var epStats = _endpointStats.GetOrAdd(endpoint, _ => new EndpointStats());
        Interlocked.Increment(ref epStats.TotalRequests);
        epStats.UniqueClients.TryAdd(clientId, 0);

        // Rate limit calculation for client
        var window = _clientWindows.GetOrAdd(clientId, _ => new ClientWindow(now));
        var (isAllowed, remaining, resetSeconds) = window.RecordAndCheck(now, _limit, _window);

        var status = isAllowed ? "OK" : "RateLimitExceeded";

        var result = new ClientRateLimitResult(
            IsAllowed: isAllowed,
            ClientId: clientId,
            Limit: _limit,
            Remaining: remaining,
            ResetSeconds: resetSeconds,
            TotalRequests: Interlocked.Read(ref _totalRequests),
            EndpointRequests: Interlocked.Read(ref epStats.TotalRequests),
            UniqueClients: _uniqueClients.Count,
            EndpointUniqueClients: epStats.UniqueClients.Count,
            Status: status
        );

        ApplyHeaders(context.Response, result);
        return result;
    }

    public TracingMetricsResponse GetMetrics(HttpContext context)
    {
        var clientId = ResolveClientId(context);
        var now = DateTime.UtcNow;

        var window = _clientWindows.GetOrAdd(clientId, _ => new ClientWindow(now));
        var (_, remaining, resetSeconds) = window.GetStatus(now, _limit, _window);

        var endpointsDict = new Dictionary<string, EndpointMetrics>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _endpointStats)
        {
            endpointsDict[kvp.Key] = new EndpointMetrics(
                TotalRequests: Interlocked.Read(ref kvp.Value.TotalRequests),
                UniqueClients: kvp.Value.UniqueClients.Count
            );
        }

        var clientStatus = new ClientRateLimitResult(
            IsAllowed: remaining > 0,
            ClientId: clientId,
            Limit: _limit,
            Remaining: remaining,
            ResetSeconds: resetSeconds,
            TotalRequests: Interlocked.Read(ref _totalRequests),
            EndpointRequests: 0,
            UniqueClients: _uniqueClients.Count,
            EndpointUniqueClients: 0,
            Status: remaining > 0 ? "OK" : "RateLimitExceeded"
        );

        return new TracingMetricsResponse(
            TotalRequests: Interlocked.Read(ref _totalRequests),
            UniqueClients: _uniqueClients.Count,
            Endpoints: endpointsDict,
            CurrentClient: clientStatus
        );
    }

    private static string ResolveClientId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Client-Id", out var clientIdHeader) &&
            !string.IsNullOrWhiteSpace(clientIdHeader))
        {
            return clientIdHeader.ToString().Trim();
        }

        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) &&
            !string.IsNullOrWhiteSpace(forwardedFor))
        {
            var ip = forwardedFor.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(ip))
            {
                return ip;
            }
        }

        var remoteIp = context.Connection.RemoteIpAddress?.ToString();
        return !string.IsNullOrWhiteSpace(remoteIp) ? remoteIp : "anonymous-client";
    }

    private static void ApplyHeaders(HttpResponse response, ClientRateLimitResult result)
    {
        response.Headers["X-Total-Requests"] = result.TotalRequests.ToString();
        response.Headers["X-Unique-Clients"] = result.UniqueClients.ToString();
        response.Headers["X-Endpoint-Requests"] = result.EndpointRequests.ToString();
        response.Headers["X-Endpoint-Unique-Clients"] = result.EndpointUniqueClients.ToString();
        response.Headers["X-RateLimit-Limit"] = result.Limit.ToString();
        response.Headers["X-RateLimit-Remaining"] = result.Remaining.ToString();
        response.Headers["X-RateLimit-Reset"] = result.ResetSeconds.ToString();
        response.Headers["X-Client-Status"] = result.Status;
    }

    private sealed class EndpointStats
    {
        public long TotalRequests;
        public readonly ConcurrentDictionary<string, byte> UniqueClients = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ClientWindow
    {
        private readonly object _lock = new();
        private DateTime _windowStart;
        private int _count;

        public ClientWindow(DateTime now)
        {
            _windowStart = now;
            _count = 0;
        }

        public (bool IsAllowed, int Remaining, int ResetSeconds) RecordAndCheck(DateTime now, int limit, TimeSpan windowDuration)
        {
            lock (_lock)
            {
                if (now - _windowStart >= windowDuration)
                {
                    _windowStart = now;
                    _count = 0;
                }

                _count++;
                var remaining = Math.Max(0, limit - _count);
                var isAllowed = _count <= limit;
                var elapsed = now - _windowStart;
                var resetSeconds = Math.Max(1, (int)(windowDuration - elapsed).TotalSeconds);

                return (isAllowed, remaining, resetSeconds);
            }
        }

        public (bool IsAllowed, int Remaining, int ResetSeconds) GetStatus(DateTime now, int limit, TimeSpan windowDuration)
        {
            lock (_lock)
            {
                if (now - _windowStart >= windowDuration)
                {
                    return (true, limit, (int)windowDuration.TotalSeconds);
                }

                var remaining = Math.Max(0, limit - _count);
                var isAllowed = _count <= limit;
                var elapsed = now - _windowStart;
                var resetSeconds = Math.Max(1, (int)(windowDuration - elapsed).TotalSeconds);

                return (isAllowed, remaining, resetSeconds);
            }
        }
    }
}
