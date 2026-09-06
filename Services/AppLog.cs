using System;
using System.IO;

namespace XTimelineViewer.Services
{
    /// <summary>
    /// error.log への追記を一手に引き受ける（#374）。
    ///
    /// 以前は同じファイルに書く実装が 2 つあり、パスの組み立ても書式も別々だった
    /// （App.LogUnhandledException と MainWindow.LogError）。App は MainWindow より
    /// 先に走るため分かれていたが、書式が揃わないので統合した。
    ///
    /// UI 依存なし。ローテーションはパスとサイズを引数で受け取り、単体でテストできる。
    /// </summary>
    internal static class AppLog
    {
        /// <summary>この大きさを超えたら世代交代する。</summary>
        internal const long DefaultMaxBytes = 1_000_000;

        private static readonly object Gate = new();
        private static string _filePath = DefaultFilePath();
        private static long   _maxBytes = DefaultMaxBytes;

        internal static string FilePath => _filePath;

        /// <summary>
        /// 既定の保存先。パッケージ版でもここに置く（従来の場所を変えると
        /// 過去のログが取り残されるため）。
        /// </summary>
        internal static string DefaultFilePath() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XTimelineViewer", "error.log");

        /// <summary>
        /// 起動時に 1 回呼ぶ。保存先を決め、必要なら世代交代する。
        /// </summary>
        internal static void Initialize(string? filePath = null, long maxBytes = DefaultMaxBytes)
        {
            lock (Gate)
            {
                _filePath = filePath ?? DefaultFilePath();
                _maxBytes = maxBytes;
            }
            RotateIfNeeded(_filePath, _maxBytes);
        }

        /// <summary>例外を記録する。</summary>
        internal static void Error(string context, Exception ex)
            => Append($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");

        /// <summary>診断用の 1 行を記録する。</summary>
        internal static void Debug(string message)
            => Append($"[{DateTime.Now:HH:mm:ss}] DBG {message}{Environment.NewLine}");

        private static void Append(string text)
        {
            // 1件の異常に長い例外でログ上限を飛び越えないようにする。
            if (text.Length > 8192) text = text[..8192] + " [truncated]" + Environment.NewLine;
            lock (Gate)
            {
                // ローテーションと追記を同じロックで守り、長時間動作でも上限を保つ。
                RotateIfNeeded(_filePath, _maxBytes);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
                    File.AppendAllText(_filePath, text);
                }
                catch { /* ログ書き込みの失敗で本来の処理を中断しない */ }
            }
        }

        /// <summary>
        /// ファイルが上限を超えていたら 1 世代だけ退避する。
        /// error.log → error.log.1（既にあれば上書き）。
        ///
        /// 世代を増やさないのは、これが調査用の一時ログで、
        /// 長期保存する必要が無いため。
        /// </summary>
        internal static void RotateIfNeeded(string filePath, long maxBytes)
        {
            try
            {
                var info = new FileInfo(filePath);
                if (!info.Exists || info.Length < maxBytes) return;
                File.Move(filePath, filePath + ".1", overwrite: true);
            }
            catch { /* 退避できなくても追記は続ける */ }
        }
    }
}
