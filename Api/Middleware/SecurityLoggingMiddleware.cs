using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Api.Middleware;

public class SecurityLoggingMiddleware(RequestDelegate next, ILogger<SecurityLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        if (context.Response.StatusCode == StatusCodes.Status401Unauthorized)
        {
            logger.LogWarning("Security Event: Unauthorized access attempt to {Path} from IP {IpAddress}",
                context.Request.Path,
                context.Connection.RemoteIpAddress);
        }
        else if (context.Response.StatusCode == StatusCodes.Status403Forbidden)
        {
            logger.LogWarning("Security Event: Forbidden access attempt to {Path} from IP {IpAddress} by User {User}",
                context.Request.Path,
                context.Connection.RemoteIpAddress,
                context.User?.Identity?.Name ?? "Unknown");
        }
    }
}
