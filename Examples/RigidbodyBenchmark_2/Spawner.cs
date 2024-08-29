using FishNet;
using FishNet.Object;
using UnityEngine;

namespace Mirror.Examples.PredictionBenchmark
{
    public class Spawner : NetworkBehaviour
    {
        [Header("生成设置")]
        public int spawnAmount = 1000;  // 生成的对象数量
        public GameObject spawnPrefab;  // 生成的预制体
        public float interleave = 1;    // 对象之间的间隔

        public int solverIterations = 200;  // 物理求解器的迭代次数

        private void Start()
        {
            if (IsServerInitialized)
            {
                 // 确保关闭垂直同步，以便进行基准测试，防止结果被限制
                 QualitySettings.vSyncCount = 0;
     
                 // 设置物理求解器的迭代次数
                 int before = Physics.defaultSolverIterations;
                 Physics.defaultSolverIterations = solverIterations;
                 Debug.Log($"物理求解器迭代次数: {before} -> {Physics.defaultSolverIterations}");
     
                 SpawnAll();               
            }

        }

        void SpawnAll()
        {
            // 计算 sqrt 以便可以生成 N * N = 数量
            float sqrt = Mathf.Sqrt(spawnAmount);

            // 根据生成数量和距离计算生成的起始位置
            float offset = -sqrt / 2 * interleave;

            // 精确生成指定数量的对象
            int spawned = 0;
            for (int spawnX = 0; spawnX < sqrt; ++spawnX)
            {
                for (int spawnY = 0; spawnY < sqrt; ++spawnY)
                {
                    // 确保生成的对象数量不超过指定数量
                    if (spawned < spawnAmount)
                    {
                        // 确保对象之间至少有 `Physics.defaultContactOffset` 的距离
                        float spacing = interleave + Physics.defaultContactOffset;
                        float x = offset + spawnX * spacing;
                        float y = spawnY * spacing;

                        // 实例化并设置位置
                        GameObject go = Instantiate(spawnPrefab);
                        go.transform.position = new Vector3(x, y, 0);
                        InstanceFinder.ServerManager.Spawn(go);
                        // 在网络上生成对象
                        ++spawned;
                    }
                }
            }
        }
    }
}