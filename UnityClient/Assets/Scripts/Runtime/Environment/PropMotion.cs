#nullable enable
using System;
using System.Collections.Generic;
using SSNoir.Core;
using UnityEngine;

namespace SSNoir
{
    /// <summary>
    /// 场景里会动的道具（唱片机的针和盘）——**状态**通道：画面是终态，动画只是终态之间的过渡。
    ///
    /// 一个地点文件里每件会动的道具有一组 Legacy clip <c>道具__状态</c>（导入器按 clips.json 切出来）。
    /// 每次快照落地时 <see cref="SyncAll"/> 从游戏状态推出每件道具该处于哪个状态：
    ///   第一次见到（新游戏、读档、进门）→ 直接摆成那个状态，不播过渡；
    ///   状态没变（换了一张唱片仍在放）→ 什么都不做；
    ///   状态变了 → 播 from→to 的过渡 clip，接着排上目标状态 clip（循环或定格）。
    /// 真相只有一份：游戏状态。这里不存第二份，读档、离开再进来都走同一条路。
    ///
    /// 道具 ↔ 游戏状态的对应现在只有一条（唱片机 ↔ 全局键 音乐），写在 <see cref="SyncAll"/> 里；
    /// 第二个机关出现时再抽成表。
    /// </summary>
    public sealed class PropMotion : MonoBehaviour
    {
        private static readonly List<PropMotion> All = new();

        private Animation _animation = null!;
        private PropClips _spec = null!;
        private readonly Dictionary<string, int> _layerByProp = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _stateByProp = new(StringComparer.Ordinal);
        // 每件道具的部件及其静止姿态（实例化那一刻 = FBX 第 1 帧 = 默认状态）。Unity 导入时会丢掉在 clip 范围内
        // 恒定的曲线，所以 Stopped 这类 clip 可能一条曲线都没有；播任何 clip 前先把部件复位到静止姿态，
        // 没被曲线驱动的通道（停播后飘在半空的音符）才回得去。
        private readonly Dictionary<string, List<(Transform t, Vector3 p, Quaternion r, Vector3 s)>> _restByProp = new(StringComparer.Ordinal);

        /// <summary>地点细节实例挂上来时调用：有 Animation 组件（导入时切了 clip）才装。</summary>
        public static void Attach(GameObject placeInstance, string placeName)
        {
            var animation = placeInstance.GetComponent<Animation>();
            if (animation == null)
                return;
            var text = Resources.Load<TextAsset>(CityPlaces.ResourcesFolder + placeName + ".clips");
            if (text == null)
                throw new InvalidOperationException(
                    $"[SSNoir] 地点 '{placeName}' 带动画但 Resources/{CityPlaces.ResourcesFolder}{placeName}.clips.json 不存在。重新 build + publish CityBox。");
            var motion = placeInstance.AddComponent<PropMotion>();
            motion.Setup(animation, PropClips.Parse(text.text));
        }

        private void Setup(Animation animation, PropClips spec)
        {
            _animation = animation;
            _spec = spec;
            _animation.playAutomatically = false;
            _animation.Stop();
            foreach (var clip in spec.clips)
            {
                var state = _animation[clip.name];
                if (state == null)
                    throw new InvalidOperationException(
                        $"[SSNoir] '{name}' 的 clips.json 有 '{clip.name}'，模型里却没有这个 clip。重新导入地点文件。");
                if (!_layerByProp.TryGetValue(clip.Prop, out int layer))
                    _layerByProp[clip.Prop] = layer = _layerByProp.Count + 1;
                state.layer = layer;                       // 各道具各一层：换一件的 clip 不会停掉另一件的
                // 过渡必须是 Once：ClampForever 的 state 永远不"完成"，PlayQueued(CompleteOthers) 排在它后面的
                // 目标状态永远轮不到——表现为落针后唱片不转、音符不出。状态 clip 才定格 / 循环。
                state.wrapMode = clip.IsTransition ? WrapMode.Once : clip.loop ? WrapMode.Loop : WrapMode.ClampForever;
            }
            var all = GetComponentsInChildren<Transform>(true);
            foreach (var group in spec.parts)
            {
                var rest = new List<(Transform, Vector3, Quaternion, Vector3)>();
                foreach (var partName in group.names)
                {
                    var t = Array.Find(all, x => x.name == partName);
                    if (t == null)
                        throw new InvalidOperationException($"[SSNoir] '{name}' 的 clips.json 说 '{group.prop}' 有部件 '{partName}'，模型里没有。");
                    rest.Add((t, t.localPosition, t.localRotation, t.localScale));
                }
                _restByProp[group.prop] = rest;
            }
        }

