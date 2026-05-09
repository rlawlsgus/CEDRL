using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AutoSpawnManager
/// Periodically scans ZaraGroupRotationSimulator and request agent spawning via Environment
/// whenever new agents appear and become active in the simulator.
/// </summary>
public class AutoSpawnManager : MonoBehaviour
{
    public static AutoSpawnManager Instance;

    [Header("Setup")]
    public ZaraGroupRotationSimulator simulator;
    
    private Environment m_env;
    private HashSet<int> spawnedTrajIds = new HashSet<int>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        
        m_env = FindObjectOfType<Environment>();
        if (simulator == null) simulator = FindObjectOfType<ZaraGroupRotationSimulator>();
    }

    void Start()
    {
        if (simulator == null)
        {
            Debug.LogError("[AutoSpawnManager] ZaraGroupRotationSimulator not found! Spawning will not proceed.");
        }
    }

    void Update()
    {
        if (m_env == null || simulator == null) return;

        // Force CustomCity setup if needed
        if (SceneManager.Instance != null && SceneManager.Instance.Setup != SceneSetup.CustomCity)
        {
            SceneManager.Instance.Setup = SceneSetup.CustomCity;
        }

        ScanAndSpawnNewAgents();
    }

    void ScanAndSpawnNewAgents()
    {
        List<AgentPointPair> newPairs = new List<AgentPointPair>();

        foreach (Transform child in simulator.transform)
        {
            // Check name AND active status to ensure valid position data
            if (child != null && child.name.StartsWith("ped_") && child.gameObject.activeInHierarchy)
            {
                if (int.TryParse(child.name.Substring(4), out int id))
                {
                    // Only spawn if we haven't spawned this ID yet
                    if (!spawnedTrajIds.Contains(id))
                    {
                        // Use GT's exact world position (including Y) at the moment it becomes active
                        Vector3 spawnPos = child.position;

                        AgentPointPair pair = new AgentPointPair
                        {
                            manualStartPos = spawnPos,
                            manualStartRot = child.rotation,
                            manualGoalPos = simulator.GetFinalWorldPosition(id),
                            spawnTime = 0f, 
                            agentId = id, 
                            useManualPoints = true
                        };
                        newPairs.Add(pair);
                        spawnedTrajIds.Add(id);
                    }
                }
            }
        }

        if (newPairs.Count > 0)
        {
            m_env.AddManualAgents(newPairs);
            Debug.Log($"[AutoSpawnManager] Detected and requested spawning for {newPairs.Count} new active agents. (Total spawned: {spawnedTrajIds.Count})");
        }
    }
}
