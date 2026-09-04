using System.Reflection;
using FlexQuery.NET.Metadata;

namespace FlexQuery.NET.QuerySurface;

/// <summary>
/// Immutable implementation of <see cref="IQuerySurface"/>.
/// </summary>
public sealed class QuerySurface : IQuerySurface
{
    /// <inheritdoc />
    public Type EntityType { get; }

    /// <inheritdoc />
    public Type? ResponseType { get; }

    /// <inheritdoc />
    public StringComparer FieldNameComparer { get; }

    private readonly Dictionary<string, ResolvedField> _bySurfaceName;
    private readonly Dictionary<string, ResolvedField> _byDtoName;
    private readonly Dictionary<string, ResolvedField> _byEntityName;

    public QuerySurface(
        Type entityType,
        Type? responseType,
        StringComparer fieldNameComparer,
        IReadOnlyList<ResolvedField> resolvableFields,
        IReadOnlyList<PropertyInfo> responseScalarProperties,
        IReadOnlyList<PropertyInfo> entityScalarProperties)
    {
        EntityType = entityType;
        ResponseType = responseType;
        FieldNameComparer = fieldNameComparer;

        _bySurfaceName = new Dictionary<string, ResolvedField>(fieldNameComparer);
        _byDtoName = new Dictionary<string, ResolvedField>(fieldNameComparer);
        _byEntityName = new Dictionary<string, ResolvedField>(fieldNameComparer);

        foreach (var field in resolvableFields)
        {
            _bySurfaceName[field.SurfaceName] = field;
            if (field.ResponseProperty != null)
                _byDtoName[field.ResponseProperty.Name] = field;

            // Computed scalar mappings carry a synthetic EntityProperty placeholder and
            // no real entity backing — exclude them from entity-name resolution.
            if (field.ResponseProperty != null || field.MappingKind != FieldMappingKind.Explicit)
                _byEntityName[field.EntityProperty.Name] = field;
        }

        ResponseScalarProperties = responseScalarProperties;
        EntityScalarProperties = entityScalarProperties;
    }

    private IReadOnlyList<PropertyInfo> ResponseScalarProperties { get; }
    private IReadOnlyList<PropertyInfo> EntityScalarProperties { get; }

    public bool TryResolve(string? fieldName, out ResolvedField field)
    {
        if (fieldName == null)
        {
            field = default;
            return false;
        }

        return _bySurfaceName.TryGetValue(fieldName, out field);
    }

    /// <inheritdoc />
    public bool TryResolveByEntityName(string? entityPropertyName, out ResolvedField field)
    {
        if (entityPropertyName == null)
        {
            field = default;
            return false;
        }

        return _byEntityName.TryGetValue(entityPropertyName, out field);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetDefaultSelectFields()
    {
        if (ResponseType != null)
        {
            return _bySurfaceName.Values
                .Where(f => f.ResponseProperty != null
                            && !f.IsNavigation
                            && TypeClassification.IsScalarType(f.ResponseProperty.PropertyType))
                .Select(f => f.SurfaceName)
                .ToList();
        }

        return EntityScalarProperties
            .Where(p => TypeClassification.IsScalarType(p.PropertyType))
            .Select(p => p.Name)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<ResolvedField> GetResolvableFields()
        => _bySurfaceName.Values.ToList();

    /// <inheritdoc />
    public IReadOnlyList<PropertyInfo> GetResponseScalarProperties()
        => ResponseScalarProperties;

    /// <inheritdoc />
    public IReadOnlyList<PropertyInfo> GetEntityScalarProperties()
        => EntityScalarProperties;

    /// <summary>
    /// Returns true if the given field name exists on the DTO/public surface,
    /// regardless of whether it has an entity mapping.
    /// </summary>
    public bool IsPublicSurfaceField(string? fieldName)
    {
        if (fieldName == null) return false;
        return ResponseType != null ? _byDtoName.ContainsKey(fieldName) : _bySurfaceName.ContainsKey(fieldName);
    }
}
