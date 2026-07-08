using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Mapping.Builders;

/// <summary>
/// Configures the mapping metadata for an entity type.
/// </summary>
/// <typeparam name="TEntity">The entity type being configured.</typeparam>
public sealed class EntityTypeBuilder<TEntity> where TEntity : class
{
    private readonly EntityMapping _mapping;

    internal EntityTypeBuilder(EntityMapping mapping)
    {
        _mapping = mapping;
    }

    /// <summary>
    /// Configures the database table mapped to the entity.
    /// </summary>
    /// <param name="tableName">The database table name.</param>
    /// <returns>The current entity builder.</returns>
    public EntityTypeBuilder<TEntity> ToTable(string tableName)
    {
        _mapping.TableName = tableName;
        return this;
    }

    /// <summary>
    /// Configures the SQL table alias used when generating queries.
    /// </summary>
    /// <param name="tableAlias">The table alias.</param>
    /// <returns>The current entity builder.</returns>
    public EntityTypeBuilder<TEntity> HasAlias(string tableAlias)
    {
        _mapping.TableAlias = tableAlias;
        return this;
    }

    /// <summary>
    /// Configures metadata for the specified entity property.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="propertyExpression">
    /// An expression identifying the property to configure.
    /// </param>
    /// <returns>A builder used to configure the property.</returns>
    public PropertyBuilder Property<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
    {
        var propertyInfo = GetPropertyInfo(propertyExpression);
        var propMapping = _mapping.GetOrAddProperty(propertyInfo);
        return new PropertyBuilder(propMapping);
    }

    /// <summary>
    /// Configures one or more primary key properties for the entity.
    /// </summary>
    /// <param name="keyExpression">
    /// An expression identifying the primary key property or properties.
    /// Composite keys can be specified using an anonymous object.
    /// </param>
    /// <returns>The current entity builder.</returns>
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

    /// <summary>
    /// Configures a one-to-many relationship.
    /// </summary>
    /// <typeparam name="TRelatedEntity">The related entity type.</typeparam>
    /// <param name="navigationExpression">
    /// An expression identifying the collection navigation property.
    /// </param>
    /// <returns>A builder used to configure the relationship.</returns>
    public RelationshipBuilder HasMany<TRelatedEntity>(
        Expression<Func<TEntity, IEnumerable<TRelatedEntity>>> navigationExpression)
        where TRelatedEntity : class
    {
        var propertyInfo = GetPropertyInfo(navigationExpression);
        var relMapping = _mapping.GetOrAddRelationship(
            propertyInfo,
            typeof(TRelatedEntity),
            RelationshipType.OneToMany);

        return new RelationshipBuilder(relMapping);
    }

    /// <summary>
    /// Configures a many-to-one relationship.
    /// </summary>
    /// <typeparam name="TRelatedEntity">The related entity type.</typeparam>
    /// <param name="navigationExpression">
    /// An expression identifying the reference navigation property.
    /// </param>
    /// <returns>A builder used to configure the relationship.</returns>
    public RelationshipBuilder HasOne<TRelatedEntity>(
        Expression<Func<TEntity, TRelatedEntity>> navigationExpression)
        where TRelatedEntity : class?
    {
        var propertyInfo = GetPropertyInfo(navigationExpression);
        var relMapping = _mapping.GetOrAddRelationship(
            propertyInfo,
            typeof(TRelatedEntity),
            RelationshipType.ManyToOne);

        return new RelationshipBuilder(relMapping);
    }

    private static List<PropertyInfo> ExtractProperties(Expression<Func<TEntity, object?>> expression)
    {
        var body = expression.Body;

        if (body is UnaryExpression unary &&
            unary.NodeType == ExpressionType.Convert)
        {
            body = unary.Operand;
        }

        if (body is MemberExpression member &&
            member.Member is PropertyInfo singleProp)
        {
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

        throw new ArgumentException(
            "Expression must be a property access or anonymous type with property initializers.",
            nameof(expression));
    }

    private static PropertyInfo GetPropertyInfo<TProperty>(
        Expression<Func<TEntity, TProperty>> expression)
    {
        if (expression.Body is MemberExpression memberExpression &&
            memberExpression.Member is PropertyInfo propertyInfo)
        {
            return propertyInfo;
        }

        throw new ArgumentException(
            "Expression must be a property access.",
            nameof(expression));
    }
}