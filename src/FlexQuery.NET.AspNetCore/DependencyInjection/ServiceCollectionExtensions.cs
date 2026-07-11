using FlexQuery.NET.AspNetCore.Filters;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering FlexQuery ASP.NET Core components.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds FlexQuery field-level security filters to the Mvc options.
    /// </summary>
    /// <param name="builder">The MVC builder to add filters to.</param>
    public static void AddFlexQuerySecurity(this IMvcBuilder builder)
    {
        builder.AddMvcOptions(options =>
        {
            options.Filters.Add<FieldAccessFilter>();
        });
    }
}
