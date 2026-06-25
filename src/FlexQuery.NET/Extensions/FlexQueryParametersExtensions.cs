using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET;

/// <summary>
/// Provides extension methods for converting <see cref="FlexQueryParameters"/>
/// into <see cref="QueryOptions"/> instances.
/// </summary>
public static class FlexQueryParametersExtensions
{
    /// <summary>
    /// Converts the specified <see cref="FlexQueryParameters"/> into a
    /// <see cref="QueryOptions"/> instance that can be executed by FlexQuery.
    /// </summary>
    /// <param name="parameters">
    /// The incoming query parameters, typically bound from an HTTP request.
    /// </param>
    /// <returns>
    /// A <see cref="QueryOptions"/> instance representing the parsed query.
    /// </returns>
    /// <remarks>
    /// This method parses all supported FlexQuery parameters, including:
    /// <list type="bullet">
    /// <item><description>Filter</description></item>
    /// <item><description>Sort</description></item>
    /// <item><description>Select (Projection)</description></item>
    /// <item><description>Include</description></item>
    /// <item><description>Group By</description></item>
    /// <item><description>Aggregates</description></item>
    /// <item><description>Having</description></item>
    /// <item><description>Paging</description></item>
    /// </list>
    ///
    /// This extension is useful when inspecting or modifying the parsed
    /// <see cref="QueryOptions"/> before executing a query, or when generating
    /// diagnostics and debugging information.
    /// </remarks>
    public static QueryOptions ToQueryOptions(this FlexQueryParameters parameters)
    {
        return QueryOptionsParser.Parse(parameters);
    }
}