using System;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;
using FishNet.Component.Prediction;
using FishNet.Managing.Timing;
using FishNet.Connection;
using Mirror;

namespace FishNet.Examples.Tanks
{
    /// <summary>
    /// 记录 3D 物体的状态（位置、旋转、大小、形状）用于延迟补偿。
    /// </summary>
    public struct Capture3D : Capture
    {
        public double timestamp { get; set; }
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 size;
        public ColliderType colliderType;

        public enum ColliderType
        {
            Box,
            Sphere,
            Mesh
        }

        public Capture3D(double timestamp, Vector3 position, Quaternion rotation, Vector3 size, ColliderType colliderType)
        {
            this.timestamp = timestamp;
            this.position = position;
            this.rotation = rotation;
            this.size = size;
            this.colliderType = colliderType;
        }

        public void DrawGizmo()
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);

            switch (colliderType)
            {
                case ColliderType.Box:
                    Gizmos.DrawWireCube(Vector3.zero, size);
                    break;
                case ColliderType.Sphere:
                    Gizmos.DrawWireSphere(Vector3.zero, size.x); // Assume size.x is the radius
                    break;
                case ColliderType.Mesh:
                    // You might need a more complex approach to visualize mesh colliders
                    break;
            }
        }

        public static Capture3D Interpolate(Capture3D from, Capture3D to, double t) =>
            new Capture3D(
                0, // 插值后的快照直接应用，不需要时间戳。
                Vector3.LerpUnclamped(from.position, to.position, (float)t),
                Quaternion.SlerpUnclamped(from.rotation, to.rotation, (float)t),
                Vector3.LerpUnclamped(from.size, to.size, (float)t),
                from.colliderType // 形状类型通常不需要插值
            );

        public override string ToString() => $"(time={timestamp} pos={position} rot={rotation} size={size} type={colliderType})";
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Network/ Lag Compensation/ Lag Compensator")]
    [HelpURL("https://fishnet-docs.fishnet.dev")]
    public class LagCompensator : NetworkBehaviour
    {
        [Header("Components")]
        [Tooltip("需要跟踪历史状态的碰撞体。")]
        public Collider trackedCollider;

        [Header("Settings")]
        [Tooltip("延迟补偿设置。")]
        public LagCompensationSettings lagCompensationSettings = new LagCompensationSettings();
        private double lastCaptureTime;

        private readonly Queue<KeyValuePair<double, Capture3D>> history = new Queue<KeyValuePair<double, Capture3D>>();

        [Header("Debugging")]
        [Tooltip("历史记录的调试颜色。")]
        public Color historyColor = Color.white;
        
        protected virtual void Update()
        {
            if (base.IsServerInitialized)
            {
                CaptureTick();
            }
        }
        [Server]
        protected virtual void CaptureTick()
        {
            // 每个捕捉间隔捕捉一次延迟补偿快照
            if (TimeManager.TicksToTime(TickType.Tick) >= lastCaptureTime + lagCompensationSettings.captureInterval)
            {
                lastCaptureTime = TimeManager.TicksToTime(TickType.Tick);
                Capture();
            }
        }

        [Server]
        protected virtual void Capture()
        {
            Capture3D capture = new Capture3D(
                TimeManager.TicksToTime(TickType.Tick),
                trackedCollider.bounds.center,
                trackedCollider.transform.rotation, // 捕捉物体的旋转
                GetColliderSize(trackedCollider), // 获取碰撞体大小
                GetColliderType(trackedCollider) // 获取碰撞体类型
            );

            // 插入到历史记录中
            LagCompensation.Insert(history, lagCompensationSettings.historyLimit, TimeManager.TicksToTime(TickType.Tick), capture);
        }

        protected virtual void OnDrawGizmos()
        {
            Gizmos.color = historyColor;
            LagCompensation.DrawGizmos(history);
        }

        [Server]
        public virtual bool Sample(NetworkConnection viewer, out Capture3D sample)
        {
            // 估算客户端时间
            double estimatedTime = TimeManager.TicksToTime(TickType.LocalTick);

            if (LagCompensation.Sample(history, estimatedTime, lagCompensationSettings.captureInterval, out Capture3D resultBefore, out Capture3D resultAfter, out double t))
            {
                sample = Capture3D.Interpolate(resultBefore, resultAfter, t);
                return true;
            }
            else
            {
                Debug.Log($"Sample: history doesn't contain {estimatedTime:F3}");
                sample = default;
                return false;
            }
        }

        [Server]
        public virtual bool BoundsCheck(
            NetworkConnection viewer,
            Vector3 hitPoint,
            float toleranceDistance,
            out float distance,
            out Vector3 nearest)
        {
            if (Sample(viewer, out Capture3D capture))
            {
                Bounds bounds = CreateBounds(capture);
                nearest = bounds.ClosestPoint(hitPoint);
                distance = Vector3.Distance(nearest, hitPoint);
                return distance <= toleranceDistance;
            }
            nearest = hitPoint;
            distance = 0;
            return false;
        }

        [Server]
        public virtual bool RaycastCheck(
            NetworkConnection viewer,
            Vector3 originPoint,
            Vector3 hitPoint,
            float tolerancePercent,
            int layerMask,
            out RaycastHit hit)
        {
            if (Sample(viewer, out Capture3D capture))
            {
                // 创建临时的 GameObject 以进行射线检测
                GameObject temp = new GameObject("LagCompensatorTest");
                temp.transform.position = capture.position;
                temp.transform.rotation = capture.rotation;

                Collider tempCollider = CreateCollider(temp, capture);
                tempCollider.transform.localScale = capture.size * (1 + tolerancePercent);

                // 执行射线检测
                Vector3 direction = hitPoint - originPoint;
                float maxDistance = direction.magnitude * 2;
                bool result = Physics.Raycast(originPoint, direction, out hit, maxDistance, layerMask);

                Destroy(temp);
                return result;
            }

            hit = default;
            return false;
        }

        private Vector3 GetColliderSize(Collider collider)
        {
            if (collider is BoxCollider box) return box.size;
            if (collider is SphereCollider sphere) return Vector3.one * sphere.radius;
            if (collider is MeshCollider mesh) return mesh.bounds.size; // For MeshCollider, size might not be straightforward
            return Vector3.zero;
        }

        private Capture3D.ColliderType GetColliderType(Collider collider)
        {
            if (collider is BoxCollider) return Capture3D.ColliderType.Box;
            if (collider is SphereCollider) return Capture3D.ColliderType.Sphere;
            if (collider is MeshCollider) return Capture3D.ColliderType.Mesh;
            return Capture3D.ColliderType.Box; // Default type
        }

        private Bounds CreateBounds(Capture3D capture)
        {
            switch (capture.colliderType)
            {
                case Capture3D.ColliderType.Box:
                    return new Bounds(capture.position, capture.size);
                case Capture3D.ColliderType.Sphere:
                    return new Bounds(capture.position, Vector3.one * capture.size.x * 2); // Assume size.x is the diameter
                case Capture3D.ColliderType.Mesh:
                    // MeshCollider 的 Bounds 在这里用来近似计算
                    return new Bounds(capture.position, capture.size); // Size 需要更复杂的处理
                default:
                    return new Bounds(capture.position, Vector3.zero);
            }
        }

        private Collider CreateCollider(GameObject gameObject, Capture3D capture)
        {
            switch (capture.colliderType)
            {
                case Capture3D.ColliderType.Box:
                    var box = gameObject.AddComponent<BoxCollider>();
                    box.size = capture.size;
                    return box;
                case Capture3D.ColliderType.Sphere:
                    var sphere = gameObject.AddComponent<SphereCollider>();
                    sphere.radius = capture.size.x; // Assume size.x is the radius
                    return sphere;
                case Capture3D.ColliderType.Mesh:
                    var mesh = gameObject.AddComponent<MeshCollider>();
                    // MeshCollider 的处理较复杂，可能需要额外的 Mesh 信息
                    return mesh;
                default:
                    return gameObject.AddComponent<BoxCollider>(); // Default type
            }
        }
    }
}
