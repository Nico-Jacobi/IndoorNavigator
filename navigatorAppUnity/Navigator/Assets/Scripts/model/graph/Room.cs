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

                bool intersect = ((pi.Lon > lon) != (pj.Lon > lon)) &&
                                 (lat < (pj.Lat - pi.Lat) * (lon - pi.Lon) / (pj.Lon - pi.Lon) + pi.Lat);

                if (intersect)
                    inside = !inside;
            }
            return inside;
        }

        public class Point
        {
            public double Lat { get; }
            public double Lon { get; }

            public Point(double lat, double lon)
            {
                Lat = lat;
                Lon = lon;
            }
        }
    }

}