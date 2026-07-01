using Microsoft.AspNetCore.Http;

namespace FlexQuery.NET.AspNetCore.Internal;

/// <summary>
/// Internal ambient HttpContext accessor.
/// </summary>
internal static class FlexQueryHttpContextAccessor
{
    private static IHttpContextAccessor? _accessor;

    /// <summary>
    /// Configures the ambient <see cref="IHttpContextAccessor"/> instance used to resolve the current <see cref="HttpContext"/>.
    /// Must be called once at application startup from the composition root.
    /// </summary>
    /// <param name="accessor">The <see cref="IHttpContextAccessor"/> registered in the DI container.</param>
    public static void Configure(
        IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    /// <summary>
    /// Gets the current <see cref="HttpContext"/> from the configured ambient accessor.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no <see cref="IHttpContextAccessor"/> has been configured or there is no active HTTP context.</exception>
    public static HttpContext Current =>
        _accessor?.HttpContext
        ?? throw new InvalidOperationException(
            "No active HttpContext.");
}