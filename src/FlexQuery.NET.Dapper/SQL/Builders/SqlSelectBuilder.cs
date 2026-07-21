using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Mapping.Metadata;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Metadata;
using System.Reflection;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Dapper.Sql.Builders;

/// <summary>
/// Builds the SELECT clause for all three projection shapes: the default tree-shaped
/// projection (<see cref="BuildSelectClause"/>, walking a <see cref="SelectionNode"/>),
/// the flat/flat-mixed projection (<see cref="BuildFlatSelectClause"/>, which also emits
/// its own JOINs since flat mode needs a specific linear join order), and aggregate-only
/// projections (<see cref="BuildAggregateSelectParts"/>, shared with GROUP BY queries and
/// the dedicated aggregates endpoint). Has no dependency on filtering — selection shape and
/// filtering are independent concerns, which is why this builder has no need of
/// <c>SqlWhereBuilder</c> or <c>SqlJoinBuilder</c>.
/// </summary>
internal sealed class SqlSelectBuilder(IMappingRegistry mappingRegistry, ISqlDialect dialect)
{
    /// <summary>
    /// Renders the SELECT parts for an aggregates list, special-casing COUNT(*)/COUNT(field-less)
    /// as COUNT(1) and otherwise emitting FUNC(column) AS alias.
    /// </summary>
    public List<string> BuildAggregateSelectParts(QueryOptions options, IEntityMapping mapping)
    {
        var selectParts = new List<string>();
        foreach (var agg in options.Aggregates)
        {
            var column = mapping.GetColumnName(agg.Field ?? "*");
            var quoted = SqlSyntaxBuilder.QuoteColumn(dialect, column, mapping);

            if (agg.Function == AggregateFunction.Count && (string.IsNullOrEmpty(agg.Field) || agg.Field == "*"))
            {
                selectParts.Add($"COUNT(1) AS {dialect.QuoteIdentifier(agg.Alias)}");
            }
            else
            {
                selectParts.Add($"{agg.Function.ToKeyword().ToUpperInvariant()}({quoted}) AS {dialect.QuoteIdentifier(agg.Alias)}");
            }
        }
        return selectParts;
    }

    /// <summary>
    /// Builds the SELECT clause by recursively walking the SelectionNode AST.
    /// When the AST is empty (no explicit <c>Select</c> or <c>Includes</c> specified),
    /// falls back to all columns from the entity mapping — which can produce a large payload
    /// for tables with many columns. Specify explicit <c>Select</c> fields to reduce the
    /// result set to only the columns actually needed.
    /// </summary>
    public string BuildSelectClause(QueryOptions options, IEntityMapping mapping, string distinctClause, SelectionNode selectTree)
    {
        var distinctPrefix = !string.IsNullOrEmpty(distinctClause) ? $"{distinctClause} " : string.Empty;
        var selectParts = new List<string>();

        if (options.Aggregates.Count > 0 && options.GroupBy?.Count > 0)
        {
            foreach (var g in options.GroupBy)
            {
                selectParts.Add(SqlSyntaxBuilder.QuoteColumn(dialect, mapping.GetColumnName(g), mapping));
            }

            selectParts.AddRange(BuildAggregateSelectParts(options, mapping));
            return $"SELECT {distinctPrefix}{string.Join(", ", selectParts)}";
        }

        if (options.GroupBy?.Count > 0)
        {
            foreach (var g in options.GroupBy)
            {
                selectParts.Add(SqlSyntaxBuilder.QuoteColumn(dialect, mapping.GetColumnName(g), mapping));
            }
            return $"SELECT {distinctPrefix}{string.Join(", ", selectParts)}";
        }

        TraverseSelectTree(selectTree, mapping, mapping.TableAlias, string.Empty, selectParts, options.Select);

        if (selectParts.Count == 0)
        {
            // Fallback when no explicit Select or Includes specified:
            // returns ALL columns from the entity. Specifying explicit Select
            // fields reduces payload and improves performance.
            var governedProps = GetGovernedProperties(mapping, options.Select);
            foreach (var p in governedProps)
            {
                selectParts.Add(SqlSyntaxBuilder.QuoteColumn(dialect, mapping.GetColumnName(p), mapping));
            }
        }

        return $"SELECT {distinctPrefix}{string.Join(", ", selectParts)}";
    }

