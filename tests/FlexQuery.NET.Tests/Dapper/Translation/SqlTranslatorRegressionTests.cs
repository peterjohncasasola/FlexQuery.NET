using FlexQuery.NET.Models;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Tests.Dapper.Translation;

/// <summary>
/// Regression tests for SqlTranslator builder refactor targeting high-risk areas.
/// Tests verify correct SQL generation without exposing internal implementation details.
/// </summary>
public class SqlTranslatorRegressionTests
{
    private static QueryOptions NoPaging(QueryOptions options)
    {
        options.Paging.Disabled = true;
        options.CaseInsensitive = false;
        return options;
    }

    #region A. Alias Collision Regression Tests

    [Fact]
    public void Translate_SelfReferencingJoin_GeneratesDistinctAliases_ToPreventCollisions()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = NoPaging(new QueryOptions
        {
            ProjectionMode = ProjectionMode.Flat,
            Select = [new SelectNode { Field = "Id" }, new SelectNode { Field = "Manager.Name" }],
            Items = { [ContextKeys.EntityType] = typeof(Employee) }
        });

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("LEFT JOIN [Employees] AS [Manager] ON [Employees].[ManagerId] = [Manager].[Id]");
        command.Sql.Should().Contain("[Manager].[Name]");
    }

    #endregion

    #region B. Parameter Ordering Regression Tests

    [Fact]
    public void Translate_Parameters_AreOrderedSequentially_BasedOnFilterOrder()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Logic = LogicOperator.And,
                Filters =
                [
                    new FilterCondition { Field = "Age", Operator = "gt", Value = "18" },
                    new FilterCondition { Field = "Name", Operator = "eq", Value = "Bob" },
                    new FilterCondition { Field = "Status", Operator = "eq", Value = "Active" }
                ]
            },
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        });

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Parameters.Should().HaveCount(3);
        command.Parameters.Should().ContainKey("@p0");
        command.Parameters.Should().ContainKey("@p1");
        command.Parameters.Should().ContainKey("@p2");
        command.Parameters["@p0"].Should().Be(18);
        command.Parameters["@p1"].Should().Be("Bob");
        command.Parameters["@p2"].Should().Be("Active");

        command.Sql.Should().Contain("[Age] > @p0");
        command.Sql.Should().Contain("[Name] = @p1");
        command.Sql.Should().Contain("[Status] = @p2");
    }

    [Fact]
    public void Translate_FilterParameters_DoNotCollideWith_PagingParameters()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = new QueryOptions
        {
            Sort = { new SortNode { Field = "Name" } },
            Filter = new FilterGroup
            {
                Logic = LogicOperator.And,
                Filters =
                [
                    new FilterCondition { Field = "Age", Operator = "gt", Value = "18" },
                    new FilterCondition { Field = "Name", Operator = "eq", Value = "Bob" }
                ]
            },
            Paging = { Page = 1, PageSize = 10 },
            CaseInsensitive = false,
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        };

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Parameters.Should().HaveCount(4);
        command.Parameters.Should().ContainKey("@p0");
        command.Parameters.Should().ContainKey("@p1");
        command.Parameters.Should().ContainKey("@Offset");
        command.Parameters.Should().ContainKey("@PageSize");
        command.Parameters["@p0"].Should().Be(18);
        command.Parameters["@p1"].Should().Be("Bob");

        command.Sql.Should().Contain("[Age] > @p0");
        command.Sql.Should().Contain("[Name] = @p1");
        command.Sql.Should().Contain("ORDER BY");
        command.Sql.Should().Contain("OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY");
    }

    [Fact]
    public void Translate_AnyOperator_ParametersAreOrdered_BeforePaging()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = new QueryOptions
        {
            Sort = { new SortNode { Field = "Name" } },
            Filter = new FilterGroup
            {
                Logic = LogicOperator.And,
                Filters =
                [
                    new FilterCondition { Field = "Name", Operator = "eq", Value = "Alice" },
                    new FilterCondition
                    {
                        Field = "Orders", Operator = "any",
                        ScopedFilter = new FilterGroup
                        {
                            Filters = [new FilterCondition { Field = "Total", Operator = "gt", Value = "100" }]
                        }
                    }
                ]
            },
            Paging = { Page = 2, PageSize = 5 },
            CaseInsensitive = false,
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        };

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("[Name] = @p0");
        command.Sql.Should().Contain("EXISTS (SELECT 1 FROM [Orders] WHERE [Orders].[CustomerId] = [Customers].[Id] AND ([Total] > @p1))");
        command.Sql.Should().Contain("ORDER BY");
        command.Sql.Should().Contain("OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY");
        
        command.Parameters.Should().ContainKey("@p0");
        command.Parameters.Should().ContainKey("@p1");
        command.Parameters.Should().ContainKey("@Offset");
        command.Parameters.Should().ContainKey("@PageSize");
        
        command.Parameters["@p0"].Should().Be("Alice");
        command.Parameters["@p1"].Should().Be(100);
    }

    [Fact]
    public void Translate_InOperator_ExpandsArray_IntoSequentialParameters()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Id", Operator = "in", Value = "1,2,3" }]
            },
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        });

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("[Id] IN (@p0, @p1, @p2)");
        command.Parameters.Should().ContainKey("@p0");
        command.Parameters.Should().ContainKey("@p1");
        command.Parameters.Should().ContainKey("@p2");
    }

    #endregion

    #region C. Nested Projection Tree Tests

    [Fact]
    public void Translate_NestedProjection_GeneratesLeftJoins_ForDeepNavigation()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = NoPaging(new QueryOptions
        {
            ProjectionMode = ProjectionMode.Flat,
            Select = [new SelectNode { Field = "Orders.OrderItems.Sku" }, new SelectNode { Field = "Orders.OrderItems.Id" }],
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        });

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("LEFT JOIN [Orders] AS [Orders] ON [Orders].[CustomerId] = [Customers].[Id]");
        command.Sql.Should().Contain("LEFT JOIN [OrderItems] AS [OrderItems] ON [OrderItems].[OrderId] = [Orders].[Id]");
        command.Sql.Should().Contain("[OrderItems].[Sku]");
        command.Sql.Should().Contain("[OrderItems].[Id]");
    }

    [Fact]
    public void Translate_OrdersItemsNested_GeneratesCorrectTwoJoins()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = NoPaging(new QueryOptions
        {
            ProjectionMode = ProjectionMode.Flat,
            Select = [new SelectNode { Field = "Orders.OrderItems.Sku" }, new SelectNode { Field = "Orders.OrderItems.Id" }],
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        });

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("LEFT JOIN [Orders] AS [Orders] ON [Orders].[CustomerId] = [Customers].[Id]");
        command.Sql.Should().Contain("LEFT JOIN [OrderItems] AS [OrderItems] ON [OrderItems].[OrderId] = [Orders].[Id]");
        command.Sql.Should().Contain("[OrderItems].[Sku]");
        command.Sql.Should().Contain("[OrderItems].[Id]");
    }

    [Fact]
    public void Translate_FlatMixed_RootAndNestedFields_FullyQualifiesColumns()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = NoPaging(new QueryOptions
        {
            ProjectionMode = ProjectionMode.FlatMixed,
            Select = [new SelectNode { Field = "Name" }, new SelectNode { Field = "Orders.Total" }],
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        });

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("[Customers].[Name]");
        command.Sql.Should().Contain("[Orders].[Total]");
        command.Sql.Should().Contain("LEFT JOIN [Orders] AS [Orders] ON [Orders].[CustomerId] = [Customers].[Id]");
    }

    #endregion

    #region D. Null Operator Tests

    [Fact]
    public void Translate_IsNullOperator_GeneratesIsNullClause()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Description", Operator = "isnull", Value = null }]
            },
            Items = { [ContextKeys.EntityType] = typeof(Product) }
        });

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("[Description] IS NULL");
        command.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Translate_IsNotNullOperator_GeneratesIsNotNullClause()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Description", Operator = "isnotnull", Value = null }]
            },
            Items = { [ContextKeys.EntityType] = typeof(Product) }
        });

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("[Description] IS NOT NULL");
        command.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Translate_IsNull_InAnySubquery_GeneratesCorrectClause()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters =
                [
                    new FilterCondition
                    {
                        Field = "Orders",
                        Operator = "any",
                        ScopedFilter = new FilterGroup
                        {
                            Filters = [new FilterCondition { Field = "Number", Operator = "isnull", Value = null }]
                        }
                    }
                ]
            },
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        });

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("EXISTS (SELECT 1 FROM [Orders] WHERE [Orders].[CustomerId] = [Customers].[Id] AND ([Number] IS NULL))");
    }

    #endregion

    #region E. Multi-Dialect Translation Tests

    [Theory]
    [InlineData(typeof(SqlServerDialect), "[", "]", "OFFSET")]
    [InlineData(typeof(SqliteDialect), "\"", "\"", "LIMIT")]
    public void Translate_DialectSpecific_IdentifierQuoting(Type dialectType, string quotePrefix, string quoteSuffix, string pagingKeyword)
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = NoPaging(new QueryOptions
        {
            Select = [new SelectNode { Field = "Id" }, new SelectNode { Field = "Name" }],
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        });

        var dialect = (ISqlDialect)Activator.CreateInstance(dialectType)!;
        var translator = new SqlTranslator(registry, dialect);
        var command = translator.Translate(options);

        command.Sql.Should().Contain($"{quotePrefix}Id{quoteSuffix}");
        command.Sql.Should().Contain($"{quotePrefix}Name{quoteSuffix}");
    }

    [Fact]
    public void Translate_Sqlite_Paging_UsesLimitOffset()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = new QueryOptions
        {
            Paging = { Page = 2, PageSize = 25 },
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        };

        var translator = new SqlTranslator(registry, new SqliteDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("LIMIT @PageSize OFFSET @Offset");
    }

    [Fact]
    public void Translate_Sqlite_Paging_WithoutSort_AutoGeneratesOrderBy()
    {
        var registry =SharedFlexQueryModel.Instance.Registry;

        var options = new QueryOptions
        {
            Paging = { Page = 1, PageSize = 10 },
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        };

        var translator = new SqlTranslator(registry, new SqliteDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("ORDER BY");
        command.Sql.Should().Contain("\"Id\"");
        command.Sql.Should().Contain("LIMIT @PageSize OFFSET @Offset");
    }
    
    #endregion

    #region F. Case-Insensitive SQL Generation Tests

    [Fact]
    public void Translate_CaseInsensitiveTrue_WrapsStringEqWithLower()
    {
        var registry = new MappingRegistry();
        registry.Entity<Customer>().ToTable("Users");

        var options = new QueryOptions
        {
            Paging = { Disabled = true },
            CaseInsensitive = true,
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "John" }]
            },
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        };

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("LOWER([Name]) = LOWER(@p0)");
    }

    [Fact]
    public void Translate_CaseInsensitiveTrue_WrapsStringContainsWithLower()
    {
        var registry = new MappingRegistry();
        registry.Entity<Customer>().ToTable("Users");

        var options = new QueryOptions
        {
            Paging = { Disabled = true },
            CaseInsensitive = true,
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "contains", Value = "john" }]
            },
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        };

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("LOWER([Name]) LIKE LOWER(@p0)");
    }

    [Fact]
    public void Translate_CaseInsensitiveTrue_WrapsStringInWithLower()
    {
        var registry = new MappingRegistry();
        registry.Entity<Customer>().ToTable("Users");

        var options = new QueryOptions
        {
            Paging = { Disabled = true },
            CaseInsensitive = true,
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "in", Value = "John,Jane" }]
            },
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        };

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("LOWER([Name]) IN (LOWER(@p0), LOWER(@p1))");
    }

    [Fact]
    public void Translate_CaseInsensitiveTrue_DoesNotWrapNonStringFields()
    {
        var registry = new MappingRegistry();
        registry.Entity<Customer>().ToTable("Users");

        var options = new QueryOptions
        {
            Paging = { Disabled = true },
            CaseInsensitive = true,
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Age", Operator = "gt", Value = "18" }]
            },
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        };

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("[Age] > @p0");
        command.Sql.Should().NotContain("LOWER");
    }

    [Fact]
    public void Translate_CaseInsensitiveFalse_DoesNotWrapStringComparisons()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = new QueryOptions
        {
            Paging = { Disabled = true },
            CaseInsensitive = false,
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "John" }]
            },
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        };

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("[Name] = @p0");
        command.Sql.Should().NotContain("LOWER");
    }

    [Fact]
    public void Translate_CaseInsensitiveTrue_WrapsStringStartsWithWithLower()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = new QueryOptions
        {
            Paging = { Disabled = true },
            CaseInsensitive = true,
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "startswith", Value = "Jo" }]
            },
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        };

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("LOWER([Name]) LIKE LOWER(@p0)");
    }

    [Fact]
    public void Translate_CaseInsensitiveTrue_WrapsStringEndsWithWithLower()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = new QueryOptions
        {
            Paging = { Disabled = true },
            CaseInsensitive = true,
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "endswith", Value = "hn" }]
            },
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        };

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("LOWER([Name]) LIKE LOWER(@p0)");
    }

    [Fact]
    public void Translate_CaseInsensitiveTrue_WrapsStringBetweenWithLower()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = new QueryOptions
        {
            Paging = { Disabled = true },
            CaseInsensitive = true,
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "between", Value = "A,Z" }]
            },
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        };

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("LOWER([Name]) BETWEEN LOWER(@p0) AND LOWER(@p1)");
    }

    #endregion

    #region G. Deep Nested Any & Logic Tests

    [Fact]
    public void Translate_DeepNestedAny_UserRolesPermissions_GeneratesCorrectExists()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters =
                [
                    new FilterCondition
                    {
                        Field = "Roles",
                        Operator = "any",
                        ScopedFilter = new FilterGroup
                        {
                            Filters =
                            [
                                new FilterCondition
                                {
                                    Field = "Permissions",
                                    Operator = "any",
                                    ScopedFilter = new FilterGroup
                                    {
                                        Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "Delete" }]
                                    }
                                }
                            ]
                        }
                    }
                ]
            },
            Items = { [ContextKeys.EntityType] = typeof(User) }
        });

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("EXISTS (SELECT 1 FROM [Roles] WHERE [Roles].[UserId] = [Users].[Id] AND (EXISTS (SELECT 1 FROM [Permissions] WHERE [Permissions].[RoleId] = [Roles].[Id] AND ([Name] = @p0))))");
        command.Parameters.Should().ContainKey("@p0");
        command.Parameters["@p0"].Should().Be("Delete");
    }

    [Fact]
    public void Translate_ComplexOrLogic_WrapsConditionsInParentheses()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Logic = LogicOperator.Or,
                Filters =
                [
                    new FilterCondition { Field = "Age", Operator = "gt", Value = "18" },
                    new FilterCondition { Field = "Status", Operator = "eq", Value = "Active" }
                ]
            },
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        });

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("([Age] > @p0 OR [Status] = @p1)");
    }

    #endregion

    #region H. FilterGroup Logical Precedence / Parenthesis Regression Tests

    [Fact]
    public void Translate_AndOrGroups_WrapsEachSubGroupInParentheses_Scenario1()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Logic = LogicOperator.Or,
                Groups =
                [
                    new FilterGroup
                    {
                        Logic = LogicOperator.And,
                        Filters =
                        [
                            new FilterCondition { Field = "Age", Operator = "gt", Value = "18" },
                            new FilterCondition { Field = "Status", Operator = "eq", Value = "Active" }
                        ]
                    },
                    new FilterGroup
                    {
                        Logic = LogicOperator.And,
                        Filters =
                        [
                            new FilterCondition { Field = "Age", Operator = "lt", Value = "10" },
                            new FilterCondition { Field = "Status", Operator = "eq", Value = "Child" }
                        ]
                    }
                ]
            },
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        });

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Match("*([Age] > @p0 AND [Status] = @p1) OR ([Age] < @p2 AND [Status] = @p3)*");

        command.Parameters.Should().HaveCount(4);
        command.Parameters["@p0"].Should().Be(18);
        command.Parameters["@p1"].Should().Be("Active");
        command.Parameters["@p2"].Should().Be(10);
        command.Parameters["@p3"].Should().Be("Child");
    }

    [Fact]
    public void Translate_AndWithNestedOrGroup_WrapsOrInParentheses_Scenario2()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Logic = LogicOperator.And,
                Filters =
                [
                    new FilterCondition { Field = "Age", Operator = "gt", Value = "18" }
                ],
                Groups =
                [
                    new FilterGroup
                    {
                        Logic = LogicOperator.Or,
                        Filters =
                        [
                            new FilterCondition { Field = "Status", Operator = "eq", Value = "Active" },
                            new FilterCondition { Field = "Status", Operator = "eq", Value = "Pending" }
                        ]
                    }
                ]
            },
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        });

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("[Age] > @p0");
        command.Sql.Should().Contain("([Status] = @p1 OR [Status] = @p2)");

        command.Parameters.Should().HaveCount(3);
        command.Parameters["@p0"].Should().Be(18);
        command.Parameters["@p1"].Should().Be("Active");
        command.Parameters["@p2"].Should().Be("Pending");
    }

    [Fact]
    public void Translate_MultipleOrGroupsUnderAnd_PreservesGrouping_Scenario3()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Logic = LogicOperator.And,
                Groups =
                [
                    new FilterGroup
                    {
                        Logic = LogicOperator.Or,
                        Filters =
                        [
                            new FilterCondition { Field = "Age", Operator = "gt", Value = "18" },
                            new FilterCondition { Field = "Status", Operator = "eq", Value = "Active" }
                        ]
                    },
                    new FilterGroup
                    {
                        Logic = LogicOperator.Or,
                        Filters =
                        [
                            new FilterCondition { Field = "Country", Operator = "eq", Value = "PH" },
                            new FilterCondition { Field = "Country", Operator = "eq", Value = "US" }
                        ]
                    }
                ]
            },
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        });

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("([Age] > @p0 OR [Status] = @p1) AND ([Country] = @p2 OR [Country] = @p3)");

        command.Parameters.Should().HaveCount(4);
        command.Parameters["@p0"].Should().Be(18);
        command.Parameters["@p1"].Should().Be("Active");
        command.Parameters["@p2"].Should().Be("PH");
        command.Parameters["@p3"].Should().Be("US");
    }

    [Fact]
    public void Translate_DeeplyNestedGroups_PreservesAllParentheses_Scenario4()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Logic = LogicOperator.And,
                Filters =
                [
                    new FilterCondition { Field = "Country", Operator = "eq", Value = "PH" }
                ],
                Groups =
                [
                    new FilterGroup
                    {
                        Logic = LogicOperator.Or,
                        Groups =
                        [
                            new FilterGroup
                            {
                                Logic = LogicOperator.And,
                                Filters =
                                [
                                    new FilterCondition { Field = "Age", Operator = "gt", Value = "18" },
                                    new FilterCondition { Field = "Status", Operator = "eq", Value = "Active" }
                                ]
                            },
                            new FilterGroup
                            {
                                Logic = LogicOperator.And,
                                Filters =
                                [
                                    new FilterCondition { Field = "Age", Operator = "lt", Value = "10" },
                                    new FilterCondition { Field = "Status", Operator = "eq", Value = "Child" }
                                ]
                            }
                        ]
                    }
                ]
            },
            Items = { [ContextKeys.EntityType] = typeof(Customer) }
        });

        var translator = new SqlTranslator(registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("[Country] = @p0");
        command.Sql.Should().Match("*([Age] > @p1 AND [Status] = @p2) OR ([Age] < @p3 AND [Status] = @p4)*");

        command.Parameters.Should().HaveCount(5);
        command.Parameters["@p0"].Should().Be("PH");
        command.Parameters["@p1"].Should().Be(18);
        command.Parameters["@p2"].Should().Be("Active");
        command.Parameters["@p3"].Should().Be(10);
        command.Parameters["@p4"].Should().Be("Child");
    }

    #endregion
}
