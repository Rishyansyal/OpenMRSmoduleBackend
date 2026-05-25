namespace Api.Middleware;

/// <summary>
/// Voegt security-headers toe aan elke HTTP-response.
/// Verkleint het aanvalsoppervlak door MIME-sniffing, clickjacking,
/// ongewenste verwijzers en gevaarlijke browser-features te blokkeren.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
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

        // Blokkeert Flash- en PDF-plugins van cross-domain dataverzoeken
        headers["X-Permitted-Cross-Domain-Policies"] = "none";

        // Content-Security-Policy:
        //   - Productie: geen unsafe-inline voor scripts (veiliger)
        //   - Development: unsafe-inline voor Swagger UI (stijlen + scripts)
        // frame-ancestors vervangt X-Frame-Options voor moderne browsers; beide zijn gezet
        // voor maximale browsercompatibiliteit.
        if (env.IsProduction())
        {
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data:; " +
                "font-src 'self'; " +
                "connect-src 'self'; " +
                "frame-ancestors 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self';";
        }
        else
        {
            // Development: unsafe-inline vereist voor Swagger UI
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data:; " +
                "font-src 'self'; " +
                "connect-src 'self'; " +
                "frame-ancestors 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self';";
        }

        // Strict-Transport-Security: dwingt HTTPS af voor 1 jaar (includeSubDomains).
        // Alleen in productie — localhost ondersteunt geen HSTS.
        // Uitzondering: 'preload' is hier bewust weggelaten omdat preload-registratie
        // een onomkeerbare stap is die apart beheerd dient te worden.
        if (env.IsProduction())
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        // Verwijder de Server-header (lekt technologie-stack info).
        // Kestrel is al geconfigureerd met AddServerHeader=false in Program.cs;
        // dit is een extra vangnet voor reverse-proxy doorgestuurde responses.
        context.Response.Headers.Remove("Server");

        await next(context);
    }
}
