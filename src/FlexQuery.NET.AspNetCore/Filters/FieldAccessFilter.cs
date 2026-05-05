using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;
using FlexQuery.NET.Models;
using FlexQuery.NET.AspNetCore.Attributes;

namespace FlexQuery.NET.AspNetCore.Filters;

/// <summary>
/// An action filter that automatically applies field-level security settings from 
/// <see cref="FieldAccessAttribute"/> to <see cref="QueryExecutionOptions"/> parameters.
/// </summary>
public class FieldAccessFilter : IActionFilter
{
    /// <inheritdoc />
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
        if (descriptor == null) return;

        // 1. Find the Attribute (Action takes priority over Controller)
        var attribute = descriptor.MethodInfo.GetCustomAttributes(typeof(FieldAccessAttribute), true).FirstOrDefault() as FieldAccessAttribute
                     ?? descriptor.ControllerTypeInfo.GetCustomAttributes(typeof(FieldAccessAttribute), true).FirstOrDefault() as FieldAccessAttribute;

        if (attribute == null) return;

        // 2. Find QueryExecutionOptions in ActionArguments
        var execOptions = context.ActionArguments.Values.OfType<QueryExecutionOptions>().FirstOrDefault();
        if (execOptions == null) return;

        // 3. Apply settings
        execOptions.AllowedFields = Merge(attribute.Allowed, execOptions.AllowedFields);
        execOptions.BlockedFields = Merge(attribute.Blocked, execOptions.BlockedFields);
        execOptions.FilterableFields = Merge(attribute.Filterable, execOptions.FilterableFields);
        execOptions.SortableFields = Merge(attribute.Sortable, execOptions.SortableFields);
        execOptions.SelectableFields = Merge(attribute.Selectable, execOptions.SelectableFields);

        if (attribute.MaxDepth > 0)
        {
            execOptions.MaxFieldDepth = attribute.MaxDepth;
        }
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
