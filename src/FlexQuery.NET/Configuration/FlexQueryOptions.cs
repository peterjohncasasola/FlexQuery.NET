using FlexQuery.NET.Parsers;
using FlexQuery.NET.Mapping;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.Configuration;

public sealed class FlexQueryOptions
{
    private bool _frozen;

    public int MaxPageSize { get; set; } = 1000;
    public int DefaultPageSize { get; set; } = 20;
    public bool IncludeTotalCount { get; set; } = true;
    public bool StrictFieldValidation { get; set; } = true;
    public int MaxFieldDepth { get; set; } = 5;
    public QuerySyntax DefaultQuerySyntax { get; set; } = QuerySyntax.NativeDsl;
    public IQueryMappingRegistry Registry { get; } = new QueryMappingRegistry();

    public ITypeMapExpression<TEntity, TDestination> CreateMap<TEntity, TDestination>()
        where TEntity : class
        where TDestination : class
    {
        ThrowIfFrozen();
        var typeMap = (TypeMap)Registry.GetOrCreate(typeof(TEntity), typeof(TDestination));
        return new TypeMapExpression<TEntity, TDestination>(typeMap);
    }

    public void ApplyTo(BaseQueryOptions target)
    {
        ArgumentNullException.ThrowIfNull(target);
        BaseQueryOptions.ApplyGlobalDefaults(target, this);
        target.GlobalRegistry = Registry;
    }

    internal void Freeze()
    {
        if (_frozen)
            return;
        Registry.Freeze();
        _frozen = true;
    }

    internal bool IsFrozen => _frozen;

    private void ThrowIfFrozen()
    {
        if (_frozen)
            throw new InvalidOperationException("FlexQuery has already executed queries; configuration must happen during startup.");
    }
}
