using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Filters;

internal static class FilterComposer
{
    public static FilterGroup? MergeFilters(FilterGroup? left, FilterGroup? right)
    {
        if (left is null) return right;
        if (right is null) return left;

        return new FilterGroup
        {
            Logic = LogicOperator.And,
            Groups = [left, right]
        };
    }
    
}

