using System;
using System.Linq;

namespace XTimelineViewer.Services
{
    /// <summary>
    /// UI スレッドの未処理例外から、壊れた状態のまま継続してもよいものを選別する。
    /// 予期しない例外を追加する「ブラックリスト」ではなく、安全と言い切れるものだけを
    /// 指定する「ホワイトリスト」として保つ。
    /// </summary>
    internal static class UiExceptionPolicy
    {
        internal static bool CanContinue(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            if (exception is OperationCanceledException)
                return true;

            // WhenAll 等が複数のキャンセルをまとめた場合に限り継続可。
            // 1 件でも未知の例外が混じる場合は継続しない。
            if (exception is not AggregateException aggregate)
                return false;

            var innerExceptions = aggregate.Flatten().InnerExceptions;
            return innerExceptions.Count > 0 &&
                   innerExceptions.All(inner => inner is OperationCanceledException);
        }
    }
}
