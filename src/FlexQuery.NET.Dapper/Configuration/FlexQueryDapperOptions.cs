using FlexQuery.NET.Options;

namespace FlexQuery.NET.Dapper.Configuration;

/// <summary>
/// Configures global FlexQuery.NET Dapper services during application startup.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="FlexQueryDapperOptions"/> is used with
/// <c>AddFlexQueryDapper(...)</c> to define the application's entity mapping model.
/// </para>
/// <para>
/// The SQL dialect is auto-detected from the supplied <see cref="System.Data.Common.DbConnection"/>
/// at runtime — no manual dialect configuration is required.
/// </para>
/// <para>
/// This type is intended for startup configuration only and should not be used
/// during query execution.
/// </para>
/// </remarks>
public sealed class FlexQueryDapperOptions
{
    internal FlexQueryDapperOptions()
    {
        Model = new ModelBuilder();
    }

    /// <summary>
    /// Gets the model builder used to configure entity mappings.
    /// </summary>
    /// <remarks>
    /// Use this builder to configure entities, relationships, and apply
    /// mapping configurations before the model is built for runtime use.
    /// </remarks>
    public ModelBuilder Model { get; }
}