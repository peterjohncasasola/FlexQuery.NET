using FlexQuery.NET.Dapper.Mapping;

namespace FlexQuery.NET.Dapper.Context;

internal sealed class HydrationContext(
    IEntityMapping mapping,
    IMappingRegistry registry,
    IReadOnlyList<string> includes,
    Dictionary<string, string>? columnAliasMap = null,
    string prefix = "")
{
    public IEntityMapping Mapping { get; } = mapping;
    public IMappingRegistry Registry { get; } = registry;
    public IReadOnlyList<string> Includes { get; } = includes;
    public Dictionary<string, string>? ColumnAliasMap { get; } = columnAliasMap;
    public string Prefix { get; } = prefix;
}