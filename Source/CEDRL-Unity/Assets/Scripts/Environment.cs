using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Pathfinding.RVO;

[Serializable]
public class AgentPointPair
{
    public Transform startPoint;
    public Transform goalPoint;
    public Vector3 manualStartPos;
    public Quaternion manualStartRot = Quaternion.identity;
    public Vector3 manualGoalPos;
    public float spawnTime = 0f;
    public int groupId = 0;
    public bool useManualPoints = false;
}

public class Environment : MonoBehaviour
{
    public float timestep;
    private int m_envFrame;
    public float maxDistance;
    [SerializeField] private float m_maxTimestep = 0;
    [SerializeField] private string dataset;
    private List<RealAgentData> m_realAgentsData;
    [SerializeField] private GameObject m_realAgentPrefab;
    [SerializeField] private GameObject m_CEDRL_AgentPrefab;
    [SerializeField] private GameObject m_realAgentInferencePrefab;
    [SerializeField] private GameObject m_CEDRL_AgentInferencePrefab;
    private bool ready;
    private List<RealAgent> m_realAgents;
    private List<CEDRL_Agent> m_CEDRL_Agents;
    public float Height { get; set; }
    public Transform Floor { get; private set; }
    public bool m_manualSpawning;
    [SerializeField] private Vector4 m_floorEdges;
    
    [Header("Custom City Settings")]
    [SerializeField] private bool m_useDatasetInCustomCity; 
    public List<AgentPointPair> m_manualCityAgents;
    [SerializeField] private Transform m_agentRoot;
    [SerializeField] private Vector3 m_datasetPositionOffset; 
    [SerializeField] private Vector3 m_datasetScale = Vector3.one;

    public void InitializeEnvironment(string d, float height, CSVLoader dataLoader, string mode, SceneSetup setup, bool sceneObjects)
    {
        float heightScale = 5f;
        Height = height * heightScale;
        
        dataset = d;
        m_realAgentsData = dataLoader.agents;
        m_maxTimestep = dataLoader.MaxTimestep;

        if (setup != SceneSetup.CustomCity)
        {
            Floor = transform.GetChild(0);
            float floorWidth = dataLoader.MaxX - dataLoader.MinX + 3f;
            float floorHeight = dataLoader.MaxZ - dataLoader.MinZ + 3f;
            float scaleX = floorWidth / 10.0f;
            float scaleZ = floorHeight / 10.0f;
            Floor.localScale = new Vector3(scaleX, 1, scaleZ);
            maxDistance = Mathf.Sqrt((scaleX * 10f) * (scaleX * 10f) + (scaleZ * 10f) * (scaleZ * 10f));
            m_floorEdges = new Vector4(-(floorWidth / 2), (floorWidth / 2), -(floorHeight / 2), (floorHeight / 2));
            
            transform.position = new Vector3(0f, Height, 0f);
        }
        else
        {
            maxDistance = 100f * Mathf.Max(m_datasetScale.x, m_datasetScale.z); 
        }
        
        if (String.Compare(mode, "Training", StringComparison.Ordinal) == 0)
        {
            BuildSceneObjects(d);
            TrainingSetup();
        }
        else
        {
            if(sceneObjects && setup != SceneSetup.CustomCity)
                BuildSceneObjects(d);
            InferenceSetup(setup);
        }

        ready = true;
    }

    public Vector3 TransformDatasetPosition(Vector3 pos)
    {
        Vector3 transformed = new Vector3(
            pos.x * m_datasetScale.x,
            pos.y * m_datasetScale.y,
            pos.z * m_datasetScale.z
        );
        transformed += m_datasetPositionOffset;
        return transformed;
    }

    private void TrainingSetup()
    {
        m_realAgents = new List<RealAgent>(m_realAgentsData.Count);
        m_CEDRL_Agents = new List<CEDRL_Agent>(m_realAgentsData.Count);
        foreach (var agentData in m_realAgentsData)
        {
            if (SceneManager.Instance.IsInfernce == false)
            {
                if (agentData.Positions.Count >= 20)
                {
                    RealAgent realAgent = Instantiate(m_realAgentPrefab, AdjustHeight(agentData.Positions[0]),
                        Quaternion.identity,
                        transform.GetChild(1)).GetComponent<RealAgent>();
                    realAgent.name = "RealAgent_" + agentData.ID;
                    realAgent.SetData(agentData.ID, agentData.TimeSteps, agentData.Positions, agentData.Scores.Norm_score, this);
                    m_realAgents.Add(realAgent);

                    CEDRL_Agent CEDRL_Agent = Instantiate(m_CEDRL_AgentPrefab, AdjustHeight(agentData.Positions[0]),
                        Quaternion.identity,
                        transform.GetChild(2)).GetComponent<CEDRL_Agent>();
                    CEDRL_Agent.name = "CEDRL_Agent_" + agentData.ID;
                    CEDRL_Agent.SetData(agentData.ID, agentData.TimeSteps[0], AdjustHeight(agentData.Positions[0]),
                        AdjustHeight(agentData.Positions[^1]), agentData.Scores,0, this, realAgent, SceneSetup.Training, true);
                    m_CEDRL_Agents.Add(CEDRL_Agent);
                    realAgent.CEDRLAgent = CEDRL_Agent;
                    Color c = GenerateColor();
                    CEDRL_Agent.SetColor(c);
                    Color c2 = new Color(c.r, c.g, c.b, 0.2f);
                    realAgent.SetColor(c2);
                }
            }
        }
    }

