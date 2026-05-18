using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Conventions;

/// <summary>
/// Convention applied to entity mappings.
/// </summary>
public interface IEntityConvention
{
    void Apply(EntityMapping mapping);
}
