using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Conventions;

/// <summary>
/// Convention applied to relationship mappings.
/// </summary>
public interface IRelationshipConvention
{
    void Apply(EntityMapping mapping, IMappingRegistry registry);
}
