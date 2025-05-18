using System;
using System.Collections.Generic;
using UnityEngine;

namespace model.graph
{
    [Serializable]
    public class Room
    {
        public int id;
        public string name;
        public List<Point> outline;

        public Room(int id, string name, List<Point> outline)
        {
            this.id = id;
            this.name = name;
            this.outline = outline;
            if (this.outline.Count < 3)
                throw new ArgumentException("Outline must have at least 3 points to form a polygon.");
        }

        /// <summary>
        /// Checks if the given point (lat, lon) is inside the polygon defined by the Outline.
        /// Uses ray-casting algorithm (not sure what it does when exactly on the outline).
        /// </summary>
        public bool IsPointInside(double lat, double lon)
        {
            bool inside = false;
            int n = outline.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var pi = outline[i];
                var pj = outline[j];

                bool intersect = ((pi.lon > lon) != (pj.lon > lon)) &&
                                 (lat < (pj.lat - pi.lat) * (lon - pi.lon) / (pj.lon - pi.lon) + pi.lat);

                if (intersect)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>
        /// flips the points to the right-handed coordinate system, this is needed, as the jsonDeserialisation doesent use the constructor
        /// </summary>
        public void flipToRightHandedCoordinateSystem()
        {
            foreach (Point p in outline)
            {
                p.lat = -p.lat;
            }
        }
        
        
        //debug function
        public void Plot(string name = "DebugRoom", Color? color = null, float height = 9f)
        {
            if (outline == null || outline.Count < 3)
            {
                Debug.LogError($"Room {id} has invalid or missing outline.");
                return;
            }

            GameObject obj = new GameObject($"{name}_{id}");
            LineRenderer line = obj.AddComponent<LineRenderer>();

            line.positionCount = outline.Count;
            line.useWorldSpace = true;
            line.loop = true;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = color ?? Color.yellow;
            line.widthMultiplier = 0.2f;

            for (int i = 0; i < outline.Count; i++)
            {
                var p = outline[i];
                line.SetPosition(i, new Vector3((float)p.lat, height, (float)p.lon));
            }
        }


        [Serializable]
        public class Point
        {
            public double lat;
            public double lon;

            public Point(double lat, double lon)
            {
                this.lat = lat;
                this.lon = lon;
            }
        }
    }

}