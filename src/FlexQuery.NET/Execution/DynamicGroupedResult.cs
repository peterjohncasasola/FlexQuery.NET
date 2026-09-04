using System.Collections;
using System.Runtime.CompilerServices;

namespace FlexQuery.NET.Execution;

/// <summary>
/// Bridges dynamic grouped/aggregate result rows into the strongly typed
/// <see cref="Models.QueryResult{T}"/> contract. Grouped result shapes are runtime
/// projections (group keys + aggregate aliases); when the response DTO does not model
/// that shape, the rows keep their dynamic runtime shape and are exposed through a
/// reference-level view — the serializer and shape converter operate on runtime types
/// via reflection, so typed members are never accessed.
/// </summary>
internal static class DynamicGroupedResult
{
    /// <summary>
    /// Wraps <paramref name="data"/> into an <see cref="IReadOnlyList{T}"/> view without
    /// copying or casting the individual items.
    /// </summary>
    public static IReadOnlyList<TResponse> WrapData<TResponse>(IReadOnlyList<object> data)
        where TResponse : class
        => new ReferenceView<TResponse>(data);

    /// <summary>
    /// A reference-level read-only list view: elements are reinterpreted as
    /// <typeparamref name="T"/> without any runtime type check. Consumers must treat the
    /// elements opaquely (serialization/reflection) — the view exists solely to satisfy
    /// the <see cref="Models.QueryResult{T}.Data"/> contract for dynamic grouped shapes.
    /// </summary>
    private sealed class ReferenceView<T>(IReadOnlyList<object> source) : IReadOnlyList<T>
        where T : class
    {
        public int Count => source.Count;

        public T this[int index]
        {
            get
            {
                var item = source[index];
                return Unsafe.As<object, T>(ref item);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
