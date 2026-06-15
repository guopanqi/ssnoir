#nullable enable
using UnityEngine;

namespace SSNoir
{
    public class StagePortalConfig : MonoBehaviour
    {
        [Tooltip("VCam on the world side (entrance). Push-in starts here and pushes forward through the portal.")]
        public Cinemachine.CinemachineVirtualCamera? IntroCam;

        [Tooltip("VCam on the stage side (interior rest position). Camera cuts here and pulls out to this view.")]
        public Cinemachine.CinemachineVirtualCamera? OutCam;

        [Tooltip("How far past IntroCam the camera pushes before cutting to the stage side.")]
        public float pushDistance = 1.5f;

        [Tooltip("How far forward from OutCam the camera starts on the stage side before pulling back to the rest position.")]
        public float pullDistance = 1.0f;
    }
}
