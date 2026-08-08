#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SSNoir
{
    /// <summary>
    /// 一场过场：一串有序的镜头。
    ///
    /// <see cref="SequenceId"/> 就是剧本里 <c>(play-animation! "tag")</c> 的那个 tag——
    /// 剧本说的是"播开场那场戏"，不是"播第三个机位"，所以 tag 认的是这一层，不是单个镜头。
    ///
    /// 黑边是整场戏的取景框，压下来一次、收起来一次，中间换镜头不重新压。一镜一个画幅的
    /// 话，每次切镜头黑边都要动一下，观感直接垮掉；画幅和动画时长由播放器统一控制。
    ///
    /// 镜头用显式列表而不是读子物体顺序。一个机位可能在一场戏里出现两次（去了又回来），
    /// 列表能表达而层级不能；顺序也不该依赖 Hierarchy 拖拽这种容易误操作的东西。
    /// 嫌手工拖麻烦，Inspector 上有「从子物体填充」。
    /// </summary>
    public class CutsceneSequence : MonoBehaviour
    {
        [Tooltip("剧本里 (play-animation! \"...\") 用的 tag。留空就用 GameObject 名。\n"
               + "必须全场景唯一。")]
        public string SequenceId = string.Empty;

        [Tooltip("按播放顺序排列的镜头。允许同一个机位出现多次。")]
        public List<CutsceneShot> Shots = new List<CutsceneShot>();

        public string DisplayName =>
            string.IsNullOrWhiteSpace(SequenceId) ? gameObject.name : SequenceId;

        /// <summary>剧本里的 tag 认这个值；没填 SequenceId 时退回 GameObject 名。</summary>
        public string ResolvedId => DisplayName;

        /// <summary>
        /// 按 tag 找一场过场。找不到返回 null——调用方要能接受"剧本写了但场景还没配"，
        /// 剧本先行、镜头后补是常态，不该因此卡住剧情。
        /// </summary>
        public static CutsceneSequence? Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            foreach (var sequence in FindObjectsOfType<CutsceneSequence>(true))
            {
                if (sequence.ResolvedId == id)
                    return sequence;
            }

            return null;
        }

        /// <summary>列表里剔掉空槽后的镜头。空槽多半是删了子物体忘了清列表。</summary>
        public IEnumerable<CutsceneShot> ValidShots()
        {
            foreach (var shot in Shots)
            {
                if (shot != null)
                    yield return shot;
            }
        }
    }
}
