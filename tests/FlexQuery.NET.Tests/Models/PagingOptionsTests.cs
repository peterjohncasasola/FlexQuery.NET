using FlexQuery.NET.Models.Paging;

namespace FlexQuery.NET.Tests.Models;

public class PagingOptionsTests
{
    [Fact]
    public void DefaultConstructor_SetsPageToOne()
    {
        var paging = new PagingOptions();
        paging.Page.Should().Be(1);
    }

    [Fact]
    public void DefaultConstructor_SetsPageSizeToTwenty()
    {
        var paging = new PagingOptions();
        paging.PageSize.Should().Be(20);
    }

    [Fact]
    public void DefaultConstructor_DisabledIsFalse()
    {
        var paging = new PagingOptions();
        paging.Disabled.Should().BeFalse();
    }

    [Fact]
    public void Page_ClampsToMinimumOfOne()
    {
        var paging = new PagingOptions { Page = 0 };
        paging.Page.Should().Be(1);

        paging.Page = -5;
        paging.Page.Should().Be(1);
    }

    [Fact]
    public void PageSize_ClampsToMinimumOfOne()
    {
        var paging = new PagingOptions { PageSize = 0 };
        paging.PageSize.Should().Be(1);

        paging.PageSize = -1;
        paging.PageSize.Should().Be(1);
    }

    [Fact]
    public void PageSize_ClampsToMaximumOfOneThousand()
    {
        var paging = new PagingOptions { PageSize = 1001 };
        paging.PageSize.Should().Be(1000);

        paging.PageSize = 5000;
        paging.PageSize.Should().Be(1000);
    }

    [Fact]
    public void Skip_ComputesCorrectly()
    {
        var paging = new PagingOptions { Page = 3, PageSize = 10 };
        paging.Skip.Should().Be(20);
    }

    [Fact]
    public void Skip_WhenPageIsOne_ReturnsZero()
    {
        var paging = new PagingOptions { Page = 1, PageSize = 20 };
        paging.Skip.Should().Be(0);
    }

    [Fact]
    public void Disabled_WhenTrue_SkipIsStillValid()
    {
        var paging = new PagingOptions { Page = 2, PageSize = 10, Disabled = true };
        paging.Skip.Should().Be(10);
    }
}
