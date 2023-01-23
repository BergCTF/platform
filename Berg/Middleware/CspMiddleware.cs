namespace Berg.Middleware;

public class CspMiddleware
{
    private const string CspNonceKey = "CSPNonce";

    private readonly RequestDelegate _next;
    private static readonly Random Random = new();

    public CspMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var buf = new byte[16];
        Random.NextBytes(buf);
        var nonce = Convert.ToHexString(buf);
        context.Items[CspNonceKey] = nonce;
        context.Response.Headers.ContentSecurityPolicy = $"script-src 'strict-dynamic' 'nonce-{nonce}' ;" +
            "default-src 'self'; object-src 'none'; base-uri 'none';" +
            "img-src 'self' data: https://cdn.discordapp.com/ ;";
        await _next(context);
    }
}