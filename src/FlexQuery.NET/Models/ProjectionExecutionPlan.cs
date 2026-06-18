using System.Linq.Expressions;

namespace FlexQuery.NET.Models;

/// <summary>
/// Represents metadata and artifacts generated during projection planning.
/// Contains the expression tree, SQL preview, and optimization information.
/// </summary>
public sealed class ProjectionExecutionPlan
{
    public Type EntityType { get; internal set; } = null!;
    public Expression? ProjectionExpression { get; internal set; }
    public Type? ProjectionResultType { get; internal set; }
    public int EstimatedColumnsSelected { get; internal set; }
    public string? SqlPreview { get; internal set; }
    public IReadOnlyDictionary<string, string> NavigationUsage { get; internal set; } = new Dictionary<string, string>();
    public IReadOnlyList<ProjectedField> SelectedFields { get; internal set; } = new List<ProjectedField>();
    public IReadOnlyList<string> OptimizationNotes { get; internal set; } = new List<string>();
    public bool IsFlatProjection { get; internal set; }
    public bool HasCollectionNavigation { get; internal set; }

    public static Builder Create(Type entityType) => new(entityType);

    public sealed class Builder
    {
        private readonly ProjectionExecutionPlan _plan = new();
        private readonly List<ProjectedField> _fields = new();
        private readonly List<string> _notes = new();
        private readonly Dictionary<string, string> _navUsage = new();

        internal Builder(Type entityType)
        {
            _plan.EntityType = entityType;
        }

        public Builder WithProjectionExpression(Expression expr)
        {
            _plan.ProjectionExpression = expr;
            return this;
        }

        public Builder WithResultType(Type type)
        {
            _plan.ProjectionResultType = type;
            return this;
        }

        public Builder WithEstimatedColumns(int count)
        {
            _plan.EstimatedColumnsSelected = count;
            return this;
        }

        public Builder AddField(ProjectedField field)
        {
            _fields.Add(field);
            return this;
        }

        public Builder AddFields(IEnumerable<ProjectedField> fields)
        {
            _fields.AddRange(fields);
            return this;
        }

        public Builder AddOptimizationNote(string note)
        {
            _notes.Add(note);
            return this;
        }

        public Builder AddNavigationUsage(string alias, string path)
        {
            _navUsage[alias] = path;
            return this;
        }

        public Builder SetFlatProjection(bool isFlat = true)
        {
            _plan.IsFlatProjection = isFlat;
            return this;
        }

        public Builder SetHasCollectionNavigation(bool hasCollection)
        {
            _plan.HasCollectionNavigation = hasCollection;
            return this;
        }

        public Builder WithSqlPreview(string sql)
        {
            _plan.SqlPreview = sql;
            return this;
        }

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