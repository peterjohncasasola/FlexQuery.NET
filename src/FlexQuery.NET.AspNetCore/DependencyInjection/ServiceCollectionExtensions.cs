using FlexQuery.NET;
using FlexQuery.NET.AspNetCore.Filters;
using FlexQuery.NET.Configuration;
using FlexQuery.NET.Serialization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering FlexQuery ASP.NET Core components.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds FlexQuery field-level security filters to the Mvc options and registers
    /// the result-surface JSON converter so an explicit typed-DTO <c>select</c> exposes
    /// only the selected output fields (using their aliases) without any opt-in.
    /// </summary>
    /// <param name="builder">The MVC builder to configure.</param>
    public static void AddFlexQuerySecurity(this IMvcBuilder builder)
    {
        builder.AddMvcOptions(options =>
        {
            options.Filters.Add<FieldAccessFilter>();
        });

        builder.AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new QueryResultShapeConverterFactory());
        });
    }

    /// <summary>
    /// Registers the JSON converter that shapes <c>QueryResult&lt;T&gt;</c> serialization so an
    /// explicit typed-DTO <c>select</c> exposes only the selected output fields (using their aliases).
    /// </summary>
    /// <param name="builder">The MVC builder to configure JSON for.</param>
    public static void AddFlexQueryJson(this IMvcBuilder builder)
    {
        builder.AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new QueryResultShapeConverterFactory());
        });
    }

    /// <summary>
    /// Configures FlexQuery global application-wide defaults, including the
    /// application-level entity → DTO mapping graph.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Global FlexQuery configuration delegate (defaults, CreateMap mappings).</param>
    /// <example>
    /// <code>
    /// builder.Services.AddFlexQuery(options =>
    /// {
    ///     options.CreateMap&lt;Customer, CustomerResponse&gt;()
    ///         .ForMember(dto =&gt; dto.CustomerFullName, entity =&gt; entity.CustomerName);
    ///     options.CreateMap&lt;Order, OrderResponse&gt;();
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddFlexQuery(this IServiceCollection services, Action<FlexQueryOptions>? configure = null)
    {
        FlexQueryCore.Configure(configure);
        return services;
    }
}
