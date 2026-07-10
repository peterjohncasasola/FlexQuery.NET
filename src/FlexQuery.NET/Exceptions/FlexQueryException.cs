namespace FlexQuery.NET.Exceptions;

/// <summary>
/// Abstract base class for all FlexQuery.NET exceptions.
/// </summary>
public abstract class FlexQueryException : Exception
{
    protected FlexQueryException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
