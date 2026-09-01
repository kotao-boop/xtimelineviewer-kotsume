using System;
using System.Collections.Generic;

namespace XTimelineViewer.Models
{
    internal class WorkspaceConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public string LayoutMode { get; set; } = "Classic";
        public List<TimelineConfig> Timelines { get; set; } = [];
        public List<double> ColumnWeights { get; set; } = [];
        public List<double> RowWeights { get; set; } = [];

        public WorkspaceConfig Clone() => new()
        {
            Id = Id,
            Name = Name,
            LayoutMode = LayoutMode,
            Timelines = Timelines.ConvertAll(t => t.Clone()),
            ColumnWeights = [.. ColumnWeights],
            RowWeights = [.. RowWeights],
        };
    }
}
