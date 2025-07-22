using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.UI;

public enum SceneSetup 
{
    Training,
    Default,
    Infinite
}

public enum DatasetSelector
{
    Zara01,
    Zara02,
    Zara03,
    Students01,
    Students03,
    Flock
}

public class SceneManager : Singleton<SceneManager>
{
    [Header("Training")]
    [SerializeField] private List<string> datasets;
    [SerializeField] private int m_quanity;
    [SerializeField] private GameObject m_envPrefab;
    public float FadingFactor;
    [Header("Inference")]
    public bool IsInfernce;
    public SceneSetup Setup;
    public DatasetSelector selectedDataset;
    public int InfiniteNumberOfAgents;
    [SerializeField] private bool m_spawnSceneObjects;
    public bool ManualComplexity;
    [Range(0.01f, 1.0f)] public float Complexity;
    [SerializeField] private Text m_complexityText;
    private int m_datasetIndex;
    private Dictionary<string, CSVLoader> m_loadedDatasets;
    private StatsRecorder StatsRecorder { get; set; }
    [Header("Saving")]
    public bool SaveTrajectories;
    public bool SaveNow;
    private string m_savePath;

    // Start is called before the first frame update
    public override void Awake()
    {
        StatsRecorder = Academy.Instance.StatsRecorder;
        FadingFactor = 1f;
        
        datasets = new List<string>(4);
        // Testing
        //datasets.Add("Flock");
        //datasets.Add("Zara03");
        // Training
        if (IsInfernce == false)
        {
            datasets.Add("Zara01");
            datasets.Add("Students03");
            datasets.Add("Zara02");
            datasets.Add("Students01");
        }
        else
        {
            switch (selectedDataset)
            {
                case DatasetSelector.Zara01:
                    datasets.Add("Zara01");
                    break;
                case DatasetSelector.Zara02:
                    datasets.Add("Zara02");
                    break;
                case DatasetSelector.Zara03:
                    datasets.Add("Zara03");
                    break;
                case DatasetSelector.Students01:
                    datasets.Add("Students01");
                    break;
                case DatasetSelector.Students03:
                    datasets.Add("Students03");
                    break;
                case DatasetSelector.Flock:
                    datasets.Add("Flock");
                    break;
                
            }
        }

        m_loadedDatasets = new Dictionary<string, CSVLoader>(datasets.Count);
        foreach (var d in datasets)
        {
            if(IsInfernce && m_loadedDatasets.Count >= m_quanity)
                continue;
            if(m_loadedDatasets.ContainsKey(d))
                continue;
            CSVLoader dataLoader = new CSVLoader();
            dataLoader.LoadCSVData(d, 0);
            m_loadedDatasets.Add(d, dataLoader);
        }

        // SET SAVE PATH BELOW
        if (SaveTrajectories)
        {
            string assetsPath = Application.dataPath;
            string projectPath = Directory.GetParent(assetsPath).FullName;
            string parentDir = Directory.GetParent(projectPath).FullName;
            m_savePath = parentDir + "/Analysis/Datasets/" + datasets[0];
            Directory.CreateDirectory(m_savePath);
        }
    }

    private void Start()
    {
        SpawnEnvironments();
    }

    private void SpawnEnvironments()
    {
        for (int i = 0; i < m_quanity; i++)
        {
            Environment env = Instantiate(m_envPrefab, Vector3.zero, Quaternion.identity, transform).GetComponent<Environment>();
            string dataset = datasets[m_datasetIndex % datasets.Count];
            env.name = "Env_" + i + "_" + dataset;
            if(IsInfernce == false)
                env.InitializeEnvironment(dataset, -i, m_loadedDatasets[dataset], "Training", SceneSetup.Training, true);
            else
                env.InitializeEnvironment(dataset, -i, m_loadedDatasets[dataset], "Inference", Setup, m_spawnSceneObjects);
            m_datasetIndex += 1;
        }
    }

    private void FixedUpdate()
    {
        if (IsInfernce == false)
        {
            FadingFactor = Academy.Instance.EnvironmentParameters.GetWithDefault("fading_step", 1.0f);
            StatsRecorder.Add("ObservationFadingStep", FadingFactor);
        }else{
            if(ManualComplexity)
                m_complexityText.text = "Complexity: " + Complexity;
            else
                m_complexityText.text = "";
        }
    }

    public void SaveAgentData(string agent, List<SaveData> data, Vector3 goalPos)
    {
        if(data.Count < 20)
            return;

        float xOffset = m_loadedDatasets.First().Value.AverageX;
        float zOffset = m_loadedDatasets.First().Value.AverageZ;
        
        string filePath = m_savePath + "/" + agent + ".csv";
        StreamWriter writer = new StreamWriter(filePath);
        for (int i = 5; i < data.Count; i += 1)
        {
            string row = Math.Round(data[i].timestep,2) + ";" + Math.Round(data[i].position.x + xOffset,3) + 
                         ";" + Math.Round(data[i].position.z + zOffset,3);
            writer.WriteLine(row);
        }
        string row_goal = Math.Round(data[^1].timestep + 0.04f,2) + ";" + Math.Round(goalPos.x + xOffset,3) + ";" + Math.Round(goalPos.z + zOffset,3) + ";" + Math.Round(data[0].complexity,3);
        writer.WriteLine(row_goal);
        
        writer.Flush();
        writer.Close();
    }
}
