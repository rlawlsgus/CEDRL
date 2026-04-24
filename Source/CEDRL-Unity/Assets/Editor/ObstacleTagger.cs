using UnityEngine;
using UnityEditor;
using System.Linq;
using Pathfinding.RVO;

public class ObstacleTagger : EditorWindow
{
    private GameObject sensorObject;
    private string targetTag = "Obstacle";
    private bool autoAddRVO = true;
    private bool clipToSensor = true; 
    private bool setupTrigger = true; 

    [MenuItem("Tools/CEDRL/Advanced Obstacle Tagger")]
    public static void ShowWindow()
    {
        GetWindow<ObstacleTagger>("Obstacle Tagger");
    }

    private void OnGUI()
    {
        GUILayout.Label("고급 장애물 설정 툴 (Bug Fixed)", EditorStyles.boldLabel);
        sensorObject = (GameObject)EditorGUILayout.ObjectField("Sensor Object", sensorObject, typeof(GameObject), true);
        
        targetTag = EditorGUILayout.TextField("Target Tag", targetTag);
        autoAddRVO = EditorGUILayout.Toggle("RVO Obstacle 설정", autoAddRVO);
        clipToSensor = EditorGUILayout.Toggle("센서 영역만큼만 RVO 계산", clipToSensor);
        setupTrigger = EditorGUILayout.Toggle("트리거 콜라이더 설정", setupTrigger);

        if (GUILayout.Button("일괄 설정 실행"))
        {
            ProcessObstacles();
        }
        
        EditorGUILayout.HelpBox("오류 해결 버전: 프리팹 자식 오브젝트의 콜라이더 추가 문제를 수정했습니다.", MessageType.Info);
    }

    private void ProcessObstacles()
    {
        if (sensorObject == null)
        {
            EditorUtility.DisplayDialog("오류", "Sensor Object를 할당해 주세요.", "확인");
            return;
        }

        if (!UnityEditorInternal.InternalEditorUtility.tags.Contains(targetTag))
        {
            EditorUtility.DisplayDialog("오류", $"프로젝트에 '{targetTag}' 태그가 없습니다. 먼저 추가해 주세요.", "확인");
            return;
        }

        Renderer sensorRenderer = sensorObject.GetComponent<Renderer>();
        if (sensorRenderer == null) return;
        
        Bounds sensorBounds = sensorRenderer.bounds;
        MeshRenderer[] allRenderers = FindObjectsOfType<MeshRenderer>();
        int count = 0;

        foreach (MeshRenderer mr in allRenderers)
        {
            if (mr == null || mr.gameObject == sensorObject) continue;

            if (sensorBounds.Intersects(mr.bounds))
            {
                GameObject obj = mr.gameObject;
                
                // 1. Tag change
                Undo.RecordObject(obj, "Advanced Tag Change");
                obj.tag = targetTag;

                Bounds targetBounds = clipToSensor ? GetIntersection(sensorBounds, mr.bounds) : mr.bounds;

                // 2. RVO setup
                if (autoAddRVO)
                {
                    RVOSquareObstacle rvo = obj.GetComponent<RVOSquareObstacle>();
                    if (rvo == null) rvo = Undo.AddComponent<RVOSquareObstacle>(obj);
                    
                    if (rvo != null)
                    {
                        Undo.RecordObject(rvo, "Setup RVO Obstacle");
                        rvo.center = obj.transform.InverseTransformPoint(targetBounds.center);
                        rvo.size = new Vector2(targetBounds.size.x, targetBounds.size.z);
                        rvo.height = targetBounds.size.y;
                        EditorUtility.SetDirty(rvo);
                    }
                }

                // 3. Trigger collider setup
                if (setupTrigger)
                {
                    SetupTriggerCollider(obj, targetBounds);
                }

                EditorUtility.SetDirty(obj);
                count++;
            }
        }

        EditorUtility.DisplayDialog("완료", $"{count}개의 장애물 설정 완료", "확인");
    }

    private Bounds GetIntersection(Bounds b1, Bounds b2)
    {
        Vector3 min = Vector3.Max(b1.min, b2.min);
        Vector3 max = Vector3.Min(b1.max, b2.max);
        Bounds b = new Bounds();
        b.SetMinMax(min, max);
        return b;
    }

    private void SetupTriggerCollider(GameObject obj, Bounds targetBounds)
    {
        if (obj == null) return;

        Collider[] colliders = obj.GetComponents<Collider>();
        if (colliders.Any(c => c != null && c.isTrigger)) return;

        // Try MeshCollider first
        MeshCollider mc = obj.GetComponent<MeshCollider>();
        if (mc != null)
        {
            Undo.RecordObject(mc, "Setup MeshCollider");
            mc.convex = true;
            mc.isTrigger = true;
            EditorUtility.SetDirty(mc);
            return;
        }

        // Add BoxCollider safely
        BoxCollider bc = obj.GetComponent<BoxCollider>();
        bool isNew = false;
        if (bc == null)
        {
            bc = obj.AddComponent<BoxCollider>();
            if (bc != null)
            {
                Undo.RegisterCreatedObjectUndo(bc, "Add BoxCollider");
                isNew = true;
            }
        }

        if (bc != null)
        {
            if (!isNew) Undo.RecordObject(bc, "Update BoxCollider");
            
            bc.center = obj.transform.InverseTransformPoint(targetBounds.center);
            
            Vector3 worldSize = targetBounds.size;
            Vector3 localScale = obj.transform.lossyScale;
            bc.size = new Vector3(
                Mathf.Approximately(localScale.x, 0) ? 0 : worldSize.x / Mathf.Abs(localScale.x),
                Mathf.Approximately(localScale.y, 0) ? 0 : worldSize.y / Mathf.Abs(localScale.y),
                Mathf.Approximately(localScale.z, 0) ? 0 : worldSize.z / Mathf.Abs(localScale.z)
            );
            
            bc.isTrigger = true;
            EditorUtility.SetDirty(bc);
        }
    }
}