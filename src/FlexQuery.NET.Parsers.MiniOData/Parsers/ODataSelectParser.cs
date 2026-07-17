using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Parsers.MiniOData;

/// <summary>
/// Strict parser for the OData <c>$select</c> query option.
/// <para>
/// Grammar: a comma-separated list of property paths. OData <c>/</c> separators are translated
/// to dot-notation and each path is validated with <see cref="ParserUtilities.IsValidPropertyPath"/>.
/// </para>
/// </summary>
internal static class ODataSelectParser
{
    /// <summary>Parses an OData <c>$select</c> value into a list of property paths.</summary>
    /// <param name="select">The raw <c>$select</c> value (e.g. <c>Id,Name,Customer/Region</c>).</param>
    /// <exception cref="MiniODataParseException">
    /// Thrown when the value is empty, contains an empty field, or an invalid property path.
    /// </exception>
    public static List<SelectNode> Parse(string? select)
    {
        if (string.IsNullOrWhiteSpace(select))
            throw new MiniODataParseException(
                "$select value is empty. Expected comma-separated field paths.");

        var result = new List<SelectNode>();

        foreach (var part in select.Split(','))
        {
            var field = part.Trim();

            if (field.Length == 0)
                throw new MiniODataParseException(
                    "Empty field in $select. Expected comma-separated field paths.");

            field = field.Replace('/', '.');

            if (!ParserUtilities.IsValidPropertyPath(field.AsSpan()))
                throw new MiniODataParseException(
                    $"Invalid property path '{field}' in $select. " +
                    "Property paths must be dot-separated identifiers (e.g. 'Customer.Region').");

            result.Add(new SelectNode { Field = field });
        }

        return result;
    }
}
