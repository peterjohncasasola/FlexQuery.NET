using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Conventions;

/// <summary>
/// Convention applied to relationship mappings.
/// </summary>
internal interface IRelationshipConvention
{
    /// <summary>Applies convention-based relationship discovery and configuration.</summary>
    void Apply(EntityMapping mapping, IMappingRegistry registry);
}
