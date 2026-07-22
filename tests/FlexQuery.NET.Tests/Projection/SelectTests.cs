using FlexQuery.NET.Helpers;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Caching;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.EntityFrameworkCore.Options;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Fql;

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
            Select = [new SelectNode { Field = "Name" }, new SelectNode { Field = "Orders.Total" }, new SelectNode { Field = "Orders.Customer.Name" }]
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
        var options = new QueryOptions { Select = [new SelectNode { Field = "Id" }, new SelectNode { Field = "Name" }] };
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
        var options = new QueryOptions { Select = [new SelectNode { Field = "Id" }, new SelectNode { Field = "Profile.Bio" }] };
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
        var options = new QueryOptions { Select = [new SelectNode { Field = "Id" }, new SelectNode { Field = "Profile.Bio" }] };
        var query = _db.Customers.Where(e => e.Id == 4).ApplySelect(options);
        
        var list = await query.ToListAsync();
        var first = list.First();
        
        var profileObj = first.GetType().GetProperty("Profile")!.GetValue(first);
        profileObj.Should().BeNull(); // The null condition expression handles it
    }

    [Fact]
    public async Task Select_CollectionProperties_ProjectsCollectionElements()
    {
        var options = new QueryOptions { Select = [new SelectNode { Field = "Id" }, new SelectNode { Field = "Orders.Total" }] };
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
        var options = new QueryOptions { Select = [new SelectNode { Field = "Id" }, new SelectNode { Field = "NonExistentField" }, new SelectNode { Field = "Profile.Fake" }] };
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
            Select = [new SelectNode { Field = "Name" }, new SelectNode { Field = "Email" }, new SelectNode { Field = "Orders.Number" }, new SelectNode { Field = "Orders.Customer.Name" }]
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
            Select = new List<SelectNode> { new SelectNode { Field = "Profile.Bio", Alias = "ProfileBio" } }
        };

        var result = SelectTreeBuilder.Build(options);
        result.TryGetChild("Profile", out var p).Should().BeTrue();
        p!.TryGetChild("Bio", out var bio).Should().BeTrue();
        bio!.Alias.Should().Be("ProfileBio");
    }

    [Fact]
    public void Select_Alias_Conflicting_Throws()
    {
        var options = new QueryOptions
        {
            Select =
            [
                new SelectNode { Field = "Profile.Bio", Alias = "FirstBio" },
                new SelectNode { Field = "Profile.Bio", Alias = "LastBio" }
            ]
        };

        var         act = () => SelectTreeBuilder.Build(options);
        act.Should().Throw<QueryValidationException>()
            .WithMessage("*is projected multiple times with different aliases*")
            .WithMessage("*'Profile.Bio'*")
            .WithMessage("*'FirstBio' and 'LastBio'*");
    }

    [Fact]
    public void Select_Alias_MultipleDuplicateNestedBranches_Merges()
    {
        var options = new QueryOptions
        {
            Select = new List<SelectNode> { new SelectNode { Field = "Customer.Name", Alias = "CustomerName" }, new SelectNode { Field = "Customer.Email", Alias = "CustomerEmail" }, new SelectNode { Field = "Customer.Name", Alias = "CustomerName" }, new SelectNode { Field = "Customer.Email", Alias = "CustomerEmail" } }
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
            Select = new List<SelectNode> { new SelectNode { Field = "Profile.Bio", Alias = "ProfileBio" }, new SelectNode { Field = "Profile.Id" } }
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
        var optionsWithAlias = new QueryOptions { Select = new List<SelectNode> { new SelectNode { Field = "Profile.Bio", Alias = "ProfileBio" } } };
        var optionsWithoutAlias = new QueryOptions { Select = new List<SelectNode> { new SelectNode { Field = "Profile.Bio" } } };

        var keyWithAlias = QueryCacheKeyBuilder.Build(optionsWithAlias, typeof(Customer), "query");
        var keyWithoutAlias = QueryCacheKeyBuilder.Build(optionsWithoutAlias, typeof(Customer), "query");

        keyWithAlias.Should().NotBe(keyWithoutAlias);
    }

    [Fact]
    public void Select_Alias_CacheKey_SameAlias_IdenticalKeys()
    {
        var options1 = new QueryOptions { Select = new List<SelectNode> { new SelectNode { Field = "Profile.Bio", Alias = "ProfileBio" } } };
        var options2 = new QueryOptions { Select = new List<SelectNode> { new SelectNode { Field = "Profile.Bio", Alias = "ProfileBio" } } };

        var key1 = QueryCacheKeyBuilder.Build(options1, typeof(Customer), "query");
        var key2 = QueryCacheKeyBuilder.Build(options2, typeof(Customer), "query");

        key1.Should().Be(key2);
    }

    #endregion

    private static SelectNode S(string field, string? alias = null, List<SelectNode>? children = null)
    {
        var node = new SelectNode { Field = field, Alias = alias };
        if (children != null) node.Children.AddRange(children);
        return node;
    }

    #region Nested Select Syntax Alias

    private static void AssertSelectionTreeEqual(SelectionNode expected, SelectionNode actual)
    {
        actual.Should().NotBeNull();
        actual!.HasChildren.Should().Be(expected.HasChildren);
        actual.IncludeAllScalars.Should().Be(expected.IncludeAllScalars);
        actual.Count.Should().Be(expected.Count);

        foreach (var expectedChild in expected.EnumerateChildren())
        {
            actual.TryGetChild(expectedChild.Key, out var actualChild).Should().BeTrue($"expected child '{expectedChild.Key}' not found");
            actualChild.Should().NotBeNull();
            actualChild!.Alias.Should().Be(expectedChild.Value.Alias);
            actualChild.IncludeAllScalars.Should().Be(expectedChild.Value.IncludeAllScalars);
            actualChild.Count.Should().Be(expectedChild.Value.Count);
            AssertSelectionTreeEqual(expectedChild.Value, actualChild);
        }
    }

    [Fact]
    public void Select_NestedSelectSyntax_SingleNestedAlias_AppliesAliasToChild()
    {
        var options = new QueryOptions
        {
            Select = new List<SelectNode>
            {
                S("Profile", children: new List<SelectNode>
                    { S("Bio", "ProfileBio") } )
            }
        };

        var expected = new SelectionNode();
        var profile = expected.GetOrAddChild("Profile");
        profile.GetOrAddChild("Bio").Alias = "ProfileBio";

        var result = SelectTreeBuilder.Build(options);
        AssertSelectionTreeEqual(expected, result);
    }

    [Fact]
    public void Select_NestedSelectSyntax_MultipleNestedAliases_AllApplied()
    {
        var options = new QueryOptions
        {
            Select = new List<SelectNode>
            {
                S("Profile", children: new List<SelectNode>
                    {
                        S("Bio", "ProfileBio"),
                        S("PreferredLanguage", "ProfileLang")
                    } )
            }
        };

        var expected = new SelectionNode();
        var profile = expected.GetOrAddChild("Profile");
        profile.GetOrAddChild("Bio").Alias = "ProfileBio";
        profile.GetOrAddChild("PreferredLanguage").Alias = "ProfileLang";

        var result = SelectTreeBuilder.Build(options);
        AssertSelectionTreeEqual(expected, result);
    }

    [Fact]
    public void Select_NestedSelectSyntax_MixedAliasedAndNonAliased_PreservesAll()
    {
        var options = new QueryOptions
        {
            Select = new List<SelectNode>
            {
                S("Profile", children: new List<SelectNode>
                    {
                        S("Bio", "ProfileBio"),
                        S("PreferredLanguage"),
                        S("LoyaltyPoints", "ProfilePoints")
                    } )
            }
        };

        var expected = new SelectionNode();
        var profile = expected.GetOrAddChild("Profile");
        var bio = profile.GetOrAddChild("Bio");
        bio.Alias = "ProfileBio";
        var lang = profile.GetOrAddChild("PreferredLanguage");
        var points = profile.GetOrAddChild("LoyaltyPoints");
        points.Alias = "ProfilePoints";

        var result = SelectTreeBuilder.Build(options);
        AssertSelectionTreeEqual(expected, result);
    }

    [Fact]
    public void Select_NestedSelectSyntax_MultipleNestedObjects_EachWithAliases()
    {
        var options = new QueryOptions
        {
            Select = new List<SelectNode>
            {
                S("Profile", children: new List<SelectNode>
                    { S("Bio", "ProfileBio") } ),
                S("Address", children: new List<SelectNode>
                    { S("City", "AddressCity") } )
            }
        };

        var expected = new SelectionNode();
        var profile = expected.GetOrAddChild("Profile");
        profile.GetOrAddChild("Bio").Alias = "ProfileBio";
        var address = expected.GetOrAddChild("Address");
        address.GetOrAddChild("City").Alias = "AddressCity";

        var result = SelectTreeBuilder.Build(options);
        AssertSelectionTreeEqual(expected, result);
    }

    [Fact]
    public void Select_NestedSelectSyntax_DeepNesting_PropagatesAliases()
    {
        var options = new QueryOptions
        {
            Select = new List<SelectNode>
            {
                S("Profile", children: new List<SelectNode>
                    {
                        S("Address", children: new List<SelectNode>
                            { S("City", "ProfileCity") } )
                    } )
            }
        };

        var expected = new SelectionNode();
        var profile = expected.GetOrAddChild("Profile");
        var address = profile.GetOrAddChild("Address");
        address.GetOrAddChild("City").Alias = "ProfileCity";

        var result = SelectTreeBuilder.Build(options);
        AssertSelectionTreeEqual(expected, result);
    }

    [Fact]
    public void Select_NestedSelectSyntax_NoDuplicateNesting_PreventsSpuriousGrandchildren()
    {
        var options = new QueryOptions
        {
            Select = new List<SelectNode>
            {
                S("Profile", children: new List<SelectNode>
                    { S("Bio", "ProfileBio") } )
            }
        };

        var result = SelectTreeBuilder.Build(options);

        result.TryGetChild("Profile", out var profile).Should().BeTrue();
        profile.Should().NotBeNull();
        profile!.Count.Should().Be(1);
        profile.TryGetChild("Bio", out var bio).Should().BeTrue();
        bio.Should().NotBeNull();
        bio!.Alias.Should().Be("ProfileBio");
        bio.Count.Should().Be(0);
        bio.IncludeAllScalars.Should().BeFalse();
    }

    [Fact]
    public void Select_NestedSelectSyntax_FlatPathAndNestedSyntax_IdenticalStructure()
    {
        var flatOptions = new QueryOptions
        {
            Select = new List<SelectNode>
            {
                new SelectNode { Field = "Profile.Bio", Alias = "ProfileBio" },
                new SelectNode { Field = "Profile.PreferredLanguage", Alias = "ProfileLang" }
            }
        };

        var nestedOptions = new QueryOptions
        {
            Select = new List<SelectNode>
            {
                S("Profile", children: new List<SelectNode>
                    {
                        S("Bio", "ProfileBio"),
                        S("PreferredLanguage", "ProfileLang")
                    } )
            }
        };

        var flatTree = SelectTreeBuilder.Build(flatOptions);
        var nestedTree = SelectTreeBuilder.Build(nestedOptions);

        AssertSelectionTreeEqual(flatTree, nestedTree);
    }

    [Fact]
    public void Select_NestedSelectSyntax_MixedFlatPathAndNestedSyntax_AllAliasesPreserved()
    {
        var options = new QueryOptions
        {
            Select = new List<SelectNode>
            {
                S("Profile.PreferredLanguage", "ProfileLangFlat" ),
                S("Profile", children: new List<SelectNode>
                    {
                        S("Bio", "ProfileBioNested")
                    } )
            }
        };

        var expected = new SelectionNode();
        var profile = expected.GetOrAddChild("Profile");
        profile.GetOrAddChild("PreferredLanguage").Alias = "ProfileLangFlat";
        profile.GetOrAddChild("Bio").Alias = "ProfileBioNested";

        var result = SelectTreeBuilder.Build(options);
        AssertSelectionTreeEqual(expected, result);
    }

    #endregion

    #region Nested Select Syntax Alias Conflicts

    [Fact]
    public void Select_NestedSelectSyntax_FlatPlusNested_ConflictingAlias_Throws()
    {
        var options = new QueryOptions
        {
            Select =
            [
                S("Profile.Bio", "A"),
                S("Profile", children: [S("Bio", "B")])
            ]
        };

        var act = () => SelectTreeBuilder.Build(options);
        act.Should().Throw<QueryValidationException>()
            .WithMessage("*is projected multiple times with different aliases*")
            .WithMessage("*'Profile.Bio'*")
            .WithMessage("*'A' and 'B'*");
    }

    [Fact]
    public void Select_NestedSelectSyntax_NestedPlusNested_ConflictingAlias_Throws()
    {
        var options = new QueryOptions
        {
            Select =
            [
                S("Profile", children:
                [
                    S("Bio", "A"),
                    S("Bio", "B")
                ])
            ]
        };

        var act = () => SelectTreeBuilder.Build(options);
        act.Should().Throw<QueryValidationException>()
            .WithMessage("*is projected multiple times with different aliases*")
            .WithMessage("*'Profile.Bio'*")
            .WithMessage("*'A' and 'B'*");
    }

    [Fact]
    public void Select_AliasConflict_FlatPlusNested_SameAlias_Succeeds()
    {
        var options = new QueryOptions
        {
            Select =
            [
                S("Profile.Bio", "ProfileBio"),
                S("Profile", children: [S("Bio", "ProfileBio")])
            ]
        };

        var result = SelectTreeBuilder.Build(options);
        result.TryGetChild("Profile", out var profile).Should().BeTrue();
        profile!.TryGetChild("Bio", out var bio).Should().BeTrue();
        bio!.Alias.Should().Be("ProfileBio");
    }

    [Fact]
    public void Select_AliasConflict_NestedPlusNested_SameAlias_Succeeds()
    {
        var options = new QueryOptions
        {
            Select =
            [
                S("Profile", children:
                [
                    S("Bio", "ProfileBio"),
                    S("Bio", "ProfileBio")
                ])
            ]
        };

        var result = SelectTreeBuilder.Build(options);
        result.TryGetChild("Profile", out var profile).Should().BeTrue();
        profile!.TryGetChild("Bio", out var bio).Should().BeTrue();
        bio!.Alias.Should().Be("ProfileBio");
    }

    [Fact]
    public void Select_AliasConflict_FlatPlusFlat_DifferentAliases_Throws()
    {
        var options = new QueryOptions
        {
            Select =
            [
                S("Profile.Bio", "FirstBio"),
                S("Profile.Bio", "SecondBio")
            ]
        };

        var act = () => SelectTreeBuilder.Build(options);
        act.Should().Throw<QueryValidationException>()
            .WithMessage("*is projected multiple times with different aliases*")
            .WithMessage("*'Profile.Bio'*")
            .WithMessage("*'FirstBio' and 'SecondBio'*");
    }

    #endregion

    #region Nested Select Syntax End-to-End

    [Fact]
    public async Task Select_NestedAlias_EndToEnd_ParserToMaterialization_ProjectsAliasedFieldNames()
    {
        Fql.Register();

        var parameters = new FlexQueryParameters
        {
            Select = "Id AS customerId,Profile(Bio AS profileBio)",
            Include = "Profile"
        };

        var options = QueryOptionsParser.Parse(parameters, QuerySyntax.Fql);

        var result = await _db.Customers.FlexQueryAsync(options);

        result.Data.Should().NotBeEmpty();
        var first = result.Data.First();
        var type = first.GetType();

        type.GetProperty("customerId").Should().NotBeNull();
        type.GetProperty("Id").Should().BeNull("root alias must replace original name");

        var profileProp = type.GetProperty("Profile");
        profileProp.Should().NotBeNull();
        var profileObj = profileProp!.GetValue(first);
        profileObj.Should().NotBeNull();
        profileObj!.GetType().GetProperty("profileBio").Should().NotBeNull("nested alias must appear on projected navigation");
        profileObj.GetType().GetProperty("Bio").Should().BeNull("original name must be hidden by alias");

        ((int)type.GetProperty("customerId")!.GetValue(first)!).Should().Be(1);
    }

    [Fact]
    public async Task Select_NestedAlias_WithInclude_AppliesAliasAndPreservesInclude()
    {
        Fql.Register();

        var parameters = new FlexQueryParameters
        {
            Include = "Profile",
            Select = "Id,Profile(Bio AS profileBio)"
        };

        var options = QueryOptionsParser.Parse(parameters, QuerySyntax.Fql);

        var result = await _db.Customers.FlexQueryAsync(options);

        result.Data.Should().NotBeEmpty();
        var first = result.Data.First();
        var type = first.GetType();

        type.GetProperty("Id").Should().NotBeNull();

        var profileProp = type.GetProperty("Profile");
        profileProp.Should().NotBeNull();
        var profileObj = profileProp!.GetValue(first);
        profileObj.Should().NotBeNull();
        profileObj!.GetType().GetProperty("profileBio").Should().NotBeNull("nested alias must appear on included navigation");
        profileObj.GetType().GetProperty("Bio").Should().BeNull("original name must be hidden by alias");
    }

    [Fact]
    public void Select_AliasConflict_MixedFlatAndNested_SameFieldDifferentAliases_Throws()
    {
        var options = new QueryOptions
        {
            Select =
            [
                S("PrimaryContact.EmailAddress", "Email"),
                S("PrimaryContact", children: [S("EmailAddress", "ContactEmail")])
            ]
        };

        var act = () => SelectTreeBuilder.Build(options);
        act.Should().Throw<QueryValidationException>()
            .WithMessage("*is projected multiple times with different aliases*")
            .WithMessage("*'PrimaryContact.EmailAddress'*")
            .WithMessage("*'Email' and 'ContactEmail'*");
    }

    #endregion

}
