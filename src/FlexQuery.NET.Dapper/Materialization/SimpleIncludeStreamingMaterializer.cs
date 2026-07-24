using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Builders;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Metadata;

namespace FlexQuery.NET.Dapper.Materialization;

internal static class SimpleIncludeStreamingMaterializer
{
    public static async Task<SimpleIncludeMaterializationResult<object>> MaterializeProjectedAsync(
        DbConnection connection,
        SimpleIncludeSqlCommand command,
        IEntityMapping rootMapping,
        int? commandTimeout,
        SelectionNode rootProjection,
        CancellationToken cancellationToken)
    {
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText = command.Sql;
        dbCommand.CommandType = CommandType.Text;
        if (commandTimeout.HasValue)
            dbCommand.CommandTimeout = commandTimeout.Value;

        foreach (var parameter in command.Parameters)
        {
            var dbParameter = dbCommand.CreateParameter();
            dbParameter.ParameterName = parameter.Key.TrimStart('@', ':', '?');
            dbParameter.Value = parameter.Value ?? DBNull.Value;
            dbCommand.Parameters.Add(dbParameter);
        }

        await using var reader = await dbCommand.ExecuteReaderAsync(cancellationToken);

        var rowOrdinals = BuildOrdinalMap(reader);

        var rootProjectedType = BuildProjectedType(rootMapping, rootProjection);
        var rootSetters = new List<(int Ordinal, Action<object, object?> Setter)>();
        if (rootProjection.IncludeAllScalars)
        {
            foreach (var propName in rootMapping.GetProperties())
            {
                var clrProp = rootMapping.Type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (clrProp == null || !clrProp.CanWrite)
                    continue;

                if (!TypeClassification.IsScalarType(clrProp.PropertyType))
                    continue;

                var columnName = rootMapping.GetColumnName(clrProp.Name);
                if (!rowOrdinals.TryGetValue(columnName, out var ordinal))
                    continue;

                var setter = CreateObjectSetter(rootProjectedType, clrProp.Name, clrProp.PropertyType);
                rootSetters.Add((ordinal, setter));
            }
        }
        else
        {
            foreach (var (propName, childNode) in rootProjection.EnumerateChildren())
            {
                if (childNode.HasChildren || childNode.IncludeAllScalars)
                    continue;

                var clrProp = rootMapping.Type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (clrProp == null || !clrProp.CanWrite)
                    continue;

                var columnName = rootMapping.GetColumnName(clrProp.Name);
                if (!rowOrdinals.TryGetValue(columnName, out var ordinal))
                    continue;

                var setter = CreateObjectSetter(rootProjectedType, clrProp.Name, clrProp.PropertyType);
                rootSetters.Add((ordinal, setter));
            }
        }

        var childProjectedType = BuildProjectedType(command.ChildMapping, includeAllScalars: true);
        var childSetters = new List<(int Ordinal, Action<object, object?> Setter)>();
        foreach (var childPropName in command.ChildMapping.GetProperties())
        {
            var clrProp = command.ChildMapping.Type.GetProperty(childPropName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (clrProp == null || !clrProp.CanWrite)
                continue;

            if (!TypeClassification.IsScalarType(clrProp.PropertyType))
                continue;

            var columnName = command.IncludePath + "_" + command.ChildMapping.GetColumnName(childPropName);
            if (!rowOrdinals.TryGetValue(columnName, out var ordinal))
                continue;

            var setter = CreateObjectSetter(childProjectedType, clrProp.Name, clrProp.PropertyType);
            childSetters.Add((ordinal, setter));
        }

        var rootKeyOrdinal = ResolveKeyOrdinal(rootMapping, rowOrdinals, prefix: string.Empty);
        var childKeyOrdinal = ResolveKeyOrdinal(command.ChildMapping, rowOrdinals, command.IncludePath + "_");
        var ordersNavProp = rootProjectedType.GetProperty(command.IncludePath, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        var rootFactory = CreateFactory(rootProjectedType);
        var childFactory = CreateFactory(childProjectedType);

        var roots = new Dictionary<object, object>();
        var rowsRead = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            rowsRead++;
            if (reader.IsDBNull(rootKeyOrdinal))
                continue;

            var rootKey = reader.GetValue(rootKeyOrdinal);

            if (!roots.TryGetValue(rootKey, out var root))
            {
                root = rootFactory();
                roots[rootKey] = root;

                foreach (var (ordinal, setter) in rootSetters)
                {
                    if (!reader.IsDBNull(ordinal))
                        setter(root, reader.GetValue(ordinal));
                }

                if (ordersNavProp != null)
                    ordersNavProp.SetValue(root, new List<object>());
            }

            if (childKeyOrdinal >= 0 && !reader.IsDBNull(childKeyOrdinal))
            {
                var child = childFactory();
                foreach (var (ordinal, setter) in childSetters)
                {
                    if (!reader.IsDBNull(ordinal))
                        setter(child, reader.GetValue(ordinal));
                }

                var list = ordersNavProp?.GetValue(root) as IList<object> ?? new List<object>();
                list.Add(child);
                if (ordersNavProp != null)
                    ordersNavProp.SetValue(root, list);
            }

            rowsRead++;
        }

        var items = roots.Values.ToList();

        return new SimpleIncludeMaterializationResult<object>(Items: items, RowsRead: rowsRead);
    }

    private static Type BuildProjectedType(IEntityMapping mapping, SelectionNode? selection = null, bool includeAllScalars = false)
    {
        var properties = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        if (includeAllScalars || selection?.IncludeAllScalars == true)
        {
            foreach (var prop in mapping.GetProperties())
            {
                var clrProp = mapping.Type.GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (clrProp == null || !clrProp.CanRead)
                    continue;

                if (!TypeClassification.IsScalarType(clrProp.PropertyType))
                    continue;

                properties[clrProp.Name] = clrProp.PropertyType;
            }

            if (selection == null) return DynamicTypeBuilder.GetDynamicType(properties);
            foreach (var (propName, childNode) in selection.EnumerateChildren())
            {
                if (childNode is { HasChildren: false, IncludeAllScalars: false }) continue;
                var clrNavProp = mapping.Type.GetProperty(propName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                properties[clrNavProp != null ? clrNavProp.Name : propName] = typeof(List<object>);
            }
        }
        else
        {
            if (selection == null) return DynamicTypeBuilder.GetDynamicType(properties);
            
            foreach (var (propName, childNode) in selection.EnumerateChildren())
            {
                if (childNode.HasChildren || childNode.IncludeAllScalars)
                {
                    var clrNavProp = mapping.Type.GetProperty(propName,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    properties[clrNavProp != null ? clrNavProp.Name : propName] = typeof(List<object>);
                }
                else
                {
                    var clrProp = mapping.Type.GetProperty(propName,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (clrProp != null && clrProp.CanRead)
                    {
                        properties[clrProp.Name] = clrProp.PropertyType;
                    }
                    else
                    {
                        properties[propName] = typeof(object);
                    }
                }
            }
        }

        return DynamicTypeBuilder.GetDynamicType(properties);
    }

    private static Action<object, object?> CreateObjectSetter(Type objectType, string propertyName, Type propertyType)
    {
        var propertyInfo = objectType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (propertyInfo == null)
            throw new ArgumentException($"Property {propertyName} not found on type {objectType}");

        var target = Expression.Parameter(typeof(object), "target");
        var value = Expression.Parameter(typeof(object), "value");
        var typedTarget = Expression.Convert(target, objectType);

        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        var isNullable = propertyType != underlyingType;
        var changeTypeMethod = typeof(Convert).GetMethod(
            nameof(Convert.ChangeType),
            [typeof(object), typeof(Type)])!;
        var converted = Expression.Call(changeTypeMethod, value, Expression.Constant(underlyingType));
        var typedConverted = Expression.Convert(converted, underlyingType);

        Expression typedValue;
        if (propertyType.IsEnum)
        {
            var toObjectMethod = typeof(Enum).GetMethod(nameof(Enum.ToObject), [typeof(Type), typeof(object)])!;
            typedValue = Expression.Convert(
                Expression.Call(toObjectMethod, Expression.Constant(propertyType), value),
                propertyType);
        }
        else if (isNullable)
        {
            var nullableType = typeof(Nullable<>).MakeGenericType(underlyingType);
            typedValue = Expression.Convert(typedConverted, nullableType);
        }
        else
        {
            typedValue = typedConverted;
        }

        var property = Expression.Property(typedTarget, propertyInfo);
        var assign = Expression.Assign(property, typedValue);

        return Expression.Lambda<Action<object, object?>>(assign, target, value).Compile();
    }

    public static async Task<SimpleIncludeMaterializationResult<T>> MaterializeAsync<T>(
        DbConnection connection,
        SimpleIncludeSqlCommand command,
        IEntityMapping rootMapping,
        int? commandTimeout,
        CancellationToken cancellationToken)
        where T : class
    {
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText = command.Sql;
        dbCommand.CommandType = CommandType.Text;
        if (commandTimeout.HasValue)
            dbCommand.CommandTimeout = commandTimeout.Value;

        foreach (var parameter in command.Parameters)
        {
            var dbParameter = dbCommand.CreateParameter();
            dbParameter.ParameterName = parameter.Key.TrimStart('@', ':', '?');
            dbParameter.Value = parameter.Value ?? DBNull.Value;
            dbCommand.Parameters.Add(dbParameter);
        }

        await using var reader = await dbCommand.ExecuteReaderAsync(cancellationToken);

        var rowOrdinals = BuildOrdinalMap(reader);
        var rootPlan = CreatePlan(rootMapping, rowOrdinals, prefix: string.Empty);
        var childPlan = CreatePlan(command.ChildMapping, rowOrdinals, prefix: command.IncludePath + "_");
        var rootKeyOrdinal = ResolveKeyOrdinal(rootMapping, rowOrdinals, prefix: string.Empty);
        if (rootKeyOrdinal < 0)
            throw new InvalidOperationException($"Simple include materialization requires a mapped key column for '{rootMapping.Type.Name}'.");

        var childKeyOrdinal = ResolveKeyOrdinal(command.ChildMapping, rowOrdinals, prefix: command.IncludePath + "_");
        var navigationProperty = rootMapping.Type.GetProperty(
            command.IncludePath,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        var navigation = navigationProperty is null
            ? null
            : NavigationPlan.Create(navigationProperty, command.ChildMapping.Type);

        var roots = new Dictionary<object, T>();
        var rowsRead = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            rowsRead++;
            if (reader.IsDBNull(rootKeyOrdinal))
                continue;

            var rootKey = reader.GetValue(rootKeyOrdinal);
            if (!roots.TryGetValue(rootKey, out var root))
            {
                root = (T)MapCurrentRow(reader, rootPlan);
                roots[rootKey] = root;
            }

            if (childKeyOrdinal < 0 || reader.IsDBNull(childKeyOrdinal))
                continue;

            var child = MapCurrentRow(reader, childPlan);
            AppendChild(root, navigation, child);
        }

        return new SimpleIncludeMaterializationResult<T>(
            roots.Values.ToList(),
            rowsRead);
    }

    private static Dictionary<string, int> BuildOrdinalMap(DbDataReader reader)
    {
        var ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
            ordinals[reader.GetName(i)] = i;

        return ordinals;
    }

    private static MaterializationPlan CreatePlan(
        IEntityMapping mapping,
        IReadOnlyDictionary<string, int> ordinals,
        string prefix)
    {
        var properties = new List<MappedProperty>();

        foreach (var propName in mapping.GetProperties())
        {
            var columnName = prefix + mapping.GetColumnName(propName);
            if (!ordinals.TryGetValue(columnName, out var ordinal))
                continue;

            var property = mapping.Type.GetProperty(
                propName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property is not { CanWrite: true })
                continue;

            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            properties.Add(new MappedProperty(CreateSetter(property), ordinal, targetType));
        }

        return new MaterializationPlan(CreateFactory(mapping.Type), properties);
    }

    private static int ResolveKeyOrdinal(
        IEntityMapping mapping,
        IReadOnlyDictionary<string, int> ordinals,
        string prefix)
    {
        var keyProperty = mapping.GetKeyProperties().FirstOrDefault()
            ?? mapping.GetProperties().FirstOrDefault(p => p.Equals("Id", StringComparison.OrdinalIgnoreCase))
            ?? mapping.GetProperties().FirstOrDefault();

        if (keyProperty == null)
            return -1;

        var keyColumn = prefix + mapping.GetColumnName(keyProperty);
        return ordinals.GetValueOrDefault(keyColumn, -1);
    }

    private static object MapCurrentRow(
        DbDataReader reader,
        MaterializationPlan plan)
    {
        var entity = plan.Factory();

        foreach (var mapped in plan.Properties)
        {
            if (reader.IsDBNull(mapped.Ordinal))
                continue;

            var value = reader.GetValue(mapped.Ordinal);
            try
            {
                if (mapped.TargetType.IsEnum)
                {
                    mapped.Setter(entity, Enum.ToObject(mapped.TargetType, value));
                }
                else if (mapped.TargetType == value.GetType())
                {
                    mapped.Setter(entity, value);
                }
                else
                {
                    mapped.Setter(entity, Convert.ChangeType(value, mapped.TargetType));
                }
            }
            catch
            {
            }
        }

        return entity;
    }

    private static void AppendChild(
        object parent,
        NavigationPlan? navigation,
        object child)
    {
        if (navigation == null)
            return;

        var value = navigation.Getter(parent);
        if (value == null)
        {
            if (navigation.ListFactory is not null)
            {
                value = navigation.ListFactory();
                navigation.Setter(parent, value);
            }
            else
            {
                navigation.Setter(parent, child);
                return;
            }
        }

        if (value is IList list)
            list.Add(child);
    }

    private static Func<object> CreateFactory(Type entityType)
    {
        var newExpression = Expression.New(entityType);
        var converted = Expression.Convert(newExpression, typeof(object));
        return Expression.Lambda<Func<object>>(converted).Compile();
    }

    private static Action<object, object?> CreateSetter(PropertyInfo property)
    {
        var target = Expression.Parameter(typeof(object), "target");
        var value = Expression.Parameter(typeof(object), "value");
        var typedTarget = Expression.Convert(target, property.DeclaringType!);
        var typedValue = Expression.Convert(value, property.PropertyType);
        var assign = Expression.Assign(Expression.Property(typedTarget, property), typedValue);
        return Expression.Lambda<Action<object, object?>>(assign, target, value).Compile();
    }

    private static Func<object, object?> CreateGetter(PropertyInfo property)
    {
        var target = Expression.Parameter(typeof(object), "target");
        var typedTarget = Expression.Convert(target, property.DeclaringType!);
        var propertyAccess = Expression.Property(typedTarget, property);
        var converted = Expression.Convert(propertyAccess, typeof(object));
        return Expression.Lambda<Func<object, object?>>(converted, target).Compile();
    }

    private sealed record MaterializationPlan(Func<object> Factory, IReadOnlyList<MappedProperty> Properties);

    private sealed record MappedProperty(Action<object, object?> Setter, int Ordinal, Type TargetType);

    private sealed record NavigationPlan(
        Func<object, object?> Getter,
        Action<object, object?> Setter,
        Func<object>? ListFactory)
    {
        public static NavigationPlan Create(PropertyInfo property, Type childType)
        {
            Func<object>? listFactory = null;
            if (property.PropertyType.IsGenericType
                && (property.PropertyType.GetGenericTypeDefinition() == typeof(List<>)
                    || property.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>)
                    || property.PropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                listFactory = CreateFactory(typeof(List<>).MakeGenericType(childType));
            }

            return new NavigationPlan(CreateGetter(property), CreateSetter(property), listFactory);
        }
    }
}

internal sealed record SimpleIncludeMaterializationResult<T>(IReadOnlyList<T> Items, int RowsRead);
