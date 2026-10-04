namespace mojoPortal.Business;

/// <summary>
/// Paging arithmetic transcribed from mojoPortal.Data DBLinks.GetPage
/// (mp_Links_GetCount + mp_Links_SelectPage semantics).
/// </summary>
public static class LinkPaging
{
    /// <summary>
    /// Exact port of the source GetPage totalPages computation:
    /// totalPages = 1; if (pageSize > 0) totalPages = totalRows / pageSize;
    /// if (totalRows &lt;= pageSize) totalPages = 1;
    /// else { remainder = totalRows % pageSize; if (remainder > 0) totalPages += 1; }
    /// Edge cases preserved: pageSize &lt;= 0, totalRows == 0.
    /// </summary>
    public static int ComputeTotalPages(int totalRows, int pageSize)
    {
        int totalPages = 1;
        if (pageSize > 0) { totalPages = totalRows / pageSize; }

        if (totalRows <= pageSize)
        {
            totalPages = 1;
        }
        else
        {
            Math.DivRem(totalRows, pageSize, out int remainder);
            if (remainder > 0) { totalPages += 1; }
        }

        return totalPages;
    }

    /// <summary>
    /// The source stored procedure assigns a 1-based IndexID over the
    /// (ViewOrder, Title) ordering and returns rows where
    /// IndexID &gt; pageLowerBound AND IndexID &lt; pageUpperBound with
    /// lower = pageSize*pageNumber - pageSize, upper = lower + pageSize + 1.
    /// This returns the equivalent (skip, take) window over the same ordering;
    /// pageNumber &lt;= 0 yields an empty window exactly as the source does.
    /// </summary>
    public static (int Skip, int Take) ComputeWindow(int pageNumber, int pageSize)
    {
        int lower = pageSize * pageNumber - pageSize;
        int upper = lower + pageSize + 1;
        // Rows satisfy lower < IndexID < upper with 1-based IndexID.
        int firstIndex = Math.Max(1, lower + 1);
        int lastIndex = upper - 1;
        int skip = firstIndex - 1;
        int take = Math.Max(0, lastIndex - firstIndex + 1);
        return (skip, take);
    }
}