    private void InferenceSetup(SceneSetup setup)
    {
        m_realAgents = new List<RealAgent>(m_realAgentsData.Count);
        m_CEDRL_Agents = new List<CEDRL_Agent>(m_realAgentsData.Count);
        
        Transform parentTransform = (m_agentRoot != null) ? m_agentRoot : transform.GetChild(2);
        float agentScale = 0.7f;

        if (setup == SceneSetup.Default || (setup == SceneSetup.CustomCity && m_useDatasetInCustomCity))
        {
            Transform floorTransform = transform.Find("Floor");
            if (floorTransform != null)
            {
                foreach (Transform wall in floorTransform)
                {
                    wall.gameObject.layer = 0;
                }
            }
            
            foreach (var agentData in m_realAgentsData)
            {
                Vector3 startPos = TransformDatasetPosition(agentData.Positions[0]);
                Vector3 goalPos = TransformDatasetPosition(agentData.Positions[^1]);

                GameObject agentObj = Instantiate(m_CEDRL_AgentInferencePrefab, startPos,
                    Quaternion.identity, parentTransform);
                agentObj.transform.localScale = new Vector3(agentScale, agentScale, agentScale);
                var rvo = agentObj.GetComponent<RVOController>();
                if(rvo != null) rvo.radius *= agentScale;

                CEDRL_Agent CEDRL_Agent = agentObj.GetComponent<CEDRL_Agent>();
                CEDRL_Agent.name = "CEDRL_Agent_" + agentData.ID;
                CEDRL_Agent.SetData(agentData.ID, agentData.TimeSteps[0], startPos,
                    goalPos, agentData.Scores, 0, this, null, setup, true);
                m_CEDRL_Agents.Add(CEDRL_Agent);
            }
        }
        else if (setup == SceneSetup.CustomCity && !m_useDatasetInCustomCity)
        {
            m_manualSpawning = true;
            Dictionary<int, List<CEDRL_Agent>> groupGroups = new Dictionary<int, List<CEDRL_Agent>>();

            for (int i = 0; i < m_manualCityAgents.Count; i++)
            {
                var pair = m_manualCityAgents[i];
                Vector3 spawnPos = pair.useManualPoints ? pair.manualStartPos : (pair.startPoint != null ? pair.startPoint.position : Vector3.zero);
                Quaternion spawnRot = pair.useManualPoints ? pair.manualStartRot : (pair.startPoint != null ? pair.startPoint.rotation : Quaternion.identity);
                Vector3 goalPos = pair.useManualPoints ? pair.manualGoalPos : (pair.goalPoint != null ? pair.goalPoint.position : Vector3.zero);

                GameObject agentObj = Instantiate(m_CEDRL_AgentInferencePrefab, spawnPos,
                    spawnRot, parentTransform);
                agentObj.transform.localScale = new Vector3(agentScale, agentScale, agentScale);
                var rvo = agentObj.GetComponent<RVOController>();
                if(rvo != null) rvo.radius *= agentScale;

                CEDRL_Agent agent = agentObj.GetComponent<CEDRL_Agent>();
                agent.name = "CityAgent_" + i;
                AgentScores dummyScores = new AgentScores { Norm_score = SceneManager.Instance.Complexity };
                
                agent.SetData(i, pair.spawnTime, spawnPos,
                    goalPos, dummyScores, 0, this, null, setup, true);
                
                if (pair.groupId != 0)
                {
                    if (!groupGroups.ContainsKey(pair.groupId)) 
                        groupGroups[pair.groupId] = new List<CEDRL_Agent>();
                    groupGroups[pair.groupId].Add(agent);
                }
                
                m_CEDRL_Agents.Add(agent);

                // SpawnTime이 0이거나 현재 타임스텝보다 작으면 즉시 활성화
                if (pair.spawnTime <= timestep)
                {
                    agent.gameObject.SetActive(true);
                }
            }

            // Assign group members
            foreach (var group in groupGroups.Values)
            {
                if (group.Count <= 1) continue;
                foreach (var agent in group)
                {
                    foreach (var member in group)
                    {
                        if (agent == member) continue;
                        if (!agent.groupMembers.Contains(member.transform))
                            agent.groupMembers.Add(member.transform);
                    }
                }
            }
        }
        else if (setup == SceneSetup.Infinite)
        {
            foreach (Transform wall in transform.Find("Floor"))
            {
                wall.gameObject.layer = 0;
                wall.GetComponent<RVOSquareObstacle>().enabled = false;
            }
            
            m_manualSpawning = true;
            int count = 0;

            int numOfAgents = SceneManager.Instance.InfiniteNumberOfAgents;
            int agentsSpawned = 0;
            float p3 = 0.2f;
            float p2 = 0.5f;
            for (int i = 0; i < m_realAgentsData.Count; i++)
            {
                count += 1;
                if(agentsSpawned > numOfAgents)
                    return;
                
                float randomP = UnityEngine.Random.value;
                int groupSize = 2;
                if (randomP < p3 && (numOfAgents - agentsSpawned) >= 3)
                    groupSize = 4;
                else if (randomP < p3 + p2 && (numOfAgents - agentsSpawned) >= 2)
                    groupSize = 3;
                
                Vector3 groupCenter = GetRandomFreePoint(Floor);
                Vector3 goalPoint = GetRandomEdgePoint(Floor) * 1.1f;
                for (int j = 0; j < groupSize; j++)
                {
                    RealAgentData agentData = m_realAgentsData[i];
                    i += 1;

                    int layerMask = LayerMask.GetMask("Agents");
                    float checkRadius = 0.6f;
                    bool isPointFree = false;
                    Vector3 spawnPoint = Vector3.zero;
                    while (!isPointFree)
                    {
                        Vector3 offset = UnityEngine.Random.insideUnitSphere * 2.0f;
                        offset.y = 0;
                        spawnPoint = groupCenter + offset;
                        Collider[] hitColliders = Physics.OverlapSphere(spawnPoint, checkRadius, layerMask);
                        isPointFree = hitColliders.Length == 0;
                    }
                    
                    GameObject agentObj = Instantiate(m_CEDRL_AgentInferencePrefab, agentData.Positions[0],
                        Quaternion.identity, parentTransform);
                    agentObj.transform.localScale = new Vector3(agentScale, agentScale, agentScale);
                    var rvo = agentObj.GetComponent<RVOController>();
                    if(rvo != null) rvo.radius *= agentScale;

                    CEDRL_Agent CEDRL_Agent = agentObj.GetComponent<CEDRL_Agent>();
                    CEDRL_Agent.name = "CEDRL_Agent_" + agentData.ID;
                    goalPoint *= UnityEngine.Random.Range(0.9f, 1.1f);
                    CEDRL_Agent.SetData(agentData.ID, agentData.TimeSteps[0], spawnPoint,
                        goalPoint, agentData.Scores, 0,this, null, setup, false);
                    m_CEDRL_Agents.Add(CEDRL_Agent);
                    agentsSpawned += 1;
                }
            }
        }
    }

