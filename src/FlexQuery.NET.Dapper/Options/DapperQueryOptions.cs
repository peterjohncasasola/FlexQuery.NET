using FlexQuery.NET.Dapper.Metadata;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.Dapper.Options;

/// <summary>
/// Represents Dapper-specific execution options for a FlexQuery request.
/// The SQL dialect is auto-detected from the supplied <see cref="System.Data.Common.DbConnection"/>
/// at runtime — no manual dialect configuration is required.
/// </summary>
/// <inheritdoc/>
public sealed class DapperQueryOptions : BaseQueryOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DapperQueryOptions"/> class.
    /// </summary>
    /// <inheritdoc/>
    public DapperQueryOptions()
    {
        IncludeTotalCount = true;
    }

    /// <summary>
    /// Gets the entity metadata model used to translate FlexQuery requests into SQL.
    /// </summary>
    internal FlexQueryModel? Model { get; private set; }

    /// <summary>
    /// Sets the metadata model used during SQL translation, overriding the
    /// global model configured via <c>FlexQueryDapper.Configure</c>.
    /// </summary>
    /// <param name="model">The metadata model.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="model"/> is <see langword="null"/>.
    /// </exception>
    internal void UseModel(FlexQueryModel model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
    }

    /// <summary>
    /// Gets or sets the database command timeout, in seconds.
    /// </summary>
    /// <value>
    /// The command timeout in seconds. The default value is <c>30</c>.
    /// </value>
    public int CommandTimeout { get; set; } = 30;
}
