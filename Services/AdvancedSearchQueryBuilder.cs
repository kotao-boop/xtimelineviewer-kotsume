using System;
using System.Collections.Generic;
using System.Linq;

namespace XTimelineViewer.Services
{
    /// <summary>
    /// 入力フォームから X の検索演算子を組み立てる純粋ロジック。
    /// 認証情報や投稿内容には触れず、公式検索ページへ渡す文字列だけを生成する。
    /// </summary>
    internal static class AdvancedSearchQueryBuilder
    {
        internal sealed record Options(
            string AllWords = "",
            string ExactPhrase = "",
            string AnyWords = "",
            string ExcludedWords = "",
            string FromAccount = "",
            string ToAccount = "",
            string Language = "",
            bool ImagesOnly = false,
            bool VideosOnly = false,
            bool ExcludeReplies = false,
            DateTimeOffset? Since = null,
            DateTimeOffset? Until = null);

        internal static string Build(Options options)
        {
            var parts = new List<string>();

            AddWords(parts, options.AllWords);
            AddQuoted(parts, options.ExactPhrase);

            var any = SplitWords(options.AnyWords).ToArray();
            if (any.Length == 1) parts.Add(any[0]);
            else if (any.Length > 1) parts.Add("(" + string.Join(" OR ", any) + ")");

            foreach (var word in SplitWords(options.ExcludedWords)) parts.Add("-" + word);
            AddAccount(parts, "from", options.FromAccount);
            AddAccount(parts, "to", options.ToAccount);

            var language = options.Language.Trim();
            if (language.Length > 0) parts.Add("lang:" + language);
            if (options.ImagesOnly) parts.Add("filter:images");
            if (options.VideosOnly) parts.Add("filter:videos");
            if (options.ExcludeReplies) parts.Add("-filter:replies");
            if (options.Since is { } since) parts.Add($"since:{since:yyyy-MM-dd}");
            // X の until: は指定日を含まない。画面上の「終了日」は利用者の期待どおり
            // その日を含めるため、検索演算子には翌日を渡す。
            if (options.Until is { } until) parts.Add($"until:{until.AddDays(1):yyyy-MM-dd}");

            return string.Join(" ", parts);
        }

        private static void AddWords(ICollection<string> parts, string value)
        {
            foreach (var word in SplitWords(value)) parts.Add(word);
        }

        private static void AddQuoted(ICollection<string> parts, string value)
        {
            var phrase = value.Trim().Replace("\"", "", StringComparison.Ordinal);
            if (phrase.Length > 0) parts.Add($"\"{phrase}\"");
        }

        private static void AddAccount(ICollection<string> parts, string prefix, string value)
        {
            var account = value.Trim().TrimStart('@');
            if (account.Length > 0) parts.Add($"{prefix}:{account}");
        }

        private static IEnumerable<string> SplitWords(string value)
            => value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(w => w.Replace("\"", "", StringComparison.Ordinal))
                .Where(w => w.Length > 0);
    }
}
