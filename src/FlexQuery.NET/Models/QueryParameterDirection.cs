namespace FlexQuery.NET.Models;

/// <summary>Specifies the direction of a query parameter for data access operations.</summary>
public enum QueryParameterDirection
{
    /// <summary>The parameter is an input parameter.</summary>
    Input,
    /// <summary>The parameter is an output parameter.</summary>
    Output,
    /// <summary>The parameter is both an input and output parameter.</summary>
    InputOutput,
    /// <summary>The parameter represents a return value from the query.</summary>
    ReturnValue
}