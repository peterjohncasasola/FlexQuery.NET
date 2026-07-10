using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers.MiniOData.Models;

namespace FlexQuery.NET.Parsers.MiniOData;

/// <summary>
/// Provides extension methods for working with MiniOData requests.
/// </summary>
public static class MiniODataExtensions
{
    /// <summary>
    /// Converts a <see cref="MiniODataRequest"/> into a <see cref="QueryOptions"/> instance.
    /// </summary>
    /// <param name="request">
    /// The MiniOData request to convert.
    /// </param>
    /// <returns>
    /// A <see cref="QueryOptions"/> populated from the MiniOData request.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="request"/> is <see langword="null"/>.
    /// </exception>
    public static QueryOptions ToQueryOptions(this MiniODataRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ODataQueryParameterParser.Parse(request);
    }
}