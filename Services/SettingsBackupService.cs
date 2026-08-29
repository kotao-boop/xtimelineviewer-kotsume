using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using XTimelineViewer.Models;

namespace XTimelineViewer.Services
{
    /// <summary>
    /// 利用者が持ち運べる設定バックアップを作成・復元する。
    /// WebView2 のユーザーデータ（Cookie、ログイン状態、キャッシュ）は意図的に対象外。
    /// </summary>
    internal static class SettingsBackupService
    {
        internal const int CurrentFormatVersion = 1;
        private const long MaxEntryBytes = 5 * 1024 * 1024;
        private const long MaxTotalBytes = 15 * 1024 * 1024;

        private static readonly string[] DataFileNames =
        [
            "settings.json",
            "profiles.json",
            "timelines.json",
            "workspaces.json",
        ];

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        internal sealed record BackupManifest(
            int FormatVersion,
            string AppVersion,
            DateTimeOffset CreatedAtUtc,
            bool IncludesLoginSessions,
            IReadOnlyList<string> Files);

        internal sealed record RestoreResult(int RestoredFileCount, string? SafetyBackupPath);

        public static int CreateBackup(string archivePath, string dataFolder, string appVersion)
        {
            if (string.IsNullOrWhiteSpace(archivePath))
                throw new ArgumentException("バックアップ先が指定されていません。", nameof(archivePath));
            if (string.IsNullOrWhiteSpace(dataFolder))
                throw new ArgumentException("設定フォルダーが指定されていません。", nameof(dataFolder));

            var files = DataFileNames
                .Where(name => File.Exists(Path.Combine(dataFolder, name)))
                .ToList();
            if (files.Count == 0)
                throw new InvalidDataException("バックアップできる設定ファイルがありません。");

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(archivePath))!);
            var temporaryPath = archivePath + ".tmp";
            try
            {
                using (var output = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(output, ZipArchiveMode.Create))
                {
                    var manifest = new BackupManifest(
                        CurrentFormatVersion,
                        appVersion,
                        DateTimeOffset.UtcNow,
                        IncludesLoginSessions: false,
                        files);
                    WriteTextEntry(archive, "backup.json", JsonSerializer.Serialize(manifest, JsonOptions));

                    foreach (var name in files)
                    {
                        var sourcePath = Path.Combine(dataFolder, name);
                        var info = new FileInfo(sourcePath);
                        if (info.Length > MaxEntryBytes)
                            throw new InvalidDataException($"{name} がバックアップ可能なサイズを超えています。");

                        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                        using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var destination = entry.Open();
                        source.CopyTo(destination);
                    }
                }

                File.Move(temporaryPath, archivePath, overwrite: true);
                return files.Count;
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        public static RestoreResult RestoreBackup(string archivePath, string dataFolder, string appVersion)
        {
            if (!File.Exists(archivePath))
                throw new FileNotFoundException("バックアップファイルが見つかりません。", archivePath);

            Dictionary<string, byte[]> restoredData;
            using (var input = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var archive = new ZipArchive(input, ZipArchiveMode.Read))
            {
                var duplicate = archive.Entries
                    .GroupBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault(g => g.Count() > 1);
                if (duplicate is not null)
                    throw new InvalidDataException("同じ名前のファイルが重複しているため、復元できません。");

                var manifestEntry = archive.GetEntry("backup.json")
                    ?? throw new InvalidDataException("XTimelineViewer のバックアップ情報がありません。");
                var manifest = ReadManifest(manifestEntry);
                if (manifest.FormatVersion != CurrentFormatVersion)
                    throw new InvalidDataException("このバックアップ形式には、現在のアプリが対応していません。");
                if (manifest.IncludesLoginSessions)
                    throw new InvalidDataException("ログイン情報を含むバックアップは、安全のため復元できません。");
                if (manifest.Files is null || manifest.Files.Count == 0)
                    throw new InvalidDataException("バックアップ対象の一覧がありません。");

                var allowed = new HashSet<string>(DataFileNames, StringComparer.OrdinalIgnoreCase);
                restoredData = [];
                long totalBytes = 0;
                foreach (var name in manifest.Files.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!allowed.Contains(name)) continue;
                    var entry = archive.GetEntry(name)
                        ?? throw new InvalidDataException($"バックアップ内の {name} が見つかりません。");
                    if (entry.Length > MaxEntryBytes || totalBytes + entry.Length > MaxTotalBytes)
                        throw new InvalidDataException("バックアップが大きすぎるため、安全に復元できません。");

                    using var stream = entry.Open();
                    using var buffer = new MemoryStream();
                    stream.CopyTo(buffer);
                    var bytes = buffer.ToArray();
                    totalBytes += bytes.Length;
                    ValidateJson(name, bytes);
                    restoredData[name] = bytes;
                }
            }

            if (restoredData.Count == 0)
                throw new InvalidDataException("復元できる設定がバックアップに含まれていません。");

            Directory.CreateDirectory(dataFolder);
            string? safetyBackupPath = null;
            if (DataFileNames.Any(name => File.Exists(Path.Combine(dataFolder, name))))
            {
                safetyBackupPath = Path.Combine(
                    dataFolder,
                    $"backup-before-restore-{DateTime.Now:yyyyMMdd-HHmmss}.xtvbackup");
                CreateBackup(safetyBackupPath, dataFolder, appVersion);
            }

            var previousData = restoredData.Keys.ToDictionary(
                name => name,
                name =>
                {
                    var path = Path.Combine(dataFolder, name);
                    return File.Exists(path) ? File.ReadAllBytes(path) : null;
                },
                StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (var (name, bytes) in restoredData)
                {
                    var targetPath = Path.Combine(dataFolder, name);
                    var temporaryPath = targetPath + ".restore.tmp";
                    File.WriteAllBytes(temporaryPath, bytes);
                    File.Move(temporaryPath, targetPath, overwrite: true);
                }
            }
            catch
            {
                foreach (var (name, bytes) in previousData)
                {
                    var targetPath = Path.Combine(dataFolder, name);
                    if (bytes is null)
                    {
                        if (File.Exists(targetPath)) File.Delete(targetPath);
                    }
                    else
                    {
                        File.WriteAllBytes(targetPath, bytes);
                    }
                }
                throw;
            }

            return new RestoreResult(restoredData.Count, safetyBackupPath);
        }

        private static BackupManifest ReadManifest(ZipArchiveEntry entry)
        {
            if (entry.Length > 64 * 1024)
                throw new InvalidDataException("バックアップ情報が大きすぎます。");
            using var stream = entry.Open();
            return JsonSerializer.Deserialize<BackupManifest>(stream)
                ?? throw new InvalidDataException("バックアップ情報を読み取れません。");
        }

        private static void ValidateJson(string name, byte[] bytes)
        {
            try
            {
                var value = name switch
                {
                    "settings.json"   => JsonSerializer.Deserialize<AppSettings>(bytes) as object,
                    "profiles.json"   => JsonSerializer.Deserialize<List<ProfileConfig>>(bytes),
                    "timelines.json"  => JsonSerializer.Deserialize<List<TimelineConfig>>(bytes),
                    "workspaces.json" => JsonSerializer.Deserialize<List<WorkspaceConfig>>(bytes),
                    _ => null,
                };
                if (value is null) throw new JsonException();
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"{name} が壊れているため、復元を中止しました。", ex);
            }
        }

        private static void WriteTextEntry(ZipArchive archive, string name, string value)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(value);
        }
    }
}
