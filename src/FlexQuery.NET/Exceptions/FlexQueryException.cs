namespace FlexQuery.NET.Exceptions;

/// <summary>
/// Abstract base class for all FlexQuery.NET exceptions.
/// </summary>
public abstract class FlexQueryException(string message, Exception? innerException = null)
    : Exception(message, innerException);
