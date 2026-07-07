using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Options;
using FlexQuery.NET.Security;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Validation;

/// <summary>
/// Validates the internal consistency of governance configuration (AllowedFields, BlockedFields,
/// operation-specific field lists, and DefaultSortField) at application startup.
/// Throws <see cref="QueryValidationException"/> when configuration errors are detected.
/// </summary>
internal static class GovernanceValidator
{
    /// <summary>
    /// Validates the complete governance configuration, checking for overlapping field lists,
    /// subset violations between operation-specific lists and AllowedFields, and DefaultSortField
    /// compatibility with the governance rules.
    /// </summary>
    /// <param name="execOptions">The execution options containing governance configuration to validate.</param>
    /// <exception cref="QueryValidationException">Thrown when configuration errors are found.</exception>
    public static void ValidateConfiguration(QueryExecutionOptions execOptions)
    {
        if (execOptions.AllowedFields?.Count > 0 && execOptions.BlockedFields?.Count > 0)
        {
            foreach (var blocked in execOptions.BlockedFields)
            {
                if (WildcardMatcher.IsMatch(blocked, execOptions.AllowedFields))
                {
                    throw new QueryValidationException(
                        $"Configuration error: BlockedFields contains '{blocked}' which matches an entry in AllowedFields. " +
                        "These lists must not overlap.");
                }
            }
        }

        if (execOptions.SelectableFields?.Count > 0 && execOptions.AllowedFields?.Count > 0)
        {
            foreach (var selectable in execOptions.SelectableFields)
            {
                if (!WildcardMatcher.IsMatch(selectable, execOptions.AllowedFields))
                {
                    throw new QueryValidationException(
                        $"Configuration error: SelectableFields contains '{selectable}' which is not in AllowedFields. " +
                        "SelectableFields must be a subset of AllowedFields when both are configured.");
                }
            }
        }

        if (execOptions.FilterableFields?.Count > 0 && execOptions.AllowedFields?.Count > 0)
        {
            foreach (var filterable in execOptions.FilterableFields)
            {
                if (!WildcardMatcher.IsMatch(filterable, execOptions.AllowedFields))
                {
                    throw new QueryValidationException(
                        $"Configuration error: FilterableFields contains '{filterable}' which is not in AllowedFields. " +
                        "FilterableFields must be a subset of AllowedFields when both are configured.");
                }
            }
        }

        if (execOptions.SortableFields?.Count > 0 && execOptions.AllowedFields?.Count > 0)
        {
            foreach (var sortable in execOptions.SortableFields)
            {
                if (!WildcardMatcher.IsMatch(sortable, execOptions.AllowedFields))
                {
                    throw new QueryValidationException(
                        $"Configuration error: SortableFields contains '{sortable}' which is not in AllowedFields. " +
                        "SortableFields must be a subset of AllowedFields when both are configured.");
                }
            }
        }

        if (execOptions.GroupableFields?.Count > 0 && execOptions.AllowedFields?.Count > 0)
        {
            foreach (var groupable in execOptions.GroupableFields)
            {
                if (!WildcardMatcher.IsMatch(groupable, execOptions.AllowedFields))
                {
                    throw new QueryValidationException(
                        $"Configuration error: GroupableFields contains '{groupable}' which is not in AllowedFields. " +
                        "GroupableFields must be a subset of AllowedFields when both are configured.");
                }
            }
        }

        if (execOptions.AggregatableFields?.Count > 0 && execOptions.AllowedFields?.Count > 0)
        {
            foreach (var aggregatable in execOptions.AggregatableFields)
            {
                if (!WildcardMatcher.IsMatch(aggregatable, execOptions.AllowedFields))
                {
                    throw new QueryValidationException(
                        $"Configuration error: AggregatableFields contains '{aggregatable}' which is not in AllowedFields. " +
                        "AggregatableFields must be a subset of AllowedFields when both are configured.");
                }
            }
        }

        FieldAccessValidator.ValidateDefaultSortFieldConfiguration(execOptions);
    }
}
