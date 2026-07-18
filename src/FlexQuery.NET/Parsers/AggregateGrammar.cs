using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Parsers.Dsl;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Canonical grammar and validation for DSL aggregate expressions of the form
/// <c>function:field</c>. Both the <c>aggregate</c> parameter parser and the
/// <c>having</c> clause parser delegate to this implementation so that aggregate
/// syntax has a single source of truth.
/// </summary>
internal static class AggregateGrammar
{
    /// <summary>
    /// Parses and validates a DSL aggregate reference (<c>function:field</c>).
    /// </summary>
    /// <param name="raw">The raw aggregate expression, e.g. <c>sum:total</c>.</param>
    /// <returns>A validated aggregate reference.</returns>
    /// <exception cref="DslParseException">Thrown when the expression is malformed.</exception>
    public static AggregateFunctionField ParseFunctionField(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new DslParseException(
                "Unable to parse aggregate expression. Expected format: Function:Field.",
                position: -1);

        var parts = raw.Split(':');

        if (parts.Length != 2)
            throw new DslParseException(
                "Unable to parse aggregate expression. Expected format: Function:Field.",
                position: -1);

        var functionPart = parts[0].Trim();
        var fieldPart = parts[1].Trim();

        if (functionPart.Length == 0)
            throw new DslParseException(
                "Unable to parse aggregate expression. Missing function.",
                position: -1);

        if (fieldPart.Length == 0)
            throw new DslParseException(
                "Unable to parse aggregate expression. Missing field.",
                position: -1);

        string functionName;
        AggregateFunction function;

        try
        {
            functionName = functionPart.ToLowerInvariant();
            if (functionName == "average") functionName = "avg";
            function = AggregateFunctionConverter.Parse(functionName);
        }
        catch
        {
            throw new DslParseException(
                $"Unable to parse aggregate expression. Unrecognized aggregate function '{functionPart}'.",
                position: -1);
        }

        if (function == AggregateFunction.Count && fieldPart == "*")
        {
            throw new DslParseException(
                "Unable to parse aggregate expression. count:* is not supported. Use count:<collection> or another aggregate over a property instead.",
                position: -1);
        }

        if (fieldPart != "*" && !ParserUtilities.IsValidPropertyPath(fieldPart.AsSpan()))
        {
            throw new DslParseException(
                $"Invalid field '{fieldPart}' in aggregate expression. Field must be a valid property path.",
                position: -1);
        }

        return new AggregateFunctionField(function, fieldPart, functionName);
    }
}

/// <summary>
/// Represents a parsed DSL aggregate reference (<c>function:field</c>).
/// </summary>
/// <param name="Function">The resolved aggregate function.</param>
/// <param name="Field">The validated field path.</param>
/// <param name="FunctionName">The normalized function name used for alias generation.</param>
internal sealed record AggregateFunctionField(AggregateFunction Function, string Field, string FunctionName);
