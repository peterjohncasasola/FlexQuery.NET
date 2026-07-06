using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Expressions;

namespace FlexQuery.NET;

/// <summary>
/// Provides the <see cref="SeekAfter{T,TKey}"/> fluent API for keyset (cursor-based) pagination.
/// Generates WHERE predicates instead of Skip/Take, enabling index seeks for large datasets.
///
/// Use keyset pagination for infinite scrolling, mobile APIs, REST APIs with sequential
/// "next page" navigation, and large datasets where offset performance degrades.
///
/// Keyset pagination does NOT support random page access (e.g. "Go to page 57").
///
/// Deterministic ordering:
/// Keyset pagination requires deterministic ordering to guarantee stable page boundaries.
/// Always include a unique column (e.g., Id) as the final ordering column:
/// <code>
/// .OrderBy(x => x.CreatedDate).ThenBy(x => x.Id)
/// </code>
/// Using non-unique ordering may cause duplicate or skipped rows across pages.
/// </summary>
public static class KeysetPaginationExtensions
{
    /// <summary>
    /// Applies a keyset (seek) predicate using the cursor value from the last item on the previous page.
    /// Requires at least one OrderBy/ThenBy clause before calling. Single-column ordering only.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TKey">The type of the cursor value.</typeparam>
    /// <param name="query">The sorted queryable.</param>
    /// <param name="cursor">The cursor value from the last item of the previous page.</param>
    /// <returns>The queryable with a WHERE predicate applied. Chain .Take(n) to set page size.</returns>
    public static IQueryable<T> SeekAfter<T, TKey>(
        this IOrderedQueryable<T> query, TKey cursor)
    {
        var orderings = KeysetPaginationBuilder.ExtractOrderings(query.Expression);
        if (orderings.Count == 0)
            throw new KeysetPaginationException(
                "Keyset pagination requires at least one OrderBy or ThenBy clause. Call .OrderBy() before .SeekAfter().");

        if (orderings.Count != 1)
            throw new KeysetPaginationException(
                $"Cursor has 1 value(s) but the query has {orderings.Count} ordering column(s). " +
                $"Use SeekAfter with {orderings.Count} value(s) or use a single-column ordering.");

        var predicate = KeysetPaginationBuilder.BuildSeekPredicate<T>(orderings, [cursor]);
        return query.Where(predicate);
    }
}
