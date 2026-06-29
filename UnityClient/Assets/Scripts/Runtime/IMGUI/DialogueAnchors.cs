#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SSNoir.IMGUI
{
    // 每帧重建的对话锚点表。HandPanelDrawer 登记在场队员 block,渲染器登记当前场景节点卡。
    // 解析顺序固定:队员 Id → 队员 Name → 场景节点 Name。解析不到由调用方报错(不静默兜底)。
    public sealed class DialogueAnchors
    {
        private readonly Dictionary<string, Rect> _actorById = new();
        private readonly Dictionary<string, Rect> _actorByName = new();
        private readonly Dictionary<string, Rect> _nodeByName = new();

        public void Clear()
        {
            _actorById.Clear();
            _actorByName.Clear();
            _nodeByName.Clear();
        }

        public void RegisterActor(string id, string name, Rect blockRect)
        {
            if (!string.IsNullOrEmpty(id)) _actorById[id] = blockRect;
            if (!string.IsNullOrEmpty(name)) _actorByName[name] = blockRect;
        }

        public void RegisterNode(string name, Rect cardRect)
        {
            if (!string.IsNullOrEmpty(name)) _nodeByName[name] = cardRect;
        }

        public bool TryResolve(string speaker, out Rect rect)
        {
            return _actorById.TryGetValue(speaker, out rect)
                || _actorByName.TryGetValue(speaker, out rect)
                || _nodeByName.TryGetValue(speaker, out rect);
        }
    }
}
