using UnityEngine;

public class SemanticLabel : MonoBehaviour
{
    public string semanticTag;

    void Awake()
    {
        semanticTag = gameObject.tag;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        semanticTag = gameObject.tag;
    }
#endif
}