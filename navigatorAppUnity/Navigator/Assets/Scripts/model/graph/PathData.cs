using UnityEngine;
using System;
using System.Collections.Generic;
using NUnit.Framework.Constraints;

namespace model.graph
{

    /// <summary>
    /// Represents a weighted path with a list of 3D points.
    /// </summary>
    [Serializable]
    public class PathData
    {
        public bool smoothPath = false;
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
        public void Plot(string name = "PlottedPath", Color? startColor = null,  Color? endColor = null, int smoothness = 5, float height = 1)
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
            line.widthMultiplier = 0.3f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = startColor ?? Color.cyan;
            line.endColor = endColor ?? Color.magenta;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View; 
            line.transform.up = Vector3.up;

            List<Vector3> positions = new List<Vector3>();
            foreach (var point in points)
            {
                //due to the right-handed coordinate system...
                positions.Add(new Vector3((float)point.lat, height, (float)point.lon));
            }

            List<Vector3> smoothPositions = positions;
            if (smoothPath)
            {
                smoothPositions = ChaikinSmooth(positions, smoothness);
            }


            line.positionCount = smoothPositions.Count;
            line.SetPositions(smoothPositions.ToArray());
        }

        /// <summary>
        /// Smooths a list of points using simple linear interpolation.
        /// </summary>
        public static List<Vector3> ChaikinSmooth(List<Vector3> points, int iterations = 2)
        {
            if (points == null || points.Count < 2) return points;

            List<Vector3> result = new List<Vector3>(points);
        
            for (int iter = 0; iter < iterations; iter++)
            {
                List<Vector3> newPoints = new List<Vector3>();
                newPoints.Add(result[0]);  // Keep first point
            
                for (int i = 0; i < result.Count - 1; i++)
                {
                    Vector3 p0 = result[i];
                    Vector3 p1 = result[i + 1];

                    // Generate two new points between p0 and p1
                    Vector3 Q = Vector3.Lerp(p0, p1, 0.25f);
                    Vector3 R = Vector3.Lerp(p0, p1, 0.75f);

                    newPoints.Add(Q);
                    newPoints.Add(R);
                }

                newPoints.Add(result[result.Count - 1]); // Keep last point
                result = newPoints;
            }
            

            return result;
        }

        
        /// <summary>
        /// Smooths a list of points using simple linear interpolation.
        /// </summary>
        List<Vector3> CatmullRomSpline(List<Vector3> pts, int smoothness)
        {
            var smoothed = new List<Vector3>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 p0 = i == 0 ? pts[i] : pts[i - 1];
                Vector3 p1 = pts[i];
                Vector3 p2 = pts[i + 1];
                Vector3 p3 = (i + 2 < pts.Count) ? pts[i + 2] : pts[i + 1];

                for (int j = 0; j <= smoothness; j++)
                {
                    float t = j / (float)smoothness;
                    Vector3 point = 0.5f * (
                        (2f * p1) +
                        (-p0 + p2) * t +
                        (2f * p0 - 5f * p1 + 4f * p2 - p3) * (t * t) +
                        (-p0 + 3f * p1 - 3f * p2 + p3) * (t * t * t));
                    smoothed.Add(point);
                }
            }
            return smoothed;
        }
        
    }
}