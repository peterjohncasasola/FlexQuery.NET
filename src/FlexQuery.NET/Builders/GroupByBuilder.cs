using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Expressions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Resolvers;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Builders;

internal static class GroupByBuilder
{
    private static readonly MethodInfo QueryableGroupBy = ExpressionMethodCache.QueryableGroupBy();

    private static readonly MethodInfo QueryableSelect = ExpressionMethodCache.QueryableSelectSimple();

    private static readonly MethodInfo QueryableWhere = ExpressionMethodCache.QueryableWhereSimple();

    public static IQueryable<object> Apply<T>(IQueryable<T> query, QueryOptions options)
        => ApplyUntyped(query, options).Cast<object>();

    internal static IQueryable ApplyUntyped<T>(IQueryable<T> query, QueryOptions options)
    {
        var groupFields = options.GroupBy ?? [];
        var aggregates = options.Aggregates;
        if (groupFields.Count == 0 && aggregates.Count == 0)
            return query;

        var sourceType = typeof(T);
        var itemParam = Expression.Parameter(sourceType, "x");

        var keyExpression = BuildGroupKeyExpression(itemParam, sourceType, groupFields, options, out var keyType);
        if (keyExpression is null || keyType is null)
            return query;

        var keySelector = Expression.Lambda(
            typeof(Func<,>).MakeGenericType(sourceType, keyType),
            keyExpression,
            itemParam);

        var groupedCall = Expression.Call(
            QueryableGroupBy.MakeGenericMethod(sourceType, keyType),
            query.Expression,
            keySelector);

        var groupingType = typeof(IGrouping<,>).MakeGenericType(keyType, sourceType);
        var groupParam = Expression.Parameter(groupingType, "g");

        var selectedFields = BuildSelectedFieldList(options, groupFields);
        var projection = BuildGroupProjection(groupParam, keyType, sourceType, selectedFields, aggregates, options, out var projectionType);
        if (projection is null || projectionType is null)
            return query;

        var selectLambda = Expression.Lambda(
            typeof(Func<,>).MakeGenericType(groupingType, projectionType),
            projection,
            groupParam);

        var projectedCall = Expression.Call(
            QueryableSelect.MakeGenericMethod(groupingType, projectionType),
            groupedCall,
            selectLambda);

        Expression finalCall = projectedCall;

        if (options.Having is not null)
        {
            var havingAlias = ParserUtilities.BuildAggregateAlias(options.Having.Function, options.Having.Field);
            var matchingAggregate = options.Aggregates.FirstOrDefault(a =>
                string.Equals(a.Function, options.Having.Function, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(a.Field, options.Having.Field, StringComparison.OrdinalIgnoreCase));
            if (matchingAggregate?.Alias != null)
                havingAlias = matchingAggregate.Alias;
            var havingLambda = HavingExpressionBuilder.Build(projectionType, options.Having, havingAlias, options.CaseInsensitive);
            if (havingLambda is not null)
            {
                finalCall = Expression.Call(
                    QueryableWhere.MakeGenericMethod(projectionType),
                    finalCall,
                    havingLambda);
            }
        }

        return query.Provider.CreateQuery(finalCall);
    }

    private static List<string> BuildSelectedFieldList(QueryOptions options, List<string> groupFields)
    {
        var selected = (options.Select ?? []).ToList();
        if (selected.Count == 0)
            selected.AddRange(groupFields);
        return selected;
    }

    private static Expression? BuildGroupKeyExpression(
        ParameterExpression itemParam,
        Type sourceType,
        List<string> groupFields,
        QueryOptions options,
        out Type? keyType)
    {
        keyType = null;
        if (groupFields.Count == 0)
        {
            keyType = typeof(int);
            return Expression.Constant(1);
        }

        if (groupFields.Count == 1)
        {
            if (!TryBuildMemberAccess(itemParam, sourceType, groupFields[0], options, out var access))
                return null;
            keyType = access.Type;
            return access;
        }

        var fields = new Dictionary<string, (Type Type, Expression Expr)>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in groupFields)
        {
            if (!TryBuildMemberAccess(itemParam, sourceType, field, options, out var access))
                return null;
            fields[field] = (access.Type, access);
        }

        var dynamicType = DynamicTypeBuilder.GetDynamicType(fields.ToDictionary(k => GetProjectionName(k.Key), v => v.Value.Type));
        var bindings = new List<MemberBinding>();
        foreach (var field in fields)
        {
            var prop = dynamicType.GetProperty(GetProjectionName(field.Key));
            if (prop is null) return null;
            bindings.Add(Expression.Bind(prop, field.Value.Expr));
        }

        keyType = dynamicType;
        return Expression.MemberInit(Expression.New(dynamicType), bindings);
    }

