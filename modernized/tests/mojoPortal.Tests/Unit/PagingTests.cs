using mojoPortal.Business;
using Xunit;

namespace mojoPortal.Tests.Unit;

/// <summary>
/// Source-derived assertions (DBLinks.GetPage / mp_Links_SelectPage):
/// legacy parity characterized from source, not verified against a live
/// WebForms instance.
/// </summary>
public sealed class PagingTests
{
    [Theory]
    [InlineData(0, 10, 1)]
    [InlineData(5, 10, 1)]
    [InlineData(10, 10, 1)]   // totalRows <= pageSize -> 1
    [InlineData(11, 10, 2)]   // remainder -> +1
    [InlineData(20, 10, 2)]   // exact multiple
    [InlineData(25, 10, 3)]
    [InlineData(30, -4, 2)]   // negative pageSize: DivRem(30,-4) rem 2 -> 1+1
    public void ComputeTotalPages_matches_source_math(int totalRows, int pageSize, int expected)
        => Assert.Equal(expected, LinkPaging.ComputeTotalPages(totalRows, pageSize));

    [Fact]
    public void PageSize_zero_with_rows_throws_like_source()
    {
        // Source Math.DivRem(12, 0) -> DivideByZeroException; the port keeps it.
        Assert.Throws<DivideByZeroException>(() => LinkPaging.ComputeTotalPages(12, 0));
    }

    [Theory]
    [InlineData(1, 10, 0, 10)]   // page 1: IndexID 1..10
    [InlineData(2, 10, 10, 10)]  // page 2: IndexID 11..20
    [InlineData(3, 5, 10, 5)]
    [InlineData(0, 10, 0, 0)]    // page 0: lower=-10, upper=1 -> empty
    [InlineData(-1, 10, 0, 0)]   // negative page: empty, as source bounds yield
    [InlineData(2, 0, 0, 0)]     // degenerate pageSize 0
    public void ComputeWindow_matches_IndexID_bounds(int pageNumber, int pageSize, int skip, int take)
    {
        var (s, t) = LinkPaging.ComputeWindow(pageNumber, pageSize);
        Assert.Equal(skip, s);
        Assert.Equal(take, t);
    }
}
