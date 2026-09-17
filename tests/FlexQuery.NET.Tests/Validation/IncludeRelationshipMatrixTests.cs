using FlexQuery.NET.Constants;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Fql;
using FlexQuery.NET.Tests.Shared.Models;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Validation;

/// <summary>
/// End-to-end spec matrix for the unified <c>include</c> relationship model:
/// collection vs single-valued cardinality (resolved from the shared fixture metadata,
/// never from names, record counts, or runtime data), nested levels validated
/// independently, DSL and FQL expression syntax, and relationship-level <c>skip</c>
/// being unsupported while root paging is untouched.
/// </summary>
public class IncludeRelationshipMatrixTests
{
    static IncludeRelationshipMatrixTests() => Fql.Register();

    private static QueryOptions Parse(string include, bool fql = false)
    {
        var parameters = new FlexQueryParameters { Include = include };
        return fql ? parameters.ToQueryOptions(QuerySyntax.Fql) : parameters.ToQueryOptions();
    }

    private static string? TryValidate(QueryOptions options)
    {
        try
        {
            options.ValidateOrThrow<Customer>();
            return null;
        }
        catch (QueryValidationException ex)
        {
            return ex.Message;
        }
    }

    // ---------------------------------------------------------------------
    // Valid — collection relationships
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("orders")]
    [InlineData("orders()")]
    [InlineData("orders(take=5)")]
    [InlineData("orders(filter=status:eq:Active)")]
    [InlineData("orders(sort=orderdate:desc)")]
    [InlineData("orders(take=5;filter=status:eq:Active;sort=orderdate:desc)")]
    [InlineData("addresses")]
    [InlineData("addresses(take=2)")]
    public void ValidCollectionIncludes_Dsl_Pass(string include)
        => TryValidate(Parse(include)).Should().BeNull();

    [Theory]
    [InlineData("orders")]
    [InlineData("orders(take=5)")]
    [InlineData("orders(filter=status = 'active')")]
    [InlineData("orders(sort=orderdate desc)")]
    [InlineData("orders(take=5;filter=status = 'active';sort=orderdate desc)")]
    public void ValidCollectionIncludes_Fql_Pass(string include)
        => TryValidate(Parse(include, fql: true)).Should().BeNull();

    // ---------------------------------------------------------------------
    // Valid — single-valued relationship without collection options
    // ---------------------------------------------------------------------

    [Fact]
    public void SingleValuedInclude_Bare_Passes()
        => TryValidate(Parse("address")).Should().BeNull();

    [Fact]
    public void SingleValuedInclude_EmptyBlock_Passes()
        => TryValidate(Parse("address()")).Should().BeNull();

    // ---------------------------------------------------------------------
    // Invalid — collection operations on single-valued relationships
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("address(take=5)", "The 'take' option can only be used with collection relationships. 'address' is a single-valued relationship.")]
    [InlineData("address(filter=city:eq:Manila)", "The 'filter' option can only be used with collection relationships. 'address' is a single-valued relationship.")]
    [InlineData("address(sort=city:asc)", "The 'sort' option can only be used with collection relationships. 'address' is a single-valued relationship.")]
    public void CollectionOptionOnSingleValued_Dsl_RejectedWithExactMessage(string include, string expectedMessage)
        => TryValidate(Parse(include)).Should().NotBeNull().And.Contain(expectedMessage);

    [Theory]
    [InlineData("address(take=5)", ValidationErrorCodes.IncludeTakeOnReference)]
    [InlineData("address(filter=city = 'Manila')", ValidationErrorCodes.IncludeFilterOnReference)]
    [InlineData("address(sort=city asc)", ValidationErrorCodes.IncludeSortOnReference)]
    public void CollectionOptionOnSingleValued_Fql_RejectedWithCode(string include, string expectedCode)
    {
        var options = Parse(include, fql: true);

        var result = ValidationResult.Success();
        new IncludeCollectionOptionValidationRule().Validate(
            options,
            new QueryContext { TargetType = typeof(Customer) },
            result);

        result.Errors.Should().ContainSingle(e =>
            e.Message.Contains($"'{expectedCode.Split('_')[1].ToLowerInvariant()}' option")
            && e.Field == "address"
            && e.Code == expectedCode);
    }

    [Fact]
    public void CollectionOptionsOnSingleValued_ReportEachOptionIndividually()
    {
        var message = TryValidate(Parse("address(take=5;filter=city:eq:Manila;sort=city:asc)"));

        message.Should().NotBeNull();
        message.Should().Contain("'take' option");
        message.Should().Contain("'filter' option");
        message.Should().Contain("'sort' option");
    }

    // ---------------------------------------------------------------------
    // Unknown relationships are semantic (validation) errors, not parse errors
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("doesNotExist")]
    [InlineData("doesNotExist(take=5)")]
    public void UnknownRelationship_RejectedSemantically(string include)
    {
        // Structurally valid syntax — the schema-aware validator rejects it
        // (parser/validator separation).
        var options = Parse(include);
        options.Includes.Should().NotBeNullOrEmpty();

        TryValidate(options).Should().NotBeNull().And.Contain("Include path 'doesNotExist'");
    }

    [Fact]
    public void UnknownNestedRelationship_RejectedSemantically()
        => TryValidate(Parse("orders(include=nope)"))
            .Should().NotBeNull().And.Contain("orders.nope");

    // ---------------------------------------------------------------------
    // Nested levels — each validated against its own relationship metadata
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("orders(take=5;include=orderitems(take=3))")]
    [InlineData("orders(include=orderitems(take=3))")]
    [InlineData("orders(take=5;filter=status:eq:Active;include=orderitems(take=3))")]
    public void NestedCollectionIncludes_Pass(string include)
        => TryValidate(Parse(include)).Should().BeNull();

    [Fact]
    public void NestedCollectionOptions_FqlWithNestedIncludeKey_Pass()
        => TryValidate(Parse("orders(take=5;include=orderitems(take=3);sort=orderdate desc)", fql: false))
            .Should().BeNull();

    [Fact]
    public void NestedSingleValuedInclude_WithoutOptions_Passes()
        => TryValidate(Parse("orders(include=customer)")).Should().BeNull();

    [Fact]
    public void NestedCollectionOptionOnSingleValued_Rejected()
    {
        var message = TryValidate(Parse("orders(include=customer(take=5))"));
        message.Should().NotBeNull().And.Contain("'take' option").And.Contain("single-valued relationship");
    }

    [Fact]
    public void NestedCollectionOptionOnSingleValued_Rejected_Fql()
    {
        var message = TryValidate(Parse("orders(include=customer(take=5))", fql: true));
        message.Should().NotBeNull().And.Contain("single-valued relationship");
    }

    // ---------------------------------------------------------------------
    // Deep dotted paths behave identically to nested blocks
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("orders.orderitems(take=3)")]
    [InlineData("orders(take=5),orders.orderitems(take=3)")]
    public void DeepDottedIncludes_Pass(string include)
        => TryValidate(Parse(include)).Should().BeNull();

    [Fact]
    public void DeepDottedSingleValuedWithOption_Rejected()
        => TryValidate(Parse("orders.orderitems.product(take=2)"))
            .Should().NotBeNull().And.Contain("single-valued relationship");

    // ---------------------------------------------------------------------
    // Relationship-level skip is not part of the option set (root paging untouched)
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("orders(take=5;skip=10)", false)]
    [InlineData("orders(take=5,skip=10)", false)]
    [InlineData("orders(skip=10)", false)]
    [InlineData("orders(take=5;skip=10)", true)]
    public void RelationshipLevelSkip_IsUnsupported(string include, bool fql)
    {
        var act = () => Parse(include, fql: fql);
        act.Should().Throw<QueryParseException>()
            .WithMessage("*'skip' option is not supported*");
    }

    [Fact]
    public void RootPagingParameters_RemainUnchanged()
    {
        var options = new FlexQueryParameters { Include = "orders", Page = 2, PageSize = 8 }.ToQueryOptions();

        options.Paging.Page.Should().Be(2);
        options.Paging.PageSize.Should().Be(8);
        IncludeTestFactory.PathStrings(options.Includes).Should().Equal("orders");
    }

    [Fact]
    public void InvalidOptionNameInsideInclude_RejectedAtParseTime()
    {
        var act = () => Parse("orders(bogusOption=1)");
        act.Should().Throw<QueryParseException>()
            .WithMessage("*Unexpected include option 'bogusOption'*");
    }

    // ---------------------------------------------------------------------
    // Cardinality is metadata-driven, not name- or data-driven
    // ---------------------------------------------------------------------

    [Fact]
    public void CardinalityIsSchemaDriven_NotNameDriven()
    {
        // "addresses" is plural and a collection; "address" is its singular sibling
        // and single-valued — opposite naming intuition to what a name heuristic
        // would assume for e.g. "police". Validation follows the types:
        TryValidate(Parse("addresses(take=2)")).Should().BeNull();
        TryValidate(Parse("address(take=2)")).Should().NotBeNull();
    }

    [Fact]
    public void CollectionStaysCollectionWithNoOrOneRows()
    {
        // Validation happens with no database access at all: a collection is a
        // collection even when zero rows exist.
        TryValidate(Parse("orders(take=5)")).Should().BeNull();
    }
}
