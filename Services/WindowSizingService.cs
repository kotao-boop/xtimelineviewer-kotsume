using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace XTimelineViewer.Services;

/// <summary>
/// WinUI の AppWindow は物理ピクセルでサイズを受け取るため、Windows の表示倍率を
/// 考慮して「画面上で見える大きさ」と相互変換する。
/// </summary>
internal static class WindowSizingService
{
    private const double BaseDpi = 96.0;

    internal readonly record struct LogicalSize(double Width, double Height);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    internal static double GetScale(Window window)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var dpi = GetDpiForWindow(hwnd);
            return dpi >= BaseDpi ? dpi / BaseDpi : 1.0;
        }
        catch
        {
            return 1.0;
        }
    }

    internal static LogicalSize GetLogicalSize(Window window)
    {
        var scale = GetScale(window);
        return new LogicalSize(window.AppWindow.Size.Width / scale, window.AppWindow.Size.Height / scale);
    }

    internal static void ResizeAndCenter(
        Window window,
        double preferredLogicalWidth,
        double preferredLogicalHeight,
        double minimumLogicalWidth,
        double minimumLogicalHeight,
        double marginLogical = 32)
    {
        var scale = GetScale(window);
        var preferredWidth = ToPhysical(preferredLogicalWidth, scale);
        var preferredHeight = ToPhysical(preferredLogicalHeight, scale);
        var minimumWidth = ToPhysical(minimumLogicalWidth, scale);
        var minimumHeight = ToPhysical(minimumLogicalHeight, scale);
        var margin = ToPhysical(marginLogical, scale);

        var displayArea = DisplayArea.GetFromWindowId(window.AppWindow.Id, DisplayAreaFallback.Primary);
        if (displayArea is null)
        {
            window.AppWindow.Resize(new SizeInt32(
                Math.Max(minimumWidth, preferredWidth),
                Math.Max(minimumHeight, preferredHeight)));
            return;
        }

        var workArea = displayArea.WorkArea;
        var availableWidth = Math.Max(1, workArea.Width - margin * 2);
        var availableHeight = Math.Max(1, workArea.Height - margin * 2);
        var width = Math.Min(Math.Max(minimumWidth, preferredWidth), availableWidth);
        var height = Math.Min(Math.Max(minimumHeight, preferredHeight), availableHeight);
        var x = workArea.X + Math.Max(0, (workArea.Width - width) / 2);
        var y = workArea.Y + Math.Max(0, (workArea.Height - height) / 2);

        window.AppWindow.Resize(new SizeInt32(width, height));
        window.AppWindow.Move(new PointInt32(x, y));
    }

    /// <summary>ユーザーが最小サイズより小さくしたときだけ、倍率を考慮して戻す。</summary>
    internal static bool EnsureMinimumSize(Window window, double minimumLogicalWidth, double minimumLogicalHeight)
    {
        var scale = GetScale(window);
        var minimumWidth = ToPhysical(minimumLogicalWidth, scale);
        var minimumHeight = ToPhysical(minimumLogicalHeight, scale);
        var current = window.AppWindow.Size;
        var width = Math.Max(current.Width, minimumWidth);
        var height = Math.Max(current.Height, minimumHeight);
        if (width == current.Width && height == current.Height) return false;

        var displayArea = DisplayArea.GetFromWindowId(window.AppWindow.Id, DisplayAreaFallback.Primary);
        if (displayArea is not null)
        {
            width = Math.Min(width, displayArea.WorkArea.Width);
            height = Math.Min(height, displayArea.WorkArea.Height);
        }

        window.AppWindow.Resize(new SizeInt32(width, height));
        return true;
    }

    private static int ToPhysical(double logicalPixels, double scale)
        => Math.Max(1, (int)Math.Round(logicalPixels * scale, MidpointRounding.AwayFromZero));
}
