using FlexQuery.NET.Helpers;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Caching;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.EntityFrameworkCore.Options;
using FlexQuery.NET.EntityFrameworkCore;

namespace FlexQuery.NET.Tests.Projection;

public class SelectTests : IDisposable
{
    private readonly TestDbContext _db = TestDbContext.CreateSeeded();
    public void Dispose() => _db.Dispose();

    [Fact]
    public void SelectTreeBuilder_MergesSiblingNavigationPaths_IntoSingleBranch()
    {
        var options = new QueryOptions
        {
            Select = ["Name", "Orders.Total", "Orders.Customer.Name"]
        };

        var tree = SelectTreeBuilder.Build(options);

        tree.TryGetChild("Name", out _).Should().BeTrue();
        tree.TryGetChild("Orders", out var ordersNode).Should().BeTrue();
        ordersNode.TryGetChild("Total", out _).Should().BeTrue();
        ordersNode.TryGetChild("Customer", out var customerNode).Should().BeTrue();
        customerNode.TryGetChild("Name", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Select_Empty_ReturnsOriginalTypePropertiesAsDynamic()
    {
        var options = new QueryOptions();
        var query = _db.Customers.ApplySelect(options);
        
        var list = await query.ToListAsync();
        list.Should().NotBeEmpty();
        
        var first = list.First();
        var type = first.GetType();
        
        // Should have primitive properties projected
        type.GetProperty("Id").Should().NotBeNull();
        type.GetProperty("Name").Should().NotBeNull();
        type.GetProperty("Age").Should().NotBeNull();
        
        // Since Select was empty, ApplySelect returns the original query cast to object,
        // so we get the full Customer back including Profile.
        type.GetProperty("Profile").Should().NotBeNull();
    }

    [Fact]
    public async Task Select_FlatFields_ProjectsOnlyRequestedFields()
    {
        var options = new QueryOptions { Select = ["Id", "Name"] };
        var query = _db.Customers.ApplySelect(options);
        
        var list = await query.ToListAsync();
        var first = list.First();
        var type = first.GetType();

        type.GetProperty("Id").Should().NotBeNull();
        type.GetProperty("Name").Should().NotBeNull();
        type.GetProperty("Age").Should().BeNull(); // Excluded
        
        // Assert values mapped correctly
        ((int)type.GetProperty("Id")!.GetValue(first)!).Should().Be(1);
        ((string)type.GetProperty("Name")!.GetValue(first)!).Should().Be("Alice Johnson");
    }

    [Fact]
    public async Task Select_NestedProperties_ResolvesAndProjects()
    {
        var options = new QueryOptions { Select = ["Id", "Profile.Bio"] };
        var query = _db.Customers.ApplySelect(options);
        
        var list = await query.ToListAsync();
        var first = list.First();
        var type = first.GetType();

        type.GetProperty("Id").Should().NotBeNull();
        var profileProp = type.GetProperty("Profile");
        profileProp.Should().NotBeNull();

        var profileObj = profileProp!.GetValue(first);
        profileObj.Should().NotBeNull();
        
        var profileType = profileObj!.GetType();
        profileType.GetProperty("Bio").Should().NotBeNull();
        ((string)profileType.GetProperty("Bio")!.GetValue(profileObj)!).Should().Be("Developer");
    }

    [Fact]
    public async Task Select_NestedProperties_HandlesNullRelationsGracefully()
    {
        // Diana Prince (Id=4) has null Profile
        var options = new QueryOptions { Select = ["Id", "Profile.Bio"] };
        var query = _db.Customers.Where(e => e.Id == 4).ApplySelect(options);
        
        var list = await query.ToListAsync();
        var first = list.First();
        
        var profileObj = first.GetType().GetProperty("Profile")!.GetValue(first);
        profileObj.Should().BeNull(); // The null condition expression handles it
    }

    [Fact]
    public async Task Select_CollectionProperties_ProjectsCollectionElements()
    {
        var options = new QueryOptions { Select = ["Id", "Orders.Total"] };
        var query = _db.Customers.Where(e => e.Id == 1).ApplySelect(options);
        
        var list = await query.ToListAsync();
        var first = list.First();
        var type = first.GetType();

        var ordersProp = type.GetProperty("Orders");
        ordersProp.Should().NotBeNull();

        var ordersList = ordersProp!.GetValue(first) as System.Collections.IEnumerable;
        ordersList.Should().NotBeNull();
        
        var items = ordersList!.Cast<object>().ToList();
        items.Should().HaveCount(2);

        var orderType = items[0].GetType();
        orderType.GetProperty("Total").Should().NotBeNull();
        orderType.GetProperty("Id").Should().BeNull(); // Not requested
        
        ((decimal)orderType.GetProperty("Total")!.GetValue(items[0])!).Should().Be(150.0m);
    }

    [Fact]
    public async Task Select_IncludeFormat_BringsInWholeNestedObject()
    {
        var options = new QueryOptions { Includes = ["Profile"] };
        var query = _db.Customers.ApplySelect(options);
        
        var list = await query.ToListAsync();
        var first = list.First();
        var type = first.GetType();

        // With Includes and no Select, the result includes root entity scalars + the included navigation
        type.GetProperty("Id").Should().NotBeNull();
        type.GetProperty("Name").Should().NotBeNull();
        type.GetProperty("Age").Should().NotBeNull();
        
        var profileProp = type.GetProperty("Profile");
        profileProp.Should().NotBeNull();
        
        var profileObj = profileProp!.GetValue(first);
        var profileType = profileObj!.GetType();
        profileType.GetProperty("Bio").Should().NotBeNull();
    }

    [Fact]
    public async Task Select_Tree_BuildsComplexProjection()
    {
        var selectTree = new SelectionNode();
        selectTree.GetOrAddChild("Id");
        var profile = selectTree.GetOrAddChild("Profile");
        profile.GetOrAddChild("Bio");
        var ordersNode = selectTree.GetOrAddChild("Orders");
        ordersNode.GetOrAddChild("Total");

        var options = new QueryOptions { SelectTree = selectTree };
        
        var query = _db.Customers.Where(e => e.Id == 1).ApplySelect(options);
        var list = await query.ToListAsync();
        var first = list.First();
        var type = first.GetType();

        type.GetProperty("Id").Should().NotBeNull();
        type.GetProperty("Profile").Should().NotBeNull();
        type.GetProperty("Orders").Should().NotBeNull();
        
        var profileType = type.GetProperty("Profile")!.GetValue(first)!.GetType();
        profileType.GetProperty("Bio").Should().NotBeNull();
        
        var orders = ((System.Collections.IEnumerable)type.GetProperty("Orders")!.GetValue(first)!).Cast<object>().ToList();
        orders[0].GetType().GetProperty("Total").Should().NotBeNull();
    }

    [Fact]
    public async Task Select_InvalidField_IsIgnoredAndDoesNotThrow()
    {
        var options = new QueryOptions { Select = ["Id", "NonExistentField", "Profile.Fake"] };
        var query = _db.Customers.ApplySelect(options);
        
        var list = await query.ToListAsync();
        var first = list.First();
        var type = first.GetType();

        type.GetProperty("Id").Should().NotBeNull();
        type.GetProperty("NonExistentField").Should().BeNull();
        
        var profileProp = type.GetProperty("Profile");
        profileProp.Should().NotBeNull();
        profileProp!.GetValue(first)!.GetType().GetProperty("Fake").Should().BeNull();
    }

    [Fact]
    public async Task Select_RelationalSql_UsesMergedNavigationAndOnlyRequestedColumns()
    {
        using var db = SqlProjectionDbContext.CreateSeeded();

        var options = new QueryOptions
        {
            Select = ["Name", "Email", "Orders.Number", "Orders.Customer.Name"]
        };

        var baseQuery = db.Customers
            .Where(x => x.Name.Contains("o") || x.Name.Contains("A"))
            .OrderBy(x => x.Id)
            .Skip(0)
            .Take(10);

        var projected = baseQuery.ApplySelect(options);
        var sql = projected.ToQueryString();

        Regex.Matches(sql, "\"Orders\"").Count.Should().BeLessOrEqualTo(2);
        sql.Should().Contain("\"Number\"");
        sql.Should().Contain("\"Name\"");
        sql.Should().Contain("\"Email\"");
        sql.Should().NotContain("\"Total\"");
        sql.Should().NotContain("\"CreatedAtUtc\"");

        var rows = await projected.ToListAsync();
        rows.Should().HaveCount(8);
    }

    // ──────────────────────────────────────────────────────────────
    //  Nested Select Integration Tests
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Select_NestedTree_ProjectsCorrectShape()
    {
        var options = new QueryOptions();
        options.SelectTree = new SelectionNode();
        options.SelectTree.GetOrAddChild("Id");
        options.SelectTree.GetOrAddChild("Name");
        var profile = options.SelectTree.GetOrAddChild("Profile");
        profile.GetOrAddChild("Id");
        profile.GetOrAddChild("Bio");

        var result = await _db.Customers.FlexQueryAsync(options);
        result.Data.Should().NotBeEmpty();

        var first = result.Data.First();
        var type = first.GetType();

        type.GetProperty("Id").Should().NotBeNull();
        type.GetProperty("Name").Should().NotBeNull();
        type.GetProperty("Age").Should().BeNull();

        var profileProp = type.GetProperty("Profile");
        profileProp.Should().NotBeNull();
        var profileObj = profileProp!.GetValue(first);
        profileObj.Should().NotBeNull();
        profileObj!.GetType().GetProperty("Id").Should().NotBeNull();
        profileObj.GetType().GetProperty("Bio").Should().NotBeNull();
        profileObj.GetType().GetProperty("Customer").Should().BeNull();
    }

    [Fact]
    public async Task Select_NestedCollection_ProjectsCorrectShape()
    {
        var options = new QueryOptions();
        options.SelectTree = new SelectionNode();
        var orders = options.SelectTree.GetOrAddChild("Orders");
        orders.GetOrAddChild("Id");
        orders.GetOrAddChild("Total");

        var result = await _db.Customers.FlexQueryAsync(options);
        result.Data.Should().NotBeEmpty();

        var first = result.Data.First();
        var type = first.GetType();

        var ordersProp = type.GetProperty("Orders");
        ordersProp.Should().NotBeNull();
        var ordersObj = ordersProp!.GetValue(first) as System.Collections.IEnumerable;
        ordersObj.Should().NotBeNull();

        var orderList = ordersObj.Cast<object>().ToList();
        orderList.Should().NotBeEmpty();
        orderList[0].GetType().GetProperty("Id").Should().NotBeNull();
        orderList[0].GetType().GetProperty("Total").Should().NotBeNull();
        orderList[0].GetType().GetProperty("Status").Should().BeNull();
    }

    [Fact]
    public async Task Select_RootWildcard_ProjectsAllowedScalars()
    {
        var options = new QueryOptions();
        options.SelectTree = new SelectionNode();
        options.SelectTree.MarkIncludeAllScalars();

        var execOptions = new EfCoreQueryOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            StrictFieldValidation = false
        };

        var result = await _db.Customers.FlexQueryAsync(options, execOptions);
        result.Data.Should().NotBeEmpty();

        var first = result.Data.First();
        var type = first.GetType();

        type.GetProperty("Id").Should().NotBeNull();
        type.GetProperty("Name").Should().NotBeNull();
        type.GetProperty("Email").Should().BeNull();
        type.GetProperty("SSN").Should().BeNull();
    }

    [Fact]
    public async Task SelectTree_Governance_BlocksNestedField_Strict()
    {
        var options = new QueryOptions();
        options.SelectTree = new SelectionNode();
        var orders = options.SelectTree.GetOrAddChild("Orders");
        orders.GetOrAddChild("Total");

        var execOptions = new EfCoreQueryOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Orders.Total" },
            StrictFieldValidation = true
        };

        var act = async () => await _db.Customers.FlexQueryAsync(options, execOptions);

        (await act.Should().ThrowAsync<QueryValidationException>())
            .Which.Message.Should().Contain("Orders.Total");
    }

