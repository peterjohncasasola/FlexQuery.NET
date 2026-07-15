using FlexQuery.NET.Exceptions;

namespace FlexQuery.NET.Parsers.MiniOData;

/// <summary>
/// Exception thrown when the Mini OData parser encounters invalid syntax.
/// </summary>
public sealed class MiniODataParseException : FlexQueryException
{
    /// <summary>Creates a new parse exception with the specified message.</summary>
    public MiniODataParseException(string message) : base(message) { }

    /// <summary>Creates a new parse exception with the specified message and inner exception.</summary>
    public MiniODataParseException(string message, Exception innerException) : base(message, innerException) { }
}
