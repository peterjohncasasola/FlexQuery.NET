using FlexQuery.NET.Dapper.Configuration;
using FlexQuery.NET.Dapper.Metadata;

namespace FlexQuery.NET.Dapper;

/// <summary>
/// Provides explicit initialization methods for FlexQuery Dapper integration,
/// without dependency injection.
/// </summary>
public static class FlexQueryDapper
{
    /// <summary>
    /// Builds a <see cref="FlexQueryModel"/> using the provided configuration delegate.
    /// Must be called during application startup to define entity mappings.
    /// </summary>
    /// <param name="configure">
    /// A delegate used to configure FlexQuery Dapper entity mappings.
    /// </param>
    /// <returns>The built <see cref="FlexQueryModel"/>.</returns>
    public static FlexQueryModel BuildModel(Action<FlexQueryDapperOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var configurer = new FlexQueryDapperOptions();
        configure(configurer);
        return configurer.Model.Build();
    }
}
