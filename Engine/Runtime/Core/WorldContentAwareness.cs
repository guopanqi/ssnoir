#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace SSNoir.Core
{
    public sealed class WorldContentRecord
    {
        public string Version { get; set; } = "";
        public long Revision { get; set; }
        public bool Unread { get; set; }
    }

    public sealed class WorldContentSaveData
    {
        public bool Initialized { get; set; }
        public long Revision { get; set; }
        public Dictionary<string, WorldContentRecord> Nodes { get; set; } = new();
    }

    public sealed class WorldContentView
    {
        public IReadOnlyDictionary<string, long> Revisions { get; init; } = new Dictionary<string, long>();
        public IReadOnlyCollection<string> MarkedNodes { get; init; } = Array.Empty<string>();
    }

    /// <summary>世界内容的阅读状态。版本只来自显式声明，不比较文案、库存、费用或Clock数值。</summary>
    public sealed class WorldContentAwareness
    {
        private WorldContentSaveData _state = new();
        public void Reset() => _state = new();
        private static bool Trackable(GameNode n) => n.IsContainer || n.Resolve?.Type != ResolveType.Note;

        public IReadOnlyList<string> Observe(GameNode root)
        {
            var live = new HashSet<string>(StringComparer.Ordinal);
            var newPlaces = new List<string>();
            void Visit(GameNode n, bool newAncestor)
            {
                bool fresh = !_state.Nodes.TryGetValue(n.Id, out var record);
                if (fresh)
                {
                    record = new WorldContentRecord { Revision = ++_state.Revision, Version = n.NoticeVersion };
                    _state.Nodes.Add(n.Id, record);
                }
                else if (record!.Version != n.NoticeVersion)
                {
                    record.Version = n.NoticeVersion;
                    record.Revision = ++_state.Revision;
                    record.Unread = _state.Initialized && !newAncestor && Trackable(n);
                }
                if (fresh)
                {
                    record!.Unread = _state.Initialized && !newAncestor && Trackable(n);
                    if (_state.Initialized && n.IsPlace) newPlaces.Add(n.Id);
                }
                live.Add(n.Id);
                foreach (var c in n.Children) Visit(c, newAncestor || (fresh && Trackable(n)));
            }
            // 根不是玩家可进入的内容，也不遮蔽首次出现的地点。
            foreach (var child in root.Children) Visit(child, false);
            foreach (var row in _state.Nodes)
                if (!live.Contains(row.Key)) row.Value.Unread = false;
            _state.Initialized = true;
            return newPlaces;
        }

        public WorldContentView Capture(GameNode root, IReadOnlyDictionary<string, long>? displayedRevisions = null)
        {
            var revisions = new Dictionary<string, long>(StringComparer.Ordinal);
            var marked = new HashSet<string>(StringComparer.Ordinal);
            bool Visit(GameNode n)
            {
                bool unread = false;
                if (_state.Nodes.TryGetValue(n.Id, out var record))
                {
                    long revision = displayedRevisions == null ? record.Revision :
                        (displayedRevisions.TryGetValue(n.Id, out var shown) ? shown : 0);
                    if (revision > 0) revisions.Add(n.Id, revision);
                    unread = revision == record.Revision && record.Unread;
                }
                foreach (var c in n.Children) unread |= Visit(c);
                if (unread) marked.Add(n.Id);
                return unread;
            }
            Visit(root);
            return new WorldContentView { Revisions = revisions, MarkedNodes = marked };
        }

        /// <summary>只确认实际展示过的版本；旧快照不能吞掉更新的提醒。</summary>
        public bool Acknowledge(WorldContentView view, IEnumerable<string> ids)
        {
            bool changed = false;
            foreach (string id in ids)
                if (view.Revisions.TryGetValue(id, out long revision) &&
                    _state.Nodes.TryGetValue(id, out var record) && record.Revision == revision && record.Unread)
                { record.Unread = false; changed = true; }
            return changed;
        }

        public WorldContentSaveData Save() => new()
        {
            Initialized = _state.Initialized, Revision = _state.Revision,
            Nodes = _state.Nodes.ToDictionary(p => p.Key, p => new WorldContentRecord
                { Version = p.Value.Version, Revision = p.Value.Revision, Unread = p.Value.Unread })
        };
        public void Load(WorldContentSaveData? data)
        {
            Reset();
            if (data == null) return; // 旧档以当前世界建立基线。
            if (data.Nodes == null || data.Revision < 0 || (!data.Initialized && data.Nodes.Count != 0))
                throw new InvalidOperationException("Invalid world content awareness save.");
            foreach (var p in data.Nodes)
                if (string.IsNullOrWhiteSpace(p.Key) || p.Value == null || p.Value.Version == null ||
                    p.Value.Revision <= 0 || p.Value.Revision > data.Revision)
                    throw new InvalidOperationException("Invalid world content node record: " + p.Key);
            _state = data;
            _state = Save(); // 不与调用方共享可变记录。
        }
    }
}
