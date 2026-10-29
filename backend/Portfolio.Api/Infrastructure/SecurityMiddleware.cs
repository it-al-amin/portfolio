namespace Portfolio.Api.Infrastructure;

public static class SecurityMiddleware
{
    public const string CsrfHeader = "X-Requested-With";

    private const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; " +
        "connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'";

    /// <summary>Adds standard security headers to every response.</summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["Content-Security-Policy"] = ContentSecurityPolicy;
        await next();
    });

    /// <summary>
    /// CSRF defence for cookie-authenticated APIs: state-changing /api requests must carry a custom header.
    /// Browsers cannot attach custom headers cross-site without a CORS preflight, which our CORS policy refuses.
    /// Combined with SameSite=Strict cookies this blocks cross-site form and fetch forgeries.
    /// </summary>
    public static IApplicationBuilder UseCsrfHeaderCheck(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var request = context.Request;
        var unsafeMethod = !(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method));
        if (unsafeMethod && request.Path.StartsWithSegments("/api") && !request.Headers.ContainsKey(CsrfHeader))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { title = "Missing required request header." });
            return;
        }
        await next();
    });
}
