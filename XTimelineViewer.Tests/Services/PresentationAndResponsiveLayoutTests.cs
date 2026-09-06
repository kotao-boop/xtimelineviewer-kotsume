using System;
using System.Collections.Generic;
using XTimelineViewer.Models;
using XTimelineViewer.Services;
using Xunit;

namespace XTimelineViewer.Tests.Services;

public sealed class PresentationAndResponsiveLayoutTests
{
    [Fact]
    public void PresentationState_DisplaysVisiblePanesByDefaultAndHonorsConfigVisibility()
    {
        var state = new TimelinePresentationState<object>();
        var pane = new object();
        var visible = new TimelineConfig { IsVisible = true };
        var hiddenByConfig = new TimelineConfig { IsVisible = false };

        Assert.True(state.IsDisplayed(pane, visible));
        Assert.False(state.IsDisplayed(pane, hiddenByConfig));
    }

    [Fact]
    public void PresentationState_FocusDisplaysOnlyFocusedPane()
    {
        var state = new TimelinePresentationState<object>
        {
            FocusActive = true,
            FocusedPane = new object(),
        };
        var otherPane = new object();
        var config = new TimelineConfig();

        Assert.True(state.IsDisplayed(state.FocusedPane!, config));
        Assert.False(state.IsDisplayed(otherPane, config));
    }

    [Fact]
    public void PresentationState_EnlargedMediaPaneTakesPriorityOverFocusAndHiddenWins()
    {
        var focusedPane = new object();
        var enlargedPane = new object();
        var state = new TimelinePresentationState<object>
        {
            FocusActive = true,
            FocusedPane = focusedPane,
            EnlargedPane = enlargedPane,
        };
        var focusedConfig = new TimelineConfig();
        var enlargedConfig = new TimelineConfig();

        Assert.False(state.IsDisplayed(focusedPane, focusedConfig));
        Assert.True(state.IsDisplayed(enlargedPane, enlargedConfig));

        state.Hidden.Add(enlargedConfig);
        Assert.False(state.IsDisplayed(enlargedPane, enlargedConfig));
    }

    [Fact]
    public void PresentationState_ResetClearsTemporaryPresentationState()
    {
        var state = new TimelinePresentationState<object>
        {
            FocusedPane = new object(),
            EnlargedPane = new object(),
            FocusActive = true,
            ModeBeforeFocus = "Grid2x2",
        };
        state.Hidden.Add(new TimelineConfig());

        state.Reset();

        Assert.Null(state.FocusedPane);
        Assert.Null(state.EnlargedPane);
        Assert.False(state.FocusActive);
        Assert.Null(state.ModeBeforeFocus);
        Assert.Empty(state.Hidden);
    }

    [Theory]
    [InlineData(320, 1000, true)]
    [InlineData(1000, 320, false)]
    [InlineData(1920, 1080, false)]
    [InlineData(1080, 1920, true)]
    [InlineData(800, 800, false)]
    public void LayoutPlanner_ViewportPlansOneToFiftyPanesWithoutOverlappingCells(
        double width, double height, bool portrait)
    {
        for (var count = 1; count <= 50; count++)
        {
            var plan = LayoutPlanner.GetAutoGrid(count, width, height);

            Assert.True(plan.Rows > 0);
            Assert.True(plan.Columns > 0);
            Assert.True(plan.Rows * plan.Columns >= count);

            var occupied = new HashSet<(int Row, int Column)>();
            for (var index = 0; index < count; index++)
            {
                var row = index / plan.Columns;
                var column = index % plan.Columns;
                var span = LayoutPlanner.GetColumnSpan(index, count, plan.Columns);

                Assert.InRange(row, 0, plan.Rows - 1);
                Assert.InRange(column, 0, plan.Columns - 1);
                Assert.InRange(span, 1, plan.Columns - column);
                for (var offset = 0; offset < span; offset++)
                    Assert.True(occupied.Add((row, column + offset)),
                        $"count={count}, pane={index} overlaps row {row}, column {column + offset}");

                if (index == count - 1)
                    Assert.Equal(plan.Columns, column + span);
            }

            if (count > 1 && width != height)
            {
                if (portrait)
                    Assert.True(plan.Rows >= plan.Columns, $"Portrait count={count} produced {plan.Rows}x{plan.Columns}");
                else
                    Assert.True(plan.Columns >= plan.Rows, $"Landscape count={count} produced {plan.Rows}x{plan.Columns}");
            }
        }
    }

    [Fact]
    public void LayoutPlanner_ResizePairOnlyChangesBoundaryPairAndPreservesTotal()
    {
        var sizes = new[] { 100d, 200d, 300d, 400d };

        var resized = LayoutPlanner.ResizePair(sizes, boundary: 1, delta: 75, minimum: 50);

        Assert.Equal(100, resized[0]);
        Assert.Equal(275, resized[1]);
        Assert.Equal(225, resized[2]);
        Assert.Equal(400, resized[3]);
        Assert.Equal(sizes[1] + sizes[2], resized[1] + resized[2]);
        Assert.Equal(sizes, new[] { 100d, 200d, 300d, 400d });
    }

    [Fact]
    public void LayoutPlanner_ResizePairClampsBothSidesToMinimumAndOneThirdLimit()
    {
        var sizes = new[] { 100d, 200d };

        var tooLarge = LayoutPlanner.ResizePair(sizes, boundary: 0, delta: 10_000, minimum: 80);
        var tooSmall = LayoutPlanner.ResizePair(sizes, boundary: 0, delta: -10_000, minimum: 80);
        var oversizedMinimum = LayoutPlanner.ResizePair(sizes, boundary: 0, delta: 10_000, minimum: 500);

        Assert.Equal(new[] { 220d, 80d }, tooLarge);
        Assert.Equal(new[] { 80d, 220d }, tooSmall);
        Assert.Equal(new[] { 200d, 100d }, oversizedMinimum);
    }

    [Fact]
    public void LayoutPlanner_ResizePairInvalidBoundaryOrDeltaLeavesInputUnchanged()
    {
        var sizes = new[] { 100d, 200d, 300d };

        Assert.Equal(sizes, LayoutPlanner.ResizePair(sizes, boundary: -1, delta: 10, minimum: 20));
        Assert.Equal(sizes, LayoutPlanner.ResizePair(sizes, boundary: 2, delta: 10, minimum: 20));
        Assert.Equal(sizes, LayoutPlanner.ResizePair(sizes, boundary: 1, delta: double.NaN, minimum: 20));
        Assert.Equal(sizes, LayoutPlanner.ResizePair(sizes, boundary: 1, delta: double.PositiveInfinity, minimum: 20));
    }
}
