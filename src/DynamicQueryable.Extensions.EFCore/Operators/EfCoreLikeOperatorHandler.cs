using System.Linq.Expressions;
using DynamicQueryable.Constants;
using DynamicQueryable.Operators;
using Microsoft.EntityFrameworkCore;

namespace DynamicQueryable.Extensions.EFCore.Operators;

/// <summary>
/// EF Core LIKE implementation backed by EF.Functions.Like.
/// </summary>
public sealed class EfCoreLikeOperatorHandler : IOperatorHandler
{
    /// <inheritdoc />
    public string Operator => FilterOperators.Like;

    /// <inheritdoc />
    public Expression? Build(Expression member, string? rawValue)
    {
        var underlying = Nullable.GetUnderlyingType(member.Type) ?? member.Type;
        if (underlying != typeof(string)) return null;

        var likeMethod = typeof(DbFunctionsExtensions).GetMethod(
            nameof(DbFunctionsExtensions.Like),
            [typeof(DbFunctions), typeof(string), typeof(string)]);
        if (likeMethod is null) return null;

        var dbFunctions = Expression.Property(null, typeof(EF), nameof(EF.Functions));
        var pattern = Expression.Constant(rawValue ?? string.Empty, typeof(string));
        return Expression.Call(likeMethod, dbFunctions, member, pattern);
    }
}
