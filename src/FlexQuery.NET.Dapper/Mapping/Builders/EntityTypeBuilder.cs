using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Mapping.Builders;

public sealed class EntityTypeBuilder<TEntity> where TEntity : class
{
    private readonly EntityMapping _mapping;

    internal EntityTypeBuilder(EntityMapping mapping)
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

    public EntityTypeBuilder<TEntity> HasKey(Expression<Func<TEntity, object?>> keyExpression)
    {
        var properties = ExtractProperties(keyExpression);
        foreach (var prop in properties)
        {
            var propMapping = _mapping.GetOrAddProperty(prop);
            propMapping.IsPrimaryKey = true;
        }
        return this;
    }

    public RelationshipBuilder HasMany<TRelatedEntity>(Expression<Func<TEntity, IEnumerable<TRelatedEntity>>> navigationExpression)
        where TRelatedEntity : class
    {
        var propertyInfo = GetPropertyInfo(navigationExpression);
        var relMapping = _mapping.GetOrAddRelationship(propertyInfo, typeof(TRelatedEntity), RelationshipType.OneToMany);
        return new RelationshipBuilder(relMapping);
    }

    public RelationshipBuilder HasOne<TRelatedEntity>(Expression<Func<TEntity, TRelatedEntity>> navigationExpression)
        where TRelatedEntity : class?
    {
        var propertyInfo = GetPropertyInfo(navigationExpression);
        var relMapping = _mapping.GetOrAddRelationship(propertyInfo, typeof(TRelatedEntity), RelationshipType.ManyToOne);
        return new RelationshipBuilder(relMapping);
    }

    private static List<PropertyInfo> ExtractProperties(Expression<Func<TEntity, object?>> expression)
    {
        var body = expression.Body;
        if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            body = unary.Operand;

        if (body is MemberExpression member)
        {
            if (member.Member is PropertyInfo singleProp)
                return [singleProp];
        }

        if (body is NewExpression newExpr)
        {
            return newExpr.Arguments
                .Select(a => a is MemberExpression me ? me.Member as PropertyInfo : null)
                .Where(p => p != null)
                .Cast<PropertyInfo>()
                .ToList();
        }

        throw new ArgumentException("Expression must be a property access or anonymous type with property initializers.", nameof(expression));
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
