using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XTimelineViewer.Models;

namespace XTimelineViewer.Services
{
    /// <summary>
    /// JSON ファイルの読み込み結果。空のデータと読み込み失敗を区別するために使う。
    /// </summary>
    internal enum PersistenceLoadStatus
    {
        Success,
        Missing,
        Corrupt,
        AccessError,
        IoError,
        UnexpectedError,
    }

    /// <summary>
    /// 読み込み結果と、互換 API が返すフォールバック値をまとめたもの。
    /// </summary>
    internal sealed class PersistenceLoadResult<T>
    {
        private PersistenceLoadResult(T value, PersistenceLoadStatus status, Exception? error)
        {
            Value = value;
            Status = status;
            Error = error;
        }

        public T Value { get; }
        public PersistenceLoadStatus Status { get; }
        public Exception? Error { get; }
        public bool IsSuccess => Status == PersistenceLoadStatus.Success;

        public static PersistenceLoadResult<T> Succeeded(T value)
            => new(value, PersistenceLoadStatus.Success, null);

        public static PersistenceLoadResult<T> Failed(
            T fallbackValue,
            PersistenceLoadStatus status,
            Exception? error = null)
            => new(fallbackValue, status, error);
    }

    /// <summary>
    /// バックアップから一次ファイルを復旧した結果。
    /// </summary>
    internal sealed class PersistenceRestoreResult
    {
        private PersistenceRestoreResult(
            bool isSuccess,
            PersistenceLoadStatus status,
            string? archivedPrimaryPath,
            Exception? error)
        {
            IsSuccess = isSuccess;
            Status = status;
            ArchivedPrimaryPath = archivedPrimaryPath;
            Error = error;
        }

        public bool IsSuccess { get; }
        public PersistenceLoadStatus Status { get; }
        public string? ArchivedPrimaryPath { get; }
        public Exception? Error { get; }

        public static PersistenceRestoreResult Succeeded(string? archivedPrimaryPath)
            => new(true, PersistenceLoadStatus.Success, archivedPrimaryPath, null);

        public static PersistenceRestoreResult Failed(
            PersistenceLoadStatus status,
            Exception? error)
            => new(false, status, null, error);
    }

    /// <summary>
    /// 既存の一次ファイルを安全に読めないため、上書きを中止したことを表す。
    /// </summary>
    internal sealed class PersistenceSaveBlockedException : IOException
    {
        public PersistenceSaveBlockedException(
            string filePath,
            PersistenceLoadStatus loadStatus,
            Exception? innerException)
            : base($"既存のデータファイルを安全に確認できないため、上書きを中止しました: {filePath}", innerException)
        {
            LoadStatus = loadStatus;
        }

        public PersistenceLoadStatus LoadStatus { get; }
    }

    /// <summary>
    /// 各 JSON ストアで共有する、結果付き読み込みと安全な置換保存。
    /// </summary>
    internal static class JsonFilePersistence
    {
        public static string GetBackupPath(string filePath) => filePath + ".bak";

        public static PersistenceLoadResult<T> Load<T>(
            string filePath,
            Func<T> fallbackFactory,
            Action<T>? normalize = null)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var value = JsonSerializer.Deserialize<T>(json);
                if (value is null)
                {
                    return PersistenceLoadResult<T>.Failed(
                        fallbackFactory(),
                        PersistenceLoadStatus.Corrupt,
                        new JsonException("JSON のルート値が null です。"));
                }

