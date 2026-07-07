using FlexQuery.NET.Dapper.Mapping;

namespace FlexQuery.NET.Dapper.Configuration;

/// <summary>
/// Represents the immutable runtime model used by FlexQuery.NET Dapper during query execution.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="FlexQueryModel"/> contains all configured entity mapping metadata,
/// including table mappings, relationships, and conventions.
/// </para>
/// <para>
/// Instances are typically created by <see cref="Mapping.Configuration.ModelBuilder.Build"/>
/// after all entity configuration has been completed, then reused across query executions.
/// </para>
/// <para>
/// In ASP.NET Core applications, a single <see cref="FlexQueryModel"/> is typically
/// registered as a singleton and supplied to queries via
/// <see cref="DapperQueryOptions.UseModel(FlexQueryModel)"/>.
/// </para>
/// </remarks>
public sealed class FlexQueryModel
{
    internal MappingRegistry Registry { get; }

    internal FlexQueryModel(MappingRegistry registry)
    {
        Registry = registry;
    }
}