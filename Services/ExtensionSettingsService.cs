using System;
using System.IO;
using System.Linq;
using XTimelineViewer.Models;

namespace XTimelineViewer.Services;

/// <summary>
/// 拡張機能の有効・無効設定を扱う、UIに依存しない小さなサービス。
/// </summary>
internal static class ExtensionSettingsService
{
    /// <summary>
    /// 拡張機能のフォルダー名を使った保存キーを作る。
    /// 同じ名前の拡張機能が同梱と利用者追加の両方にあっても区別できるよう、
    /// 保存元の種類を先頭に付ける。
    /// </summary>
    public static string GetKey(string directoryPath, bool isUserAdded)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("拡張機能のフォルダーが指定されていません。", nameof(directoryPath));

        var normalized = directoryPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var folderName = Path.GetFileName(normalized);
        if (string.IsNullOrWhiteSpace(folderName)) folderName = normalized;

        return $"{(isUserAdded ? "user" : "bundled")}:{folderName}";
    }

    public static bool IsEnabled(
        AppSettings settings,
        string directoryPath,
        bool isUserAdded)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var key = GetKey(directoryPath, isUserAdded);
        return !(settings.DisabledExtensionKeys ?? []).Contains(
            key,
            StringComparer.OrdinalIgnoreCase);
    }

    public static void SetEnabled(
        AppSettings settings,
        string directoryPath,
        bool isUserAdded,
        bool enabled)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var key = GetKey(directoryPath, isUserAdded);
        settings.DisabledExtensionKeys ??= [];
        settings.DisabledExtensionKeys.RemoveAll(
            value => string.Equals(value, key, StringComparison.OrdinalIgnoreCase));
        if (!enabled) settings.DisabledExtensionKeys.Add(key);
    }
}
