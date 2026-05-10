using Microsoft.AspNetCore.Http;

namespace FlexQuery.NET.AspNetCore.Internal;

/// <summary>
/// Internal ambient HttpContext accessor.
/// </summary>
internal static class FlexQueryHttpContextAccessor
{
    private static IHttpContextAccessor? _accessor;

    public static void Configure(
        IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public static HttpContext Current =>
        _accessor?.HttpContext
        ?? throw new InvalidOperationException(
            "No active HttpContext.");
}