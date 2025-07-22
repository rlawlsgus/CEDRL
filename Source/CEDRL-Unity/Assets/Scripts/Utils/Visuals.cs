using UnityEngine;

public class Visuals : MonoBehaviour
{
    private Gradient m_plasmaGradient;
    private TrailRenderer m_trail;
    private MeshRenderer m_body;
    private float colorChangeSpeed = 10f;
    private CEDRL_Agent m_agent;
    public bool UseGroupColor;
    public Color GroupColor;
    
    void Awake()
    {
        m_agent = GetComponent<CEDRL_Agent>();
        m_trail = GetComponentInChildren<TrailRenderer>();
        m_body = GetComponent<MeshRenderer>();
        
        m_plasmaGradient = new Gradient();
        GradientColorKey[] colorKeys = new GradientColorKey[4];
        colorKeys[0].color = new Color(0.14f, 0f, 0.31f); // Dark Purple
        colorKeys[0].time = 0.0f;
        colorKeys[1].color = new Color(0.47f, 0.32f, 0.66f); // Blue to Purple
        colorKeys[1].time = 0.33f;
        colorKeys[2].color = new Color(0.95f, 0.65f, 0f); // Vivid Orange
        colorKeys[2].time = 0.66f;
        colorKeys[3].color = new Color(1f, 0.93f, 0.29f); // Bright Yellow
        colorKeys[3].time = 1.0f;
        // Set the color keys to the gradient
        m_plasmaGradient.SetKeys(colorKeys, new GradientAlphaKey[0]);
    }
    
    public Color GetSpeedColor(float value)
    {
        value = Mathf.Clamp01(value);
        return m_plasmaGradient.Evaluate(value);
    }

    private void Update()
    {
        Color color;
        if (UseGroupColor)
        {
            color = GroupColor;
        }
        else
        {
            Color currentColor = m_body.material.color;
            Color targetColor = GetSpeedColor(m_agent.DrawSpeed);
            color = Color.Lerp(currentColor, targetColor, Time.deltaTime * colorChangeSpeed);
        }

        m_body.material.color = color;
        m_trail.startColor = color;
        m_trail.endColor = color;
    }
}