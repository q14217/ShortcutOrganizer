using System;
using System.Collections.Generic;

namespace ShortcutOrganizer
{
    /// <summary>
    /// 线程安全的 LRU 缓存。容量满时淘汰最久未访问的项。
    /// </summary>
    public class LruCache<TKey, TValue>
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _map;
        private readonly LinkedList<KeyValuePair<TKey, TValue>> _list;
        private readonly object _lock = new object();

        public LruCache(int capacity, IEqualityComparer<TKey> comparer = null)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _map = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>(comparer);
            _list = new LinkedList<KeyValuePair<TKey, TValue>>();
        }

        public int Count { get { lock (_lock) return _map.Count; } }

        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (_lock)
            {
                if (_map.TryGetValue(key, out var node))
                {
                    // 移到链表头部（最近使用）
                    _list.Remove(node);
                    _list.AddFirst(node);
                    value = node.Value.Value;
                    return true;
                }
                value = default(TValue);
                return false;
            }
        }

        public void Add(TKey key, TValue value)
        {
            lock (_lock)
            {
                if (_map.TryGetValue(key, out var existing))
                {
                    _list.Remove(existing);
                    _map.Remove(key);
                }

                var node = new LinkedListNode<KeyValuePair<TKey, TValue>>(
                    new KeyValuePair<TKey, TValue>(key, value));
                _list.AddFirst(node);
                _map[key] = node;

                // 淘汰
                while (_map.Count > _capacity && _list.Last != null)
                {
                    var last = _list.Last;
                    _list.RemoveLast();
                    _map.Remove(last.Value.Key);
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _map.Clear();
                _list.Clear();
            }
        }
    }
}