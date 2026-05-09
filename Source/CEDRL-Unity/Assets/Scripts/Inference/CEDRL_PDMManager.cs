using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// CEDRL_PDMManager
/// Measures ADE/FDE using the Predictive Distance Metric (PDM) approach.
/// Syncs CEDRL agents with live GT agents from ZaraGroupRotationSimulator every N frames.
/// </summary>
public class CEDRL_PDMManager : MonoBehaviour
{
    public static CEDRL_PDMManager Instance;

    [Header("Setup")]
    public ZaraGroupRotationSimulator simulator;
    [Tooltip("How many frames between each rolling reset (PDM interval)")]
    public int evaluationInterval = 20; 
    
    [Header("Map Generation")]
    public bool generateMap = true;
    public string mapFileName = "CEDRL_PDM_Map.png";
    public int mapResolution = 2048;
    public float mapSize = 100f;
    public Color gtColor = Color.blue;
    public Color agentColor = Color.red;
    public Color obstacleColor = new Color(1f, 0.5f, 0f);

    // Data Storage (for setup only)
    private struct SpawnInfo
    {
        public int id;
        public Vector3 startPos;
        public Quaternion startRot;
        public Vector3 goalPos;
        public float spawnTime;
        public int groupId;
    }
    private List<SpawnInfo> spawnInfos = new List<SpawnInfo>();

    // Agent Tracking
    private class AgentTrack
    {
        public int trajId;
        public CEDRL_Agent simAgent;
        public Transform gtTransform;
        public List<Vector3> gtPath = new List<Vector3>();
        public List<Vector3> simPath = new List<Vector3>();
        public float currentIntervalDiffSum = 0f;
        public int currentIntervalFrameCount = 0;
    }
    private Dictionary<int, AgentTrack> activeTracks = new Dictionary<int, AgentTrack>();
    private Environment m_env;
    private int frameCounter = 0;

    // Metrics
    private List<float> adeList = new List<float>();
    private List<float> fdeList = new List<float>();