    private static Expression? BuildGroupProjection(
        ParameterExpression groupParam,
        Type keyType,
        Type sourceType,
        List<string> selectedFields,
        List<AggregateModel> aggregates,
        QueryOptions options,
        out Type? projectionType)
    {
        projectionType = null;
        var fields = new Dictionary<string, (Type Type, Expression Expr)>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in selectedFields)
        {
            var outputName = GetProjectionName(field);
            if (options.GroupBy is not { Count: > 0 })
                continue;

            var keyExpr = Expression.Property(groupParam, nameof(IGrouping<object, object>.Key));
            var keyProp = keyType.GetProperty(outputName);
            Expression keyAccess = keyProp is null
                ? keyExpr
                : Expression.Property(keyExpr, keyProp);

            fields[outputName] = (keyAccess.Type, keyAccess);
        }

        foreach (var aggregate in aggregates)
        {
            var aggregateExpr = BuildAggregateExpression(groupParam, sourceType, aggregate, options);
            if (aggregateExpr is null) continue;
            fields[aggregate.Alias] = (aggregateExpr.Type, aggregateExpr);
        }

        if (fields.Count == 0)
            return null;

        var dynamicType = DynamicTypeBuilder.GetDynamicType(fields.ToDictionary(k => k.Key, v => v.Value.Type));
        var bindings = new List<MemberBinding>();
        foreach (var field in fields)
        {
            var prop = dynamicType.GetProperty(field.Key);
            if (prop is null) continue;
            bindings.Add(Expression.Bind(prop, field.Value.Expr));
        }

        projectionType = dynamicType;
        return Expression.MemberInit(Expression.New(dynamicType), bindings);
    }

    private static Expression? BuildAggregateExpression(ParameterExpression grouping, Type sourceType, AggregateModel aggregate, QueryOptions options)
    {
        var fn = aggregate.Function.ToLowerInvariant();

        if (fn == "count")
        {
            var countMethod = ExpressionMethodCache.EnumerableCount(sourceType);
            return Expression.Call(countMethod, grouping);
        }

        if (string.IsNullOrWhiteSpace(aggregate.Field))
            return null;

        var item = Expression.Parameter(sourceType, "i");
        if (!TryBuildMemberAccess(item, sourceType, aggregate.Field, options, out var body))
            return null;
        var selectorBody = body;

        // Keep SQLite translation server-side by promoting decimal aggregates.
        if (body.Type == typeof(decimal))
            selectorBody = Expression.Convert(body, typeof(double));
        else if (body.Type == typeof(decimal?))
            selectorBody = Expression.Convert(body, typeof(double?));

        var selector = Expression.Lambda(selectorBody, item);

        if (fn is "min" or "max")
        {
            var genericMethod = fn == "min"
                ? ExpressionMethodCache.EnumerableMinWithSelector(sourceType, selectorBody.Type)
                : ExpressionMethodCache.EnumerableMaxWithSelector(sourceType, selectorBody.Type);
            return Expression.Call(genericMethod, grouping, selector);
        }

        if (fn is "sum" or "avg" or "average")
        {
            var genericMethod = fn is "avg" or "average"
                ? ExpressionMethodCache.EnumerableAverageWithSelector(sourceType, selectorBody.Type)
                : ExpressionMethodCache.EnumerableSumWithSelector(sourceType, selectorBody.Type);
            return Expression.Call(genericMethod, grouping, selector);
        }

        return null;
    }

    private static bool TryBuildMemberAccess(Expression root, Type rootType, string path, QueryOptions options, out Expression access)
    {
        access = root;

        if (FieldResolver.TryResolveMappedExpression(root, path, options, out var resolvedExpr, out _))
        {
            access = resolvedExpr;
            return true;
        }

        if (!SafePropertyResolver.TryResolveChain(rootType, path, out var chain)) return false;
        if (chain.Any(p => SafePropertyResolver.TryGetCollectionElementType(p.PropertyType, out _))) return false;
        foreach (var prop in chain)
            access = Expression.Property(access, prop);
        return true;
    }

    internal static string GetProjectionName(string path)
    {
        var last = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? path;
        return string.Concat(last.Where(ch => ch != '_'));
    }
    
}
