namespace XTimelineViewer.Models
{
    internal class TimelineConfig
    {
        public string Name               { get; set; } = "";
        public string Url                { get; set; } = "";
        public double Width              { get; set; } = 350;
        public bool   HideSidebar       { get; set; } = false;
        public bool   HideCompose       { get; set; } = true;
        public bool   HideListHeader    { get; set; } = false;
        public bool   HardReloadEnabled { get; set; } = false;
        public int    HardReloadInterval{ get; set; } = 3;
        public string ProfileId         { get; set; } = "default";
        public bool   IsVisible          { get; set; } = true;

        /// <summary>
        /// 「リスト」メニューから追加した、アクティブアカウントのリスト一覧を追跡するタイムライン。
        /// X の委任アカウントを考慮し、ハンドルは作成時のキャッシュではなくペイン読み込みごとに
        /// ライブ取得して URL（https://x.com/&lt;active&gt;/lists）を解決する (#211)。
        /// </summary>
        public bool   IsListsIndex      { get; set; } = false;

        public TimelineConfig Clone() => new()
        {
            Name               = Name,
            Url                = Url,
            Width              = Width,
            HideSidebar        = HideSidebar,
            HideCompose        = HideCompose,
            HideListHeader     = HideListHeader,
            HardReloadEnabled  = HardReloadEnabled,
            HardReloadInterval = HardReloadInterval,
            ProfileId          = ProfileId,
            IsVisible          = IsVisible,
            IsListsIndex       = IsListsIndex,
        };
    }
}
