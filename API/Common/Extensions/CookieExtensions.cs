namespace API.Common.Extensions;

public static class CookieExtensions
{
    public static void SetAuthCookies(
        this HttpContext httpContext,
        string accessToken,
        string refreshToken,
        IConfiguration configuration,
        DateTime refreshTokenExpiry)
    {
        var accessTokenExpirationMinutes = int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"]!);

        // Disable Secure flag in Testing environment to allow HTTP requests in integration tests
        var environment = httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var isSecure = !environment.EnvironmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase);

        // Set access token cookie with LONG expiry (same as refresh token)
        // Why? Browser must keep cookie so middleware can read & refresh expired tokens
        // Token inside cookie can be expired - that's OK! Middleware will refresh it.
        // Cookie expiry = how long browser keeps the cookie
        // Token expiry = how long token is valid (JWT validates this)
        httpContext.Response.Cookies.Append("AccessToken", accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure, // HTTPS required except in Testing
            SameSite = SameSiteMode.Strict,
            Expires = refreshTokenExpiry, // CHANGED: Use refresh token expiry, not access token expiry!
            Path = "/"
        });

        // Set refresh token cookie with actual token expiry (respects RememberMe and sliding expiration)
        httpContext.Response.Cookies.Append("RefreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure, // HTTPS required except in Testing
            SameSite = SameSiteMode.Strict,
            Expires = refreshTokenExpiry,
            Path = "/"
        });
    }

    public static void ClearAuthCookies(this HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete("AccessToken");
        httpContext.Response.Cookies.Delete("RefreshToken");
    }

    public static string? GetRefreshTokenFromCookie(this HttpContext httpContext)
    {
        return httpContext.Request.Cookies["RefreshToken"];
    }
}
