using UnityEngine;

namespace SSNoir
{
    public class NodeAnchor : MonoBehaviour
    {
        [Tooltip("The SCM node name this anchor corresponds to.")]
        public string NodeName;

        [Tooltip("Optional camera transform to look at when this node is selected/focused.")]
        public Transform FocusCameraTransform;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            Gizmos.DrawRay(transform.position, Vector3.up * 1.5f);

            if (FocusCameraTransform != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, FocusCameraTransform.position);
                Gizmos.DrawWireSphere(FocusCameraTransform.position, 0.15f);

                var forward = FocusCameraTransform.forward;
                Gizmos.DrawRay(FocusCameraTransform.position, forward * 0.8f);
                Gizmos.DrawRay(FocusCameraTransform.position + forward * 0.6f, Quaternion.Euler(0, 30, 0) * -forward * 0.2f);
                Gizmos.DrawRay(FocusCameraTransform.position + forward * 0.6f, Quaternion.Euler(0, -30, 0) * -forward * 0.2f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