                normalize?.Invoke(value);
                return PersistenceLoadResult<T>.Succeeded(value);
            }
            catch (FileNotFoundException ex)
            {
                return PersistenceLoadResult<T>.Failed(
                    fallbackFactory(), PersistenceLoadStatus.Missing, ex);
            }
            catch (DirectoryNotFoundException ex)
            {
                return PersistenceLoadResult<T>.Failed(
                    fallbackFactory(), PersistenceLoadStatus.Missing, ex);
            }
            catch (JsonException ex)
            {
                return PersistenceLoadResult<T>.Failed(
                    fallbackFactory(), PersistenceLoadStatus.Corrupt, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                return PersistenceLoadResult<T>.Failed(
                    fallbackFactory(), PersistenceLoadStatus.AccessError, ex);
            }
            catch (SecurityException ex)
            {
                return PersistenceLoadResult<T>.Failed(
                    fallbackFactory(), PersistenceLoadStatus.AccessError, ex);
            }
            catch (IOException ex)
            {
                return PersistenceLoadResult<T>.Failed(
                    fallbackFactory(), PersistenceLoadStatus.IoError, ex);
            }
            catch (Exception ex)
            {
                return PersistenceLoadResult<T>.Failed(
                    fallbackFactory(), PersistenceLoadStatus.UnexpectedError, ex);
            }
        }

        public static PersistenceLoadStatus Classify(Exception error)
            => error switch
            {
                FileNotFoundException => PersistenceLoadStatus.Missing,
                DirectoryNotFoundException => PersistenceLoadStatus.Missing,
                JsonException => PersistenceLoadStatus.Corrupt,
                UnauthorizedAccessException => PersistenceLoadStatus.AccessError,
                SecurityException => PersistenceLoadStatus.AccessError,
                IOException => PersistenceLoadStatus.IoError,
                _ => PersistenceLoadStatus.UnexpectedError,
            };

        /// <summary>
        /// 通常保存の前に一次ファイルが安全に読めたことを確認する。
        /// Missing は初回保存として許可し、それ以外の失敗は .bak を守るため上書きしない。
        /// 戻り値は直前版を .bak に保存すべきかを表す。
        /// </summary>
        public static bool ShouldCreateBackupBeforeSave<T>(
            string filePath,
            PersistenceLoadResult<T> currentResult)
        {
            if (currentResult.IsSuccess) return true;
            if (currentResult.Status == PersistenceLoadStatus.Missing && !File.Exists(filePath))
                return false;

            throw new PersistenceSaveBlockedException(
                filePath,
                currentResult.Status,
                currentResult.Error);
        }

        /// <summary>
        /// JSON を同じフォルダーの一時ファイルへ書き切ってから置き換える。
        /// 既存ファイルがある場合、createBackup=true なら直前版を .bak に残す。
        /// </summary>
        public static void SaveAtomically(string filePath, string json, bool createBackup)
        {
            var fullPath = PrepareDestination(filePath);
            var tempPath = CreateTempPath(fullPath);
            try
            {
                WriteTempFile(tempPath, json);
                ReplacePrimary(tempPath, fullPath, createBackup ? GetBackupPath(fullPath) : null);
            }
            finally
            {
                TryDeleteTempFile(tempPath);
            }
        }

        public static async Task SaveAtomicallyAsync(
            string filePath,
            string json,
            bool createBackup)
        {
            var fullPath = PrepareDestination(filePath);
            var tempPath = CreateTempPath(fullPath);
            try
            {
                await WriteTempFileAsync(tempPath, json).ConfigureAwait(false);
                ReplacePrimary(tempPath, fullPath, createBackup ? GetBackupPath(fullPath) : null);
            }
            finally
            {
                TryDeleteTempFile(tempPath);
            }
        }

        /// <summary>
        /// 型検証済みのバックアップ内容を、同じフォルダーの一時ファイル経由で復旧する。
        /// 一次ファイルがある場合は timestamp 付き .corrupt ファイルとして同時に退避し、
        /// 元の .bak は変更しない。
        /// </summary>
        public static PersistenceRestoreResult RestoreValidatedBackup<T>(
            string filePath,
            PersistenceLoadResult<T> backupResult,
            Func<T, string> serialize)
        {
            if (!backupResult.IsSuccess)
                return PersistenceRestoreResult.Failed(backupResult.Status, backupResult.Error);

            string? archivedPath = null;
            string? tempPath = null;
            try
            {
                var json = serialize(backupResult.Value);
                var fullPath = PrepareDestination(filePath);
                tempPath = CreateTempPath(fullPath);
                WriteTempFile(tempPath, json);

                if (File.Exists(fullPath))
                {
                    archivedPath = CreateRecoveryArchivePath(fullPath);
                    ReplacePrimary(tempPath, fullPath, archivedPath);
                }
                else
                {
                    File.Move(tempPath, fullPath);
                }

                return PersistenceRestoreResult.Succeeded(archivedPath);
            }
            catch (Exception ex)
            {
                return PersistenceRestoreResult.Failed(Classify(ex), ex);
            }
            finally
            {
                if (tempPath is not null) TryDeleteTempFile(tempPath);
            }
        }

