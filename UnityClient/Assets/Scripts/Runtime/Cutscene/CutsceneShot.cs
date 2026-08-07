#nullable enable
using Cinemachine;
using UnityEngine;

namespace SSNoir
{
    /// <summary>
    /// 一镜过场：挂在虚拟相机上，说明这个机位要放哪段视频。
    ///
    /// 挂在相机身上而不是单开一张配置表，是为了让整条链路对着同一个对象转：选中它，预览窗
    /// 就是这一镜的构图，<c>shot</c> 出的首帧图就是喂给模型的那张，生成回来的视频填进
    /// <see cref="VideoFileName"/>，播出来的还是这一镜。中间没有一个环节需要靠名字去对。
    ///
    /// 视频走 StreamingAssets 下的文件名而不是 VideoClip 引用：这一步要反复换片子，扔个文件
    /// 进去就能重播，不用等 Unity 转码，也不用回编辑器重新拖引用。代价是拼错文件名要到运行
    /// 时才知道，所以播放器找不到文件时会明说。
    /// </summary>
    [RequireComponent(typeof(CinemachineVirtualCamera))]
    public class CutsceneShot : MonoBehaviour
    {
        [Tooltip("测试面板里显示的名字。留空就用 GameObject 名。")]
        public string ShotId = string.Empty;

        [Tooltip("StreamingAssets/Cutscenes/ 下的文件名，含扩展名（如 敲门.mp4）。\n"
               + "留空则只走影幕流程不放片子——镜头和黑边可以先于视频单独测。")]
        public string VideoFileName = string.Empty;

        [Tooltip("没有配视频时，影幕里停留多久（秒）。")]
        public float HoldSeconds = 3f;

        [Tooltip("影幕画幅。2.39 是宽银幕，1.85 窄一些；16:9 全屏是 1.78，等于没有黑边。")]
        public float LetterboxAspect = 2.39f;

        [Tooltip("黑边压下来和收回去各用多久（秒）。")]
        public float LetterboxDuration = 0.5f;

        public string DisplayName =>
            string.IsNullOrWhiteSpace(ShotId) ? gameObject.name : ShotId;

        public CinemachineVirtualCamera Camera => GetComponent<CinemachineVirtualCamera>();
    }
}
