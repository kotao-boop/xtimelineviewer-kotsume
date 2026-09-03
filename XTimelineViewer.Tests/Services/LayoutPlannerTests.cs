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

    [Theory]
    [InlineData("Grid2x2", 4, "Grid2x2")]
    [InlineData("Grid2x2", 5, "Auto")]
    [InlineData("Grid2x3", 6, "Grid2x3")]
    [InlineData("Grid2x3", 7, "Auto")]
    [InlineData("VerticalSplit", 2, "VerticalSplit")]
    [InlineData("VerticalSplit", 3, "Auto")]
    [InlineData("Focus", 12, "Focus")]
    [InlineData("unknown", 2, "Classic")]
    public void GetSafeMode_NeverOverfillsFixedTemplates(string requested, int visibleCount, string expected)
        => Assert.Equal(expected, LayoutPlanner.GetSafeMode(requested, visibleCount));

    [Theory]
    [InlineData(640, 500, 2)]
    [InlineData(900, 700, 4)]
    [InlineData(1200, 700, 6)]
    [InlineData(1920, 1080, 9)]
    [InlineData(0, 0, 6)]
    public void GetAutoPageCapacity_KeepsPanesUsable(double width, double height, int expected)
        => Assert.Equal(expected, LayoutPlanner.GetAutoPageCapacity(width, height));
}
