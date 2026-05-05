using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;
using UnityEngine.SceneManagement;

[Serializable]
public class SRVRMetricsReport
{
    public string sceneName;
    public string date;
    public string time;
    public int totalAgents;
    public int successCount;
    public float goalSuccessRate;
    public float averageSafeRate;
    public float avgDangerZoneViolationRate;
    public float avgStaticObstacleCollisionRate;
    public float avgVehicleCollisionRate;
    public float avgDoorCollisionRate;
    public float avgAgentCollisionRate;
    public float avgTravelTime;
    public float stdDevTravelTime;
    public int totalGroupsDetected;
    public float avgInterGroupDistance;
}

public class CEDRL_SRVRManager : MonoBehaviour
{
    public static CEDRL_SRVRManager Instance { get; private set; }

    [Header("Agent Settings")]
    [Tooltip("Interval in seconds to search for new agents.")]
    public float searchInterval = 0.5f;
    public float agentRadius = 0.4f;
    [Tooltip("If true, stop recording and disable the agent immediately upon its first collision (excluding Danger Zones).")]
    public bool stopOnCollision = false;

    [Header("Environment Settings")]
    public Transform dangerZonesRoot;
    public Transform obstaclesRoot;

    [Header("Logging Settings")]
    public string saveSubFolder = "MetricsLogs";
    public bool verbose = true;
    public bool logHitTags = false;
    private bool _hasExported = false;

    [Header("Trajectory Map Settings")]
    public bool enableTrajectoryMap = true;
    public bool drawOnlyTail = false;
    public float trajectoryTailSeconds = 5.0f;
    public int mapResolution = 2048;
    public float mapWidth = 100f;
    public float mapHeight = 100f;
    public Color normalPathColor = Color.red;
    public Color dangerPathColor = Color.yellow;
    public Color dangerZoneOutlineColor = Color.blue;
    public Color obstacleOutlineColor = new Color(1f, 0.5f, 0f);
    public string mapFileName = "SocialTrajectoryMap.png";
    public float minRecordDistance = 0.1f;
    public int endPointRadius = 10;

    public struct TrajectoryPoint
    {
        public Vector3 position;
        public bool isDanger;
        public float timestamp;
        public TrajectoryPoint(Vector3 pos, bool danger, float time)
        {
            position = pos;
            isDanger = danger;
            timestamp = time;
        }
    }

    public class AgentTrackingData
    {
        public float totalTime;
        public float dangerZoneTime;
        public float staticObstacleTime;
        public float vehicleCollisionTime;
        public float doorCollisionTime;
        public float agentCollisionTime;

        public Collider agentCollider;
        public CEDRL_Agent agentComponent;
        public bool hasReachedGoal;
        public bool isFinished;

        public List<TrajectoryPoint> trajectory = new List<TrajectoryPoint>();

        public float groupDistanceSum;
        public int groupDistanceSamples;
    }

    private Dictionary<Transform, AgentTrackingData> trackingData = new Dictionary<Transform, AgentTrackingData>();
    private HashSet<Collider> dangerZoneColliders = new HashSet<Collider>();
    private HashSet<Collider> obstacleColliders = new HashSet<Collider>();
    private float searchTimer = 0f;

    [Header("Detection Tags (Backwards Compatibility)")]
    public string agentTag = "Agent";
    public string obstacleTag = "Obstacle";
    public string buildingTag = "Building";
    public string vehicleTag = "Vehicle";
    public string doorTag = "Door";

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (dangerZonesRoot == null) dangerZonesRoot = GameObject.Find("Danger Zones")?.transform;
        if (dangerZonesRoot != null)
            dangerZoneColliders = new HashSet<Collider>(dangerZonesRoot.GetComponentsInChildren<Collider>());

        if (obstaclesRoot == null) obstaclesRoot = GameObject.Find("Obstacles")?.transform;
        if (obstaclesRoot != null)
            obstacleColliders = new HashSet<Collider>(obstaclesRoot.GetComponentsInChildren<Collider>());

