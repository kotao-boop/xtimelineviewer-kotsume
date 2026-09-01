using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using XTimelineViewer.Models;

namespace XTimelineViewer.Services
{
    internal static class WorkspaceStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public static List<WorkspaceConfig> Load(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var workspaces = JsonSerializer.Deserialize<List<WorkspaceConfig>>(json) ?? [];
                foreach (var workspace in workspaces)
                {
                    workspace.Timelines ??= [];
                    workspace.ColumnWeights ??= [];
                    workspace.RowWeights ??= [];
                }
                return workspaces;
            }
            catch
            {
                return [];
            }
        }

        public static void Save(string filePath, IReadOnlyList<WorkspaceConfig> workspaces)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            var tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(workspaces, JsonOptions));
            File.Move(tempPath, filePath, overwrite: true);
        }
    }
}
