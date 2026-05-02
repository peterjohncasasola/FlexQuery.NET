using Microsoft.Extensions.DependencyInjection;
using FlexQuery.NET.AspNetCore.Filters;

namespace FlexQuery.NET.AspNetCore.Extensions;

/// <summary>
/// Extension methods for registering FlexQuery ASP.NET Core components.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds FlexQuery field-level security filters to the Mvc options.
    /// </summary>
    public static void AddFlexQuerySecurity(this IMvcBuilder builder)
    {
        builder.AddMvcOptions(options =>
        {
            options.Filters.Add<FieldAccessFilter>();
        });
    }
}