    [Fact]
    public async Task SelectTree_Governance_RemovesNestedField_NonStrict()
    {
        var options = new QueryOptions();
        options.SelectTree = new SelectionNode();
        var orders = options.SelectTree.GetOrAddChild("Orders");
        orders.GetOrAddChild("Total");
        orders.GetOrAddChild("Status");

        var execOptions = new EfCoreQueryOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Orders.Total" },
            StrictFieldValidation = false
        };

        options.ValidateSafe<Customer>(execOptions);

        orders.TryGetChild("Total", out _).Should().BeFalse();
        orders.TryGetChild("Status", out _).Should().BeTrue();

        var query = _db.Customers.ApplySelect(options);
        var list = await query.ToListAsync();
        list.Should().NotBeEmpty();

        var first = list.First();
        var type = first.GetType();

        var ordersProp = type.GetProperty("Orders");
        ordersProp.Should().NotBeNull();
        var ordersObj = ordersProp!.GetValue(first) as System.Collections.IEnumerable;
        ordersObj.Should().NotBeNull();

        var orderList = ordersObj.Cast<object>().ToList();
        orderList.Should().NotBeEmpty();
        orderList[0].GetType().GetProperty("Total").Should().BeNull();
        orderList[0].GetType().GetProperty("Status").Should().NotBeNull();
    }

    [Fact]
    public async Task Select_NestedField_AllowedByAllowedFields_Succeeds()
    {
        var options = new QueryOptions();
        options.SelectTree = new SelectionNode();
        var profile = options.SelectTree.GetOrAddChild("Profile");
        profile.GetOrAddChild("Bio");

        var execOptions = new EfCoreQueryOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Profile.Bio" }
        };

        var result = await _db.Customers.FlexQueryAsync(options, execOptions);
        result.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Select_NestedField_BlockedByBlockedFields_Strict()
    {
        var options = new QueryOptions();
        options.SelectTree = new SelectionNode();
        var profile = options.SelectTree.GetOrAddChild("Profile");
        profile.GetOrAddChild("Bio");

        var execOptions = new EfCoreQueryOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Profile.Bio" },
            StrictFieldValidation = true
        };

        var act = async () => await _db.Customers.FlexQueryAsync(options, execOptions);

        (await act.Should().ThrowAsync<QueryValidationException>())
            .Which.Message.Should().Contain("Profile.Bio");
    }

    [Fact]
    public async Task Select_NestedField_RemovedByBlockedFields_NonStrict()
    {
        var options = new QueryOptions();
        options.SelectTree = new SelectionNode();
        var profile = options.SelectTree.GetOrAddChild("Profile");
        profile.GetOrAddChild("Bio");
        profile.GetOrAddChild("PreferredLanguage");

        var execOptions = new EfCoreQueryOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Profile.Bio" },
            StrictFieldValidation = false
        };

        var result = await _db.Customers.FlexQueryAsync(options, execOptions);
        result.Data.Should().NotBeEmpty();

        var first = result.Data.First();
        var type = first.GetType();

        var profileProp = type.GetProperty("Profile");
        profileProp.Should().NotBeNull();
        var profileObj = profileProp!.GetValue(first);
        profileObj.Should().NotBeNull();
        profileObj!.GetType().GetProperty("Bio").Should().BeNull();
        profileObj.GetType().GetProperty("PreferredLanguage").Should().NotBeNull();
    }

    #region Nested Alias Integration

    [Fact]
    public async Task Select_NestedAlias_ProjectsWithAliasedFieldNames()
    {
        var options = new QueryOptions();
        options.SelectTree = new SelectionNode();
        var profile = options.SelectTree.GetOrAddChild("Profile");
        var bio = profile.GetOrAddChild("Bio");
        bio.Alias = "ProfileBio";

        var result = await _db.Customers.FlexQueryAsync(options);
        result.Data.Should().NotBeEmpty();

        var first = result.Data.First();
        var type = first.GetType();

        var profileProp = type.GetProperty("Profile");
        profileProp.Should().NotBeNull();
        var profileObj = profileProp!.GetValue(first);
        profileObj.Should().NotBeNull();
        profileObj?.GetType().GetProperty("ProfileBio").Should().NotBeNull();
        profileObj.GetType().GetProperty("Bio").Should().BeNull();
    }

    [Fact]
    public async Task Select_NestedAlias_DeepNesting_PropagatesCorrectly()
    {
        var options = new QueryOptions();
        options.SelectTree = new SelectionNode();
        var profile = options.SelectTree.GetOrAddChild("Profile");
        var bio = profile.GetOrAddChild("Bio");
        bio.Alias = "ProfileBio";

        var result = await _db.Customers.FlexQueryAsync(options);
        result.Data.Should().NotBeEmpty();

        var first = result.Data.First();
        var type = first.GetType();

        var profileProp = type.GetProperty("Profile");
        profileProp.Should().NotBeNull();
        var profileObj = profileProp!.GetValue(first);
        profileObj.Should().NotBeNull();
        profileObj.GetType().GetProperty("ProfileBio").Should().NotBeNull();
        profileObj.GetType().GetProperty("Bio").Should().BeNull();
    }

    [Fact]
    public void Select_MergeTree_PropagatesAliases()
    {
        var tree = new SelectionNode();
        var profile = tree.GetOrAddChild("Profile");
        profile.GetOrAddChild("Bio").Alias = "ProfileBio";
        profile.GetOrAddChild("Email").Alias = "ProfileEmail";

        tree.TryGetChild("Profile", out var sourceProfile).Should().BeTrue();
        sourceProfile!.TryGetChild("Bio", out var sourceBio).Should().BeTrue();
        sourceBio!.Alias.Should().Be("ProfileBio");
        sourceProfile.TryGetChild("Email", out var sourceEmail).Should().BeTrue();
        sourceEmail!.Alias.Should().Be("ProfileEmail");

        var result = SelectTreeBuilder.Build(new QueryOptions { SelectTree = tree });

        result.HasChildren.Should().BeTrue();
        result.TryGetChild("Profile", out var p).Should().BeTrue();
        p!.HasChildren.Should().BeTrue();
        p.TryGetChild("Bio", out var bio).Should().BeTrue();
        bio!.Alias.Should().Be("ProfileBio");
        p.TryGetChild("Email", out var email).Should().BeTrue();
        email!.Alias.Should().Be("ProfileEmail");
    }

    [Fact]
    public async Task Select_Alias_IgnoresValidation()
    {
        var options = new QueryOptions();
        options.SelectTree = new SelectionNode();
        var profile = options.SelectTree.GetOrAddChild("Profile");
        var bio = profile.GetOrAddChild("Bio");
        bio.Alias = "CustomerBio";

        var execOptions = new EfCoreQueryOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Profile.Bio" }
        };

        var result = await _db.Customers.FlexQueryAsync(options, execOptions);
        result.Data.Should().NotBeEmpty();
    }

    [Fact]
    public void Select_Alias_DuplicateIdentical_Merges()
    {
        var tree = new SelectionNode();
        var profile = tree.GetOrAddChild("Profile");
        profile.GetOrAddChild("Bio").Alias = "ProfileBio";

        var options = new QueryOptions
        {
            SelectTree = tree,
            Select = new List<string> { "Profile.Bio AS ProfileBio" }
        };

        var result = SelectTreeBuilder.Build(options);
        result.TryGetChild("Profile", out var p).Should().BeTrue();
        p!.TryGetChild("Bio", out var bio).Should().BeTrue();
        bio!.Alias.Should().Be("ProfileBio");
    }

    [Fact]
    public void Select_Alias_Conflicting_Throws()
    {
        var tree = new SelectionNode();
        var profile = tree.GetOrAddChild("Profile");
        profile.GetOrAddChild("Bio").Alias = "FirstBio";

        var options = new QueryOptions
        {
            SelectTree = tree,
            Select = new List<string> { "Profile.Bio AS LastBio" }
        };

        var act = () => SelectTreeBuilder.Build(options);

        act.Should().Throw<QueryValidationException>()
            .WithMessage("*Conflicting aliases*");
    }

    [Fact]
    public void Select_Alias_MultipleDuplicateNestedBranches_Merges()
    {
        var options = new QueryOptions
        {
            Select = new List<string>
            {
                "Customer.Name AS CustomerName",
                "Customer.Email AS CustomerEmail",
                "Customer.Name AS CustomerName",
                "Customer.Email AS CustomerEmail"
            }
        };

        var result = SelectTreeBuilder.Build(options);
        result.TryGetChild("Customer", out var customer).Should().BeTrue();
        customer!.TryGetChild("Name", out var name).Should().BeTrue();
        name!.Alias.Should().Be("CustomerName");
        customer.TryGetChild("Email", out var email).Should().BeTrue();
        email!.Alias.Should().Be("CustomerEmail");
    }

    [Fact]
    public void Select_Alias_DeepRecursiveMerge_PropagatesAliases()
    {
        var options = new QueryOptions
        {
            Select = new List<string>
            {
                "Profile.Bio AS ProfileBio",
                "Profile.Id"
            }
        };

        var result = SelectTreeBuilder.Build(options);
        result.TryGetChild("Profile", out var profile).Should().BeTrue();
        profile!.TryGetChild("Bio", out var bio).Should().BeTrue();
        bio!.Alias.Should().Be("ProfileBio");
        profile.TryGetChild("Id", out _).Should().BeTrue();
    }

    [Fact]
    public void Select_Alias_CacheKey_DifferentFromBareField()
    {
        var optionsWithAlias = new QueryOptions { Select = new List<string> { "Profile.Bio AS ProfileBio" } };
        var optionsWithoutAlias = new QueryOptions { Select = new List<string> { "Profile.Bio" } };

        var keyWithAlias = QueryCacheKeyBuilder.Build(optionsWithAlias, typeof(Customer), "query");
        var keyWithoutAlias = QueryCacheKeyBuilder.Build(optionsWithoutAlias, typeof(Customer), "query");

        keyWithAlias.Should().NotBe(keyWithoutAlias);
    }

    [Fact]
    public void Select_Alias_CacheKey_SameAlias_IdenticalKeys()
    {
        var options1 = new QueryOptions { Select = new List<string> { "Profile.Bio AS ProfileBio" } };
        var options2 = new QueryOptions { Select = new List<string> { "Profile.Bio AS ProfileBio" } };

        var key1 = QueryCacheKeyBuilder.Build(options1, typeof(Customer), "query");
        var key2 = QueryCacheKeyBuilder.Build(options2, typeof(Customer), "query");

        key1.Should().Be(key2);
    }

    #endregion

}
