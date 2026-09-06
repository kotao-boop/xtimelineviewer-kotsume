using System;
using System.Collections.Generic;

namespace XTimelineViewer.Services;

/// <summary>古い項目から追い出し、長時間動作しても件数が増え続けないキャッシュ。</summary>
internal sealed class BoundedCache<TKey, TValue>(int capacity) where TKey : notnull
{
    private readonly int _capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
    private readonly Dictionary<TKey, (TValue Value, LinkedListNode<TKey> Node)> _items = [];
    private readonly LinkedList<TKey> _order = new();
    private readonly object _gate = new();
    internal int Count { get { lock (_gate) return _items.Count; } }
    internal void Set(TKey key, TValue value)
    {
        lock (_gate)
        {
            if (_items.Remove(key, out var old)) _order.Remove(old.Node);
            _items[key] = (value, _order.AddLast(key));
            if (_items.Count > _capacity && _order.First is { } first)
            {
                _items.Remove(first.Value);
                _order.RemoveFirst();
            }
        }
    }
    internal bool TryGetValue(TKey key, out TValue value)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(key, out var item)) { value = default!; return false; }
            _order.Remove(item.Node);
            _order.AddLast(item.Node);
            value = item.Value;
            return true;
        }
    }
    internal void Clear() { lock (_gate) { _items.Clear(); _order.Clear(); } }
}
