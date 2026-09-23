#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SSNoir.IMGUI
{
    // 每帧重建的对话锚点表。HandPanelDrawer 登记在场队员 block,渲染器登记场景节点的世界锚点
    // 与节点卡。解析顺序固定:队员 Id → 队员 Name → 节点的世界锚点 → 节点卡。
    // 解析不到由调用方报错(不静默兜底)。
    //
    // 世界锚点排在卡前面:卡是从锚点投影出来、又被排布推开的一块 UI,气泡尾巴指着卡,
    // 读起来是"界面在说话";指着锚点上方那个人形的头,读起来才是"那儿有个人在说话"。
    // 卡只在节点没有锚点(网格卡)或锚点没登记时才当落点。
    public sealed class DialogueAnchors
    {
        // 落点画成的小矩形:尾巴尖端会投到它的边上,所以它就是"头"的大小。
        private const float WorldPointHalf = 12f;

        private readonly Dictionary<string, Rect> _actorById = new();
        private readonly Dictionary<string, Rect> _actorByName = new();
        private readonly Dictionary<string, Rect> _worldByName = new();
        private readonly Dictionary<string, Rect> _nodeByName = new();

        public void Clear()
        {
            _actorById.Clear();
            _actorByName.Clear();
            _worldByName.Clear();
            _nodeByName.Clear();
        }

        public void RegisterActor(string id, string name, Rect blockRect)
        {
            if (!string.IsNullOrEmpty(id)) _actorById[id] = blockRect;
            if (!string.IsNullOrEmpty(name)) _actorByName[name] = blockRect;
        }

        // 节点在世界里的说话点(虚拟 GUI 坐标),已经抬到人形头部高度。
        public void RegisterWorldPoint(string name, Vector2 point)
        {
            if (string.IsNullOrEmpty(name)) return;
            _worldByName[name] = new Rect(
                point.x - WorldPointHalf, point.y - WorldPointHalf,
                WorldPointHalf * 2f, WorldPointHalf * 2f);
        }

        public void RegisterNode(string name, Rect cardRect)
        {
            if (!string.IsNullOrEmpty(name)) _nodeByName[name] = cardRect;
        }

        public bool TryResolve(string speaker, out Rect rect)
            => TryResolve(speaker, preferCard: false, out rect);

        // preferCard：对方回合里一个人在动的时候，他的卡就是他——气泡从卡上冒出来，
        // 和卡上跳动的钟、挂在卡下的结果是同一件东西。这时不去找世界锚点：巷子里
        // 那几个锚点投在屏幕顶上，气泡飘到那儿就和卡断了联系。
        public bool TryResolve(string speaker, bool preferCard, out Rect rect)
        {
            if (preferCard && _nodeByName.TryGetValue(speaker, out rect))
                return true;
            return _actorById.TryGetValue(speaker, out rect)
                || _actorByName.TryGetValue(speaker, out rect)
                || _worldByName.TryGetValue(speaker, out rect)
                || _nodeByName.TryGetValue(speaker, out rect);
        }
    }
}
