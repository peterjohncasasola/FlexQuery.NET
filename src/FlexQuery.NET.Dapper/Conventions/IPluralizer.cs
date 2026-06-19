namespace FlexQuery.NET.Dapper.Conventions;

/// <summary>
/// Service responsible for pluralizing entity names into table names.
/// </summary>
public interface IPluralizer
{
    /// <summary>
    /// Pluralizes a singular name.
    /// </summary>
    string Pluralize(string name);
}
