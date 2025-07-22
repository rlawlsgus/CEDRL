using System.Linq;
using UnityEngine;

public class GridDetector : MonoBehaviour
{
    public class GridCell
    {
        public bool assigned;
        public float speed;
        public float rotation;

        public GridCell()
        {
            assigned = false;
            speed = 0;
            rotation = 0;
        }

        public void SetValues(float s, float r)
        {
            assigned = true;
            speed = s;
            rotation = r;
        }
    }
    
    [SerializeField] private GameObject m_AgentGameObject; 
    private int gridSize = 5;
    private float cellSize = 0.6f;
    private float cellPadding = 0f; 
    private float detectionHeight = 0.1f;
    [SerializeField] private LayerMask detectionLayer;
    private GridCell[,] detectionGrid;

    public void SetAgentGameObject(GameObject obj)
    {
        m_AgentGameObject = obj;
    }
    
    void Awake()
    {
        detectionGrid = new GridCell[gridSize, gridSize];
        ClearDetectionGrid();
    }

    public GridCell[,] PerformDetection(float maxSpeed, bool isReal)
    {
        ClearDetectionGrid();
        // Calculate the total distance covered by the grid (including padding)
        float totalGridSize = gridSize * cellSize + (gridSize - 1) * cellPadding;
        Vector3 gridOffset = new Vector3(totalGridSize / 2, 0, totalGridSize / 2);

        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                // Calculate world position for each cell, accounting for padding
                Vector3 cellPositionOffset = new Vector3(x * (cellSize + cellPadding), 0, z * (cellSize + cellPadding)) - gridOffset;
                Vector3 worldCellPosition = transform.position + transform.rotation * 
                    (cellPositionOffset + new Vector3(cellSize / 2, detectionHeight / 2, cellSize / 2));
                Collider[] results = new Collider[3];
                var size = Physics.OverlapBoxNonAlloc(worldCellPosition, 
                    new Vector3(cellSize / 2, detectionHeight / 2, cellSize / 2), results, transform.rotation, detectionLayer);
                
                var sortedResults = results
                    .Where(coll => coll != null)
                    .OrderBy(coll => (coll.transform.position - worldCellPosition).sqrMagnitude)
                    .ToArray();
                
                if(size > 0)
                {
                    float cellSpeed = 0;
                    float cellRotation = 0;

                    GameObject obj = sortedResults[0].gameObject;
                    if(m_AgentGameObject == obj){
                        continue; //Do not include the real agent that follows
                    }

                    cellSpeed = Mathf.Clamp01(obj.GetComponent<RealAgent>().CurrentVelocity.magnitude / maxSpeed);
                    cellRotation = obj.transform.rotation.eulerAngles.y;
                    detectionGrid[x, z].SetValues(cellSpeed, cellRotation);
                }
            }
        }
        
        // Set velocity of parent in middle cell
        float parentSpeed = 0;
        float parentRotation = 0;
        if (isReal)
        {
            parentSpeed = Mathf.Clamp01(m_AgentGameObject.GetComponent<RealAgent>().CurrentVelocity.magnitude / maxSpeed);
            parentRotation = m_AgentGameObject.transform.eulerAngles.y;
        }
        else{
            parentSpeed = Mathf.Clamp01(transform.parent.gameObject.GetComponent<CEDRL_Agent>().CurrentVelocity.magnitude / maxSpeed);
            parentRotation = transform.parent.eulerAngles.y;
        }
        detectionGrid[gridSize / 2, gridSize / 2].SetValues(parentSpeed, parentRotation);
        
        return detectionGrid;
    }
    
    public float CalculateSimilarity(GridCell[,] grid2, float maxSpeed)
    {
        // [0.5, 1, 1.5, 2]
        float sigma = 1.5f;
        int center = gridSize / 2;
        float totalSimilarity = 0f;
        float maxTotalWeight = 0f;

        PerformDetection(maxSpeed, false);

        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                // Translate coordinates so the center of the grid is at (0, 0)
                float translatedX = x - center;
                float translatedZ = z - center;
                    
                // Calculate weight using the adjusted standard normal distribution
                float weight = Mathf.Exp(-0.5f * (translatedX * translatedX + translatedZ * translatedZ) / (sigma * sigma));

                if (detectionGrid[x, z].assigned && grid2[x, z].assigned)
                {
                    // Calculate the speed difference, normalized to [0, 1]
                    float speedDifference = Mathf.Abs(detectionGrid[x, z].speed - grid2[x, z].speed);
                    float speedSimilarity = 1 - speedDifference;

                    // Calculate the angular difference correctly considering wrap-around
                    float angleDifference = Mathf.Abs(Mathf.DeltaAngle(detectionGrid[x, z].rotation, grid2[x, z].rotation));
                    angleDifference /= 180f; // Convert in range [0,1]
                    float directionSimilarity = 1 - angleDifference;

                    // Combine the speed and direction similarities
                    float cellSimilarity = (speedSimilarity + directionSimilarity) / 2;

                    // Apply the weight to the cell similarity
                    totalSimilarity += (weight * cellSimilarity);
                    maxTotalWeight += weight;
                }else if (detectionGrid[x, z].assigned || grid2[x, z].assigned) // If only one cell is set add 0 similarity
                {
                    totalSimilarity += 0f;
                    maxTotalWeight += weight;
                }
            }
        }

        float normalizedSimilarity = totalSimilarity / maxTotalWeight;
        return Mathf.Clamp01(normalizedSimilarity);
    }

    void ClearDetectionGrid()
    {
        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                detectionGrid[x, z] = new GridCell();
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (detectionGrid == null) return;
        float totalGridSize = gridSize * cellSize + (gridSize - 1) * cellPadding;
        Vector3 gridOffset = new Vector3(totalGridSize / 2, 0, totalGridSize / 2);
        Vector3 bottomLeft = transform.position - transform.rotation * gridOffset;

        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                Vector3 cellPositionOffset = new Vector3(x * (cellSize + cellPadding), 0, z * (cellSize + cellPadding)) - gridOffset;
                Vector3 worldCellPosition = transform.position + transform.rotation * 
                    (cellPositionOffset + new Vector3(cellSize / 2, detectionHeight / 2, cellSize / 2));

                GridCell cell = detectionGrid[x, z];
                Color velocityColor = new Color(0.8f, 0.8f, 0.8f, 0.25f); // Default color for empty or negligible velocity cells
                // If there's significant velocity, adjust the color based on direction and intensity
                if (cell.assigned)
                {
                    float intensity = cell.speed;
                    velocityColor = cell.rotation >= 180 ? new Color(1, 0, 0, intensity) : new Color(0, 0, 1, intensity);
                }

                Gizmos.color = velocityColor;
                Gizmos.matrix = Matrix4x4.TRS(worldCellPosition, transform.rotation, new Vector3(1, 1, 1));
                Gizmos.DrawCube(Vector3.zero, new Vector3(cellSize, detectionHeight, cellSize));
            }
        }
        Gizmos.matrix = Matrix4x4.identity; // Reset Gizmos matrix after drawing
    }
}