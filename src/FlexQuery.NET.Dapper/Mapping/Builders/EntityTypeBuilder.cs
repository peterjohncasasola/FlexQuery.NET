using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Mapping.Builders;

/// <summary>
/// Fluent builder for configuring entity type mappings.
/// </summary>
public class EntityTypeBuilder<TEntity> where TEntity : class
{
    private readonly EntityMapping _mapping;

    /// <summary>Creates a new builder for the given entity mapping.</summary>
    public EntityTypeBuilder(EntityMapping mapping)
    {
        _mapping = mapping;
    }

    /// <summary>Specifies the table name for the entity.</summary>
    public EntityTypeBuilder<TEntity> ToTable(string tableName)
    {
        _mapping.TableName = tableName;
        return this;
    }

    /// <summary>Sets a table alias for the entity in SQL queries.</summary>
    public EntityTypeBuilder<TEntity> HasAlias(string tableAlias)
    {
        _mapping.TableAlias = tableAlias;
        return this;
    }

    /// <summary>Begins configuration for a scalar property.</summary>
    public PropertyBuilder Property<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
    {
        var propertyInfo = GetPropertyInfo(propertyExpression);
        var propMapping = _mapping.GetOrAddProperty(propertyInfo);
        return new PropertyBuilder(propMapping);
    }

    /// <summary>Begins configuration for a one-to-many relationship.</summary>
    public RelationshipBuilder HasMany<TRelatedEntity>(Expression<Func<TEntity, IEnumerable<TRelatedEntity>>> navigationExpression)
        where TRelatedEntity : class
    {
        var propertyInfo = GetPropertyInfo(navigationExpression);
        var relMapping = _mapping.GetOrAddRelationship(propertyInfo, typeof(TRelatedEntity), RelationshipType.OneToMany);
        return new RelationshipBuilder(relMapping);
    }

    /// <summary>Begins configuration for a reference navigation property (many-to-one or one-to-one from the dependent side).</summary>
    public RelationshipBuilder HasOne<TRelatedEntity>(Expression<Func<TEntity, TRelatedEntity>> navigationExpression)
        where TRelatedEntity : class?
    {
        var propertyInfo = GetPropertyInfo(navigationExpression);
        var relMapping = _mapping.GetOrAddRelationship(propertyInfo, typeof(TRelatedEntity), RelationshipType.ManyToOne);
        return new RelationshipBuilder(relMapping);
    }

    private PropertyInfo GetPropertyInfo<TProperty>(Expression<Func<TEntity, TProperty>> expression)
    {
        if (expression.Body is MemberExpression memberExpression && memberExpression.Member is PropertyInfo propertyInfo)
        {
            return propertyInfo;
        }
        throw new ArgumentException("Expression must be a property access.", nameof(expression));
    }
}
