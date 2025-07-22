using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Pathfinding.RVO;

public class SaveData
{
    public float timestep;
    public Vector3 position;
    public float complexity;
}

public class CEDRL_Agent : Unity.MLAgents.Agent
{
    public int id;
    
    [Header("Movement")]
    [Tooltip("Maximum movement speed")]
    [SerializeField] private float m_moveSpeed = 2.25f;
    [Tooltip("Maximum turn speed in degrees per second")]
    [SerializeField] private float m_turnSpeed = 90f;

    [Header("Training")]
    [Tooltip("The real agent to be imitated")]
    [SerializeField] private RealAgent m_realAgent;
    [Tooltip("The grid sensor component for observations")]
    [SerializeField] private GridSensorComponent m_gridSensor;
    [Tooltip("The ray perception sensor component for observations")]
    [SerializeField] private RayPerceptionSensorComponentBase m_raySensor;
    [Tooltip("The complexity of the current task")]
    [SerializeField] private float m_complexity;
    [Tooltip("The quality of the imitation")]
    [SerializeField] private float m_imitationQuality;
    [Tooltip("Whether to receive observations from the real agent")]
    public bool receiveRealObs;

    [Header("Debug")]
    [Tooltip("The current speed of the agent")]
    [SerializeField] private float m_currentSpeed;
    [Tooltip("The starting position of the agent")]
    [SerializeField] private Vector3 m_startingPos;
    [Tooltip("The goal position of the agent")]
    [SerializeField] private Vector3 m_goalPos;
    [Tooltip("The distance to the goal")]
    [SerializeField] private float m_goalDistance;
    [Tooltip("The angle to the goal")]
    [SerializeField] private float m_goalAngle;
    [Tooltip("The current scene setup")]
    [SerializeField] private SceneSetup m_setup;
    [Tooltip("The number of frames that have passed in the current episode")]
    [SerializeField] private int m_frameCounter;
    
    public Vector3 CurrentVelocity { get; private set; }
    public float SpawnTimestep { get; set; }
    public float DrawSpeed { get; set; }

    private int m_episode = 0;
    private float m_initialGoalDistance;
    private float m_initialSpeed = 0;
    private Rigidbody m_rb;
    private SceneManager m_manager;
    private Environment m_env;
    private RVOController m_rvoController;
    private Color m_color;
    private TrailRenderer m_trailRenderer;
    private Visuals m_visuals;
    private float m_previousMoveInput;
    private float m_previousTurnAmount;
    private Vector3 m_lastPos;
    private List<SaveData> m_saveList;
    private bool m_endedFromTime;

    /// <summary>
    /// Sets the initial data for the agent.
    /// </summary>
    public void SetData(int agentId, float spawnTime, Vector3 spawn, Vector3 goal, AgentScores scores, 
        float initialSpeed, Environment env, RealAgent real, SceneSetup setup, bool disable)
    {
        m_env = env;
        id = agentId;
        SpawnTimestep = spawnTime;
        m_startingPos = spawn;
        m_goalPos = goal;
        m_initialGoalDistance = Vector3.Distance(spawn, goal);
        m_complexity = scores.Norm_score;
        m_initialSpeed = initialSpeed;
        m_setup = setup;
        
        if (real != null)
        {
            m_realAgent = real;
            m_gridSensor.AgentGameObject = m_realAgent.gameObject;
            m_raySensor.AgentGameObject = m_realAgent.gameObject;
        }
        else
        {
            m_gridSensor.AgentGameObject = this.gameObject;
            m_raySensor.AgentGameObject = this.gameObject;
        }
        
        if(disable)
            gameObject.SetActive(false);
    }
    
    /// <summary>
    /// Sets the color of the agent.
    /// </summary>
    public void SetColor(Color c)
    {
        GetComponent<Renderer>().material.color = c;
        m_color = c;
    }

    /// <summary>
    /// Sets the group color of the agent.
    /// </summary>
    public void SetGroupColor(Color c)
    {
        m_visuals.GroupColor = c;
        m_visuals.UseGroupColor = true;
    }
    
    public override void Initialize()
    {
        m_rb = GetComponent<Rigidbody>();
        m_rvoController = GetComponent<RVOController>();
        m_visuals = GetComponent<Visuals>();
        if (transform.Find("Trail") != null)
            m_trailRenderer = transform.Find("Trail").GetComponent<TrailRenderer>();
        
        m_manager = SceneManager.Instance;
        m_env = transform.parent.parent.GetComponent<Environment>();
        
        m_rvoController.enabled = false;
        m_saveList = new List<SaveData>();
    }
    
    private void Update()
    {
        if(m_manager.SaveNow && m_manager.SaveTrajectories)
            FinishEpisode(true);
        
        if(m_rvoController == null || !m_rvoController.enabled)
            return;
        
        Vector3 delta = m_rvoController.CalculateMovementDelta(transform.position, Time.fixedDeltaTime);
        Vector3 newVelocity = delta / Time.fixedDeltaTime;
        m_rb.velocity = newVelocity;
        CurrentVelocity = m_rb.velocity;
        m_currentSpeed = CurrentVelocity.magnitude;
    }
    
