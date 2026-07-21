using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Mapping.Builders;

/// <summary>
/// Fluent builder for configuring relationship mappings.
/// </summary>
public sealed class RelationshipBuilder
{
    private readonly RelationshipMapping _mapping;

    internal RelationshipBuilder(RelationshipMapping mapping)
    {
        _mapping = mapping;
    }

    /// <summary>Sets the foreign key column name.</summary>
    public RelationshipBuilder HasForeignKey(string foreignKey)
    {
        _mapping.ForeignKey = foreignKey;
        return this;
    }

    /// <summary>Sets the foreign key property using a lambda expression.</summary>
    public RelationshipBuilder HasForeignKey<TProperty>(Expression<Func<TProperty, object?>> foreignKeyExpression)
    {
        var propertyName = GetPropertyName(foreignKeyExpression);
        _mapping.ForeignKey = propertyName;
        return this;
    }

    /// <summary>Sets the principal key column name on the target entity.</summary>
    public RelationshipBuilder HasPrincipalKey(string principalKey)
    {
        _mapping.PrincipalKey = principalKey;
        return this;
    }

    /// <summary>Sets the principal key property using a lambda expression.</summary>
    public RelationshipBuilder HasPrincipalKey<TProperty>(Expression<Func<TProperty, object?>> principalKeyExpression)
    {
        var propertyName = GetPropertyName(principalKeyExpression);
        _mapping.PrincipalKey = propertyName;
        return this;
    }

    /// <summary>Configures a many-to-many relationship using an explicit join table.</summary>
    public RelationshipBuilder UsingJoinTable(string joinTableName, string joinTableForeignKey, string joinTableTargetKey)
    {
        _mapping.RelationshipType = RelationshipType.ManyToMany;
        _mapping.JoinTable = joinTableName;
        _mapping.JoinTableForeignKey = joinTableForeignKey;
        _mapping.JoinTableTargetKey = joinTableTargetKey;
        return this;
    }

    private static string GetPropertyName<TProperty>(Expression<Func<TProperty, object?>> expression)
    {
        if (expression.Body is MemberExpression memberExpression &&
            memberExpression.Member is PropertyInfo propertyInfo)
        {
            return propertyInfo.Name;
        }

        if (expression.Body is UnaryExpression unary &&
            unary.NodeType == ExpressionType.Convert &&
            unary.Operand is MemberExpression memberOperand &&
            memberOperand.Member is PropertyInfo convertedProperty)
        {
            return convertedProperty.Name;
        }

        throw new ArgumentException(
            "Expression must be a property access.",
            nameof(expression));
    }
}
