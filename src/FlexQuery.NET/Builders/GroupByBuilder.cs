using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Builders;

internal static class GroupByBuilder
{
    private static readonly MethodInfo QueryableGroupBy = typeof(Queryable).GetMethods()
        .Single(m => m.Name == nameof(Queryable.GroupBy)
                     && m.IsGenericMethodDefinition
                     && m.GetGenericArguments().Length == 2
                     && m.GetParameters().Length == 2);

    private static readonly MethodInfo QueryableSelect = typeof(Queryable).GetMethods()
        .Single(m => m.Name == nameof(Queryable.Select)
                     && m.IsGenericMethodDefinition
                     && m.GetGenericArguments().Length == 2
                     && m.GetParameters().Length == 2
                     && GetFuncArity(m.GetParameters()[1].ParameterType) == 2);

    private static readonly MethodInfo QueryableWhere = typeof(Queryable).GetMethods()
        .Single(m => m.Name == nameof(Queryable.Where)
                     && m.IsGenericMethodDefinition
                     && m.GetGenericArguments().Length == 1
                     && m.GetParameters().Length == 2
                     && GetFuncArity(m.GetParameters()[1].ParameterType) == 2);

    public static IQueryable<object> Apply<T>(IQueryable<T> query, QueryOptions options)
    {
        var groupFields = options.GroupBy ?? [];
        var aggregates = options.Aggregates ?? [];
        if (groupFields.Count == 0 && aggregates.Count == 0)
            return query.Cast<object>();

        var sourceType = typeof(T);
        var itemParam = Expression.Parameter(sourceType, "x");

        var keyExpression = BuildGroupKeyExpression(itemParam, sourceType, groupFields, out var keyType);
        if (keyExpression is null || keyType is null)
            return query.Cast<object>();

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
        var projection = BuildGroupProjection(groupParam, keyType, sourceType, selectedFields, aggregates, out var projectionType);
        if (projection is null || projectionType is null)
            return query.Cast<object>();

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
            var havingAlias = QueryOptionsParser.BuildAggregateAlias(options.Having.Function, options.Having.Field);
            var havingLambda = HavingExpressionBuilder.Build(projectionType, options.Having, havingAlias);
            if (havingLambda is not null)
            {
                finalCall = Expression.Call(
                    QueryableWhere.MakeGenericMethod(projectionType),
                    finalCall,
                    havingLambda);
            }
        }

        var finalQuery = query.Provider.CreateQuery(finalCall);
        return finalQuery.Cast<object>();
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
            if (!TryBuildMemberAccess(itemParam, sourceType, groupFields[0], out var access))
                return null;
            keyType = access.Type;
            return access;
        }

        var fields = new Dictionary<string, (Type Type, Expression Expr)>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in groupFields)
        {
            if (!TryBuildMemberAccess(itemParam, sourceType, field, out var access))
                return null;
            fields[field] = (access.Type, access);
        }

        var dynamicType = DynamicTypeBuilder.GetDynamicType(fields.ToDictionary(k => ToProjectionName(k.Key), v => v.Value.Type));
        var bindings = new List<MemberBinding>();
        foreach (var field in fields)
        {
            var prop = dynamicType.GetProperty(ToProjectionName(field.Key));
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
        out Type? projectionType)
    {
        projectionType = null;
        var fields = new Dictionary<string, (Type Type, Expression Expr)>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in selectedFields)
        {
            var outputName = ToProjectionName(field);
            if (keyType == typeof(int))
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
            var aggregateExpr = BuildAggregateExpression(groupParam, sourceType, aggregate);
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

    private static Expression? BuildAggregateExpression(ParameterExpression grouping, Type sourceType, AggregateModel aggregate)
    {
        var fn = aggregate.Function.ToLowerInvariant();

        if (fn == "count")
        {
            var countMethod = typeof(Enumerable).GetMethods()
                .Single(m => m.Name == nameof(Enumerable.Count) && m.GetParameters().Length == 1)
                .MakeGenericMethod(sourceType);
            return Expression.Call(countMethod, grouping);
        }

        if (string.IsNullOrWhiteSpace(aggregate.Field))
            return null;

        var item = Expression.Parameter(sourceType, "i");
        if (!TryBuildMemberAccess(item, sourceType, aggregate.Field, out var body))
            return null;
        var selectorBody = body;

        // Keep SQLite translation server-side by promoting decimal aggregates.
        if (body.Type == typeof(decimal))
            selectorBody = Expression.Convert(body, typeof(double));
        else if (body.Type == typeof(decimal?))
            selectorBody = Expression.Convert(body, typeof(double?));

        var selector = Expression.Lambda(selectorBody, item);

        if (fn is not ("sum" or "avg")) return null;

        var aggregateMethod = typeof(Enumerable).GetMethods()
            .Where(m => m.Name.Equals(fn, StringComparison.OrdinalIgnoreCase)
                        && m.GetParameters().Length == 2)
            .FirstOrDefault(m =>
            {
                if (!m.IsGenericMethodDefinition) return false;
                var paramType = m.GetParameters()[1].ParameterType;
                if (!paramType.IsGenericType) return false;
                var args = paramType.GetGenericArguments();
                return args.Length == 2 && args[1] == selectorBody.Type;
            });

        if (aggregateMethod is null) return null;
        var genericMethod = aggregateMethod.MakeGenericMethod(sourceType);
        return Expression.Call(genericMethod, grouping, selector);
    }

    private static bool TryBuildMemberAccess(Expression root, Type rootType, string path, out Expression access)
    {
        access = root;
        if (!SafePropertyResolver.TryResolveChain(rootType, path, out var chain)) return false;
        if (chain.Any(p => SafePropertyResolver.TryGetCollectionElementType(p.PropertyType, out _))) return false;
        foreach (var prop in chain)
            access = Expression.Property(access, prop);
        return true;
    }

    private static string ToProjectionName(string path)
    {
        var last = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? path;
        return string.Concat(last.Where(ch => ch != '_'));
    }

    private static int GetFuncArity(Type expressionType)
    {
        if (!expressionType.IsGenericType) return 0;
        var wrapped = expressionType.GetGenericArguments().FirstOrDefault();
        if (wrapped is null || !wrapped.IsGenericType) return 0;
        return wrapped.GetGenericArguments().Length;
    }
}
