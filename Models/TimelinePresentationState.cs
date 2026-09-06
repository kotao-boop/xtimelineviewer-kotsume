using System.Collections.Generic;

namespace XTimelineViewer.Models;

/// <summary>保存する基本配置と分けて、画面を一時的に変える操作を管理する。</summary>
internal sealed class TimelinePresentationState<TPane> where TPane : class
{
    internal TPane? FocusedPane { get; set; }
    internal TPane? EnlargedPane { get; set; }
    internal bool FocusActive { get; set; }
    internal string? ModeBeforeFocus { get; set; }
    internal HashSet<TimelineConfig> Hidden { get; } = [];

    internal bool IsDisplayed(TPane pane, TimelineConfig config)
        => config.IsVisible && !Hidden.Contains(config)
           && (EnlargedPane is not null ? ReferenceEquals(pane, EnlargedPane)
               : !FocusActive || ReferenceEquals(pane, FocusedPane));

    internal void Reset()
    {
        FocusedPane = null;
        EnlargedPane = null;
        FocusActive = false;
        ModeBeforeFocus = null;
        Hidden.Clear();
    }
}
