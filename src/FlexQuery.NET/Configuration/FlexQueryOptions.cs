using FlexQuery.NET.Parsers;
using FlexQuery.NET.Mapping;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.Configuration;

public sealed class FlexQueryOptions
{
    private bool _frozen;
    private int _maxPageSize = 1000;
    private int _defaultPageSize = 20;
    private bool _includeTotalCount = true;
    private bool _strictFieldValidation = true;
    private int _maxFieldDepth = 5;
    private QuerySyntax _defaultQuerySyntax = QuerySyntax.NativeDsl;

    public int MaxPageSize { get => _maxPageSize; set { ThrowIfFrozen(); _maxPageSize = value; } }
    public int DefaultPageSize { get => _defaultPageSize; set { ThrowIfFrozen(); _defaultPageSize = value; } }
    public bool IncludeTotalCount { get => _includeTotalCount; set { ThrowIfFrozen(); _includeTotalCount = value; } }
    public bool StrictFieldValidation { get => _strictFieldValidation; set { ThrowIfFrozen(); _strictFieldValidation = value; } }
    public int MaxFieldDepth { get => _maxFieldDepth; set { ThrowIfFrozen(); _maxFieldDepth = value; } }
    public QuerySyntax DefaultQuerySyntax { get => _defaultQuerySyntax; set { ThrowIfFrozen(); _defaultQuerySyntax = value; } }
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
        if (_frozen) return;
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