    private void TraverseSelectTree(SelectionNode node, IEntityMapping currentMapping, string? currentAlias, string prefix, List<string> selectParts, IReadOnlyList<SelectNode>? governedSelectFields = null)
    {
        bool hasSpecificFields = false;

        foreach (var child in node.EnumerateChildren())
        {
            var rel = currentMapping.GetRelationship(child.Key);
            if (rel != null)
            {
                var targetMapping = mappingRegistry.GetMapping(rel.TargetType);
                var nextPrefix = prefix + rel.NavigationPropertyName + "_";
                var nextAlias = rel.NavigationPropertyName;

                TraverseSelectTree(child.Value, targetMapping, nextAlias, nextPrefix, selectParts, governedSelectFields);
            }
            else
            {
                hasSpecificFields = true;
                var col = currentMapping.GetColumnName(child.Key);
                var outputName = child.Value.Alias ?? (prefix + col);

                var quotedAlias = string.IsNullOrEmpty(currentAlias) ? "" : dialect.QuoteIdentifier(currentAlias) + ".";
                var quotedCol = dialect.QuoteIdentifier(col);
                var quotedOutput = dialect.QuoteIdentifier(outputName);

                selectParts.Add($"{quotedAlias}{quotedCol} AS {quotedOutput}");
            }
        }

        if (node.IncludeAllScalars || (!hasSpecificFields && !node.HasChildren))
        {
            var props = GetGovernedProperties(currentMapping, governedSelectFields);
            foreach (var prop in props)
            {
                var col = currentMapping.GetColumnName(prop);
                var outputName = prefix + col;

                var quotedAlias = string.IsNullOrEmpty(currentAlias) ? "" : dialect.QuoteIdentifier(currentAlias) + ".";
                var quotedCol = dialect.QuoteIdentifier(col);
                var quotedOutput = dialect.QuoteIdentifier(outputName);

                selectParts.Add($"{quotedAlias}{quotedCol} AS {quotedOutput}");
            }
        }
    }

    private static IEnumerable<string> GetGovernedProperties(IEntityMapping mapping, IReadOnlyList<SelectNode>? governedSelectFields)
    {
        var rootFields = GetRootGovernedPropertyNames(governedSelectFields);
        if (rootFields != null)
            return mapping.GetProperties().Where(rootFields.Contains);

        return mapping.GetProperties();
    }

    /// <summary>
    /// Builds the SELECT clause and accompanying JOINs for flat/flat-mixed projection mode.
    /// Unlike the tree-shaped SELECT, flat mode requires a single linear navigation path and
    /// emits its own joins in that path's order, so join-building stays alongside selection here
    /// rather than being shared with <c>SqlJoinBuilder</c> (which handles tree-shaped, possibly
    /// branching navigation instead).
    /// </summary>
    public (string selectClause, string joinClause, List<string> flatJoins) BuildFlatSelectClause(
        QueryOptions options, IEntityMapping mapping, string distinctClause, SelectionNode selectTree)
    {
        var allowRootScalars = options.ProjectionMode == ProjectionMode.FlatMixed;
        var (navPath, fields) = DecomposeFlatSelection(selectTree, mapping, allowRootScalars, 0, options.Select);

        var distinctPrefix = !string.IsNullOrEmpty(distinctClause) ? $"{distinctClause} " : string.Empty;
        var selectParts = new List<string>();
        var flatJoins = new List<string>();
        var joins = new List<string>();

        if (navPath.Count == 0)
        {
            var dialectTable = SqlSyntaxBuilder.QuoteTable(dialect, mapping);

            foreach (var f in fields)
            {
                var col = f.Mapping.GetColumnName(f.PropName);
                selectParts.Add($"{dialectTable}.{dialect.QuoteIdentifier(col)} AS {dialect.QuoteIdentifier(f.OutputName)}");
            }

            if (selectParts.Count == 0)
            {
                var governedProps = GetGovernedProperties(mapping, options.Select);
                foreach (var propName in governedProps)
                {
                    var col = mapping.GetColumnName(propName);
                    selectParts.Add($"{dialectTable}.{dialect.QuoteIdentifier(col)}");
                }
            }

            return ($"SELECT {distinctPrefix}{string.Join(", ", selectParts)}", string.Empty, flatJoins);
        }

        var currentAlias = mapping.TableAlias ?? mapping.TableName;
        var currentMapping = mapping;
        var rootTable = SqlSyntaxBuilder.QuoteTable(dialect, mapping);

        // Project root scalars (level -1) for FlatMixed mode
        if (allowRootScalars)
        {
            foreach (var f in fields.Where(f => f.Level == -1))
            {
                var col = f.Mapping.GetColumnName(f.PropName);
                selectParts.Add($"{rootTable}.{dialect.QuoteIdentifier(col)} AS {dialect.QuoteIdentifier(f.OutputName)}");
            }
        }

        for (var i = 0; i < navPath.Count; i++)
        {
            var navName = navPath[i];
            var rel = currentMapping.GetRelationship(navName);
            if (rel == null) continue;

            var targetMapping = mappingRegistry.GetMapping(rel.TargetType);
            var navAlias = rel.NavigationPropertyName;

            var joinCondition = SqlSyntaxBuilder.BuildJoinCondition(dialect, rel, currentMapping, currentAlias, targetMapping, navAlias);

            joins.Add($"LEFT JOIN {SqlSyntaxBuilder.QuoteTable(dialect, targetMapping)} AS {dialect.QuoteIdentifier(navAlias)} ON {joinCondition}");
            flatJoins.Add(navAlias);

            if (allowRootScalars)
            {
                foreach (var f in fields.Where(f => f.Level == i))
                {
                    var col = f.Mapping.GetColumnName(f.PropName);
                    selectParts.Add($"{dialect.QuoteIdentifier(navAlias)}.{dialect.QuoteIdentifier(col)} AS {dialect.QuoteIdentifier(f.OutputName)}");
                }
            }

            currentMapping = targetMapping;
            currentAlias = navAlias;
        }

        foreach (var f in fields.Where(f => f.Level == navPath.Count))
        {
            var col = f.Mapping.GetColumnName(f.PropName);
            selectParts.Add($"{dialect.QuoteIdentifier(currentAlias)}.{dialect.QuoteIdentifier(col)} AS {dialect.QuoteIdentifier(f.OutputName)}");
        }

        if (selectParts.Count == 0)
        {
            var governedProps = GetGovernedProperties(currentMapping, options.Select);
            foreach (var propName in governedProps)
            {
                var col = currentMapping.GetColumnName(propName);
                selectParts.Add($"{dialect.QuoteIdentifier(currentAlias)}.{dialect.QuoteIdentifier(col)}");
            }
        }

        var joinClause = string.Join(" ", joins);
        return ($"SELECT {distinctPrefix}{string.Join(", ", selectParts)}", joinClause, flatJoins);
    }

