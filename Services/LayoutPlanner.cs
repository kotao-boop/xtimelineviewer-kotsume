using System;

namespace XTimelineViewer.Services
{
    /// <summary>
    /// 表示中のタイムライン数から、無理のない自動整列の行数・列数を決める。
    /// UI から切り離しておくことで、並び方を単体テストできるようにする。
    /// </summary>
    internal static class LayoutPlanner
    {
        internal readonly record struct GridPlan(int Rows, int Columns);

        internal static GridPlan GetAutoGrid(int visibleCount)
        {
            if (visibleCount <= 1) return new GridPlan(1, 1);
            if (visibleCount == 2) return new GridPlan(1, 2);
            if (visibleCount <= 4) return new GridPlan(2, 2);
            if (visibleCount <= 6) return new GridPlan(2, 3);
            if (visibleCount <= 9) return new GridPlan(3, 3);

            var columns = (int)Math.Ceiling(Math.Sqrt(visibleCount));
            var rows = (int)Math.Ceiling((double)visibleCount / columns);
            return new GridPlan(rows, columns);
        }
    }
}