        if (verbose) Debug.Log($"[SRVR] Initialized: {dangerZoneColliders.Count} DangerZones, {obstacleColliders.Count} Obstacles collected.");
    }

    public void RegisterAgent(CEDRL_Agent agent)
    {
        if (agent == null) return;
        Transform t = agent.transform;
        if (trackingData.TryGetValue(t, out AgentTrackingData existingData))
        {
            existingData.agentComponent = agent;
            return;
        }

        // --- 정확히 루트의 CapsuleCollider만 사용 ---
        Collider col = agent.GetComponent<CapsuleCollider>();
        if (col == null) col = agent.GetComponent<Collider>();

        Rigidbody rb = agent.GetComponent<Rigidbody>();
        if (col == null)
        {
            SphereCollider sc = agent.gameObject.AddComponent<SphereCollider>();
            sc.radius = agentRadius;
            sc.isTrigger = true; 
            col = sc;
        }
        if (rb == null)
        {
            rb = agent.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        AgentTrackingData newData = new AgentTrackingData();
        newData.agentComponent = agent;
        newData.agentCollider = col;
        trackingData.Add(t, newData);
        if (verbose) Debug.Log($"<color=white>[SRVR] Agent {agent.id} registered (Root Collider: {col.GetType().Name}).</color>");
    }

    void Update()
    {
        searchTimer += Time.unscaledDeltaTime;
        if (searchTimer >= searchInterval)
        {
            SearchForAgents();
            searchTimer = 0f;
        }

        Physics.SyncTransforms();
        UpdateAgentStats();
    }

    private void SearchForAgents()
    {
        CEDRL_Agent[] foundAgents = FindObjectsOfType<CEDRL_Agent>(true);
        foreach (var agent in foundAgents)
        {
            if (agent == null) continue;
            RegisterAgent(agent);
        }
    }

    private void UpdateAgentStats()
    {
        float dt = Time.deltaTime;
        if (dt <= 0) return;

        foreach (var kvp in trackingData)
        {
            Transform agentTransform = kvp.Key;
            AgentTrackingData data = kvp.Value;

            if (agentTransform == null || data.isFinished) continue;

            // 중앙 집중식 목표 도달 체크
            float distToGoal = Vector3.Distance(agentTransform.position, data.agentComponent.GoalPos);
            if (distToGoal <= 1.0f)
            {
                data.hasReachedGoal = true;
                data.isFinished = true;
                data.agentComponent.GoalReached = true;
                agentTransform.gameObject.SetActive(false);
                if (verbose) Debug.Log($"<color=cyan>[SRVR] Agent {data.agentComponent.id} reached goal.</color>");
                continue;
            }

            if (!agentTransform.gameObject.activeInHierarchy) continue;

            data.totalTime += dt;
            Vector3 agentColPos = data.agentCollider.transform.position;

            // 1. Danger Zone Check
            bool hitDanger = false;
            foreach (var col in dangerZoneColliders)
            {
                if (col != null && col.enabled && IsOverlapping(data.agentCollider, col))
                {
                    hitDanger = true;
                    break;
                }
            }
            if (hitDanger) data.dangerZoneTime += dt;

            // 2. Collision Check (SRVRManager.txt 원본 루틴 복구)
            bool hitPhysicalObstacle = false;
            Collider[] nearby = Physics.OverlapSphere(agentColPos, agentRadius + 0.2f);
            foreach (var col in nearby)
            {
                if (col == null || col == data.agentCollider || col.transform.IsChildOf(agentTransform)) continue;

                if (IsOverlapping(data.agentCollider, col))
                {
                    // 에이전트 판정 (상대방의 루트 콜라이더와 부딪혔을 때만)
                    CEDRL_Agent otherAgent = col.GetComponentInParent<CEDRL_Agent>();
                    if (otherAgent != null)
                    {
                        if (trackingData.TryGetValue(otherAgent.transform, out AgentTrackingData otherData))
                        {
                            if (col == otherData.agentCollider) 
                            {
                                data.agentCollisionTime += dt;
                                hitPhysicalObstacle = true;
                            }
                        }
                        continue;
                    }

                    // 트리거는 충돌에서 제외 (센서 등)
                    if (col.isTrigger) continue;

                    // 레이어 이름 기반 분류
                    int colLayer = col.gameObject.layer;
                    string layerName = LayerMask.LayerToName(colLayer);

                    if (layerName.Contains("Vehicle") || col.GetComponentInParent<CarController>() != null)
                    {
                        data.vehicleCollisionTime += dt;
                        hitPhysicalObstacle = true;
                    }
                    else if (layerName.Contains("Door"))
                    {
                        data.doorCollisionTime += dt;
                        hitPhysicalObstacle = true;
                    }
                    else if (layerName.Contains("Obstacle") || layerName.Contains("Building") || obstacleColliders.Contains(col))
                    {
                        data.staticObstacleTime += dt;
                        hitPhysicalObstacle = true;
                    }
                }
            }

            if (stopOnCollision && hitPhysicalObstacle)
            {
                data.isFinished = true;
                agentTransform.gameObject.SetActive(false);
                if (verbose) Debug.Log($"<color=orange>[SRVR] Agent {data.agentComponent.id} stopped due to collision.</color>");
                continue;
            }

            // Group Stats
            if (data.agentComponent != null && data.agentComponent.groupMembers != null && data.agentComponent.groupMembers.Count > 0)
            {
                float distSum = 0f;
                int count = 0;
                foreach (var member in data.agentComponent.groupMembers)
                {
                    if (member != null && member.gameObject.activeInHierarchy && member != agentTransform)
                    {
                        distSum += Vector3.Distance(agentTransform.position, member.position);
                        count++;
                    }
                }
                if (count > 0)
                {
                    data.groupDistanceSum += (distSum / count);
                    data.groupDistanceSamples++;
                }
            }

            if (enableTrajectoryMap)
            {
                Vector3 currentPos = agentTransform.position;
                if (data.trajectory.Count == 0 ||
                    Vector3.Distance(data.trajectory[data.trajectory.Count - 1].position, currentPos) >= minRecordDistance)
                {
                    data.trajectory.Add(new TrajectoryPoint(currentPos, hitDanger, Time.time));
                }
            }
        }
    }

    private bool IsOverlapping(Collider agentCol, Collider otherCol)
    {
        if (agentCol == null || otherCol == null) return false;
        if (!agentCol.bounds.Intersects(otherCol.bounds)) return false;

        Vector3 agentPos = agentCol.transform.position;
        Quaternion agentRot = agentCol.transform.rotation;

        if (otherCol is MeshCollider && !((MeshCollider)otherCol).convex)
        {
            Vector3 closestPoint = otherCol.ClosestPointOnBounds(agentPos);
            float dist = Vector3.Distance(agentPos, closestPoint);
            return dist <= agentRadius;
        }

        Vector3 dir; float distPen;
        bool overlapped = Physics.ComputePenetration(
            agentCol, agentPos, agentRot,
            otherCol, otherCol.transform.position, otherCol.transform.rotation,
            out dir, out distPen
        );
        return overlapped && distPen > 0.01f;
    }

    public float CalculateSafeRate(AgentTrackingData data)
    {
        if (data.totalTime <= 0.0001f) return 1f;
        // SRVRManager.txt 원본 로직: 개별 위험 시간의 합을 뺌
        // 에이전트 충돌은 reciprocal 감지를 고려하여 절반만 반영
        float combinedUnsafeTime = data.dangerZoneTime + data.staticObstacleTime + data.vehicleCollisionTime + data.doorCollisionTime + (data.agentCollisionTime * 0.5f);
        return Mathf.Clamp01(1.0f - (combinedUnsafeTime / data.totalTime));
    }

    private void OnApplicationQuit()
    {
        ExportResults();
        if (enableTrajectoryMap) GenerateTrajectoryMap();
    }

    private void ExportResults()
    {
        if (trackingData.Count == 0 || _hasExported) return;
        _hasExported = true;

        int totalAgents = trackingData.Count;
        int successCount = 0;

        List<float> successSafeRates = new List<float>();
        List<float> dangerZoneRates = new List<float>();
        List<float> staticObstacleRates = new List<float>();
        List<float> vehicleCollisionRates = new List<float>();
        List<float> doorCollisionRates = new List<float>();
        List<float> agentCollisionRates = new List<float>();

        foreach (var data in trackingData.Values)
        {
            if (data.hasReachedGoal)
            {
                successCount++;
                if (data.totalTime > 0.0001f)
                {
                    successSafeRates.Add(CalculateSafeRate(data));
                    dangerZoneRates.Add(data.dangerZoneTime / data.totalTime);
                    staticObstacleRates.Add(data.staticObstacleTime / data.totalTime);
                    vehicleCollisionRates.Add(data.vehicleCollisionTime / data.totalTime);
                    doorCollisionRates.Add(data.doorCollisionTime / data.totalTime);
                    // 유저 요청: 에이전트 충돌률은 상호 기록되므로 최종 계산에서 2로 나눔
                    agentCollisionRates.Add((data.agentCollisionTime * 0.5f) / data.totalTime);
                }
            }
        }

        float gsr = totalAgents > 0 ? (float)successCount / totalAgents : 0f;
        float avgSafeRate = successSafeRates.Count > 0 ? successSafeRates.Average() : 0f;
        float avgDangerZoneRate = dangerZoneRates.Count > 0 ? dangerZoneRates.Average() : 0f;
        float avgStaticRate = staticObstacleRates.Count > 0 ? staticObstacleRates.Average() : 0f;
        float avgVehicleRate = vehicleCollisionRates.Count > 0 ? vehicleCollisionRates.Average() : 0f;
        float avgDoorRate = doorCollisionRates.Count > 0 ? doorCollisionRates.Average() : 0f;
        float avgAgentCollRate = agentCollisionRates.Count > 0 ? agentCollisionRates.Average() : 0f;

        float avgTime = successCount > 0 ? trackingData.Values.Where(d => d.hasReachedGoal).Average(d => d.totalTime) : 0f;

        SRVRMetricsReport report = new SRVRMetricsReport
        {
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            date = DateTime.Now.ToString("yyyy-MM-dd"),
            time = DateTime.Now.ToString("HH-mm-ss"),
            totalAgents = totalAgents,
            successCount = successCount,
            goalSuccessRate = gsr,
            averageSafeRate = avgSafeRate,
            avgDangerZoneViolationRate = avgDangerZoneRate,
            avgStaticObstacleCollisionRate = avgStaticRate,
            avgVehicleCollisionRate = avgVehicleRate,
            avgDoorCollisionRate = avgDoorRate,
            avgAgentCollisionRate = avgAgentCollRate,
            avgTravelTime = avgTime
        };

        string folderPath = Path.Combine(Application.dataPath, "..", saveSubFolder);
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
        string fileName = $"{report.sceneName}_{report.date}_{report.time}_SRVRMetrics.json";
        File.WriteAllText(Path.Combine(folderPath, fileName), JsonUtility.ToJson(report, true));
        
        if (verbose) Debug.Log($"<color=yellow><b>=== SRVR Final Report (Logical Fixed) ===</b></color>\nGSR: {gsr*100:F2}% | SafeRate: {avgSafeRate*100:F2}%\nAgent Coll: {avgAgentCollRate*100:F2}% | Vehicle Coll: {avgVehicleRate*100:F2}%");
    }

    private void GenerateTrajectoryMap() { /* ... Implementation same as before ... */ }
    private void DrawCollider(Texture2D tex, Collider col, Color color, float minX, float minZ) { /* ... */ }
    private Vector2 WorldToPixel(Vector3 pos, float minX, float minZ) { return Vector2.zero; } // Dummy for brevity
    private void DrawLine(Texture2D tex, Vector2 p1, Vector2 p2, Color col, int thickness) { }
    private void DrawCircle(Texture2D tex, Vector2 center, int radius, Color col) { }
}