    private record FlatField(int Level, string OutputName, string PropName, IEntityMapping Mapping);

    private (List<string> navPath, List<FlatField> fields) DecomposeFlatSelection(
        SelectionNode node, IEntityMapping mapping, bool allowRootScalars, int level = 0,
        IReadOnlyList<SelectNode>? governedSelectFields = null)
    {
        var navPath = new List<string>();
        var fields = new List<FlatField>();

        var navChildren = new List<(string name, SelectionNode child, RelationshipMapping rel)>();
        var scalarChildren = new List<(string name, SelectionNode child)>();

        foreach (var child in node.EnumerateChildren())
        {
            var rel = mapping.GetRelationship(child.Key);
            if (rel != null)
            {
                navChildren.Add((child.Key, child.Value, rel));
            }
            else
            {
                scalarChildren.Add((child.Key, child.Value));
            }
        }

        if (!allowRootScalars && navChildren.Count > 1)
            throw new InvalidOperationException(
                "Flat mode does not support branching multiple navigation paths. Select a single linear path.");

        if (navChildren.Count == 0)
        {
            foreach (var (propName, childNode) in scalarChildren)
            {
                var outputName = childNode.Alias ?? propName;
                fields.Add(new FlatField(level, outputName, propName, mapping));
            }

            if (node.IncludeAllScalars || node.HasChildren == false)
            {
                var governedPropNames = GetRootGovernedPropertyNames(governedSelectFields);
                foreach (var propName in mapping.GetProperties())
                {
                    if (governedPropNames != null && !governedPropNames.Contains(propName))
                        continue;

                    var prop = mapping.Type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop != null && TypeClassification.IsScalarType(prop.PropertyType))
                    {
                        var outputName = propName;
                        fields.Add(new FlatField(level, outputName, propName, mapping));
                    }
                }
            }
        }

        foreach (var (_, navNode, rel) in navChildren)
        {
            var targetMapping = mappingRegistry.GetMapping(rel.TargetType);
            navPath.Add(rel.NavigationPropertyName);

            var (subPath, subFields) = DecomposeFlatSelection(navNode, targetMapping, allowRootScalars, level + 1, governedSelectFields);
            navPath.AddRange(subPath);
            fields.AddRange(subFields);

            if (allowRootScalars)
            {
                foreach (var (propName, childNode) in scalarChildren)
                {
                    var outputName = childNode.Alias ?? propName;
                    fields.Add(new FlatField(-1, outputName, propName, mapping));
                }
            }
        }

        return (navPath, fields);
    }

    private static HashSet<string>? GetRootGovernedPropertyNames(IReadOnlyList<SelectNode>? governedSelectFields)
    {
        if (governedSelectFields is not { Count: > 0 })
            return null;

        var rootFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in governedSelectFields)
        {
            var root = field.Field.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
            rootFields.Add(root);
        }
        return rootFields;
    }
}