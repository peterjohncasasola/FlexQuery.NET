using FlexQuery.NET.AspNetCore.Filters;
using FlexQuery.NET.Serialization;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static void AddFlexQuerySecurity(this IMvcBuilder builder)
    {
        builder.AddMvcOptions(options => options.Filters.Add<FieldAccessFilter>());
        builder.AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new QueryResultShapeConverterFactory()));
    }

    public static void AddFlexQueryJson(this IMvcBuilder builder)
    {
        builder.AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new QueryResultShapeConverterFactory()));
    }
}
