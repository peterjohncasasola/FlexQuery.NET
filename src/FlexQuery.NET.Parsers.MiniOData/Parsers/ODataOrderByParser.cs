using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Parsers.MiniOData;

/// <summary>
/// Strict parser for the OData <c>$orderby</c> query option.
/// <para>
/// Grammar: a comma-separated list of <c>field [asc|desc]</c> items. Each field is a
/// property path (OData <c>/</c> separators are translated to dot-notation and validated with
/// <see cref="ParserUtilities.IsValidPropertyPath"/>).
/// </para>
/// </summary>
internal static class ODataOrderByParser
{
    /// <summary>Parses an OData <c>$orderby</c> value into a list of <see cref="SortNode"/>.</summary>
    /// <param name="orderBy">The raw <c>$orderby</c> value (e.g. <c>Name desc,CreatedAt asc</c>).</param>
    /// <exception cref="MiniODataParseException">
    /// Thrown when the value is empty or contains malformed grammar.
    /// </exception>
    public static List<SortNode> Parse(string? orderBy)
    {
        if (string.IsNullOrWhiteSpace(orderBy))
            throw new MiniODataParseException(
                "$orderby value is empty. Expected one or more 'field [asc|desc]' items.");

        var result = new List<SortNode>();

        foreach (var segment in orderBy.Split(',', StringSplitOptions.TrimEntries))
        {
            if (segment.Length == 0)
                throw new MiniODataParseException(
                    "$orderby contains an empty item. Expected format: field [asc|desc].");

            var parts = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                throw new MiniODataParseException(
                    "$orderby contains an empty item. Expected format: field [asc|desc].");

            var field = parts[0].Replace('/', '.');
            if (!ParserUtilities.IsValidPropertyPath(field.AsSpan()))
                throw new MiniODataParseException(
                    $"Invalid property path '{field}' in $orderby. " +
                    "Property paths must be dot-separated identifiers (e.g. 'Customer.Region').");

            bool descending;
            if (parts.Length == 1)
            {
                descending = false;
            }
            else if (parts.Length == 2)
            {
                var direction = parts[1];
                if (direction.Equals("desc", StringComparison.OrdinalIgnoreCase))
                {
                    descending = true;
                }
                else if (direction.Equals("asc", StringComparison.OrdinalIgnoreCase))
                {
                    descending = false;
                }
                else
                {
                    throw new MiniODataParseException(
                        $"Invalid sort direction '{direction}' in $orderby. Expected 'asc' or 'desc'.");
                }
            }
            else
            {
                throw new MiniODataParseException(
                    $"Too many tokens in $orderby item '{segment}'. Expected format: field [asc|desc].");
            }

            result.Add(new SortNode
            {
                Field = field,
                Descending = descending
            });
        }

        return result;
    }
}
