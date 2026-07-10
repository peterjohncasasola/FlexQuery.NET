using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Parsers.MiniOData;

/// <summary>
/// Dedicated parser for the OData <c>$expand</c> query option.
/// <para>
/// Grammar: a comma-separated list of flat navigation property paths. OData <c>/</c> separators
/// are translated to dot-notation and each path is validated with
/// <see cref="ParserUtilities.IsValidPropertyPath"/>.
/// </para>
/// <para>
/// Nested query options inside <c>$expand</c> (for example <c>$expand=Orders($filter=...)</c>)
/// are intentionally unsupported by MiniOData and are rejected with a clear exception, because
/// FlexQuery keeps filtering/sorting/projection/paging/aggregation as top-level operations.
/// </para>
/// </summary>
internal static class MiniODataExpandParser
{
    /// <summary>Parses an OData <c>$expand</c> value into a list of navigation paths.</summary>
    /// <param name="expand">The raw <c>$expand</c> value (e.g. <c>Orders,Items</c>).</param>
    /// <exception cref="MiniODataParseException">
    /// Thrown when the value is empty, contains an empty path, a nested query option, or an
    /// invalid navigation property path.
    /// </exception>
    public static List<string> Parse(string? expand)
    {
        if (string.IsNullOrWhiteSpace(expand))
            throw new MiniODataParseException(
                "$expand value is empty. Expected comma-separated navigation paths.");

        var result = new List<string>();

        foreach (var part in expand.Split(','))
        {
            var path = part.Trim();

            if (path.Length == 0)
                throw new MiniODataParseException(
                    "Empty navigation path in $expand. Expected comma-separated navigation paths.");

            if (path.Contains('('))
                throw new MiniODataParseException(
                    "Nested query options inside $expand are not supported by MiniOData. " +
                    "$expand accepts a flat list of navigation paths (e.g. '$expand=Orders,Items').");

            path = path.Replace('/', '.');

            if (!ParserUtilities.IsValidPropertyPath(path.AsSpan()))
                throw new MiniODataParseException(
                    $"Invalid navigation path '{path}' in $expand. " +
                    "Property paths must be dot-separated identifiers (e.g. 'Orders.Items').");

            result.Add(path);
        }

        return result;
    }
}