        private void ResetToRest(string prop)
        {
            if (!_restByProp.TryGetValue(prop, out var rest))
                return;
            foreach (var (t, p, r, s) in rest)
            {
                t.localPosition = p;
                t.localRotation = r;
                t.localScale = s;
            }
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public bool Has(string prop) => _layerByProp.ContainsKey(prop);

        /// <summary>把一件道具摆到目标状态：首次直接摆，状态变了才播过渡。</summary>
        public void Apply(string prop, string target)
        {
            string stateClip = prop + "__" + target;
            if (_animation[stateClip] == null)
                throw new InvalidOperationException($"[SSNoir] '{name}' 没有 clip '{stateClip}'。");
            if (!_stateByProp.TryGetValue(prop, out var current))
            {
                ResetToRest(prop);
                _animation.Play(stateClip, PlayMode.StopSameLayer);
                _stateByProp[prop] = target;
                return;
            }
            if (string.Equals(current, target, StringComparison.Ordinal))
                return;
            _stateByProp[prop] = target;
            PropClips.Clip? transition = null;
            foreach (var c in _spec.clips)
            {
                if (c.IsTransition && c.Prop == prop && c.from == current && c.to == target)
                {
                    transition = c;
                    break;
                }
            }
            ResetToRest(prop);
            if (transition == null)
            {
                // 没做这条过渡就跳变——画面仍然正确，只是少一段演出
                Debug.LogWarning($"[SSNoir] '{prop}' 没有 {current}→{target} 的过渡 clip，直接跳到 {target}。");
                _animation.Play(stateClip, PlayMode.StopSameLayer);
                return;
            }
            _animation.Play(transition.name, PlayMode.StopSameLayer);
            var queued = _animation.PlayQueued(stateClip, QueueMode.CompleteOthers, PlayMode.StopSameLayer);
            if (queued != null)
            {
                // 排队产生的是 state 的克隆，层和 wrapMode 要再设一遍，不然它落回第 0 层、按默认模式播
                queued.layer = _layerByProp[prop];
                queued.wrapMode = _animation[stateClip].wrapMode;
            }
        }

        /// <summary>场上哪件地点实例有这件道具（道具名在全城唯一：大吊灯只在剧院里）。</summary>
        public static PropMotion? Find(string prop)
        {
            foreach (var motion in All)
                if (motion.Has(prop))
                    return motion;
            return null;
        }

        /// <summary>
        /// 剧情演出：从道具此刻的状态播过渡到 <paramref name="target"/>，之后停在那儿。
        /// 没被 <see cref="Apply"/> 摆过的道具视作还在默认状态（第一个 clip = 导入姿态），所以第一次也播过渡，
        /// 不像 Apply 那样直接摆——这条通道的意义就是让人看见它动。
        /// 道具之后不归任何游戏状态管（SyncAll 不认识它就不会碰它），这一场里它就一直停在目标状态。
        /// </summary>
        public void PlayTransition(string prop, string target)
        {
            if (!_stateByProp.ContainsKey(prop))
            {
                foreach (var c in _spec.clips)
                {
                    if (c.Prop == prop && !c.IsTransition)
                    {
                        _stateByProp[prop] = c.Tail;
                        break;
                    }
                }
            }
            Apply(prop, target);
        }

        /// <summary>过渡 clip 还在播吗（演出等它播完再收机位）。</summary>
        public bool IsTransitioning(string prop)
        {
            foreach (var c in _spec.clips)
                if (c.IsTransition && c.Prop == prop && _animation.IsPlaying(c.name))
                    return true;
            return false;
        }

        /// <summary>每次快照落地时由 GameManager 调一次：从游戏状态推出每件道具的状态。</summary>
        public static void SyncAll(GameState gameState)
        {
            bool playing = gameState.Get<object>("音乐") is string music && music.Length > 0;
            foreach (var motion in All)
            {
                if (motion.Has("唱片机"))
                    motion.Apply("唱片机", playing ? "Playing" : "Stopped");
            }
        }
    }
}
