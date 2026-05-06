namespace FlexQuery.NET.Models;

/// <summary>
/// A dynamic container used for representing the result of a joined query before projection.
/// It holds the Left and Right entities generically.
/// </summary>
public class JoinResult<TOuter, TInner>
{
    public TOuter Left { get; set; } = default!;
    public TInner Right { get; set; } = default!;
}
