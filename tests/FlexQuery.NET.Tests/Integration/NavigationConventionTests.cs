using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Models;
using FlexQuery.NET.Options;
using FlexQuery.NET.QuerySurface;

namespace FlexQuery.NET.Tests.Integration;

/// <summary>
/// Focused tests for the v4 navigation-property convention rule:
///   same-name + same exact CLR type (reference or collection) navigation
///     -> convention-resolvable, but excluded from default projection;
///   different DTO navigation type -> not automatically mapped.
/// </summary>
public class NavigationConventionTests : IDisposable
{
    private readonly TestDbContext _db = TestDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    // ── DTOs ───────────────────────────────────────────────────────────────

    public class CustomerWithProfileDto
    {
        public int Id { get; set; }
        public Profile Profile { get; set; } = null!;
    }

    public class CustomerWithProfileDtoMismatch
    {
        public int Id { get; set; }
        public ProfileDto Profile { get; set; } = null!;
    }

    public class CustomerWithOrdersDto
    {
        public int Id { get; set; }
        public List<Order> Orders { get; set; } = [];
    }

    public class CustomerWithOrdersDtoMismatch
    {
        public int Id { get; set; }
        public ICollection<Order> Orders { get; set; } = [];
    }

    public class ProfileDto
    {
        public int Id { get; set; }
        public string? Bio { get; set; }
    }

    private static IQuerySurface BuildSurface<TEntity, TResponse>()
        where TEntity : class
        where TResponse : class
    {
        var options = new QueryExecutionOptions();
        return QuerySurfaceBuilder.Build(typeof(TEntity), typeof(TResponse), options);
    }

    // ── 1. Same-name + same-type reference navigation is resolvable ─────────

    [Fact]
    public void SameName_SameType_ReferenceNavigation_IsResolvable()
    {
        var surface = BuildSurface<Customer, CustomerWithProfileDto>();

        surface.TryResolve("Profile", out var field).Should().BeTrue();
        field.IsNavigation.Should().BeTrue();
        field.MappingKind.Should().Be(FieldMappingKind.Convention);

        // scalar is still resolvable and not a navigation

        surface.TryResolve("Id", out var idField).Should().BeTrue();
        idField.IsNavigation.Should().BeFalse();
    }

    // ── 2. Same-name + different DTO navigation type is not auto-resolvable ──

    [Fact]
    public void SameName_DifferentDtoNavigationType_IsNotResolvable()
    {
        var surface = BuildSurface<Customer, CustomerWithProfileDtoMismatch>();

        surface.TryResolve("Profile", out _).Should().BeFalse();
        // The scalar property is still resolved by convention.
        surface.TryResolve("Id", out _).Should().BeTrue();
    }

    // ── 3. Same-name + same-type collection navigation is resolvable ───────

    [Fact]
    public void SameName_SameType_CollectionNavigation_IsResolvable()
    {
        var surface = BuildSurface<Customer, CustomerWithOrdersDto>();

        surface.TryResolve("Orders", out var field).Should().BeTrue();
        field.IsNavigation.Should().BeTrue();
        field.MappingKind.Should().Be(FieldMappingKind.Convention);
    }

    [Fact]
    public void SameName_DifferentDtoCollectionElementType_IsResolvableForNestedProjection()
    {
        // Collections resolve element-wise: List<ICollection<Order>> over List<Order>
        // is convention-resolvable so nested select trees (Orders(OrderId,...)) can
        // project against the DTO element surface. (Reference navigation type changes
        // remain non-resolvable — see SameName_DifferentDtoNavigationType_IsNotResolvable.)
        var surface = BuildSurface<Customer, CustomerWithOrdersDtoMismatch>();

        surface.TryResolve("Orders", out var field).Should().BeTrue();
        field.IsNavigation.Should().BeTrue();
    }

    // ── 4. Reference navigation is excluded from default projection ────────

    [Fact]
    public void ReferenceNavigation_ExcludedFromDefaultProjection()
    {
        var surface = BuildSurface<Customer, CustomerWithProfileDto>();

        var defaults = surface.GetDefaultSelectFields();
        defaults.Should().Contain("Id");
        defaults.Should().NotContain("Profile");
    }

