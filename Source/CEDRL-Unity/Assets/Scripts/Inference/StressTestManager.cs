using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// StressTestManager
/// Adapted for CEDRL project to spawn agents in various scenarios (Intersection, Hallway, Density).
/// Utilizes the Environment's AddManualAgents system for consistent agent setup.
/// </summary>
public class StressTestManager : MonoBehaviour
{
    public enum ScenarioType { Intersection, Hallway, Density }
    public enum AgentCount { Count_3 = 3, Count_6 = 6, Count_9 = 9, Count_12 = 12 }

    [Header("Setup")]
    public Environment environment;

    [Header("Settings")]
    public ScenarioType scenario = ScenarioType.Intersection;
    public AgentCount agentsPerGroup = AgentCount.Count_9;
    public float gridSpacing = 2.0f; 

    [Header("Density Scenario Settings")]
    public int densitySpawnCount = 50;
    public float densityAreaSize = 30f;

    private bool registrationDone = false;

    void Awake()
    {
        if (environment == null) environment = FindObjectOfType<Environment>();
    }

    void Start()
    {
        if (environment == null)
        {
            Debug.LogError("[StressTestManager] Environment not found!");
        }
    }

    void Update()
    {
        if (!registrationDone && environment != null)
        {
            SpawnScenario();
            registrationDone = true;
        }
    }

    [ContextMenu("Spawn Scenario")]
    public void SpawnScenario()
    {
        List<AgentPointPair> pairs = new List<AgentPointPair>();

        if (scenario == ScenarioType.Intersection)
        {
            // Group 1: x=-15 -> x=15 (Facing Right)
            pairs.AddRange(GenerateGroupPairs(new Vector3(-15, 0, 0), new Vector3(15, 0, 0)));

            // Group 2: z=15 -> z=-15 (Facing Back)
            pairs.AddRange(GenerateGroupPairs(new Vector3(0, 0, 15), new Vector3(0, 0, -15)));
        }
        else if (scenario == ScenarioType.Hallway)
        {
            // Group 1: x=15 -> x=-15 (Facing Left)
            pairs.AddRange(GenerateGroupPairs(new Vector3(15, 0, 0), new Vector3(-15, 0, 0)));

            // Group 2: x=-15 -> x=15 (Facing Right)
            pairs.AddRange(GenerateGroupPairs(new Vector3(-15, 0, 0), new Vector3(15, 0, 0)));
        }
        else if (scenario == ScenarioType.Density)
        {
            pairs.AddRange(GenerateDensityPairs());
        }

        if (pairs.Count > 0)
        {
            environment.AddManualAgents(pairs);
            Debug.Log($"[StressTestManager] Spawned {pairs.Count} agents for {scenario} scenario.");
        }
    }

    List<AgentPointPair> GenerateGroupPairs(Vector3 centerPos, Vector3 targetPos)
    {
        List<AgentPointPair> groupPairs = new List<AgentPointPair>();
        
        int total = (int)agentsPerGroup;
        int widthCount = 3;
        int depthCount = total / widthCount;

        // Direction vector from center to target (flattened to XZ plane)
        Vector3 dir = (targetPos - centerPos);
        dir.y = 0;
        dir.Normalize();
        
        Quaternion lookRot = Quaternion.identity;
        if (dir != Vector3.zero) lookRot = Quaternion.LookRotation(dir);

        for (int d = 0; d < depthCount; d++)
        {
            for (int w = 0; w < widthCount; w++)
            {
                float localX = (w - (widthCount - 1) * 0.5f) * gridSpacing;
                float localZ = -(d - (depthCount - 1) * 0.5f) * gridSpacing;

                Vector3 localOffset = new Vector3(localX, 0, localZ);
                // Rotate the grid formation to match the movement direction
                // Note: We use lookRot for grid positioning
                Quaternion gridRot = Quaternion.identity;
                if (dir != Vector3.zero) gridRot = Quaternion.LookRotation(dir);
                Vector3 worldOffset = gridRot * localOffset;

                Vector3 spawnPos = centerPos + worldOffset;
                Vector3 goalPos = targetPos + worldOffset;

                groupPairs.Add(new AgentPointPair
                {
                    manualStartPos = environment.AdjustHeight(spawnPos),
                    manualStartRot = lookRot,
                    manualGoalPos = environment.AdjustHeight(goalPos),
                    spawnTime = 0f,
                    useManualPoints = true
                });
            }
        }
        return groupPairs;
    }

    List<AgentPointPair> GenerateDensityPairs()
    {
        List<AgentPointPair> densityPairs = new List<AgentPointPair>();
        float halfSize = densityAreaSize * 0.5f;

        for (int i = 0; i < densitySpawnCount; i++)
        {
            Vector3 spawnPos = new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize));
            Vector3 goalPos = new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize));

            int attempts = 0;
            while (Vector3.Distance(spawnPos, goalPos) < 5.0f && attempts < 10)
            {
                goalPos = new Vector3(Random.Range(-halfSize, halfSize), 0, Random.Range(-halfSize, halfSize));
                attempts++;
            }

            Vector3 dir = (goalPos - spawnPos);
            dir.y = 0;
            dir.Normalize();
            
            Quaternion rot = Quaternion.identity;
            if (dir != Vector3.zero) rot = Quaternion.LookRotation(dir);

            densityPairs.Add(new AgentPointPair
            {
                manualStartPos = environment.AdjustHeight(spawnPos),
                manualStartRot = rot,
                manualGoalPos = environment.AdjustHeight(goalPos),
                spawnTime = 0f,
                useManualPoints = true
            });
        }
        return densityPairs;
    }
}
