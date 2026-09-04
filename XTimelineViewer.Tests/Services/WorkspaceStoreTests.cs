using System;
using System.Collections.Generic;
using System.IO;
using XTimelineViewer.Models;
using XTimelineViewer.Services;
using Xunit;

namespace XTimelineViewer.Tests.Services
{
    public class WorkspaceStoreTests
    {
        [Fact]
        public void SaveAndLoad_RoundTripsWorkspaceAndTimelineUxState()
        {
            var dir = Path.Combine(Path.GetTempPath(), "xtv-workspace-tests", Guid.NewGuid().ToString("N"));
            var path = Path.Combine(dir, "workspaces.json");
            try
            {
                var source = new List<WorkspaceConfig>
                {
                    new()
                    {
                        Name = "仕事",
                        LayoutMode = "Grid2x2",
                        Timelines = [new TimelineConfig { Name = "確認", Url = "https://x.com/home", IsVisible = false }],
                    },
                };
                WorkspaceStore.Save(path, source);
                var loaded = WorkspaceStore.Load(path);

                Assert.Single(loaded);
                Assert.Equal("仕事", loaded[0].Name);
                Assert.Equal("Grid2x2", loaded[0].LayoutMode);
                Assert.Equal("確認", loaded[0].Timelines[0].Name);
                Assert.False(loaded[0].Timelines[0].IsVisible);
                Assert.False(File.Exists(path + ".tmp"));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void Load_InvalidJson_ReturnsEmptyList()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "not-json");
                Assert.Empty(WorkspaceStore.Load(path));
            }
            finally
            {
                File.Delete(path);
            }
        }


        [Fact]
        public void Save_ReplacingFile_PreservesPreviousVersionAsBackup()
        {
            var dir = Path.Combine(Path.GetTempPath(), "xtv-workspace-tests", Guid.NewGuid().ToString("N"));
            var path = Path.Combine(dir, "workspaces.json");
            try
            {
                WorkspaceStore.Save(path, [new WorkspaceConfig { Name = "Old" }]);
                WorkspaceStore.Save(path, [new WorkspaceConfig { Name = "New" }]);

                Assert.Equal("New", Assert.Single(WorkspaceStore.Load(path)).Name);
                Assert.Equal("Old", Assert.Single(WorkspaceStore.LoadBackupResult(path).Value).Name);
                Assert.Empty(Directory.GetFiles(dir, "workspaces.json.*.tmp"));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void RestoreFromBackup_ArchivesCorruptPrimaryAndKeepsBackup()
        {
            var dir = Path.Combine(Path.GetTempPath(), "xtv-workspace-tests", Guid.NewGuid().ToString("N"));
            var path = Path.Combine(dir, "workspaces.json");
            try
            {
                WorkspaceStore.Save(path, [new WorkspaceConfig { Name = "Old" }]);
                WorkspaceStore.Save(path, [new WorkspaceConfig { Name = "New" }]);
                File.WriteAllText(path, "broken primary");

                var result = WorkspaceStore.RestoreFromBackup(path);

                Assert.True(result.IsSuccess);
                Assert.NotNull(result.ArchivedPrimaryPath);
                Assert.Equal("broken primary", File.ReadAllText(result.ArchivedPrimaryPath));
                Assert.Equal("Old", Assert.Single(WorkspaceStore.Load(path)).Name);
                Assert.Equal("Old", Assert.Single(WorkspaceStore.LoadBackupResult(path).Value).Name);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void Save_CorruptPrimary_BlocksOverwriteAndKeepsBackup()
        {
            var dir = Path.Combine(Path.GetTempPath(), "xtv-workspace-tests", Guid.NewGuid().ToString("N"));
            var path = Path.Combine(dir, "workspaces.json");
            try
            {
                WorkspaceStore.Save(path, [new WorkspaceConfig { Name = "Old" }]);
                WorkspaceStore.Save(path, [new WorkspaceConfig { Name = "New" }]);
                File.WriteAllText(path, "broken primary");

                var error = Assert.Throws<PersistenceSaveBlockedException>(() =>
                    WorkspaceStore.Save(path, [new WorkspaceConfig { Name = "Must not overwrite" }]));

                Assert.Equal(PersistenceLoadStatus.Corrupt, error.LoadStatus);
                Assert.Equal("broken primary", File.ReadAllText(path));
                Assert.Equal("Old", Assert.Single(WorkspaceStore.LoadBackupResult(path).Value).Name);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
        }


        [Fact]
        public void LoadResult_InvalidJson_ReportsCorruptInsteadOfEmptySuccess()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "not-json");

                var result = WorkspaceStore.LoadResult(path);

                Assert.Equal(PersistenceLoadStatus.Corrupt, result.Status);
                Assert.False(result.IsSuccess);
                Assert.Empty(result.Value);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void LoadResult_ValidEmptyList_IsSuccess()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "[]");

                var result = WorkspaceStore.LoadResult(path);

                Assert.Equal(PersistenceLoadStatus.Success, result.Status);
                Assert.True(result.IsSuccess);
                Assert.Empty(result.Value);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Load_NullCollections_AreRecoveredAsEmpty()
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path,
                    """[{"Name":"Legacy","Timelines":null,"ColumnWeights":null,"RowWeights":null}]""");

                var workspace = Assert.Single(WorkspaceStore.Load(path));

                Assert.Empty(workspace.Timelines);
                Assert.Empty(workspace.ColumnWeights);
                Assert.Empty(workspace.RowWeights);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
