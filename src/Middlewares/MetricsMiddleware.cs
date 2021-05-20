using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Prometheus;

public class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;

    public MetricsMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
    {
        _next = next;
        _logger = loggerFactory.CreateLogger<MetricsMiddleware>();
        
    }
    
    public async Task Invoke(HttpContext httpContext)  
    {  
        var path = httpContext.Request.Path.Value;  
        var method = httpContext.Request.Method;  
  
        var counter = Metrics.CreateCounter("request_count", "Total http Requests", new CounterConfiguration  
        {  
            LabelNames = new[] { "path", "method", "status" }  
        });

        int statusCode; 
  
        try  
        {  
            await _next.Invoke(httpContext);  
        }  
        catch (Exception)  
        {  
            statusCode = 500;  
            counter.Labels(path ?? string.Empty, method, statusCode.ToString()).Inc();  
  
            throw;  
        }  
          
        if (path != "/metrics")  
        {  
            statusCode = httpContext.Response.StatusCode;  
            counter.Labels(path ?? string.Empty, method, statusCode.ToString()).Inc();  
        }  
    }  
}


public static class RequestMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<MetricsMiddleware>();
    }
}