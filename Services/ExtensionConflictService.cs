using System;
using System.Collections.Generic;

namespace XTimelineViewer.Services;

/// <summary>
/// 拡張機能を同じページへ二重に読み込まないための名前管理を行う。
/// </summary>
internal static class ExtensionConflictService
{
    /// <summary>
    /// manifest.json の表示名を、重複確認に使える形へそろえる。
    /// 大文字・小文字と余分な空白の違いは同じ拡張機能として扱う。
    /// </summary>
    public static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var words = name.Trim().Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words).ToUpperInvariant();
    }

    public static bool IsNameClaimed(
        ISet<string> claimedNames,
        string? name)
    {
        ArgumentNullException.ThrowIfNull(claimedNames);
        var identity = NormalizeName(name);
        return identity.Length > 0 && claimedNames.Contains(identity);
    }

    public static void ClaimName(
        ISet<string> claimedNames,
        string? name)
    {
        ArgumentNullException.ThrowIfNull(claimedNames);
        var identity = NormalizeName(name);
        if (identity.Length > 0) claimedNames.Add(identity);
    }
}
