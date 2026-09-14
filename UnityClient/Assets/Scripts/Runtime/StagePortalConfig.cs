#nullable enable
using UnityEngine;

namespace SSNoir
{
    /// <summary>
    /// 挂在 Stage 的 Anchor 上。进门的路只由城市侧这一台相机描述；Stage 内的揭幕由
    /// <see cref="StageTransitionController"/> 从交锋根机位自己算出来，不需要第二台相机。
    /// </summary>
    public class StagePortalConfig : MonoBehaviour
    {
        [Tooltip("VCam on the world side (entrance). Push-in starts here and pushes forward through the portal.")]
        public Cinemachine.CinemachineVirtualCamera? IntroCam;
    }
}