    // ── 5. Collection navigation is excluded from default projection ───────

    [Fact]
    public void CollectionNavigation_ExcludedFromDefaultProjection()
    {
        var surface = BuildSurface<Customer, CustomerWithOrdersDto>();

        var defaults = surface.GetDefaultSelectFields();
        defaults.Should().Contain("Id");
        defaults.Should().NotContain("Orders");
    }

    // ── 6. Explicitly requested same-type navigation can be resolved ───────

    [Fact]
    public async Task ExplicitlyRequested_ReferenceNavigation_ProjectsServerSide()
    {
        var parameters = new FlexQueryParameters
        {
            Filter = "id:eq:1",
            Select = "Id,Profile"
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerWithProfileDto>(parameters);

        result.Data.Should().ContainSingle();
        result.Data[0].Id.Should().Be(1);
        result.Data[0].Profile.Should().NotBeNull();
        result.Data[0].Profile.Bio.Should().Be("Developer");
    }

    [Fact]
    public async Task ExplicitlyRequested_CollectionNavigation_ProjectsServerSide()
    {
        var parameters = new FlexQueryParameters
        {
            Filter = "id:eq:1",
            Select = "Id,Orders"
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerWithOrdersDto>(parameters);

        result.Data.Should().ContainSingle();
        result.Data[0].Id.Should().Be(1);
        result.Data[0].Orders.Should().NotBeNull();
        result.Data[0].Orders.Should().HaveCount(2);
    }

    // ── 7. Scalar default projection behavior remains unchanged ────────────

    [Fact]
    public void ScalarDefaultProjection_Unchanged()
    {
        var surface = BuildSurface<Customer, CustomerWithProfileDto>();

        var defaults = surface.GetDefaultSelectFields();
        // Only scalar resolvable fields are projected by default.
        defaults.Should().BeEquivalentTo(new[] { "Id" });

        // No navigation should ever appear in the default projection.
        var navigations = surface.GetResolvableFields()
            .Where(f => f.IsNavigation)
            .Select(f => f.SurfaceName)
            .ToList();
        navigations.Should().NotBeEmpty();
        defaults.Should().NotIntersectWith(navigations);
    }

    // ── 8. Include / Expand injects navigations into the typed DTO projection ──

    [Fact]
    public async Task Include_InjectsNavigationIntoDtoProjection()
    {
        // Expand may only target collection navigations; Profile is a reference
        // navigation, so it is materialized through include + select only.
        var parameters = new FlexQueryParameters
        {
            Filter = "id:eq:1",
            Select = "Id,Profile(Bio)",
            Include = "Profile"
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerWithProfileDto>(parameters);

        result.Data.Should().ContainSingle();
        result.Data[0].Profile.Should().NotBeNull();
        result.Data[0].Profile.Bio.Should().Be("Developer");
    }

    [Fact]
    public async Task Expand_ReferenceNavigation_IsRejected()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQuery.NET.Exceptions.FlexQueryException>(async () =>
            await _db.Customers.FlexQueryAsync<Customer, CustomerWithProfileDto>(
                new FlexQueryParameters
                {
                    Filter = "id:eq:1",
                    Select = "Id",
                    Include = "Profile",
                    Expand = "Profile"
                }));

        ex.Message.Should().Contain("reference navigation");
    }

    // ── 9. No navigation is materialized client-side before final execution ─

    [Fact]
    public async Task NoNavigation_MaterializedClientSide_BeforeFinalExecution()
    {
        // Default projection (no explicit select) must not inject the navigation.
        var defaults = await _db.Customers.FlexQueryAsync<Customer, CustomerWithProfileDto>(new FlexQueryParameters());
        defaults.Data.Should().NotBeEmpty();
        defaults.Data.All(d => d.Profile == null).Should().BeTrue();

        // When explicitly requested, the navigation is resolved inside the
        // server-side IQueryable projection (not via post-materialization mapping).
        var requested = await _db.Customers.FlexQueryAsync<Customer, CustomerWithProfileDto>(
            new FlexQueryParameters { Filter = "id:eq:1", Select = "Id,Profile" });
        requested.Data.Should().ContainSingle();
        requested.Data[0].Profile.Should().NotBeNull();
    }
}

