using System.Linq.Expressions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Security;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.Resolvers;

internal static class FieldResolver
{
    public static bool TryResolveMappedExpression(
        Expression current,
        string path,
        QueryOptions options,
        out Expression resolvedExpression,
        out Type resolvedType)
    {
        resolvedExpression = null!;
        resolvedType = null!;

        if (string.IsNullOrWhiteSpace(path)) return false;

        var mappings = GetMappings(options);

        if (mappings != null && mappings.TryGetValue(path, out var exactMappedLambda))
        {
            resolvedExpression = ReplaceParameter(exactMappedLambda, current);
            resolvedType = exactMappedLambda.ReturnType;
            return true;
        }

        return false;
    }

    public static bool TryResolveType(
        Type entityType,
        string path,
        BaseQueryOptions? execOptions,
        out Type resolvedType)
    {
        resolvedType = null!;
        if (string.IsNullOrWhiteSpace(path)) return false;

        var mappings = execOptions?.ExpressionMappings;

        if (mappings != null && mappings.TryGetValue(path, out var exactMappedLambda))
        {
            resolvedType = exactMappedLambda.ReturnType;
            return true;
        }

        if (SafePropertyResolver.TryResolveChain(entityType, path, out var fullChain) && fullChain.Count > 0)
        {
            resolvedType = fullChain.Last().PropertyType;
            return true;
        }

        return false;
    }

    private static IReadOnlyDictionary<string, LambdaExpression>? GetMappings(QueryOptions options)
    {
        if (options.Items.TryGetValue(ContextKeys.ExpressionMappings, out var mappingsObj) && mappingsObj is IReadOnlyDictionary<string, LambdaExpression> mappings)
        {
            return mappings;
        }
        return null;
    }

    private static Expression ReplaceParameter(LambdaExpression lambda, Expression replacement)
    {
        var map = new Dictionary<ParameterExpression, Expression>
        {
            { lambda.Parameters[0], replacement }
        };
        return ParameterRebinder.ReplaceParameters(map, lambda.Body);
    }
}