        /// <summary>
        /// 復旧の初期化前に、現在の一次ファイルを timestamp 付き .corrupt へ退避する。
        /// ファイルが無ければ何もしない。退避に失敗した場合は例外を投げるため、
        /// 呼び出し側はその後の初期値保存を中止すること。.bak には触れない。
        /// </summary>
        public static string? ArchivePrimaryForRecovery(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("保存先が指定されていません。", nameof(filePath));

            var fullPath = Path.GetFullPath(filePath);
            if (!File.Exists(fullPath)) return null;

            var archivePath = CreateRecoveryArchivePath(fullPath);
            try
            {
                File.Move(fullPath, archivePath);
                return archivePath;
            }
            catch (FileNotFoundException)
            {
                // 存在確認直後に別処理が削除した場合も「退避対象なし」と同じ扱い。
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }

        private static string PrepareDestination(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("保存先が指定されていません。", nameof(filePath));

            var fullPath = Path.GetFullPath(filePath);
            var directory = Path.GetDirectoryName(fullPath)
                ?? throw new ArgumentException("保存先フォルダーを取得できません。", nameof(filePath));
            Directory.CreateDirectory(directory);
            return fullPath;
        }

        private static string CreateTempPath(string fullPath)
            => fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        private static string CreateRecoveryArchivePath(string fullPath)
            => fullPath
               + ".corrupt-"
               + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture)
               + "-"
               + Guid.NewGuid().ToString("N")[..8];

        private static void WriteTempFile(string tempPath, string contents)
        {
            using var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough);
            using var writer = new StreamWriter(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 4096,
                leaveOpen: true);
            writer.Write(contents);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        private static async Task WriteTempFileAsync(string tempPath, string contents)
        {
            await using var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            await using (var writer = new StreamWriter(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 4096,
                leaveOpen: true))
            {
                await writer.WriteAsync(contents).ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
            }
            stream.Flush(flushToDisk: true);
        }

        private static void ReplacePrimary(string tempPath, string fullPath, string? backupPath)
        {
            if (File.Exists(fullPath))
            {
                if (backupPath is not null)
                    File.Replace(tempPath, fullPath, backupPath, ignoreMetadataErrors: true);
                else
                    File.Move(tempPath, fullPath, overwrite: true);
            }
            else
            {
                File.Move(tempPath, fullPath);
            }
        }

