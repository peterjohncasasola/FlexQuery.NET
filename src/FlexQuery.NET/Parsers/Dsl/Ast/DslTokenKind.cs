namespace FlexQuery.NET.Parsers.Dsl;

/// <summary>Token kinds used by the filter DSL parser.</summary>
internal enum DslTokenKind
{
    /// <summary>A field name, operator, or raw value segment.</summary>
    Identifier,
    /// <summary>The ':' separator between condition parts.</summary>
    Colon,
    /// <summary>The '&amp;' logical AND operator.</summary>
    And,
    /// <summary>The '|' logical OR operator.</summary>
    Or,
    /// <summary>The unary '!' NOT operator.</summary>
    Not,
    /// <summary>The '(' group opener.</summary>
    OpenParen,
    /// <summary>The ')' group closer.</summary>
    CloseParen,
    /// <summary>The ';' option separator.</summary>
    Semicolon,
    /// <summary>End of input.</summary>
    End
}