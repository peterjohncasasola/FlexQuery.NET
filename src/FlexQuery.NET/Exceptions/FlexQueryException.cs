namespace FlexQuery.NET.Exceptions;

/// <summary>
/// Base class for all FlexQuery.NET exceptions.
/// </summary>
public class FlexQueryException(string message, Exception? innerException = null)
    : Exception(message, innerException);
