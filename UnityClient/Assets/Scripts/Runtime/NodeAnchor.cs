using UnityEngine;

namespace SSNoir
{
    public class NodeAnchor : MonoBehaviour
    {
        [Tooltip("The SCM node name this anchor corresponds to.")]
        public string NodeName;

        [Tooltip("Optional virtual camera to use when this node is selected/focused.")]
        public Cinemachine.CinemachineVirtualCamera FocusVirtualCamera;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            Gizmos.DrawRay(transform.position, Vector3.up * 1.5f);

            if (FocusVirtualCamera != null)
            {
                var camTransform = FocusVirtualCamera.transform;
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, camTransform.position);
                Gizmos.DrawWireSphere(camTransform.position, 0.15f);

                var forward = camTransform.forward;
                Gizmos.DrawRay(camTransform.position, forward * 0.8f);
                Gizmos.DrawRay(camTransform.position + forward * 0.6f, Quaternion.Euler(0, 30, 0) * -forward * 0.2f);
                Gizmos.DrawRay(camTransform.position + forward * 0.6f, Quaternion.Euler(0, -30, 0) * -forward * 0.2f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
