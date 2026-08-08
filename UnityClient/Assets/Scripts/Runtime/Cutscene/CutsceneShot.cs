#nullable enable
using Cinemachine;
using UnityEngine;

namespace SSNoir
{
    /// <summary>
    /// 过场里的一镜：一个机位 + 一段视频 + 怎么进入这个机位。
    ///
    /// 挂在虚拟相机上，让整条链路对着同一个对象转：选中它，预览窗就是这一镜的构图，
    /// <c>shot</c> 出的首帧图就是喂给模型的那张，生成回来的视频填进 <see cref="VideoFileName"/>，
    /// 播出来的还是这一镜。中间没有一个环节需要靠名字去对。
    ///
    /// 一镜不知道自己属于哪场戏，也不管影幕——那些归 <see cref="CutsceneSequence"/>。
    /// 黑边是整场一个框，不能一镜一个画幅。
    ///
    /// 也没有"怎么推""推多久"这种字段。过场换镜走的就是游戏里其他每一次换镜那条路——
    /// 焦点运镜的弧线，时长读 Cinemachine brain 的默认混合。玩家不该能从运镜方式上看出
    /// "这一下是过场"。真要调快调慢，改 brain 那一处，全游戏一起变。
    /// </summary>
    [RequireComponent(typeof(CinemachineVirtualCamera))]
    public class CutsceneShot : MonoBehaviour
    {
        [Tooltip("StreamingAssets/Cutscenes/ 下的文件名，含扩展名（如 敲门.mp4）。\n"
               + "留空则这一镜只停不放片子——镜头和影幕可以先于视频单独测。")]
        public string VideoFileName = string.Empty;

        public string DisplayName => gameObject.name;

        public CinemachineVirtualCamera Camera => GetComponent<CinemachineVirtualCamera>();

        public bool HasVideo => !string.IsNullOrWhiteSpace(VideoFileName);
    }
}
