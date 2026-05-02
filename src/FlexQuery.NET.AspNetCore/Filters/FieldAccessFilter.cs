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
        if (attribute.Allowed != null)
        {
            optionsEntry.AllowedFields ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in attribute.Allowed) optionsEntry.AllowedFields.Add(f);
        }

        if (attribute.Blocked != null)
        {
            optionsEntry.BlockedFields ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in attribute.Blocked) optionsEntry.BlockedFields.Add(f);
        }

        if (attribute.MaxDepth > 0)
        {
            // Attribute wins if it's set to a positive value
            optionsEntry.MaxFieldDepth = attribute.MaxDepth;
        }
    }

    /// <inheritdoc />
    public void OnActionExecuted(ActionExecutedContext context) { }
}
