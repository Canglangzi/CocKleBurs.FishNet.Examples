using System;
using FishNet.CodeGenerating;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.AI;
using FishNet.Component.Prediction;

namespace FishNet.Examples.Tanks
{
    public class Tank : NetworkBehaviour
    {
        [Header("Components")]
        public NavMeshAgent agent;
        public Animator animator;
        public TextMesh healthBar;
        public Transform turret;

        [Header("Movement")]
        public float rotationSpeed = 100;

        [Header("Firing")]
        public KeyCode shootKey = KeyCode.Space;
        public GameObject projectilePrefab;
        public NetworkObject projectilenob;
        public Transform projectileMount;

        [Header("Stats")]
        [AllowMutableSyncType]
        public SyncVar<int> health = new SyncVar<int>(4);
           
        private NetworkCollision _networkCollision;

        private void Awake()
        {
            _networkCollision = GetComponent<NetworkCollision>();
            _networkCollision.OnEnter += NetworkCollisionEnter;

            projectilenob = projectilePrefab.GetComponent<NetworkObject>();
            InstanceFinder.NetworkManager.CacheObjects(projectilenob, 20, IsServerInitialized);
        }

        void Update()
        {
            // always update health bar.
            // (SyncVar hook would only update on clients, not on server)
            int clampedHealth = Mathf.Max(health.Value, 0); // Ensure health.Value is not negative
            healthBar.text = new string('-', clampedHealth);
            
            // take input from focused window only
            if (!Application.isFocused) return; 

            // movement for local player
            if (IsOwner)
            {
                // rotate
                float horizontal = Input.GetAxis("Horizontal");
                transform.Rotate(0, horizontal * rotationSpeed * Time.deltaTime, 0);

                // move
                float vertical = Input.GetAxis("Vertical");
                Vector3 forward = transform.TransformDirection(Vector3.forward);
                agent.velocity = forward * Mathf.Max(vertical, 0) * agent.speed;
                animator.SetBool("Moving", agent.velocity != Vector3.zero);

                // shoot
                if (Input.GetKeyDown(shootKey))
                {
                    CmdFire();
                }

                RotateTurret();
            }
        }

        [ServerRpc]
        void CmdFire()
        {
            uint tick = base.TimeManager.Tick; // Record the tick when firing

            // Get an instance from the object pool
            NetworkObject projectileNob = NetworkManager.GetPooledInstantiated(projectilenob, true);
            projectileNob.transform.position = projectileMount.position;
            projectileNob.transform.rotation = projectileMount.rotation;

            // Set up the projectile
            Projectile projectileScript = projectileNob.GetComponent<Projectile>();
            projectileScript.SetFireTick(tick); // Set the fire tick

            // Spawn the projectile on the network
            base.Spawn(projectileNob.gameObject);

            // Notify all observers
            RpcOnFire();
        }

        // This is called on the tank that fired for all observers
        [ObserversRpc]
        void RpcOnFire()
        {
            animator.SetTrigger("Shoot");
        }
        
        [Server]
        private void NetworkCollisionEnter(Collider other)
        {
            // if (other.GetComponent<Projectile>() != null)
            // {
            //     --health.Value;
            //     if (health.Value == 0)
            //         InstanceFinder.ServerManager.Despawn(gameObject);
            // }
        }
        [Server]
        public void TakeDamage(int damage)
        {
            if (!IsServerInitialized)
                return;

            health.Value -= damage;

            // Check if the tank is destroyed
            if (health.Value <= 0)
            {
                // You may want to add more logic here, such as notifying other clients
                InstanceFinder.ServerManager.Despawn(gameObject);
            }
        }
        void RotateTurret()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100))
            {
                Debug.DrawLine(ray.origin, hit.point);
                Vector3 lookRotation = new Vector3(hit.point.x, turret.transform.position.y, hit.point.z);
                turret.transform.LookAt(lookRotation);
            }
        }
    }
}
