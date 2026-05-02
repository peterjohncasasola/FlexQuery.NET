using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;
using FlexQuery.NET.Models;
using FlexQuery.NET.AspNetCore.Attributes;

namespace FlexQuery.NET.AspNetCore.Filters;

/// <summary>
/// An action filter that automatically applies field-level security settings from 
/// <see cref="FieldAccessAttribute"/> to <see cref="QueryOptions"/> parameters.
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

        // 2. Find QueryOptions in ActionArguments
        var optionsEntry = context.ActionArguments.Values.OfType<QueryOptions>().FirstOrDefault();
        if (optionsEntry == null) return;

        // 3. Apply settings
        ApplyList(attribute.Allowed, ref optionsEntry.AllowedFields);
        ApplyList(attribute.Blocked, ref optionsEntry.BlockedFields);
        ApplyList(attribute.Filterable, ref optionsEntry.FilterableFields);
        ApplyList(attribute.Sortable, ref optionsEntry.SortableFields);
        ApplyList(attribute.Selectable, ref optionsEntry.SelectableFields);

        if (attribute.MaxDepth > 0)
        {
            optionsEntry.MaxFieldDepth = attribute.MaxDepth;
        }
    }

    private void ApplyList(string[]? source, ref HashSet<string>? target)
    {
        if (source == null || source.Length == 0) return;
        
        target ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    /// <inheritdoc />
    public void OnActionExecuted(ActionExecutedContext context) { }
}
