using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Xml;
using System.Globalization;

public class LineObstacle
{
    public float x1, z1, x2, z2, thickness;
    // Constructor and other methods
}
    
public class CircleObstacle
{
    public float x, z, radius;
    // Constructor and other methods
}

public class SceneObjects : MonoBehaviour
{
    public void BuildSceneObjects(string xmlFilePath, Transform parent, Vector2 envScale)
    {
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.Load(xmlFilePath);
        XmlNodeList linesList = xmlDoc.GetElementsByTagName("Line");

        List<LineObstacle> obstacles = new List<LineObstacle>();
        foreach (XmlNode lineNode in linesList)
        {
            XmlElement lineElement = (XmlElement)lineNode;
            LineObstacle obstacle = new LineObstacle()
            {
                x1 = float.Parse(lineElement.GetAttribute("x1")),
                z1 = float.Parse(lineElement.GetAttribute("y1")),
                x2 = float.Parse(lineElement.GetAttribute("x2")),
                z2 = float.Parse(lineElement.GetAttribute("y2")),
                thickness = float.Parse(lineElement.GetAttribute("thickness"))
            };
            obstacles.Add(obstacle);
        }
        
        XmlNodeList circleList = xmlDoc.GetElementsByTagName("Circle");

        List<CircleObstacle> circleObstacles = new List<CircleObstacle>();
        foreach (XmlNode circleNode in circleList)
        {
            XmlElement circleElement = (XmlElement)circleNode;
            CircleObstacle circle = new CircleObstacle()
            {
                x = float.Parse(circleElement.GetAttribute("x")),
                z = float.Parse(circleElement.GetAttribute("y")),
                radius = float.Parse(circleElement.GetAttribute("radius"))
            };
            circleObstacles.Add(circle);
        }

        CreateLineObstacles(obstacles, parent, envScale);
        CreateCircleObstacles(circleObstacles, parent, envScale);
    }

    private void CreateLineObstacles(List<LineObstacle> obstacles, Transform parent, Vector2 envScale)
    {
        float offsetX = envScale.x / 2;
        float offsetY = envScale.y / 2;
        
        foreach (LineObstacle obstacle in obstacles)
        {
            GameObject lineObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lineObj.transform.parent = parent;
            lineObj.name = "LineObstacle";

            Vector3 start = new Vector3(obstacle.z1 + offsetX, 0f, obstacle.x1 + offsetY);
            Vector3 end = new Vector3(obstacle.z2 + offsetX, 0f, obstacle.x2 + offsetY);
            Vector3 direction = end - start;
            lineObj.transform.localPosition = start + direction / 2;
            lineObj.transform.localScale = new Vector3(obstacle.thickness, direction.magnitude, obstacle.thickness);
            lineObj.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
        }
    }

    private void CreateCircleObstacles(List<CircleObstacle> circleObstacles, Transform parent, Vector2 envScale)
    {
        float offsetX = envScale.x / 2;
        float offsetY = envScale.y / 2;
        
        foreach (CircleObstacle circle in circleObstacles)
        {
            GameObject circleObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            circleObj.transform.parent = parent;
            circleObj.name = "CircleObstacle";

            circleObj.transform.localPosition = new Vector3(circle.z + offsetX, 0f, circle.x + offsetY);
            circleObj.transform.localScale = new Vector3(circle.radius * 2, circle.radius, circle.radius * 2);
        }
    }
}
