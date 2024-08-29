using UnityEngine;
using FishNet;
using FishNet.Object;
using FishNet.Managing.Server;
using System.Collections.Generic;
using FishNet.Connection;

namespace FishNet.Examples.CCU
{
    [AddComponentMenu("")]
    public class CCUNetwork: NetworkBehaviour
    {
        [Header("Spawns")]
        public int spawnAmount = 10_000;
        public float interleave = 1;
        public GameObject spawnPrefab;

        [Range(0, 1)] public float spawnPositionRatio = 0.01f;

        private System.Random random = new System.Random(42);
        private List<Transform> startPositions = new List<Transform>();

        // Method to spawn all the objects
        private void SpawnAll()
        {
            // Clear previous player spawn positions in case we start twice
            foreach (Transform position in startPositions)
                Destroy(position.gameObject);

            startPositions.Clear();

            float sqrt = Mathf.Sqrt(spawnAmount);
            float offset = -sqrt / 2 * interleave;

            int spawned = 0;
            for (int spawnX = 0; spawnX < sqrt; ++spawnX)
            {
                for (int spawnZ = 0; spawnZ < sqrt; ++spawnZ)
                {
                    if (spawned < spawnAmount)
                    {
                        GameObject go = Instantiate(spawnPrefab);
                        float x = offset + spawnX * interleave;
                        float z = offset + spawnZ * interleave;
                        Vector3 position = new Vector3(x, 0, z);
                        go.transform.position = position;

                        // Spawn the object on the server
                        base.ServerManager.Spawn(go);
                        ++spawned;

                        // Add random spawn position for players
                        if (random.NextDouble() <= spawnPositionRatio)
                        {
                            GameObject spawnGO = new GameObject("Spawn");
                            spawnGO.transform.position = position;
                            startPositions.Add(spawnGO.transform);
                        }
                    }
                }
            }
        }

        // Override to get a random start position
        public Transform GetRandomStartPosition()
        {
            startPositions.RemoveAll(t => t == null); // Clean up null positions

            if (startPositions.Count == 0)
                return null;

            int index = random.Next(0, startPositions.Count); // DETERMINISTIC
            return startPositions[index];
        }

        // Server-side initialization
        public override void OnStartServer()
        {
            base.OnStartServer();
            SpawnAll();
        }
        public override void OnSpawnServer(NetworkConnection connection)
        {
            base.OnSpawnServer(connection);
            if (base.IsServerInitialized)
            {
                Transform startPos = GetRandomStartPosition();
                if (startPos != null)
                {
                    // Set the player position to a random spawn point
                    this.transform.position = startPos.position;
                }
            }
        }
    }
}
