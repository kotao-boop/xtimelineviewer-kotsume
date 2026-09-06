using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XTimelineViewer.Services;
using Xunit;

namespace XTimelineViewer.Tests.Services;

public sealed class BoundedResourceTests
{
    [Fact]
    public void BoundedCache_ReadingEntryMakesItMostRecentlyUsed()
    {
        var cache = new BoundedCache<string, int>(capacity: 2);

        cache.Set("old", 1);
        cache.Set("recent", 2);
        Assert.True(cache.TryGetValue("old", out var oldValue));
        Assert.Equal(1, oldValue);

        cache.Set("new", 3);

        Assert.True(cache.TryGetValue("old", out oldValue));
        Assert.Equal(1, oldValue);
        Assert.True(cache.TryGetValue("new", out var newValue));
        Assert.Equal(3, newValue);
        Assert.False(cache.TryGetValue("recent", out _));
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void BoundedCache_UpdatingExistingKeyKeepsCapacityAndRefreshesEntry()
    {
        var cache = new BoundedCache<string, string>(capacity: 2);

        cache.Set("first", "v1");
        cache.Set("second", "v2");
        cache.Set("first", "v3");
        cache.Set("third", "v4");

        Assert.Equal(2, cache.Count);
        Assert.True(cache.TryGetValue("first", out var firstValue));
        Assert.Equal("v3", firstValue);
        Assert.True(cache.TryGetValue("third", out var thirdValue));
        Assert.Equal("v4", thirdValue);
        Assert.False(cache.TryGetValue("second", out _));
    }

    [Fact]
    public async Task BoundedCache_ConcurrentReadsAndWritesNeverExceedCapacity()
    {
        const int capacity = 7;
        var cache = new BoundedCache<int, int>(capacity);

        var workers = Enumerable.Range(0, 12).Select(worker => Task.Run(() =>
        {
            for (var i = 0; i < 1_000; i++)
            {
                var key = (worker * 1_000 + i) % 40;
                cache.Set(key, i);
                cache.TryGetValue((key + 1) % 40, out _);
            }
        }));

        await Task.WhenAll(workers);

        Assert.InRange(cache.Count, 0, capacity);
    }

    [Fact]
    public void BoundedCache_ClearRemovesEntries()
    {
        var cache = new BoundedCache<string, int>(capacity: 2);
        cache.Set("one", 1);
        cache.Set("two", 2);

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryGetValue("one", out _));
        Assert.False(cache.TryGetValue("two", out _));
    }

    [Fact]
    public async Task BoundedStreamReader_ExactlyMaxBytesReturnsRewoundStream()
    {
        var expected = Enumerable.Range(0, 128).Select(i => (byte)i).ToArray();

        await using var result = await BoundedStreamReader.ReadAsync(
            new MemoryStream(expected), expected.Length, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0, result!.Position);
        using var reader = new BinaryReader(result, System.Text.Encoding.UTF8, leaveOpen: true);
        Assert.Equal(expected, reader.ReadBytes(expected.Length));
    }

    [Fact]
    public async Task BoundedStreamReader_OverLimitReturnsNull()
    {
        var input = new MemoryStream(new byte[129]);

        var result = await BoundedStreamReader.ReadAsync(input, maxBytes: 128, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task BoundedStreamReader_CanceledTokenPropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            BoundedStreamReader.ReadAsync(new MemoryStream(new byte[16]), 128, cancellation.Token));
    }

    [Fact]
    public async Task BoundedStreamReader_RejectsNonPositiveLimit()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            BoundedStreamReader.ReadAsync(new MemoryStream(), maxBytes: 0, CancellationToken.None));
    }
}
