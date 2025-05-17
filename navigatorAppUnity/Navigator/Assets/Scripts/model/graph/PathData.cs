using UnityEngine;
using System;
using System.Collections.Generic;

namespace model.graph
{

    /// <summary>
    /// Represents a weighted path with a list of 3D points.
    /// </summary>
    [Serializable]
    public class PathData
    {
        public double weight;
        public List<Point> points;

        public PathData(double weight, List<Point> points)
        {
            this.weight = weight;
            this.points = points;

        }

        public void AddStartAndEndPoints(Point start, Point end)
        {
            // Insert start point at the beginning of the list
            points.Insert(0, start);

            // Insert end point at the end of the list
            points.Add(end);
        }

        /// <summary>
        /// Returns a reversed deep copy of this PathData instance.
        /// </summary>
        public PathData GetReverseCopy()
        {
            List<Point> reversePoints = new List<Point>();
            for (int i = points.Count - 1; i >= 0; i--)
            {
                reversePoints.Add(new Point(points[i].lat, points[i].lon));
            }

            return new PathData(weight, reversePoints);
        }

        /// <summary>
        /// Plots this PathData as a smooth line in the scene using a LineRenderer.
        /// </summary>
        public void Plot(string name = "PlottedPath", Color? startColor = null,  Color? endColor = null, int smoothness = 10, float height = 1)
        {
            if (points == null)
            {
                Debug.LogError("Points list is null!");
                return;
            }

            if (points.Count < 2)
            {
                Debug.LogError($"Not enough points to plot! Only {points.Count} point(s).");
                return;
            }

            GameObject pathObj = new GameObject(name);
            pathObj.tag = "PlottedPath";
            LineRenderer line = pathObj.AddComponent<LineRenderer>();

            // Basic line settings
            line.widthMultiplier = 0.1f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = startColor ?? Color.cyan;
            line.endColor = endColor ?? Color.magenta;
            line.useWorldSpace = true;

            List<Vector3> positions = new List<Vector3>();
            foreach (var point in points)
            {
                //due to the right-handed coordinate system...
                positions.Add(new Vector3((float)point.lon, height, (float)point.lat));    //todo fix
            }

            List<Vector3> smoothPositions = SmoothLine(positions, smoothness);

            line.positionCount = smoothPositions.Count;
            line.SetPositions(smoothPositions.ToArray());
        }

        /// <summary>
        /// Smooths a list of points using simple linear interpolation.
        /// </summary>
        private List<Vector3> SmoothLine(List<Vector3> original, int smoothness)
        {
            if (original == null || original.Count < 2 || smoothness < 1)
                return original;

            List<Vector3> smoothedLine = new List<Vector3>();
    
            // Add the first point
            smoothedLine.Add(original[0]);
    
            // Process each segment between adjacent points
            for (int i = 0; i < original.Count - 1; i++)
            {
                Vector3 start = original[i];
                Vector3 end = original[i + 1];
        
                // Add intermediate points for smoothing
                for (int j = 1; j <= smoothness; j++)
                {
                    float t = j / (smoothness + 1.0f);
                    Vector3 intermediatePoint = Vector3.Lerp(start, end, t);
                    smoothedLine.Add(intermediatePoint);
                }
        
                // Add the end point of the current segment
                // (except for the last segment to avoid duplicating the final point)
                if (i < original.Count - 2)
                {
                    smoothedLine.Add(end);
                }
            }
    
            // Add the very last point
            smoothedLine.Add(original[original.Count - 1]);
    
            return smoothedLine;
        }
    }
}