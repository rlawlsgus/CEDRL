using System;
using UnityEngine;
using System.Collections.Generic;
using Pathfinding.RVO;

public class RealAgentDemo : MonoBehaviour
{
    public int id;
    private List<float> timesteps;
    private List<Vector3> positions;
    private List<float> speeds;
    private Environment m_env;
    private RVOController m_rvoController;
    [SerializeField] private float m_turnSpeed = 45f;
    private int m_index;
    private bool m_spawned;
    private Renderer m_renderer;
    private Renderer m_frontRenderer;
    private Collider m_collider;
    private bool m_dataLoaded;
    private Vector3 m_previousPosition;
    private float m_previousTime;
    public float Score;
    public Vector3 CurrentVelocity { get; private set; }
    public float CurrentSpeed { get; private set; }
    public CEDRL_Agent CEDRLAgent { get; set; }
    private List<SaveData> m_saveList;
    private SceneManager m_manager;

    private void Awake()
    {
        m_renderer = GetComponent<MeshRenderer>();
        m_frontRenderer = transform.GetChild(0).GetComponent<MeshRenderer>();
        m_collider = GetComponent<CapsuleCollider>();
        m_rvoController = GetComponent<RVOController>();
        m_rvoController.enabled = false;
        m_manager = SceneManager.Instance;
        m_saveList = new List<SaveData>();
    }

    private void Update()
    {
        if (m_manager.SaveNow && m_manager.SaveTrajectories)
        {
            m_manager.SaveAgentData(gameObject.name + "_", m_saveList, positions[^1]);
            Destroy(this.gameObject);
        }
    }

    private void FixedUpdate()
    {
        if (!m_dataLoaded)
            return;

        if (m_env.timestep >= timesteps[0] && !m_spawned)
        {
            m_spawned = true;
            m_collider.enabled = true;
            m_renderer.enabled = true;
            m_frontRenderer.enabled = true;
            m_rvoController.enabled = true;
            if (CEDRLAgent != null)
            {
                CEDRLAgent.gameObject.SetActive(true);
            }
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
                m_rvoController.SetTarget(nextPosition, speeds[m_index - 1], 2.25f);
                Vector3 delta = m_rvoController.CalculateMovementDelta(transform.position, Time.fixedDeltaTime);
                // Directly move the transform
                transform.position += delta;
                
                if (m_manager.SaveTrajectories)
                {
                    SaveData sd = new SaveData();
                    sd.timestep = m_env.timestep;
                    sd.position = new Vector3(transform.position.x, 0f, transform.position.z);
                    sd.complexity = Score;
                    m_saveList.Add(sd);
                }
            }
        }

        if (m_index >= positions.Count - 1)
        {
            m_manager.SaveAgentData(gameObject.name + "_", m_saveList, positions[^1]);
            Destroy(this.gameObject);
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
}
