using UnityEngine;
using System.Collections.Generic;
using System;

public class CEDRL_ScenarioSpawner : MonoBehaviour
{
    [Serializable]
    public class AgentScenarioData
    {
        public string name = "Agent";
        public Vector2 startPos; // 2D planar position
        public float startRotation; // 2D planar rotation (degrees)
        public Vector2 goalPos;  // 2D planar position
        public int groupId = 0;
        public Color debugColor = Color.cyan;
    }

    [Serializable]
    public class ScenarioExportData
    {
        public float worldHeight;
        public List<AgentScenarioData> agents;
    }

    [Header("Settings")]
    public GameObject agentPrefab;
    [Tooltip("Drag and drop the scenario JSON file here.")]
    public TextAsset scenarioJsonFile;
    public bool registerOnStart = true;

    [Header("Runtime Info")]
    private ScenarioExportData lastGizmoData;
    private TextAsset lastJsonFile;
    private Environment m_env;

    private void Start()
    {
        m_env = FindObjectOfType<Environment>();
        if (registerOnStart)
        {
            RegisterToEnvironment();
        }
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying || scenarioJsonFile == null) return;

        if (lastGizmoData == null || lastJsonFile != scenarioJsonFile)
        {
            try
            {
                lastGizmoData = JsonUtility.FromJson<ScenarioExportData>(scenarioJsonFile.text);
                lastJsonFile = scenarioJsonFile;
            }
            catch
            {
                return;
            }
        }

        if (lastGizmoData == null || lastGizmoData.agents == null) return;

        float y = lastGizmoData.worldHeight;
        foreach (var agent in lastGizmoData.agents)
        {
            Vector3 s = new Vector3(agent.startPos.x, y, agent.startPos.y);
            Vector3 g = new Vector3(agent.goalPos.x, y, agent.goalPos.y);

            Gizmos.color = agent.debugColor;
            Gizmos.DrawLine(s, g);
            Gizmos.DrawWireSphere(s, 0.3f);
            Gizmos.DrawLine(s, s + Vector3.up * 1.5f);

            Vector3 forward = Quaternion.Euler(0, agent.startRotation, 0) * Vector3.forward;
            Gizmos.DrawLine(s, s + forward * 0.8f);
            Gizmos.DrawWireSphere(s + forward * 0.8f, 0.05f);

            Color goalColor = agent.debugColor;
            goalColor.a = 0.5f;
            Gizmos.color = goalColor;
            Gizmos.DrawWireCube(g, new Vector3(0.5f, 0.1f, 0.5f));
            Gizmos.DrawLine(g, g + Vector3.up * 0.5f);
        }
    }

    [ContextMenu("Register To Environment")]
    public void RegisterToEnvironment()
    {
        if (scenarioJsonFile == null) return;
        if (m_env == null) m_env = FindObjectOfType<Environment>();
        if (m_env == null)
        {
            Debug.LogError("[ScenarioSpawner] Environment not found!");
            return;
        }

        string json = scenarioJsonFile.text;
        ScenarioExportData data = JsonUtility.FromJson<ScenarioExportData>(json);

        if (data == null || data.agents == null) return;

        float y = data.worldHeight;
        foreach (var agentData in data.agents)
        {
            Vector3 spawnPos = new Vector3(agentData.startPos.x, y, agentData.startPos.y);
            Vector3 goalPos = new Vector3(agentData.goalPos.x, y, agentData.goalPos.y);
            Quaternion spawnRot = Quaternion.Euler(0, agentData.startRotation, 0);

            AgentPointPair pair = new AgentPointPair
            {
                manualStartPos = spawnPos,
                manualStartRot = spawnRot,
                manualGoalPos = goalPos,
                spawnTime = 0f,
                groupId = agentData.groupId,
                useManualPoints = true
            };

            m_env.m_manualCityAgents.Add(pair);
        }
        
        Debug.Log($"[ScenarioSpawner] Registered {data.agents.Count} agents to Environment.");
    }
}