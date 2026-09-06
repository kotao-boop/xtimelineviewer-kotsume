using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace XTimelineViewer.Services;

internal static class BoundedStreamReader
{
    internal static async Task<MemoryStream?> ReadAsync(Stream input, int maxBytes, CancellationToken token)
    {
        if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        var output = new MemoryStream();
        var buffer = new byte[32768];
        try
        {
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, maxBytes - output.Length + 1)), token)) > 0)
            {
                if (output.Length + read > maxBytes) { output.Dispose(); return null; }
                await output.WriteAsync(buffer.AsMemory(0, read), token);
            }
            output.Position = 0;
            return output;
        }
        catch { output.Dispose(); throw; }
    }
}
