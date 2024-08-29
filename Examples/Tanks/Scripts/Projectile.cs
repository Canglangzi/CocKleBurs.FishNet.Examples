using FishNet.Component.Prediction;
using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Examples.Tanks
{
    public class Projectile : NetworkBehaviour
    {
        public float destroyAfter = 2;
        public Rigidbody rigidBody;
        public float force = 1000;
        private uint fireTick; // 新增字段
        private float startTime; // 记录弹丸开始运动的时间

        private NetworkCollision _networkCollision;
        
        private Vector3 direction; // 弹丸的运动方向
        public float maxDistance; // 最大检测距离

        public LayerMask shootableMask;
        public override void OnStartServer()
        {
            Invoke(nameof(DestroySelf), destroyAfter);
            _networkCollision = GetComponent<NetworkCollision>();
            _networkCollision.OnEnter += NetworkCollisionEnter;
            
            // Calculate direction and max distance for raycast
            direction = transform.forward;
        
        }

        // 设置发射 Tick
        public void SetFireTick(uint tick)
        {
            fireTick = tick;
            startTime = Time.time; // 记录实际开始时间
        }

        // 在 Start 方法中根据 Tick 进行延迟补偿
        // Projectile.cs
        void Start()
        {
            float passedTime = (float)base.TimeManager.TimePassed(fireTick, false); // 计算时间差
            float moveRate = force; // 初始速度

            // 计算加速度
            float adjustedForce = moveRate * (1f + passedTime / destroyAfter);
    
            // 应用力
            rigidBody.AddForce(transform.forward * adjustedForce);
        }
        void FixedUpdate()
        {
            if (IsServerInitialized)
            {
                  PreciseTick pt = base.TimeManager.GetPreciseTick(TickType.LastPacketTick);
                  
                  
                  RaycastHit hit;
                  if (Physics.Raycast(transform.position, direction, out hit, maxDistance))
                  {
                      HandleHit(hit);
                  }              
            }

        }
        [Server]
        private void HandleHit(RaycastHit hit)
        {
            if (hit.collider.TryGetComponent(out Tank tank))
            {
                LagCompensator compensator = tank.GetComponent<LagCompensator>();
                if (compensator.RaycastCheck(LocalConnection,transform.position, transform.forward, 0.5f, shootableMask, out hit))
                {
                    tank.TakeDamage(1); 
                    Debug.Log("服务器收到并执行TakeDamageHealth操作");

                }
            }
        }
        // [Server]
        // private void HandleHit(PreciseTick pt)
        // {
        //     //Rollback using the precise tick sent in.
        //     base.RollbackManager.Rollback(pt, RollbackPhysicsType.Physics, base.IsOwner);
        //     //Perform your raycast normally.
        //     RaycastHit hit;
        //     if (Physics.Raycast(transform.position, transform.forward, out hit)) { }
        //     //Return the colliders to their proper positions.
        //     base.RollbackManager.Return();
        //     
        //     if (hit.collider.TryGetComponent(out Tank tank))
        //     { 
        //         tank.TakeDamage(1);
        //         Debug.Log("服务器收到并执行TakeDamageHealth操作");
        //         
        //     }
        // }
        [Server]
        void DestroySelf()
        {
            InstanceFinder.ServerManager.Despawn(gameObject); 
        }
        private void NetworkCollisionEnter(Collider other)
        {
            DestroySelf();
        }
    }
}