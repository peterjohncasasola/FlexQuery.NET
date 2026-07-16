using FlexQuery.NET.Dapper.Mapping;

namespace FlexQuery.NET.Dapper.Metadata;

/// <summary>
/// Represents the immutable metadata model used by FlexQuery Dapper
/// to translate entity mappings into SQL. This is an internal implementation
/// detail of the Dapper integration and is not part of the public API.
/// </summary>
public sealed class FlexQueryModel
{
    /// <summary>
    /// Gets the mapping registry containing all configured entity mappings.
    /// </summary>
    internal MappingRegistry Registry { get; }

    internal FlexQueryModel(MappingRegistry registry)
    {
        Registry = registry;
    }
}
