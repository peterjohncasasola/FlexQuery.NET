namespace FlexQuery.NET.Models;

/// <summary>Defines the database column types supported for query parameters and projections.</summary>
public enum QueryDbType
{
    /// <summary>An untyped or object reference.</summary>
    Object,
    /// <summary>A fixed or variable-length string.</summary>
    String,
    /// <summary>A 32-bit signed integer.</summary>
    Int32,
    /// <summary>A 64-bit signed integer.</summary>
    Int64,
    /// <summary>A double-precision floating point number.</summary>
    Double,
    /// <summary>A Boolean value (true/false).</summary>
    Boolean,
    /// <summary>A date and time value.</summary>
    DateTime,
    /// <summary>A 128-bit decimal with high precision.</summary>
    Decimal,
    /// <summary>An 8-bit unsigned integer.</summary>
    Byte,
    /// <summary>A fixed-length binary data.</summary>
    Binary,
    /// <summary>A globally unique identifier.</summary>
    Guid
}