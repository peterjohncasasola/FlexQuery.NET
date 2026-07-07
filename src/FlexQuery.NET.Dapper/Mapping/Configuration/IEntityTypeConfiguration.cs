using FlexQuery.NET.Dapper.Mapping.Builders;

namespace FlexQuery.NET.Dapper.Mapping.Configuration;

/// <summary>
/// Defines configuration for an entity type using the Fluent API.
/// </summary>
/// <typeparam name="TEntity">
/// The entity type being configured.
/// </typeparam>
/// <remarks>
/// Implement this interface to encapsulate the mapping configuration for an entity,
/// such as table mappings, relationships, and other metadata.
///
/// Configuration classes can be applied individually using
/// <see cref="ModelBuilder.ApplyConfiguration{TEntity}(IEntityTypeConfiguration{TEntity})"/>
/// or discovered automatically using
/// <see cref="ModelBuilder.ApplyConfigurationsFromAssembly(System.Reflection.Assembly)"/>.
/// </remarks>
public interface IEntityTypeConfiguration<TEntity> where TEntity : class
{
    /// <summary>
    /// Configures the specified entity type.
    /// </summary>
    /// <param name="builder">
    /// The builder used to configure the entity's mappings and relationships.
    /// </param>
    void Configure(EntityTypeBuilder<TEntity> builder);
}