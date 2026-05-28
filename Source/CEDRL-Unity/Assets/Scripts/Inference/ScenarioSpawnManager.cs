using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using TMPro;

/// <summary>
/// ScenarioSpawnManager
/// Adapted for CEDRL project to spawn agents from a JSON scenario file.
/// Utilizes the Environment's AddManualAgents system for consistent agent setup.
/// </summary>
public class ScenarioSpawnManager : MonoBehaviour
{
    [Header("Setup")]
    public Environment environment;
    public TextMeshProUGUI stateText;

    [Header("General Settings")]
    public bool autoStart = false;

    [Header("Spawn Settings")]
    public string scenarioFileName = "SpawnScenario.txt";
    public bool pdmMode = true;
    public bool disableAgentsOnGoal = true;

    // Data classes matching the JSON structure
    [System.Serializable]
    public class ScenarioData
    {
        public int totalAgents;
        public List<TileSpawnInfo> spawns = new List<TileSpawnInfo>();
    }

    [System.Serializable]
    public class TileSpawnInfo
    {
        public int sidewalkIndex;
        public string type;
        public List<AgentInfo> agents = new List<AgentInfo>();
    }

    [System.Serializable]
    public class AgentInfo
    {
        public int spawnId;
        public string agentName;
        public float[] position;
        public float[] goalPosition;
        public int groupSize;
    }

    void Awake()
    {
        if (environment == null) environment = FindObjectOfType<Environment>();
    }

    private void Start()
    {
        if (autoStart)
        {
            OnClickSpawn();
        }
    }

    [ContextMenu("Spawn Scenario")]
    public void OnClickSpawn()
    {
        if (environment == null)
        {
            Debug.LogError("[ScenarioSpawnManager] Environment not found!");
            return;
        }

        string path = Path.Combine(Application.dataPath, "DailyScene", scenarioFileName);
        if (!File.Exists(path))
        {
            // Try another common path if not found (based on typical project structure)
            path = Path.Combine(Application.dataPath, "Scenarios", scenarioFileName);
            if (!File.Exists(path))
            {
                Debug.LogError($"[ScenarioSpawnManager] Scenario file not found at {path}");
                return;
            }
        }

        string fileContent = File.ReadAllText(path);

        // Find the start of the JSON object
        int jsonStartIndex = fileContent.IndexOf('{');
        if (jsonStartIndex == -1)
        {
            Debug.LogError("[ScenarioSpawnManager] No JSON object found in scenario file.");
            return;
        }

        string jsonContent = fileContent.Substring(jsonStartIndex);
        ScenarioData scenarioData = JsonConvert.DeserializeObject<ScenarioData>(jsonContent);

        if (scenarioData == null)
        {
            Debug.LogError("[ScenarioSpawnManager] Failed to parse scenario data.");
            return;
        }

        List<AgentPointPair> pairs = new List<AgentPointPair>();

        foreach (var tileSpawn in scenarioData.spawns)
        {
            foreach (var agentInfo in tileSpawn.agents)
            {
                Vector3 spawnPos = new Vector3(agentInfo.position[0], agentInfo.position[1], agentInfo.position[2]);
                Vector3 goalPos = new Vector3(agentInfo.goalPosition[0], agentInfo.goalPosition[1], agentInfo.goalPosition[2]);

                // Calculate initial rotation facing the goal
                Vector3 dir = (goalPos - spawnPos);
                dir.y = 0;
                dir.Normalize();
                Quaternion lookRot = Quaternion.identity;
                if (dir != Vector3.zero) lookRot = Quaternion.LookRotation(dir);

                pairs.Add(new AgentPointPair
                {
                    manualStartPos = environment.AdjustHeight(spawnPos),
                    manualStartRot = lookRot,
                    manualGoalPos = environment.AdjustHeight(goalPos),
                    spawnTime = 0f,
                    useManualPoints = true,
                    groupId = agentInfo.groupSize > 1 ? (tileSpawn.sidewalkIndex + 1) : 0
                });
            }
        }

        if (pairs.Count > 0)
        {
            List<CEDRL_Agent> createdAgents = environment.AddManualAgents(pairs);
            
            if (createdAgents != null)
            {
                foreach (var agent in createdAgents)
                {
                    agent.pdmMode = pdmMode;
                    agent.disableOnGoal = disableAgentsOnGoal;
                }
            }
            
            Debug.Log($"[ScenarioSpawnManager] Successfully spawned {pairs.Count} agents from {scenarioFileName}");
            if (stateText != null) stateText.text = $"Spawned {pairs.Count} Agents from Scenario";
        }
    }
}
