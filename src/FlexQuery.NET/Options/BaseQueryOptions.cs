using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Configuration;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Security;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Mapping;
using FlexQuery.NET.Resolvers;

namespace FlexQuery.NET.Options;

/// <summary>Infrastructure and customization base for all FlexQuery execution options.</summary>
public abstract class BaseQueryOptions
{
    protected BaseQueryOptions()
    {
        IncludeTotalCount = true;
        DefaultPageSize = 20;
    }

    public Dictionary<string, LambdaExpression>? ExpressionMappings { get; set; }

    public void MapField<TEntity, TProperty>(string alias, Expression<Func<TEntity, TProperty>> expression)
    {
        ExpressionMappings ??= new Dictionary<string, LambdaExpression>(StringComparer.OrdinalIgnoreCase);
        ExpressionMappings[alias] = expression;
    }

    public Dictionary<string, string>? FieldMappings { get; set; }

    private QueryMappingRegistry? _mappingRegistry;
    internal IQueryMappingRegistry? GlobalRegistry { get; set; }

    internal QueryMappingRegistry MappingRegistry
        => _mappingRegistry ??= new QueryMappingRegistry(GlobalRegistry);

    public ITypeMapExpression<TEntity, TResponse> CreateMap<TEntity, TResponse>()
        where TEntity : class
        where TResponse : class
    {
        var typeMap = (TypeMap)MappingRegistry.GetOrCreate(typeof(TEntity), typeof(TResponse));
        return new TypeMapExpression<TEntity, TResponse>(typeMap);
    }

    internal Dictionary<string, TypedDtoMapping>? DtoFieldMappings { get; set; }

    public void MapField<TDto, TEntity, TProperty>(
        Expression<Func<TDto, TProperty>> dtoSelector,
        Expression<Func<TEntity, TProperty>> entitySelector)
        where TDto : class
        where TEntity : class
    {
        var dtoProp = DirectPropertySelector.Extract(dtoSelector, nameof(dtoSelector));
        var entityProp = DirectPropertySelector.Extract(entitySelector, nameof(entitySelector));

        var typeMap = (TypeMap)MappingRegistry.GetOrCreate(typeof(TEntity), typeof(TDto));
        typeMap.RegisterMember(new PropertyMap
        {
            DestinationName = dtoProp.Name,
            SourceExpression = entitySelector,
            SourceProperty = entityProp,
            DestinationProperty = dtoProp,
            SourceValueType = entityProp.PropertyType,
            DestinationValueType = dtoProp.PropertyType,
            IsImplicit = false,
            IsNavigation = !Metadata.TypeClassification.IsScalarType(entityProp.PropertyType)
        });

        DtoFieldMappings ??= new Dictionary<string, TypedDtoMapping>(StringComparer.OrdinalIgnoreCase);
        DtoFieldMappings[dtoProp.Name] = new TypedDtoMapping(
            typeof(TDto), dtoProp, typeof(TEntity), entityProp, entitySelector);
    }

    public bool IncludeTotalCount { get; set; }
    public int DefaultPageSize { get; set; } = 20;
    public int? MaxPageSize { get; set; }
    public QuerySyntax? QuerySyntax { get; set; }
    public bool DisablePaging { get; set; }
    public IFlexQueryExecutionListener? Listener { get; set; }

    internal static void ApplyGlobalDefaults(BaseQueryOptions target, FlexQueryOptions global)
    {
        target.MaxPageSize ??= global.MaxPageSize;
        if (target.DefaultPageSize is 0 or 20)
            target.DefaultPageSize = global.DefaultPageSize;
        if (target.IncludeTotalCount)
            target.IncludeTotalCount = global.IncludeTotalCount;

        if (target is QueryGovernanceOptions governance)
        {
            if (governance.StrictFieldValidation && !global.StrictFieldValidation)
                governance.StrictFieldValidation = false;
            governance.MaxFieldDepth ??= global.MaxFieldDepth;
        }
    }
}

internal sealed record TypedDtoMapping(
    Type DtoType,
    PropertyInfo DtoProperty,
    Type EntityType,
    PropertyInfo EntityProperty,
    LambdaExpression EntityExpression);