    private Dictionary<int, CEDRL_Agent> inactiveSimAgentsByTrajId = new Dictionary<int, CEDRL_Agent>();
    private List<int> trajIdsToRemoveFromInactive = new List<int>();
    private bool registrationDone = false;

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
            Debug.LogError("[CEDRL_PDMManager] ZaraGroupRotationSimulator not found! Spawning will not proceed.");
        }
    }

    void Update()
    {
        // Wait until Environment and Simulator are ready
        if (!registrationDone && m_env != null && simulator != null)
        {
            // Ensure simulator has spawned its agents
            bool pedsFound = false;
            foreach (Transform child in simulator.transform)
            {
                if (child.name.StartsWith("ped_")) { pedsFound = true; break; }
            }

            if (!pedsFound) return; 

            if (SceneManager.Instance != null && SceneManager.Instance.Setup != SceneSetup.CustomCity)
            {
                SceneManager.Instance.Setup = SceneSetup.CustomCity;
            }

            ScanSimulatorForAgents();
            RegisterToEnvironment();
            registrationDone = true;
            
            // Cache sim agents for manual activation
            CEDRL_Agent[] allAgents = FindObjectsOfType<CEDRL_Agent>(true);
            foreach (var agent in allAgents)
            {
                if (agent != null && agent.name.StartsWith("CityAgent_"))
                {
                    if (agent.id >= 0 && agent.id < m_env.m_manualCityAgents.Count)
                    {
                        int trajId = m_env.m_manualCityAgents[agent.id].agentId;
                        inactiveSimAgentsByTrajId[trajId] = agent;
                    }
                }
            }
        }
    }

    void ScanSimulatorForAgents()
    {
        spawnInfos.Clear();
        foreach (Transform child in simulator.transform)
        {
            if (child != null && child.name.StartsWith("ped_"))
            {
                if (int.TryParse(child.name.Substring(4), out int id))
                {
                    SpawnInfo info = new SpawnInfo
                    {
                        id = id,
                        startPos = child.position,
                        startRot = child.rotation,
                        goalPos = simulator.GetFinalWorldPosition(id),
                        spawnTime = 999999f, 
                        groupId = 0
                    };
                    spawnInfos.Add(info);
                }
            }
        }
        Debug.Log($"[CEDRL_PDMManager] Found {spawnInfos.Count} ped agents in simulator.");
    }

    public void RegisterToEnvironment()
    {
        if (m_env == null) return;

        List<AgentPointPair> pairs = new List<AgentPointPair>();
        foreach (var info in spawnInfos)
        {
            AgentPointPair pair = new AgentPointPair
            {
                manualStartPos = info.startPos,
                manualStartRot = info.startRot,
                manualGoalPos = info.goalPos,
                spawnTime = info.spawnTime,
                agentId = info.id, 
                useManualPoints = true
            };
            pairs.Add(pair);
        }

        m_env.AddManualAgents(pairs);
        Debug.Log($"[CEDRL_PDMManager] Requested spawning for {pairs.Count} agents via Environment.");
    }

    void FixedUpdate()
    {
        if (m_env == null || simulator == null) return;

        frameCounter++;
        bool isResetFrame = (frameCounter % evaluationInterval == 0);

        // 1. Activate simulation agents when their GT counterpart becomes active
        trajIdsToRemoveFromInactive.Clear();
        foreach (var kvp in inactiveSimAgentsByTrajId)
        {
            int trajId = kvp.Key;
            CEDRL_Agent simAgent = kvp.Value;

            if (simAgent == null)
            {
                trajIdsToRemoveFromInactive.Add(trajId);
                continue;
            }

            if (!simAgent.gameObject.activeInHierarchy)
            {
                GameObject gtObj = GameObject.Find("ped_" + trajId);
                if (gtObj != null && gtObj.activeInHierarchy)
                {
                    simAgent.gameObject.SetActive(true);
                    
                    // Pairing
                    if (!activeTracks.ContainsKey(simAgent.id))
                    {
                        AgentTrack track = new AgentTrack
                        {
                            trajId = trajId,
                            simAgent = simAgent,
                            gtTransform = gtObj.transform
                        };
                        simAgent.pdmMode = true;
                        activeTracks[simAgent.id] = track;
                        
                        // Immediate sync on activation
                        simAgent.ForceReset(gtObj.transform.position, gtObj.transform.rotation, Vector3.zero);
                    }
                    trajIdsToRemoveFromInactive.Add(trajId);
                }
            }
        }
        foreach(var id in trajIdsToRemoveFromInactive) inactiveSimAgentsByTrajId.Remove(id);

        // 2. Evaluation and Reset
        List<int> toRemove = new List<int>();

        foreach (var kvp in activeTracks)
        {
            int simId = kvp.Key;
            AgentTrack track = kvp.Value;

            if (track == null || track.simAgent == null || !track.simAgent.gameObject.activeInHierarchy || 
                track.gtTransform == null || !track.gtTransform.gameObject.activeInHierarchy)
            {
                toRemove.Add(simId);
                continue;
            }

            Vector3 gtPos = track.gtTransform.position;
            Vector3 simPos = track.simAgent.transform.position;

            // Goal reach check
            float distToGoal = Vector3.Distance(new Vector3(simPos.x, 0, simPos.z), 
                                                new Vector3(track.simAgent.GoalPos.x, 0, track.simAgent.GoalPos.z));
            if (distToGoal <= 1.0f)
            {
                toRemove.Add(simId);
                continue;
            }

            // Metric accumulation (XZ distance)
            float dist = Vector3.Distance(new Vector3(gtPos.x, 0, gtPos.z), new Vector3(simPos.x, 0, simPos.z));
            track.currentIntervalDiffSum += dist;
            track.currentIntervalFrameCount++;

            if (generateMap)
            {
                track.gtPath.Add(gtPos);
                track.simPath.Add(simPos);
            }

            if (isResetFrame)
            {
                fdeList.Add(dist);
                if (track.currentIntervalFrameCount > 0)
                    adeList.Add(track.currentIntervalDiffSum / track.currentIntervalFrameCount);

                Rigidbody rb = track.simAgent.GetComponent<Rigidbody>();
                Vector3 currentVel = rb != null ? rb.velocity : Vector3.zero;
                
                track.simAgent.ForceReset(gtPos, track.gtTransform.rotation, currentVel);
                
                track.currentIntervalDiffSum = 0f;
                track.currentIntervalFrameCount = 0;

                if (generateMap) track.simPath.Add(Vector3.negativeInfinity);
            }
        }

        foreach (int id in toRemove)
        {
            if (activeTracks.TryGetValue(id, out var track))
            {
                if (track != null && track.simAgent != null) track.simAgent.FinishEpisode(true);
            }
            activeTracks.Remove(id);
        }
    }

    void OnApplicationQuit()
    {
        ReportMetrics();
        if (generateMap) GenerateMap();
    }

    void ReportMetrics()
    {
        if (adeList.Count > 0)
        {
            float avgAde = adeList.Average();
            float stdAde = CalculateStdDev(adeList, avgAde);
            float ci95Ade = 1.96f * (stdAde / Mathf.Sqrt(adeList.Count));
            Debug.Log($"<color=#00FFFF>[PDM Report]</color> Final ADE: {avgAde:F4} ± {ci95Ade:F4} m (95% CI, n={adeList.Count})");
        }
        if (fdeList.Count > 0)
        {
            float avgFde = fdeList.Average();
            float stdFde = CalculateStdDev(fdeList, avgFde);
            float ci95Fde = 1.96f * (stdFde / Mathf.Sqrt(fdeList.Count));
            Debug.Log($"<color=#00FFFF>[PDM Report]</color> Final FDE: {avgFde:F4} ± {ci95Fde:F4} m (95% CI, n={fdeList.Count})");
        }
    }

    float CalculateStdDev(List<float> values, float mean)
    {
        if (values.Count <= 1) return 0;
        double sum = values.Sum(v => Math.Pow(v - mean, 2));
        return (float)Math.Sqrt(sum / values.Count);
    }

    void GenerateMap()
    {
        if (adeList.Count == 0) return;
        Texture2D texture = new Texture2D(mapResolution, mapResolution);
        for (int i = 0; i < mapResolution * mapResolution; i++) texture.SetPixel(i % mapResolution, i / mapResolution, Color.white);

        float minX = transform.position.x - mapSize / 2f;
        float minZ = transform.position.z - mapSize / 2f;

        foreach (var track in activeTracks.Values)
        {
            DrawPath(texture, track.gtPath, gtColor, minX, minZ);
            DrawPath(texture, track.simPath, agentColor, minX, minZ);
        }

        texture.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "..", mapFileName), texture.EncodeToPNG());
        Debug.Log($"[PDMManager] Visualization map saved to {mapFileName}");
    }

    // --- Drawing Utilities ---
    void DrawPath(Texture2D tex, List<Vector3> path, Color col, float minX, float minZ)
    {
        if (path.Count < 2) return;
        Vector2 prev = Vector2.zero; bool hasPrev = false;
        for (int i = 0; i < path.Count; i++)
        {
            if (float.IsNegativeInfinity(path[i].x)) { hasPrev = false; continue; }
            Vector2 cur = WorldToPixel(path[i], minX, minZ);
            if (hasPrev) DrawLine(tex, prev, cur, col);
            prev = cur; hasPrev = true;
        }
    }

    Vector2 WorldToPixel(Vector3 pos, float minX, float minZ)
    {
        float u = (pos.x - minX) / mapSize;
        float v = (pos.z - minZ) / mapSize;
        return new Vector2(u * (mapResolution - 1), v * (mapResolution - 1));
    }

    void DrawLine(Texture2D tex, Vector2 p1, Vector2 p2, Color col)
    {
        int x0 = (int)p1.x, y0 = (int)p1.y, x1 = (int)p2.x, y1 = (int)p2.y;
        int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx - dy;
        while (true)
        {
            if (x0 >= 0 && x0 < mapResolution && y0 >= 0 && y0 < mapResolution) tex.SetPixel(x0, y0, col);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }
}
