// players can apply force to any stacked cube.
// this has to be on the player instead of on the cube via OnMouseDown,
// because OnMouseDown would get blocked by the predicted ghost objects.

using FishNet;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Examples.RigidbodyBenchmark
{
    public class PlayerForce : NetworkBehaviour
    {
        public float force = 50;

        void Update()
        {
            if (!base.IsOwner) return;

            if (Input.GetMouseButtonDown(0))
            {
                // raycast into camera direction
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                        // apply force in a random direction, this looks best
                        Debug.Log($"Applying force to: {hit.collider.name}");
                        Vector3 impulse = Random.insideUnitSphere * force;
                        NetworkObject networkObject = GetComponent<NetworkObject>();
                        CmdApplyForce(networkObject, impulse);
                }
            }

        }

        // every play can apply force to this object (no authority required)
        [ServerRpc]
        void CmdApplyForce(NetworkObject cube, Vector3 impulse)
        {
            // apply force in that direction
            Debug.LogWarning($"CmdApplyForce: {force} to {cube.name}");
            Rigidbody rb = cube.GetComponent<Rigidbody>();
            rb.AddForce(impulse, ForceMode.Impulse);
        }
    }
}
