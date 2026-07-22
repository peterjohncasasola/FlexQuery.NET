using FlexQuery.NET;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Tests.Integration;

public class QueryOptionsNormalizeTests
{
    [Fact]
    public void Normalize_PreservesExpandSortAndTake()
    {
        var options = new QueryOptions
        {
            Expand =
            [
                new IncludeNode
                {
                    Path = "Orders",
                    Sort = [new SortNode { Field = "OrderDate", Descending = true }],
                    Take = 3,
                    Children =
                    [
                        new IncludeNode
                        {
                            Path = "OrderItems",
                            Sort = [new SortNode { Field = "Id", Descending = true }],
                            Take = 2
                        }
                    ]
                }
            ]
        };

        var normalized = options.Normalize();

        normalized.Expand.Should().ContainSingle();
        normalized.Expand![0].Sort.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new SortNode { Field = "OrderDate", Descending = true });
        normalized.Expand[0].Take.Should().Be(3);
        normalized.Expand[0].Children.Should().ContainSingle();
        normalized.Expand[0].Children[0].Sort.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new SortNode { Field = "Id", Descending = true });
        normalized.Expand[0].Children[0].Take.Should().Be(2);
    }
}
