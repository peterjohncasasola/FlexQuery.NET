namespace FlexQuery.NET.Models;

/// <summary>
/// Represents a named parameter used in a parameterized query, such as a SQL parameter or a LINQ parameter.
/// </summary>
public sealed record QueryParameter
{
    /// <summary>Gets the name of the parameter.</summary>
    public string Name { get; init; }
    /// <summary>Gets the value of the parameter. May be null.</summary>
    public object? Value { get; init; }
    /// <summary>Gets the database type hint for the parameter, if specified.</summary>
    public QueryDbType? DbType { get; init; }
    /// <summary>Gets the direction of the parameter. Defaults to Input.</summary>
    public QueryParameterDirection Direction { get; init; } = QueryParameterDirection.Input;

    /// <summary>Initializes a new instance with a parameter name and value.</summary>
    /// <param name="name">The parameter name. Must not be null.</param>
    /// <param name="value">The parameter value.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public QueryParameter(string name, object? value)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Value = value;
    }

    /// <summary>Initializes a new instance with a parameter name, value, type hint, and direction.</summary>
    /// <param name="name">The parameter name. Must not be null.</param>
    /// <param name="value">The parameter value.</param>
    /// <param name="dbType">Optional database type hint for the parameter.</param>
    /// <param name="direction">The parameter direction. Defaults to Input.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public QueryParameter(string name, object? value, QueryDbType? dbType, QueryParameterDirection direction = QueryParameterDirection.Input)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Value = value;
        DbType = dbType;
        Direction = direction;
    }
}
