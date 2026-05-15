using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Mapping.Builders;

public class EntityTypeBuilder<TEntity> where TEntity : class
{
    private readonly EntityMapping _mapping;

    public EntityTypeBuilder(EntityMapping mapping)
    {
        _mapping = mapping;
    }

    public EntityTypeBuilder<TEntity> ToTable(string tableName)
    {
        _mapping.TableName = tableName;
        return this;
    }

    public EntityTypeBuilder<TEntity> HasAlias(string tableAlias)
    {
        _mapping.TableAlias = tableAlias;
        return this;
    }

    public PropertyBuilder Property<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
    {
        var propertyInfo = GetPropertyInfo(propertyExpression);
        var propMapping = _mapping.GetOrAddProperty(propertyInfo);
        return new PropertyBuilder(propMapping);
    }

    public RelationshipBuilder HasMany<TRelatedEntity>(Expression<Func<TEntity, IEnumerable<TRelatedEntity>>> navigationExpression)
        where TRelatedEntity : class
    {
        var propertyInfo = GetPropertyInfo(navigationExpression);
        var relMapping = _mapping.GetOrAddRelationship(propertyInfo, typeof(TRelatedEntity), RelationshipType.OneToMany);
        return new RelationshipBuilder(relMapping);
    }

    public RelationshipBuilder HasOne<TRelatedEntity>(Expression<Func<TEntity, TRelatedEntity>> navigationExpression)
        where TRelatedEntity : class
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
