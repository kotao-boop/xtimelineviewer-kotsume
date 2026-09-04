namespace XTimelineViewer.Models
{
    /// <summary>
    /// 設定ウィンドウを閉じるときに、現在の設定を保存してよいかを管理する。
    /// バックアップ復元後は、ウィンドウが保持している復元前の設定で
    /// settings.json を上書きしないよう、保存を明示的に止める。
    /// </summary>
    internal sealed class SettingsWindowCloseState
    {
        internal bool ShouldSaveSettingsOnClose { get; private set; } = true;

        internal void SuppressSettingsSaveAfterRestore()
            => ShouldSaveSettingsOnClose = false;
    }
}
