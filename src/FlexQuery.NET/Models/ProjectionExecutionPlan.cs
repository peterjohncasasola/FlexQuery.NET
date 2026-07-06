using System.Linq.Expressions;

namespace FlexQuery.NET.Models;

/// <summary>
/// Represents metadata and artifacts generated during projection planning.
/// Contains the expression tree, SQL preview, and optimization information.
/// </summary>
public sealed class ProjectionExecutionPlan
{
    /// <summary>The CLR type of the entity being projected.</summary>
    public Type EntityType { get; internal set; } = null!;
    /// <summary>The LINQ expression representing the projection, if built.</summary>
    public Expression? ProjectionExpression { get; internal set; }
    /// <summary>The CLR type of the projected result.</summary>
    public Type? ProjectionResultType { get; internal set; }
    /// <summary>Estimated number of columns included in the projection.</summary>
    public int EstimatedColumnsSelected { get; internal set; }
    /// <summary>A SQL preview string representing the projection, if available.</summary>
    public string? SqlPreview { get; internal set; }
    /// <summary>Maps navigation property aliases to their full property paths.</summary>
    public IReadOnlyDictionary<string, string> NavigationUsage { get; internal set; } = new Dictionary<string, string>();
    /// <summary>The list of fields selected in the projection.</summary>
    public IReadOnlyList<ProjectedField> SelectedFields { get; internal set; } = new List<ProjectedField>();
    /// <summary>Notes produced during projection optimization.</summary>
    public IReadOnlyList<string> OptimizationNotes { get; internal set; } = new List<string>();
    /// <summary>Whether the projection uses flat (SelectMany) mode.</summary>
    public bool IsFlatProjection { get; internal set; }
    /// <summary>Whether the projection includes any collection navigation properties.</summary>
    public bool HasCollectionNavigation { get; internal set; }

    /// <summary>Creates a new <see cref="Builder"/> for constructing a <see cref="ProjectionExecutionPlan"/>.</summary>
    /// <param name="entityType">The entity type being projected.</param>
    /// <returns>A new builder instance.</returns>
    internal static Builder Create(Type entityType) => new(entityType);

    /// <summary>Fluent builder for constructing a <see cref="ProjectionExecutionPlan"/>.</summary>
    internal sealed class Builder
    {
        private readonly ProjectionExecutionPlan _plan = new();
        private readonly List<ProjectedField> _fields = new();
        private readonly List<string> _notes = new();
        private readonly Dictionary<string, string> _navUsage = new();

        internal Builder(Type entityType)
        {
            _plan.EntityType = entityType;
        }

        /// <summary>Sets the projection expression.</summary>
        /// <param name="expr">The LINQ expression representing the projection.</param>
        /// <returns>The builder instance for chaining.</returns>
        public Builder WithProjectionExpression(Expression expr)
        {
            _plan.ProjectionExpression = expr;
            return this;
        }

        /// <summary>Sets the result type of the projection.</summary>
        /// <param name="type">The CLR type of the projected result.</param>
        /// <returns>The builder instance for chaining.</returns>
        public Builder WithResultType(Type type)
        {
            _plan.ProjectionResultType = type;
            return this;
        }

        /// <summary>Sets the estimated column count for the projection.</summary>
        /// <param name="count">The estimated number of columns.</param>
        /// <returns>The builder instance for chaining.</returns>
        public Builder WithEstimatedColumns(int count)
        {
            _plan.EstimatedColumnsSelected = count;
            return this;
        }

        /// <summary>Adds a single projected field to the plan.</summary>
        /// <param name="field">The projected field metadata.</param>
        /// <returns>The builder instance for chaining.</returns>
        public Builder AddField(ProjectedField field)
        {
            _fields.Add(field);
            return this;
        }

        /// <summary>Adds multiple projected fields to the plan.</summary>
        /// <param name="fields">The projected field metadata to add.</param>
        /// <returns>The builder instance for chaining.</returns>
        public Builder AddFields(IEnumerable<ProjectedField> fields)
        {
            _fields.AddRange(fields);
            return this;
        }

        /// <summary>Adds a single optimization note to the plan.</summary>
        /// <param name="note">The optimization note text.</param>
        /// <returns>The builder instance for chaining.</returns>
        public Builder AddOptimizationNote(string note)
        {
            _notes.Add(note);
            return this;
        }

        /// <summary>Records a navigation property usage mapping.</summary>
        /// <param name="alias">The alias used for the navigation.</param>
        /// <param name="path">The full property path of the navigation.</param>
        /// <returns>The builder instance for chaining.</returns>
        public Builder AddNavigationUsage(string alias, string path)
        {
            _navUsage[alias] = path;
            return this;
        }

        /// <summary>Marks the projection as flat (SelectMany-based).</summary>
        /// <param name="isFlat">Whether the projection is flat. Defaults to true.</param>
        /// <returns>The builder instance for chaining.</returns>
        public Builder SetFlatProjection(bool isFlat = true)
        {
            _plan.IsFlatProjection = isFlat;
            return this;
        }

        /// <summary>Marks whether the projection includes collection navigations.</summary>
        /// <param name="hasCollection">Whether collection navigations are present.</param>
        /// <returns>The builder instance for chaining.</returns>
        public Builder SetHasCollectionNavigation(bool hasCollection)
        {
            _plan.HasCollectionNavigation = hasCollection;
            return this;
        }

        /// <summary>Attaches a SQL preview string to the plan.</summary>
        /// <param name="sql">The SQL preview text.</param>
        /// <returns>The builder instance for chaining.</returns>
        public Builder WithSqlPreview(string sql)
        {
            _plan.SqlPreview = sql;
            return this;
        }

        /// <summary>Finalizes and returns the <see cref="ProjectionExecutionPlan"/>.</summary>
        public ProjectionExecutionPlan Build()
        {
            if (_plan.EstimatedColumnsSelected == 0 && _fields.Count > 0)
            {
                _plan.EstimatedColumnsSelected = _fields.Count;
            }

            _plan.SelectedFields = _fields.ToList();
            _plan.OptimizationNotes = _notes.ToList();
            _plan.NavigationUsage = new Dictionary<string, string>(_navUsage);
            return _plan;
        }
    }
}