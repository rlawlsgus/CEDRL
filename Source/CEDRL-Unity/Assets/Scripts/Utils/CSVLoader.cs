using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

public class AgentScores
{
    public float Score1 { get; set; }
    public float Speed_dir { get; set; }
    public float Goal_dev { get; set; }
    public float Gr { get; set; }
    public float Norm_score { get; set; }
}

public class RealAgentData
{
    public int ID { get; set; }
    public List<Vector3> Positions { get; private set; }
    public List<float> TimeSteps { get; private set; }
    public AgentScores Scores { get; private set; }

    
    public RealAgentData(int id, AgentScores scores)
    {
        ID = id;
        Positions = new List<Vector3>();
        TimeSteps = new List<float>();
        Scores = scores;
    }
}

public class CSVLoader
{
    public float MaxTimestep { get; private set; } = float.MinValue;
    public float MinX { get; private set; } = float.MaxValue;
    public float MaxX { get; private set; } = float.MinValue;
    public float MinZ { get; private set; } = float.MaxValue;
    public float MaxZ { get; private set; } = float.MinValue;
    public float TotalX { get; private set; } = 0;
    public float TotalZ { get; private set; } = 0;
    public float AverageX { get; private set; } = 0;
    public float AverageZ { get; private set; } = 0;
    public int TotalPoints { get; private set; } = 0;

    public List<RealAgentData> agents;
    
    public void LoadCSVData(string dataset, float height)
    {
        agents = new List<RealAgentData>();
        string folderPath = Path.Combine(Application.streamingAssetsPath, "Datasets", dataset, "Trajectories");
        string scoresPath = Path.Combine(Application.streamingAssetsPath, "Datasets", dataset, "Metadata", "scores.json");
        
        // Read JSON scores file and deserialize it
        Dictionary<string, AgentScores> scoresDict = new Dictionary<string, AgentScores>();
        if (File.Exists(scoresPath))
        {
            string jsonContent = File.ReadAllText(scoresPath);
            scoresDict = JsonConvert.DeserializeObject<Dictionary<string, AgentScores>>(jsonContent);
        }

        string[] filePaths = Directory.GetFiles(folderPath, "*.csv");
        foreach (string filePath in filePaths)
        {
            string fileName = Path.GetFileName(filePath);
            int id = int.Parse(fileName.Split('_')[1].Split('.')[0]);
            
            AgentScores scores = scoresDict[fileName];
            scores.Norm_score = Mathf.Clamp01(scores.Norm_score);

            RealAgentData agent = new RealAgentData(id, scores);
            string[] lines = File.ReadAllLines(filePath);

            foreach (string line in lines)
            {
                string[] values = line.Split(';');
                if (values.Length >= 3)
                {
                    float timeStep = float.Parse(values[0]);
                    float posX = float.Parse(values[1]);
                    float posZ = float.Parse(values[2]);

                    // Update min/max values and total sum for average calculation
                    MaxTimestep = Mathf.Max(MaxTimestep, timeStep);
                    MinX = Mathf.Min(MinX, posX);
                    MaxX = Mathf.Max(MaxX, posX);
                    MinZ = Mathf.Min(MinZ, posZ);
                    MaxZ = Mathf.Max(MaxZ, posZ);
                    TotalX += posX;
                    TotalZ += posZ;
                    TotalPoints++;
                    
                    agent.TimeSteps.Add(timeStep);
                    agent.Positions.Add(new Vector3(posX, height, posZ));
                }
            }

            agents.Add(agent);
        }

        // Calculate average position
        AverageX = TotalX / TotalPoints;
        AverageZ = TotalZ / TotalPoints;

        float xScale = 1f;
        if (dataset.Contains("Flock"))
            xScale = 1.35f;
        
        // Adjust the positions of each agent
        foreach (RealAgentData agent in agents)
        {
            for (int i = 0; i < agent.Positions.Count; i++)
            {
                Vector3 position = agent.Positions[i];
                float adjustedX = position.x - (xScale * AverageX);
                float adjustedZ = position.z - AverageZ;
                agent.Positions[i] = new Vector3(adjustedX, position.y, adjustedZ);
            }
        }
    }
}