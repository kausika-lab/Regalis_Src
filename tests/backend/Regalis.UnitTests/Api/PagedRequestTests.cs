using FluentAssertions;
using Regalis.Shared.Api;

namespace Regalis.UnitTests.Api;

public class PagedRequestTests
{
    [Fact]
    public void PageSize_IsCappedAtOneHundred()
    {
        var request = new PagedRequest { PageSize = 5000 };
        request.PageSize.Should().Be(PagedRequest.MaxPageSize);
    }

    [Fact]
    public void InvalidPageAndSize_FallBackToDefaults()
    {
        var request = new PagedRequest { Page = -3, PageSize = 0 };
        request.Page.Should().Be(1);
        request.PageSize.Should().Be(PagedRequest.DefaultPageSize);
    }

    [Fact]
    public void SortDir_DefaultsToDescending()
    {
        new PagedRequest().IsDescending.Should().BeTrue();
        new PagedRequest { SortDir = "asc" }.IsDescending.Should().BeFalse();
    }

    [Fact]
    public void PagedResult_ComputesPagingMetadata()
    {
        var result = PagedResult<int>.Create([1, 2, 3], page: 2, pageSize: 3, totalCount: 7);

        result.TotalPages.Should().Be(3);
        result.HasPrevious.Should().BeTrue();
        result.HasNext.Should().BeTrue();
    }
}
