namespace Api.Middleware;

/// <summary>
/// Voegt security-headers toe aan elke HTTP-response.
/// Verkleint het aanvalsoppervlak door MIME-sniffing, clickjacking,
/// ongewenste verwijzers en gevaarlijke browser-features te blokkeren.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Voorkomt MIME-type sniffing (bijv. een .txt uitvoeren als script)
        headers["X-Content-Type-Options"] = "nosniff";

        // Beschermt tegen clickjacking-aanvallen
        headers["X-Frame-Options"] = "DENY";

        // Beperkt hoeveel referrer-info wordt doorgestuurd
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Schakelt potentieel gevaarlijke browser-features uit
        headers["Permissions-Policy"] =
            "camera=(), microphone=(), geolocation=(), payment=(), usb=()";

        // Basis Content-Security-Policy — geen inline scripts, alleen eigen origin
        // Swagger UI vereist 'unsafe-inline' voor styles; in productie aanscherpen.
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data:; " +
            "font-src 'self'; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none';";

        // Verwijder het server-header (lekt technologie-stack info)
        context.Response.Headers.Remove("Server");

        await next(context);
    }
}
