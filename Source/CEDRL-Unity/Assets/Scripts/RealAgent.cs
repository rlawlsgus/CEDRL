using UnityEngine;
using System.Collections.Generic;
using Pathfinding.RVO;

public class RealAgent : MonoBehaviour
{
    public int id;
    private List<float> timesteps;
    private List<Vector3> positions;
    private Environment m_env;
    private RVOController m_rvoController;
    [SerializeField] private float m_turnSpeed = 45f;
    private int m_index;
    [SerializeField] private int m_indexObs;
    private bool m_spawned;
    private Renderer m_renderer;
    private Renderer m_frontRenderer;
    private Collider m_collider;
    public CEDRL_Agent CEDRLAgent { get; set; }
    private bool m_dataLoaded;
    private Vector3 m_previousPosition;
    private float m_previousTime;
    public float Score { get; private set; }
    public float InitialSpeed { get; private set; }
    public Vector3 InitialLookPoint { get; private set; }
    public Vector3 CurrentVelocity { get; private set; }
    public float CurrentSpeed { get; private set; }
    public float Progress { get; private set; }

    private void Awake()
    {
        m_renderer = GetComponent<MeshRenderer>();
        m_frontRenderer = transform.GetChild(0).GetComponent<MeshRenderer>();
        m_collider = GetComponent<CapsuleCollider>();
        m_rvoController = GetComponent<RVOController>();
        m_rvoController.enabled = false;
    }
    
    private void OnDisable()
    {
        ResetAgent();
    }

    public void SetData(int idIn, List<float> t, List<Vector3> p, float s, Environment env)
    {
        m_env = env;
        id = idIn;
        timesteps = t;
        positions = p;
        Score = s;
        m_previousPosition = m_env.AdjustHeight(positions[0]);
        m_dataLoaded = true;
        
        Vector3 initialVelocity = (m_env.AdjustHeight(positions[1]) - m_env.AdjustHeight(positions[0])) / (timesteps[1] - timesteps[0]);
        InitialSpeed = initialVelocity.magnitude;
        InitialLookPoint = m_env.AdjustHeight(positions[1]);

        m_indexObs = (int)(SceneManager.Instance.FadingFactor * timesteps.Count);
    }

    public void SetColor(Color c)
    {
        GetComponent<Renderer>().material.color = c;
    }

    private void FixedUpdate()
    {
        Progress = (float)m_index / (float)timesteps.Count;
        
        if (!m_dataLoaded)
            return;

        if (m_env.timestep >= timesteps[0] && !m_spawned)
        {
            m_indexObs = (int)(SceneManager.Instance.FadingFactor * timesteps.Count);
            m_collider.enabled = true;
            m_renderer.enabled = true;
            m_frontRenderer.enabled = true;
            m_rvoController.enabled = true;
            if (CEDRLAgent != null)
            {
                CEDRLAgent.gameObject.SetActive(true);
                CEDRLAgent.receiveRealObs = true;
            }
            m_spawned = true;
        }

        if (m_spawned)
        {
            m_index += 1;
            Vector3 nextPosition = m_env.AdjustHeight(positions[m_index]);
            Vector3 movementDirection = nextPosition - transform.position;
            if (movementDirection != Vector3.zero)
            {
                // Calculate rotation towards the next position
                Quaternion lookRotation = Quaternion.LookRotation(movementDirection.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * m_turnSpeed);
                // Move towards the next position
                transform.position = nextPosition;
            }
        }

        if (m_index >= positions.Count - 1)
        {
            gameObject.SetActive(false);
            if(CEDRLAgent != null)
                CEDRLAgent.FinishEpisode(false);
        }

        if (m_index >= m_indexObs)
        {
            if (CEDRLAgent != null)
                CEDRLAgent.receiveRealObs = false;
        }

        // Calculate current velocity
        float timeDelta = m_env.timestep - m_previousTime;
        if (timeDelta > 0f)
        {
            CurrentVelocity = (transform.position - m_previousPosition) / timeDelta;
        }

        CurrentSpeed = CurrentVelocity.magnitude;
        
        // Update for next frame
        m_previousPosition = transform.position;
        m_previousTime = m_env.timestep;
    }

    private void ResetAgent()
    {
        m_spawned = false;
        m_index = 0;
        m_previousTime = 0f;
        m_previousPosition = m_env.AdjustHeight(positions[0]);
        m_collider.enabled = false;
        m_renderer.enabled = false;
        m_frontRenderer.enabled = false;
        m_rvoController.enabled = false;
    }
}
