using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Builders;

namespace FlexQuery.NET.Dapper.Materialization;

internal static class SimpleIncludeStreamingMaterializer
{
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

        var executionSw = Stopwatch.StartNew();
        await using var reader = await dbCommand.ExecuteReaderAsync(cancellationToken);
        var executionMs = executionSw.ElapsedMilliseconds;

        var iterationSw = Stopwatch.StartNew();
        var rowOrdinals = BuildOrdinalMap(reader);
        var rootPlan = CreatePlan(rootMapping, rowOrdinals, prefix: string.Empty);
        var childPlan = CreatePlan(command.ChildMapping, rowOrdinals, prefix: command.IncludePath + "_");
        var rootKeyOrdinal = ResolveKeyOrdinal(rootMapping, rowOrdinals, prefix: string.Empty);
        var childKeyOrdinal = ResolveKeyOrdinal(command.ChildMapping, rowOrdinals, command.IncludePath + "_");
        if (rootKeyOrdinal < 0)
            throw new InvalidOperationException($"Simple include materialization requires a mapped key column for '{rootMapping.Type.Name}'.");

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

        var items = roots.Values.ToList();
        return new SimpleIncludeMaterializationResult<T>(
            items,
            rowsRead,
            executionMs,
            iterationSw.ElapsedMilliseconds,
            0,
            0,
            0);
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

internal sealed record SimpleIncludeMaterializationResult<T>(
    IReadOnlyList<T> Items,
    int RowsRead,
    long SqlExecutionMs,
    long ReaderIterationMs,
    long RootMaterializationMs,
    long ChildMaterializationMs,
    long NavigationHydrationMs);
