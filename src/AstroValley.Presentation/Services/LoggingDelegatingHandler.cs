using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http;

namespace AstroValley.Presentation.Services;

/// <summary>
/// HTTP message handler that logs all outgoing requests and their responses.
/// This handler is inserted into the HttpClient pipeline to provide observability
/// for all HTTP operations, tracking request/response details and timing.
/// </summary>
/// <remarks>
/// Best Practice: Delegating handlers allow cross-cutting concerns like logging,
/// authentication, and telemetry to be applied consistently across all HTTP clients
/// without modifying individual client implementations.
/// </remarks>
public class LoggingDelegatingHandler : DelegatingHandler
{
    private readonly ILogger<LoggingDelegatingHandler> _logger;

    public LoggingDelegatingHandler(ILogger<LoggingDelegatingHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Intercepts HTTP requests to log timing, status codes, and errors.
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Log the outgoing request
        _logger.LogInformation(
            "HTTP {Method} {Uri} - Request initiated",
            request.Method,
            request.RequestUri);

        // Start timing the request
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Execute the actual HTTP request through the pipeline
            var response = await base.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            // Log successful completion with status code and timing
            _logger.LogInformation(
                "HTTP {Method} {Uri} - Completed with {StatusCode} in {ElapsedMs}ms",
                request.Method,
                request.RequestUri,
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Log failures with exception details and timing
            _logger.LogError(ex,
                "HTTP {Method} {Uri} - Failed after {ElapsedMs}ms",
                request.Method,
                request.RequestUri,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}
