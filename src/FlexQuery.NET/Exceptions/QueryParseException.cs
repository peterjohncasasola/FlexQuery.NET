using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Exceptions;

/// <summary>
/// Thrown when a supported FlexQuery query parameter is supplied with a value that
/// cannot be parsed using the configured query syntax (NativeDsl, Fql, or MiniOData).
/// </summary>
/// <remarks>
/// This is a top-level exception. The <see cref="FlexQueryException"/> contains
/// parser-specific details from the underlying grammar parser (e.g.,
/// <c>DslParseException</c> or <c>FqlParseException</c>).
///
/// Unknown query parameters (those not in the official FlexQuery set) should never
/// produce this exception — they are ignored by the parser infrastructure.
/// </remarks>
public sealed class QueryParseException : FlexQueryException
{
    /// <summary>
    /// Creates a new <see cref="QueryParseException"/> for a specific parameter.
    /// </summary>
    /// <param name="parameterName">The query parameter name that failed to parse (e.g. "filter", "sort", "select").</param>
    /// <param name="syntax">The configured query syntax that was used during parsing.</param>
    /// <param name="receivedValue">The raw value that was supplied for the parameter, or <c>null</c> if none.</param>
    /// <param name="innerException">The parser-specific exception with grammar-level detail.</param>
    /// <param name="position">Zero-based character position in the input, or -1 when unknown.</param>
    /// <param name="expected">The token or syntax the parser expected, or null.</param>
    /// <param name="found">The token or syntax the parser actually found, or null.</param>
    public QueryParseException(
        string parameterName,
        QuerySyntax syntax,
        string? receivedValue,
        Exception innerException,
        int position = -1,
        string? expected = null,
        string? found = null)
        : base(BuildMessage(parameterName, receivedValue, innerException, position, expected, found), innerException)
    {
        ParameterName = parameterName;
        Syntax = syntax;
        ReceivedValue = receivedValue;
        Position = position;
        Expected = expected;
        Found = found;
    }

    /// <summary>
    /// Creates a new <see cref="QueryParseException"/> without structured position metadata.
    /// </summary>
    public QueryParseException(string parameterName, QuerySyntax syntax, string? receivedValue, Exception innerException)
        : this(parameterName, syntax, receivedValue, innerException, position: -1, expected: null, found: null)
    {
    }

    /// <summary>The query parameter name that could not be parsed (e.g. "filter", "sort", "select").</summary>
    public string ParameterName { get; }

    /// <summary>The configured query syntax that was active during parsing (NativeDsl, Fql, MiniOData).</summary>
    public QuerySyntax Syntax { get; }

    /// <summary>The raw value that was supplied for the parameter. <c>null</c> when no value was provided.</summary>
    public string? ReceivedValue { get; }

    /// <summary>The raw input string being parsed. Equivalent to <see cref="ReceivedValue"/>.</summary>
    public string? Input => ReceivedValue;

    /// <summary>Zero-based character position in the input, or -1 when unknown.</summary>
    public int Position { get; }

    /// <summary>The token or syntax the parser expected, or null.</summary>
    public string? Expected { get; }

    /// <summary>The token or syntax the parser actually found, or null.</summary>
    public string? Found { get; }

    private static string BuildMessage(
        string parameterName,
        string? receivedValue,
        Exception innerException,
        int position,
        string? expected,
        string? found)
    {
        var input = receivedValue ?? string.Empty;
        var error = string.IsNullOrWhiteSpace(innerException.Message)
            ? "Unknown parse error."
            : innerException.Message;

        var hasPosition = position >= 0 && position < input.Length;
        var hasExpected = !string.IsNullOrWhiteSpace(expected);

        var sections = new List<string?>
        {
            $"Failed to parse '{parameterName}' query parameter.",
            string.Empty,
            "Input:",
            input
        };

        if (hasPosition)
        {
            sections.Add(BuildCaret(input, position));
        }

        sections.Add(string.Empty);
        sections.Add("Error:");
        sections.Add(error);

        if (hasExpected)
        {
            sections.Add(string.Empty);
            sections.Add("Expected:");
            sections.Add(expected);
        }

        if (!hasPosition) return string.Join("\n", sections.Where(s => s is not null));
        
        sections.Add(string.Empty);
        sections.Add($"Position: {position}");
        sections.Add(ComputeLineAndColumn(input, position));

        return string.Join("\n", sections.Where(s => s is not null));
    }

    private static string? BuildCaret(string input, int position)
    {
        if (position < 0 || position >= input.Length)
            return null;

        return new string(' ', position) + "^";
    }

    private static string? ComputeLineAndColumn(string input, int position)
    {
        if (position < 0 || position >= input.Length)
            return null;

        var line = 1;
        var column = 1;

        for (var i = 0; i < position; i++)
        {
            if (input[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return $"Line: {line}, Column: {column}";
    }
}
