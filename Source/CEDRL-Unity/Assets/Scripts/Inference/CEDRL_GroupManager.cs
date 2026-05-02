using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CEDRL_GroupManager : MonoBehaviour
{
    public static CEDRL_GroupManager Instance;

    [Header("Logging Settings")]
    public string saveSubFolder = "MetricsLogs";
    public bool verbose = true;

    [Header("Sampling Settings")]
    [Tooltip("Sampling interval in seconds (default 0.04s = 25fps)")]
    public float samplingInterval = 0.04f;

    private List<float> allAgentToCentroidDistances = new List<float>();
    private int lastProcessedFrame = -1;
    private float _accumulatedSimTime = 0f;
    private bool hasExported = false;

    [Serializable]
    public class GroupMetricsReport
    {
        public string sceneName;
        public string date;
        public string time;
        public int totalSamples;
        public float meanDistanceToCentroid;
        public float stdDevDistanceToCentroid;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void LateUpdate()
    {
        if (Time.timeScale > 0)
        {
            _accumulatedSimTime += Time.deltaTime;
        }

        int currentFrame = Mathf.FloorToInt(_accumulatedSimTime / samplingInterval + 0.001f);
        
        if (currentFrame > lastProcessedFrame)
        {
            for (int f = lastProcessedFrame + 1; f <= currentFrame; f++)
            {
                SampleGroupDistances();
            }
            lastProcessedFrame = currentFrame;
        }
    }

    void SampleGroupDistances()
    {
        CEDRL_Agent[] allAgents = FindObjectsOfType<CEDRL_Agent>();
        if (allAgents.Length == 0) return;

        HashSet<CEDRL_Agent> processedInThisFrame = new HashSet<CEDRL_Agent>();

        foreach (var agent in allAgents)
        {
            if (agent == null || !agent.gameObject.activeInHierarchy || processedInThisFrame.Contains(agent))
                continue;
            
            if (agent.groupMembers == null || agent.groupMembers.Count == 0)
                continue;

            HashSet<CEDRL_Agent> groupSet = new HashSet<CEDRL_Agent>();
            Queue<CEDRL_Agent> queue = new Queue<CEDRL_Agent>();
            
            queue.Enqueue(agent);
            groupSet.Add(agent);

            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                if (curr.groupMembers == null) continue;

                foreach (var memberTransform in curr.groupMembers)
                {
                    if (memberTransform == null) continue;
                    
                    CEDRL_Agent memberAgent = memberTransform.GetComponent<CEDRL_Agent>();
                    if (memberAgent != null && !groupSet.Contains(memberAgent))
                    {
                        groupSet.Add(memberAgent);
                        queue.Enqueue(memberAgent);
                    }
                }
            }

            foreach (var member in groupSet)
            {
                processedInThisFrame.Add(member);
            }

            if (groupSet.Count > 1)
            {
                Vector3 centroid = Vector3.zero;
                foreach (var member in groupSet)
                {
                    centroid += member.transform.position;
                }
                centroid /= groupSet.Count;

                foreach (var member in groupSet)
                {
                    float dist = Vector2.Distance(
                        new Vector2(member.transform.position.x, member.transform.position.z), 
                        new Vector2(centroid.x, centroid.z)
                    );
                    allAgentToCentroidDistances.Add(dist);
                }
            }
        }
    }

    public void ExportToJson()
    {
        if (allAgentToCentroidDistances.Count == 0 || hasExported) return;
        hasExported = true;

        float meanDist = allAgentToCentroidDistances.Average();
        float stdDevDist = CalculateStdDev(allAgentToCentroidDistances, meanDist);

        GroupMetricsReport report = new GroupMetricsReport
        {
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            date = DateTime.Now.ToString("yyyy-MM-dd"),
            time = DateTime.Now.ToString("HH-mm-ss"),
            totalSamples = allAgentToCentroidDistances.Count,
            meanDistanceToCentroid = meanDist,
            stdDevDistanceToCentroid = stdDevDist
        };

        string folderPath = Path.Combine(Application.dataPath, "..", saveSubFolder);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string fileName = $"{report.sceneName}_{report.date}_{report.time}_GroupMetrics.json";
        string fullPath = Path.Combine(folderPath, fileName);
        
        try
        {
            File.WriteAllText(fullPath, JsonUtility.ToJson(report, true));
            
            if (verbose)
            {
                Debug.Log($"<color=#FFA500>[GroupManager]</color> Exported Group Metrics to {fullPath}");
                Debug.Log($"<color=#FFA500>[GroupManager]</color> Mean Distance to Centroid: {meanDist:F4}, Samples: {report.totalSamples}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[GroupManager] Failed to export metrics: {e.Message}");
        }
    }

    private float CalculateStdDev(List<float> values, float mean)
    {
        if (values.Count <= 1) return 0f;
        double sumOfSquares = values.Sum(v => Math.Pow(v - mean, 2));
        return (float)Math.Sqrt(sumOfSquares / values.Count);
    }

    void OnApplicationQuit()
    {
        ExportToJson();
    }
}
