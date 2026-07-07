using FlexQuery.NET.Dapper.Mapping;

namespace FlexQuery.NET.Dapper.Metadata;

public sealed class FlexQueryModel
{
    internal MappingRegistry Registry { get; }

    internal FlexQueryModel(MappingRegistry registry)
    {
        Registry = registry;
    }
}