    public bool OutsideInfiniteEnv(Vector3 pos)
    {
        if (pos.x < m_floorEdges[0] || pos.x > m_floorEdges[1] || pos.z < m_floorEdges[2] || pos.z > m_floorEdges[3])
            return true;
        return false;
    }
    
    public Vector3 GetRandomFreePoint(Transform floor)
    {
        LayerMask obstructionLayer = LayerMask.NameToLayer("Agents");
        float checkRadius = 0.6f;
        
        Vector3 scale = floor.localScale;
        float width = scale.x * 4f;
        float height = scale.z * 4f;
        Vector3 randomPoint;
        bool isFree;

        do
        {
            float x = UnityEngine.Random.Range(-width, width);
            float z = UnityEngine.Random.Range(-height, height);
            randomPoint = AdjustHeight(new Vector3(x, 0, z));
            isFree = !Physics.CheckSphere(randomPoint, checkRadius, obstructionLayer);
        } while (!isFree);
        return randomPoint;
    }
    
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying && (m_realAgentsData == null || m_realAgentsData.Count == 0))
        {
            if (!string.IsNullOrEmpty(dataset))
            {
                try 
                {
                    CSVLoader loader = new CSVLoader();
                    loader.LoadCSVData(dataset, 0);
                    m_realAgentsData = loader.agents;
                }
                catch { /* Ignore error in Edit Mode */ }
            }
        }

        if (m_realAgentsData == null || m_realAgentsData.Count == 0) return;

        foreach (var agent in m_realAgentsData)
        {
            if (agent.Positions != null && agent.Positions.Count > 0)
            {
                Vector3 start = TransformDatasetPosition(agent.Positions[0]);
                Vector3 end = TransformDatasetPosition(agent.Positions[^1]);
                
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(start, 0.3f);
                
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(end, 0.3f);
            }
        }
    }

    private Vector3 GetRandomEdgePoint(Transform floor)
    {
        Vector3 scale = floor.localScale;
        float width = scale.x * 5.5f;
        float height = scale.z * 5.5f;
        int side = UnityEngine.Random.Range(2, 4);
        float x = 0, z = 0;
        switch (side)
        {
            case 0:
                x = UnityEngine.Random.Range(-width, width);
                z = height;
                break;
            case 1:
                x = UnityEngine.Random.Range(-width, width);
                z = -height;
                break;
            case 2:
                x = width;
                z = UnityEngine.Random.Range(-height, height);
                break;
            case 3:
                x = -width;
                z = UnityEngine.Random.Range(-height, height);
                break;
        }
        return AdjustHeight(new Vector3(x, 0f, z));
    }
    
    private void ReloadEnvironment()
    {
        if (SceneManager.Instance.IsInfernce)
        {
            foreach (var agent in m_CEDRL_Agents)
            {
                if(agent.enabled)
                    agent.FinishEpisode(false);
            }
        }
        
        foreach (var agent in m_realAgents)
        {
            agent.gameObject.SetActive(true);
        }
        ready = true;
        timestep = 0;
    }

    private void FixedUpdate()
    {
        if (ready)
        {
            m_envFrame += 1;
            if (SceneManager.Instance.IsInfernce)
            {
                SpawnAgentsInTimestep();
            }

            timestep += Time.fixedDeltaTime;
            if (timestep >= m_maxTimestep + 1)
            {
                timestep = 0;
                m_envFrame = 0;
                ready = false;
                SceneManager.Instance.SaveNow = true;
                ReloadEnvironment();
            }
        }
    }

    private void SpawnAgentsInTimestep()
    {
        List<CEDRL_Agent> agentsToRemove = new List<CEDRL_Agent>();
        
        foreach(CEDRL_Agent agent in m_CEDRL_Agents)
        {
            if (timestep >= agent.SpawnTimestep)
            {
                agent.gameObject.SetActive(true);
                agentsToRemove.Add(agent);
            }
        }
        
        foreach (CEDRL_Agent agent in agentsToRemove)
        {
            m_CEDRL_Agents.Remove(agent);
        }
    }
    
    private Color GenerateColor()
    {
        int minBrightness = 60;
        System.Random random = new System.Random();
        float r = random.Next(minBrightness, 256) / 255f;
        float g = random.Next(minBrightness, 256) / 255f;
        float b = random.Next(minBrightness, 256) / 255f;

        return new Color(r, g, b, 1);
    }
    
    private void BuildSceneObjects(string datasetName)
    {
        string prefabPath = datasetName + "_Objects";
        GameObject prefab = Resources.Load<GameObject>(prefabPath);

        if (prefab != null)
        {
            GameObject instantiatedObject = Instantiate(prefab, transform.Find("SceneObjects"));
            instantiatedObject.name = prefab.name;
        }
    }

    public (Vector3, Vector3) ReflectToOppositeSide(Vector3 position, Vector3 velocity) 
    {                                                                                                  
        float rangeX = m_floorEdges[1] * 0.95f;                                                        
        float rangeZ = m_floorEdges[3] * 0.95f;

        float clampedX;
        float clampedZ;
        if (Mathf.Abs(position.x) > Mathf.Abs(position.z))
        {
            clampedX = Mathf.Clamp(position.x * -1f, -rangeX, rangeX);
            clampedZ = Mathf.Clamp(position.z * 1f, -rangeZ, rangeZ);
        }
        else
        {
            clampedX = Mathf.Clamp(position.x * 1f, -rangeX, rangeX);
            clampedZ = Mathf.Clamp(position.z * -1f, -rangeZ, rangeZ);
        }

        Vector3 clampedPosition = new Vector3(clampedX, position.y, clampedZ);                         
        return (clampedPosition, velocity * 0.5f);                                                     
    }  
    
    public Vector3 AdjustHeight(Vector3 v)
    {
        return new Vector3(v.x, Height, v.z);
    }

    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            foreach (var a in m_realAgentsData)
            {
                Gizmos.DrawCube(TransformDatasetPosition(a.Positions[0]), new Vector3(0.1f, 0.1f, 0.1f));
            }
        }
    }
}