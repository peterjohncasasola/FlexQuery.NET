using FlexQuery.NET.Models;

namespace FlexQuery.NET.Parsers.MiniOData;

/// <summary>
/// Implementation of <see cref="IQueryParser"/> for OData-compatible syntax.
/// </summary>
public sealed class MiniODataQueryParser : IQueryParser
{
    /// <inheritdoc />
    public QuerySyntax Syntax => QuerySyntax.MiniOData;

    /// <inheritdoc />
    public bool CanParse(FlexQueryParameters parameters)
    {
        // Detect OData by checking for $ prefix in any raw parameter keys.
        if (parameters.RawParameters != null)
        {
            foreach (var key in parameters.RawParameters.Keys)
            {
                if (key.StartsWith("$")) return true;
            }
        }

        // Also check if Filter string looks like OData (e.g., contains ' eq ')
        // though this is less reliable than key detection.
        if (!string.IsNullOrWhiteSpace(parameters.Filter))
        {
            var f = parameters.Filter;
            if (f.Contains(" eq ", StringComparison.OrdinalIgnoreCase) || 
                f.Contains(" ne ", StringComparison.OrdinalIgnoreCase) ||
                f.Contains("contains(", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public QueryOptions Parse(FlexQueryParameters parameters)
    {
        // If we have raw parameters (ideal for OData), use them.
        if (parameters.RawParameters != null && parameters.RawParameters.Count > 0)
        {
            return ODataQueryParameterParser.Parse(parameters.RawParameters);
        }

        // Otherwise, map from FlexQueryParameters properties.
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        if (!string.IsNullOrWhiteSpace(parameters.Filter)) dict["filter"] = parameters.Filter;
        if (!string.IsNullOrWhiteSpace(parameters.Sort)) dict["orderby"] = parameters.Sort;
        if (!string.IsNullOrWhiteSpace(parameters.Select)) dict["select"] = parameters.Select;
        if (!string.IsNullOrWhiteSpace(parameters.Include)) dict["expand"] = parameters.Include;
        
        if (parameters.PageSize.HasValue) dict["top"] = parameters.PageSize.Value.ToString();
        if (parameters.Page.HasValue && parameters.PageSize.HasValue)
        {
            var skip = (parameters.Page.Value - 1) * parameters.PageSize.Value;
            dict["skip"] = skip.ToString();
        }
        
        if (parameters.IncludeCount.HasValue) dict["count"] = parameters.IncludeCount.Value.ToString().ToLowerInvariant();

        return ODataQueryParameterParser.Parse(dict);
    }
}
