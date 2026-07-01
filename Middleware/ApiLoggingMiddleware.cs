using System.Diagnostics;
using DenunciaYA.API.Models;
using DenunciaYA.API.Services;

namespace DenunciaYA.API.Middleware;

public class ApiLoggingMiddleware(RequestDelegate next, ILogger<ApiLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, MongoLogService logService)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/openapi") || path.StartsWith("/scalar"))
        {
            await next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        string? error = null;

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            throw;
        }
        finally
        {
            sw.Stop();

            if (context.Response.StatusCode >= 400)
                error ??= $"HTTP {context.Response.StatusCode}";

            var log = new ApiLog
            {
                Timestamp = DateTime.UtcNow,
                Method = context.Request.Method,
                Endpoint = path,
                StatusCode = context.Response.StatusCode,
                DurationMs = sw.ElapsedMilliseconds,
                Error = error
            };

            // Fire-and-forget: no bloquea la respuesta HTTP
            _ = logService.InsertAsync(log).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    logger.LogError(t.Exception, "[MongoDB] Error al guardar log de {Method} {Path}", context.Request.Method, path);
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
