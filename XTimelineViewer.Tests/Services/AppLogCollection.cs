using Xunit;

namespace XTimelineViewer.Tests.Services
{
    /// <summary>
    /// AppLog はプロセス全体で一つの出力先を共有するため、AppLog を変更するテストは直列実行する。
    /// </summary>
    [CollectionDefinition("AppLog", DisableParallelization = true)]
    public sealed class AppLogCollection
    {
    }
}
