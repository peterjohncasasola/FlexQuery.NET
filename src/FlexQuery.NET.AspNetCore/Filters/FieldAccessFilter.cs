using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;
using FlexQuery.NET.AspNetCore.Attributes;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.AspNetCore.Filters;

/// <summary>
/// An action filter that automatically applies field-level security settings from 
/// <see cref="FieldAccessAttribute"/> to <see cref="BaseQueryOptions"/> and stores it in HttpContext.Items.
/// </summary>
public sealed class FieldAccessFilter : IActionFilter
{
    private const string LegacyExecutionOptionsKey = "FlexQueryExecutionOptions";

    /// <inheritdoc />
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
        if (descriptor == null) return;

        // 1. Find the Attribute (Action takes priority over Controller)
        var attribute = descriptor.MethodInfo.GetCustomAttributes(typeof(FieldAccessAttribute), true).FirstOrDefault() as FieldAccessAttribute
                     ?? descriptor.ControllerTypeInfo.GetCustomAttributes(typeof(FieldAccessAttribute), true).FirstOrDefault() as FieldAccessAttribute;

        if (attribute == null) return;

        // 2. Retrieve or create QueryExecutionOptions
        var execOptions = context.HttpContext.Items[ContextKeys.ExecutionOptions] as QueryExecutionOptions
                          ?? context.HttpContext.Items[LegacyExecutionOptionsKey] as QueryExecutionOptions
                          ?? new QueryExecutionOptions();

        // 3. Apply settings
        execOptions.AllowedFields = Merge(attribute.Allowed, execOptions.AllowedFields);
        execOptions.BlockedFields = Merge(attribute.Blocked, execOptions.BlockedFields);
        execOptions.FilterableFields = Merge(attribute.Filterable, execOptions.FilterableFields);
        execOptions.SortableFields = Merge(attribute.Sortable, execOptions.SortableFields);
        execOptions.SelectableFields = Merge(attribute.Selectable, execOptions.SelectableFields);
        execOptions.GroupableFields = Merge(attribute.Groupable, execOptions.GroupableFields);
        execOptions.AggregatableFields = Merge(attribute.Aggregatable, execOptions.AggregatableFields);
        execOptions.AllowedIncludes = Merge(attribute.AllowedIncludes, execOptions.AllowedIncludes);

        if (attribute.DefaultSortField != null)
        {
            execOptions.DefaultSortField = attribute.DefaultSortField;
        }

        if (!string.IsNullOrWhiteSpace(attribute.DefaultSortDirection))
        {
            execOptions.DefaultSortDescending = attribute.DefaultSortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase)
                || attribute.DefaultSortDirection.Equals("descending", StringComparison.OrdinalIgnoreCase);
        }

        if (attribute.MaxDepth > 0)
        {
            execOptions.MaxFieldDepth = attribute.MaxDepth;
        }

        // 4. Store in HttpContext
        context.HttpContext.Items[ContextKeys.ExecutionOptions] = execOptions;
    }

    private HashSet<string>? Merge(string[]? source, HashSet<string>? target)
    {
        if (source == null || source.Length == 0) return target;
        
        var result = target ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in source)
        {
            result.Add(item);
        }
        return result;
    }

    /// <inheritdoc />
    public void OnActionExecuted(ActionExecutedContext context) { }
}
