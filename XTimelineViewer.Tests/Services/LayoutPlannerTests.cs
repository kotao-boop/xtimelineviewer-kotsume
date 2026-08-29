using XTimelineViewer.Services;
using Xunit;

namespace XTimelineViewer.Tests.Services;

public class LayoutPlannerTests
{
    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 1, 1)]
    [InlineData(2, 1, 2)]
    [InlineData(3, 2, 2)]
    [InlineData(4, 2, 2)]
    [InlineData(5, 2, 3)]
    [InlineData(6, 2, 3)]
    [InlineData(7, 3, 3)]
    [InlineData(9, 3, 3)]
    [InlineData(10, 3, 4)]
    public void GetAutoGrid_ReturnsCompactGrid(int visibleCount, int expectedRows, int expectedColumns)
    {
        var plan = LayoutPlanner.GetAutoGrid(visibleCount);

        Assert.Equal(expectedRows, plan.Rows);
        Assert.Equal(expectedColumns, plan.Columns);
    }
}
