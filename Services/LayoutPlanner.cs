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

        internal static GridPlan GetAutoGrid(int visibleCount, double availableWidth, double availableHeight)
        {
            if (visibleCount <= 0) return new GridPlan(1, 1);
            if (!double.IsFinite(availableWidth) || availableWidth <= 0) availableWidth = 1200;
            if (!double.IsFinite(availableHeight) || availableHeight <= 0) availableHeight = 700;

            // 全ペインを画面内へ配置する。列数を固定せず、画面比率と本数から選ぶ。
            var maxColumns = Math.Max(1, visibleCount);
            var best = new GridPlan((int)Math.Ceiling((double)visibleCount / maxColumns), maxColumns);
            var bestScore = double.PositiveInfinity;
            for (var columns = 1; columns <= maxColumns; columns++)
            {
                var rows = (int)Math.Ceiling((double)visibleCount / columns);
                var emptyCells = rows * columns - visibleCount;
                var cellWidth = availableWidth / columns;
                var cellHeight = availableHeight / rows;
                var aspectPenalty = Math.Abs(Math.Log(Math.Max(0.01, cellWidth / Math.Max(1, cellHeight) / 0.8)));
                var score = emptyCells * 2 + aspectPenalty * 10 + rows * 0.01;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = new GridPlan(rows, columns);
                }
            }
            return best;
        }

        internal static int GetColumnSpan(int index, int count, int columns)
            => index == count - 1 && count % columns != 0 ? columns - count % columns + 1 : 1;

        internal static double[] ResizePair(double[] sizes, int boundary, double delta, double minimum)
        {
            var result = (double[])sizes.Clone();
            if (boundary < 0 || boundary + 1 >= sizes.Length) return result;
            var total = sizes[boundary] + sizes[boundary + 1];
            if (!double.IsFinite(total) || total <= 0 || !double.IsFinite(delta)) return result;
            minimum = Math.Min(Math.Max(0, minimum), total / 3);
            result[boundary] = Math.Clamp(sizes[boundary] + delta, minimum, total - minimum);
            result[boundary + 1] = total - result[boundary];
            return result;
        }

        /// <summary>
        /// 小さいウィンドウへ列を詰め込みすぎないため、1ページに表示できる列数を求める。
        /// 横は最大3列、縦は最大3行とし、各列の操作領域を保つ。
        /// </summary>
        internal static int GetAutoPageCapacity(double availableWidth, double availableHeight)
        {
            if (!double.IsFinite(availableWidth) || availableWidth <= 0) availableWidth = 1200;
            if (!double.IsFinite(availableHeight) || availableHeight <= 0) availableHeight = 700;
            var columns = Math.Clamp((int)(availableWidth / 320), 1, 3);
            var rows = Math.Clamp((int)(availableHeight / 280), 1, 3);
            return columns * rows;
        }

        /// <summary>
        /// 固定テンプレートのマス数を超えた場合は Auto へ退避する。
        /// 同じ Grid セルへ複数のタイムラインを重ねて見失うことを防ぐ。
        /// Focus は選択中の1本だけを表示する一時モードなので本数制限はない。
        /// </summary>
        internal static string GetSafeMode(string? requestedMode, int visibleCount)
        {
            var mode = requestedMode switch
            {
                "Auto" or "Grid2x2" or "Grid2x3" or "VerticalSplit" or "Focus" => requestedMode,
                _ => "Classic",
            };

            return mode switch
            {
                "Grid2x2" when visibleCount > 4 => "Auto",
                "Grid2x3" when visibleCount > 6 => "Auto",
                "VerticalSplit" when visibleCount > 2 => "Auto",
                _ => mode,
            };
        }
    }
}
