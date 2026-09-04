using System;
using System.Collections.Generic;
using System.Text.Json;
using XTimelineViewer.Models;

namespace XTimelineViewer.Services
{
    internal static class WorkspaceStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public static List<WorkspaceConfig> Load(string filePath) => LoadResult(filePath).Value;

        /// <summary>
        /// ワークスペース一覧を読み込み、空の一覧と読み込み失敗を区別する。
        /// </summary>
        public static PersistenceLoadResult<List<WorkspaceConfig>> LoadResult(string filePath)
            => JsonFilePersistence.Load(
                filePath,
                static () => new List<WorkspaceConfig>(),
                static workspaces =>
                {
                    foreach (var workspace in workspaces)
                    {
                        workspace.Timelines ??= [];
                        workspace.ColumnWeights ??= [];
                        workspace.RowWeights ??= [];
                    }
                });

        public static PersistenceLoadResult<List<WorkspaceConfig>> LoadBackupResult(string filePath)
            => LoadResult(JsonFilePersistence.GetBackupPath(filePath));

        public static PersistenceRestoreResult RestoreFromBackup(string filePath)
            => JsonFilePersistence.RestoreValidatedBackup(
                filePath,
                LoadBackupResult(filePath),
                value => JsonSerializer.Serialize(value, JsonOptions));

        public static void Save(string filePath, IReadOnlyList<WorkspaceConfig> workspaces)
        {
            ArgumentNullException.ThrowIfNull(workspaces);

            var createBackup = JsonFilePersistence.ShouldCreateBackupBeforeSave(
                filePath,
                LoadResult(filePath));
            JsonFilePersistence.SaveAtomically(
                filePath,
                JsonSerializer.Serialize(workspaces, JsonOptions),
                createBackup);
        }
    }
}