        private static void TryDeleteTempFile(string tempPath)
        {
            // 置換に失敗しても、元ファイルは触らず一時ファイルだけを片付ける。
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch
            {
                // 保存時の本来の例外を、一時ファイル削除の失敗で隠さない。
            }
        }
    }

    /// <summary>
    /// アプリ設定・プロファイルの JSON 永続化を担当するサービス。
    /// UI 依存なし。MainWindow から状態を受け取り、ファイル I/O のみを行う。
    /// </summary>
    internal static class SettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        // ── App settings ──────────────────────────────────────────────────────

        public static AppSettings LoadSettings(string filePath)
            => LoadSettingsResult(filePath).Value;

        /// <summary>
        /// 設定を読み込み、未作成・破損・アクセス失敗を呼び出し側へ伝える。
        /// </summary>
        public static PersistenceLoadResult<AppSettings> LoadSettingsResult(string filePath)
        {
            return JsonFilePersistence.Load(
                filePath,
                static () => new AppSettings(),
                static settings =>
                {
                    settings.LayoutColumnWeights ??= [];
                    settings.LayoutRowWeights ??= [];
                });
        }

        /// <summary>
        /// 直前に正常保存されていた設定（settings.json.bak）を結果付きで読み込む。
        /// 一次ファイルの失敗を隠さないため、自動復旧ではなく呼び出し側が明示的に使う。
        /// </summary>
        public static PersistenceLoadResult<AppSettings> LoadSettingsBackupResult(string filePath)
            => LoadSettingsResult(JsonFilePersistence.GetBackupPath(filePath));

        public static PersistenceRestoreResult RestoreSettingsFromBackup(string filePath)
            => JsonFilePersistence.RestoreValidatedBackup(
                filePath,
                LoadSettingsBackupResult(filePath),
                value => JsonSerializer.Serialize(value, JsonOptions));

        public static void SaveSettings(string filePath, AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            var createBackup = JsonFilePersistence.ShouldCreateBackupBeforeSave(
                filePath,
                LoadSettingsResult(filePath));
            JsonFilePersistence.SaveAtomically(
                filePath,
                JsonSerializer.Serialize(settings, JsonOptions),
                createBackup);
        }

        // ── Profiles ──────────────────────────────────────────────────────────

        public static List<ProfileConfig> LoadProfiles(string filePath)
            => LoadProfilesResult(filePath).Value;

        /// <summary>
        /// プロファイル一覧を読み込み、空の一覧と読み込み失敗を区別する。
        /// </summary>
        public static PersistenceLoadResult<List<ProfileConfig>> LoadProfilesResult(string filePath)
            => JsonFilePersistence.Load(filePath, static () => new List<ProfileConfig>());

        /// <summary>
        /// 直前に正常保存されていたプロファイル一覧（profiles.json.bak）を結果付きで読み込む。
        /// </summary>
        public static PersistenceLoadResult<List<ProfileConfig>> LoadProfilesBackupResult(string filePath)
            => LoadProfilesResult(JsonFilePersistence.GetBackupPath(filePath));

        public static PersistenceRestoreResult RestoreProfilesFromBackup(string filePath)
            => JsonFilePersistence.RestoreValidatedBackup(
                filePath,
                LoadProfilesBackupResult(filePath),
                value => JsonSerializer.Serialize(value, JsonOptions));

        public static void SaveProfiles(string filePath, List<ProfileConfig> profiles)
        {
            ArgumentNullException.ThrowIfNull(profiles);
            var createBackup = JsonFilePersistence.ShouldCreateBackupBeforeSave(
                filePath,
                LoadProfilesResult(filePath));
            JsonFilePersistence.SaveAtomically(
                filePath,
                JsonSerializer.Serialize(profiles, JsonOptions),
                createBackup);
        }

        /*
         * LoadSettings / LoadProfiles は既存呼び出しとの互換性のため残している。
         * 起動処理のようにフォールバック値を保存・削除判断へ使う箇所では、
         * 必ず結果付き API を使い、Success / Missing 以外では上書きしないこと。
         */

        /// <summary>
        /// <paramref name="profilesDir"/> 内のフォルダのうち
        /// <paramref name="knownIds"/> に含まれないものを孤立フォルダとして削除する。
        /// </summary>
        public static void CleanupOrphanedProfileFolders(string profilesDir, IEnumerable<string> knownIds)
        {
            try
            {
                if (!Directory.Exists(profilesDir)) return;
                var knownSet = new HashSet<string>(knownIds);
                foreach (var folder in Directory.GetDirectories(profilesDir))
                {
                    var name = Path.GetFileName(folder);
                    if (!knownSet.Contains(name))
                    {
                        try
                        {
                            Directory.Delete(folder, recursive: true);
                            Debug.WriteLine($"[Profile] Cleaned up orphaned folder: {name}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Profile] Failed to clean up orphaned folder: {name} ({ex.Message})");
                        }
                    }
                }
            }
            catch { }
        }
    }
}

