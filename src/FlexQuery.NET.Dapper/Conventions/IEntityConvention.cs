using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Conventions;

/// <summary>
/// Convention applied to entity mappings.
/// </summary>
internal interface IEntityConvention
{
    /// <summary>Applies convention-based mapping rules to the given entity mapping.</summary>
    void Apply(EntityMapping mapping);
}