    private void FixedUpdate()
    {
        if (m_manager.IsInfernce)
        {
            if(m_manager.ManualComplexity)
                m_complexity = m_manager.Complexity;

            if (m_setup == SceneSetup.Infinite && m_env.OutsideInfiniteEnv(transform.position))
            {
                FinishEpisode(false);
            }
        }

        if (m_manager.SaveTrajectories)
        {
            SaveData sd = new SaveData
            {
                timestep = m_env.timestep,
                position = new Vector3(transform.position.x, 0f, transform.position.z),
                complexity = m_complexity
            };
            m_saveList.Add(sd);
        }
        
        m_frameCounter++;
    }
    
    private Vector3 GetRandomPointInCircle(Vector3 center, float radius)
    {
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2);
        float x = center.x + Mathf.Cos(angle) * radius;
        float z = center.z + Mathf.Sin(angle) * radius;
        return new Vector3(x, center.y, z);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if(m_manager.IsInfernce && m_rvoController != null)
        {
            m_rvoController.enabled = true;
        }
    }
    
    public override void OnEpisodeBegin()
    {
        m_episode++;
        m_frameCounter = 0;
        m_saveList.Clear();
        
        if(!m_manager.IsInfernce)
            m_startingPos = GetRandomPointInCircle(m_startingPos, 0f);
        
        transform.position = m_startingPos;
        m_lastPos = transform.position;
        m_rvoController.enabled = true;
        
        if(m_trailRenderer != null)
            m_trailRenderer.Clear();
        
        if (m_realAgent != null)
        {
            transform.LookAt(m_realAgent.InitialLookPoint);
            float noiseAngle = UnityEngine.Random.Range(-30f, 30f);
            transform.Rotate(Vector3.up, noiseAngle);
            m_rb.velocity = transform.forward * m_realAgent.InitialSpeed;
        }
        else
        {
            if (m_setup is SceneSetup.Infinite){
                if (m_episode == 1)
                {
                    transform.LookAt(m_goalPos);
                    m_rb.velocity = transform.forward * UnityEngine.Random.Range(0f, m_moveSpeed);
                }
            }
        }

        m_goalDistance = Vector3.Distance(transform.position, m_goalPos);
        if (m_setup is SceneSetup.Infinite)
            m_goalDistance = m_initialGoalDistance;
        
        Vector3 goalVector = m_goalPos - transform.position;
        m_goalAngle = Vector3.SignedAngle(transform.forward, goalVector, Vector3.up);
    }

    private float RandomizeObservationValue(float value)
    {
        float temp = UnityEngine.Random.Range(0.95f, 1.05f) * value;
        return Mathf.Clamp01(temp);
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        var localVelocity = transform.InverseTransformDirection(m_rb.velocity / m_moveSpeed);
        sensor.AddObservation(localVelocity.x);
        sensor.AddObservation(localVelocity.z);
        
        float goalDistanceNorm = m_goalDistance / m_env.maxDistance;
        float goalAngleNorm = ((m_goalAngle / 180f) + 1) / 2f;
        sensor.AddObservation(goalDistanceNorm);
        sensor.AddObservation(goalAngleNorm);
        
        sensor.AddObservation(RandomizeObservationValue(m_complexity));

        if (m_realAgent != null && receiveRealObs)
        {
            Vector3 relativePos = m_realAgent.transform.position - transform.position;
            Vector3 normalizedRelativePos = relativePos / m_env.maxDistance;
            sensor.AddObservation(normalizedRelativePos.x);
            sensor.AddObservation(normalizedRelativePos.z);
            
            Vector3 relativeVel = transform.InverseTransformDirection(m_realAgent.CurrentVelocity - m_rb.velocity);
            sensor.AddObservation(relativeVel.x);
            sensor.AddObservation(relativeVel.z);
            
            float relativeOrientation = (1 - Vector3.Dot(transform.forward, m_realAgent.transform.forward)) / 2;
            sensor.AddObservation(relativeOrientation);
            
            float distanceToRealAgent = normalizedRelativePos.magnitude;
            sensor.AddObservation(distanceToRealAgent);
        }
        else
        {
            sensor.AddObservation(new float[6]);
        }
    }
    
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        DrawSpeed = (Vector3.Distance(transform.position, m_lastPos) / Time.fixedDeltaTime) / m_moveSpeed;
        m_lastPos = transform.position;
        (float move, float turn) = CalculateMovement(actionBuffers.ContinuousActions);

        m_goalDistance = Vector3.Distance(transform.position, m_goalPos);
        Vector3 goalVector = m_goalPos - transform.position;
        m_goalAngle = Vector3.SignedAngle(transform.forward, goalVector, Vector3.up);

        AssignSmoothMovementReward(move, turn);
        AssignImitationReward();
    }
    
    private (float, float) CalculateMovement(ActionSegment<float> act)
    {
        float moveInput = RescaleValue(act[0], -1f, 1f, -0.5f, 1f) * m_moveSpeed;
        float turnAmount = Mathf.Clamp(act[1], -1f, 1f);
        
        float turn = turnAmount * m_turnSpeed * Time.fixedDeltaTime;
        transform.Rotate(0, turn, 0);

        float currentSpeed = m_rb.velocity.magnitude;
        float targetSpeed = Mathf.Clamp(currentSpeed + moveInput * Time.fixedDeltaTime, 0, m_moveSpeed);

        Vector3 move = transform.forward * targetSpeed;
        m_rvoController.SetTarget(transform.position + move, targetSpeed, m_moveSpeed);
        return (moveInput, turnAmount);
    }

    private void AssignSmoothMovementReward(float currentMoveInput, float currentTurnAmount)
    {
        float moveDifference = Mathf.Abs(currentMoveInput - m_previousMoveInput);
        float turnDifference = Mathf.Abs(currentTurnAmount - m_previousTurnAmount);
        float normalizedMoveChange = RescaleValue(moveDifference, 0, 1.5f, 0f, 1f);
        float normalizedTurnChange = RescaleValue(turnDifference, 0, 2, 0, 1);
        float combinedChange = (normalizedMoveChange + normalizedTurnChange) / 2;
        
        if(combinedChange > 0.5f)
            AddReward(-0.001f * combinedChange);

        m_previousMoveInput = currentMoveInput;
        m_previousTurnAmount = currentTurnAmount;
    }
    
    private void AssignImitationReward()
    {
        if (m_manager.IsInfernce)
        {
            if (m_goalDistance <= 2f)
            {
                if (m_setup == SceneSetup.Infinite)
                {
                    m_endedFromTime = true;
                    FinishEpisode(false);
                }
                if (m_frameCounter > 100 && m_currentSpeed > 0.2f)
                    FinishEpisode(false);
            }
            return;
        }
        
        float normSpeed = m_rb.velocity.magnitude / m_moveSpeed;
        float normRealSpeed = m_realAgent.CurrentSpeed / m_moveSpeed;
        float velocityDifference = Mathf.Clamp01(Mathf.Abs(normSpeed - normRealSpeed));
        float velocitySimilarity = 1f - Mathf.Sqrt(velocityDifference);
        
        float optimalProximity = 5.0f;
        float distanceToRealAgent = Vector3.Distance(m_realAgent.transform.position, transform.position);
        float proximitySimilarity = 1f - Mathf.Sqrt(Mathf.Clamp01(distanceToRealAgent / optimalProximity));
        
        float deltaOrientation = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, m_realAgent.transform.eulerAngles.y));
        deltaOrientation /= 180f;
        float orientationSimilarity = 1f - Mathf.Sqrt(Mathf.Clamp01(deltaOrientation));

        m_imitationQuality = 0.25f * velocitySimilarity + 0.5f * proximitySimilarity + 0.25f * orientationSimilarity;
        
        float imitationQualityReward = 0.005f * m_complexity * m_imitationQuality;
        AddReward(imitationQualityReward);

        if (m_goalDistance <= 1.5f && m_realAgent.Progress > 0.8f)
        {
            float goalReward = 0.5f * m_imitationQuality;
            AddReward(goalReward);
            FinishEpisode(false);
        }
    }
    
    /// <summary>
    /// Finishes the episode and optionally destroys the agent.
    /// </summary>
    public void FinishEpisode(bool destroy)
    {
        m_frameCounter = 0;
        
        if(m_manager.SaveTrajectories)
            m_manager.SaveAgentData(gameObject.name + "_" + m_episode, m_saveList, m_goalPos);
        
        if(destroy)
            Destroy(this.gameObject);
        
        if (m_setup is SceneSetup.Infinite)
        {
            if (!m_endedFromTime)
                (m_startingPos, m_rb.velocity) = m_env.ReflectToOppositeSide(transform.position, m_rb.velocity);
            else
            {
                m_startingPos = transform.position;
                m_endedFromTime = false;
            }

            EndEpisode();
        }
        else
        {
            EndEpisode();
            gameObject.SetActive(false);   
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("ObstacleIgnore") || other.gameObject.CompareTag("Obstacle"))
        {
            if (StepCount >= 30)
            {
                AddReward(-0.5f);
                FinishEpisode(false);
            }
        }
    }

    private float RescaleValue(float value, float currentMin, float currentMax, float newMin, float newMax)
    {
        float normalized = (value - currentMin) / (currentMax - currentMin);
        float rescaled = normalized * (newMax - newMin) + newMin;
        return rescaled;
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Debug.DrawLine(transform.position, m_goalPos, Color.red);
            if(m_realAgent)
                Debug.DrawLine(transform.position, m_realAgent.transform.position, m_color);
        }
    }
}