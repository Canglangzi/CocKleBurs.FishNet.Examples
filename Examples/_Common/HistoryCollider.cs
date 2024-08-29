using System.Collections.Generic;
using FishNet.Managing.Timing;
using Mirror;
using UnityEngine;

namespace FishNet.Examples.Tanks
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/ Lag Compensation/ History Collider")]
    public class HistoryCollider : MonoBehaviour
    {
        [Header("Components")]
        [Tooltip("The object's actual collider. We need to know where it is, and how large it is.")]
        public Collider actualCollider;

        [Tooltip("The helper collider that the history bounds are projected onto.\nNeeds to be added to a child GameObject to counter-rotate an axis aligned Bounding Box onto it.\nThis is only used by this component.")]
        public BoxCollider boundsCollider;

        [Header("History")]
        [Tooltip("Keep this many past bounds in the buffer. The larger this is, the further we can raycast into the past.\nMaximum time := historyAmount * captureInterval")]
        public int boundsLimit = 8;

        [Tooltip("Gather N bounds at a time into a bucket for faster encapsulation. A factor of 2 will be twice as fast, etc.")]
        public int boundsPerBucket = 2;

        [Tooltip("Capture bounds every 'captureInterval' seconds. Larger values will require fewer computations, but may not capture every small move.")]
        public float captureInterval = 0.100f; // 100 ms
        private double lastCaptureTime = 0;

        [Header("Debug")]
        public Color historyColor = new Color(1.0f, 0.5f, 0.0f, 1.0f);
        public Color currentColor = Color.red;

        private HistoryBounds history = null;
        private TimeManager timeManager;

        private void Awake()
        {
            history = new HistoryBounds(boundsLimit, boundsPerBucket);

            // Ensure colliders were set
            if (actualCollider == null) Debug.LogError("HistoryCollider: actualCollider was not set.");
            if (boundsCollider == null) Debug.LogError("HistoryCollider: boundsCollider was not set.");
            if (boundsCollider.transform.parent != transform) Debug.LogError("HistoryCollider: boundsCollider must be a child of this GameObject.");
            if (!boundsCollider.isTrigger) Debug.LogError("HistoryCollider: boundsCollider must be a trigger.");

            // Initialize TimeManager
            timeManager = FindObjectOfType<TimeManager>();
            if (timeManager == null) Debug.LogError("HistoryCollider: TimeManager not found in the scene.");
        }

        private void FixedUpdate()
        {
            // Use TimeManager for time management
            if (timeManager.TicksToTime(timeManager.LocalTick) >= lastCaptureTime + captureInterval)
            {
                lastCaptureTime = timeManager.TicksToTime(timeManager.LocalTick);
                CaptureBounds();
            }

            // Project bounds onto helper collider
            ProjectBounds();
        }

        private void CaptureBounds()
        {
            // Grab current collider bounds
            Bounds bounds = actualCollider.bounds;

            // Insert into history
            history.Insert(bounds);
        }

        private void ProjectBounds()
        {
            // Grab total collider encapsulating all of history
            Bounds total = history.total;

            // Don't assign empty bounds; this will throw a Unity warning
            if (history.boundsCount == 0) return;

            // Scale projection doesn't work yet.
            // For now, don't allow scale changes
            if (transform.lossyScale != Vector3.one)
            {
                Debug.LogWarning($"HistoryCollider: {name}'s transform global scale must be (1,1,1).");
                return;
            }

            // Counter-rotate the child collider against the GameObject's rotation
            // We need this to always be axis-aligned
            boundsCollider.transform.localRotation = Quaternion.Inverse(transform.rotation);

            // Project world space bounds to collider's local space
            boundsCollider.center = boundsCollider.transform.InverseTransformPoint(total.center);
            boundsCollider.size = total.size; // TODO projection?
        }

        // Optional runtime drawing for debugging
        private void OnDrawGizmos()
        {
            // Draw total bounds
            Gizmos.color = historyColor;
            Gizmos.DrawWireCube(history.total.center, history.total.size);

            // Draw current bounds
            Gizmos.color = currentColor;
            Gizmos.DrawWireCube(actualCollider.bounds.center, actualCollider.bounds.size);
        }
    }
}
