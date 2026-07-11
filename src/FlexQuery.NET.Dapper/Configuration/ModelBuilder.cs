using System.Reflection;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Mapping.Builders;
using FlexQuery.NET.Dapper.Metadata;

namespace FlexQuery.NET.Dapper.Configuration;

/// <summary>
/// Provides a fluent API for configuring entity mappings used by FlexQuery.NET Dapper.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ModelBuilder"/> is responsible for building the application's mapping model,
/// including table mappings, relationships, and other entity metadata.
/// </para>
/// <para>
/// After all mappings have been configured, call <see cref="Build"/> to produce
/// an immutable <see cref="FlexQueryModel"/> for runtime query execution.
/// </para>
/// </remarks>
public sealed class ModelBuilder
{
    private MappingRegistry Registry { get; } = new();

    /// <summary>
    /// Begins configuring the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to configure.</typeparam>
    /// <returns>
    /// An <see cref="EntityTypeBuilder{TEntity}"/> used to configure the entity's
    /// table mapping, relationships, and other metadata.
    /// </returns>
    public EntityTypeBuilder<TEntity> Entity<TEntity>() where TEntity : class
        => Registry.Entity<TEntity>();

    /// <summary>
    /// Applies the specified entity configuration class.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being configured.</typeparam>
    /// <param name="config">
    /// The configuration instance implementing
    /// <see cref="IEntityTypeConfiguration{TEntity}"/>.
    /// </param>
    /// <returns>The current <see cref="ModelBuilder"/> instance.</returns>
    public ModelBuilder ApplyConfiguration<TEntity>(IEntityTypeConfiguration<TEntity> config)
        where TEntity : class
    {
        var builder = Entity<TEntity>();
        config.Configure(builder);
        return this;
    }

    /// <summary>
    /// Discovers and applies all entity configuration classes in the specified assembly.
    /// </summary>
    /// <param name="assembly">
    /// The assembly to scan for implementations of
    /// <see cref="IEntityTypeConfiguration{TEntity}"/>.
    /// </param>
    /// <returns>The current <see cref="ModelBuilder"/> instance.</returns>
    /// <remarks>
    /// All non-abstract, public types implementing
    /// <see cref="IEntityTypeConfiguration{TEntity}"/> are instantiated using their
    /// parameterless constructor and applied automatically.
    /// </remarks>
    public ModelBuilder ApplyConfigurationsFromAssembly(Assembly assembly)
    {
        var configInterfaceType = typeof(IEntityTypeConfiguration<>);

        var configTypes = assembly.GetExportedTypes()
            .Where(t => t is { IsAbstract: false, IsGenericTypeDefinition: false })
            .Select(t => new
            {
                Type = t,
                ConfigInterface = t.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == configInterfaceType)
            })
            .Where(x => x.ConfigInterface != null);

        foreach (var configInfo in configTypes)
        {
            var entityType = configInfo.ConfigInterface!.GetGenericArguments()[0];
            var instance = Activator.CreateInstance(configInfo.Type)!;

            var applyMethod = typeof(ModelBuilder)
                .GetMethod(nameof(ApplyConfiguration), BindingFlags.Public | BindingFlags.Instance)!
                .MakeGenericMethod(entityType);

            applyMethod.Invoke(this, [instance]);
        }

        return this;
    }

    /// <summary>
    /// Discovers and applies all entity configuration classes in the assembly
    /// containing the specified marker type.
    /// </summary>
    /// <typeparam name="TMarker">
    /// A type located in the assembly to scan.
    /// </typeparam>
    /// <returns>The current <see cref="ModelBuilder"/> instance.</returns>
    public ModelBuilder ApplyConfigurationsFromAssembly<TMarker>()
        => ApplyConfigurationsFromAssembly(typeof(TMarker).Assembly);

    /// <summary>
    /// Finalizes the configured mappings and creates a runtime model.
    /// </summary>
    /// <returns>
    /// An immutable <see cref="FlexQueryModel"/> containing all configured
    /// entity mapping metadata.
    /// </returns>
    /// <remarks>
    /// This method represents the transition from the mutable configuration phase
    /// to the immutable runtime model used during query execution. It is invoked
    /// internally by <c>FlexQueryDapper.Configure</c>; callers should configure
    /// mappings through that method rather than building the model directly.
    /// Future versions may perform validation and metadata optimization during this step.
    /// </remarks>
    internal FlexQueryModel Build()
    {
        return new FlexQueryModel(Registry);
    }
}