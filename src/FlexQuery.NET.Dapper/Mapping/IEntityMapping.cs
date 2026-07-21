using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Mapping;

/// <summary>
/// Configuration for a database entity.
/// </summary>
internal interface IEntityMapping
{
    /// <summary>Entity type.</summary>
    Type Type { get; }

    /// <summary>Table name.</summary>
    string TableName { get; }

    /// <summary>Schema name.</summary>
    string? Schema { get; }

    /// <summary>Table alias.</summary>
    string? TableAlias { get; set; }

    /// <summary>Get the column name for a property.</summary>
    string GetColumnName(string propertyName);

    /// <summary>Get the property name for a column.</summary>
    string? GetPropertyName(string columnName);

    /// <summary>Get all mapped property names.</summary>
    IEnumerable<string> GetProperties();

    /// <summary>Get primary key property names.</summary>
    IEnumerable<string> GetKeyProperties();

    /// <summary>Check whether a property is ignored.</summary>
    bool IsIgnored(string propertyName);

    /// <summary>Get relationship mapping metadata.</summary>
    RelationshipMapping? GetRelationship(string navigationProperty);

    /// <summary>Get join information for an include relationship.</summary>
    JoinInfo? GetJoinInfo(string navigationProperty);
}
