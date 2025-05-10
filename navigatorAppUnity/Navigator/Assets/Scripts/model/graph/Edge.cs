using UnityEngine;

namespace model.graph
{

    public class Edge
    {
        public Vertex source;
        public Vertex target;
        public PathData path;

        public Edge(Vertex source, Vertex target, PathData path)
        {
            this.source = source;
            this.target = target;
            this.path = path;
            this.path.AddStartAndEndPoints(new Point(source.lat, source.lon), new Point(target.lat, target.lon));
        }
        
        /// <summary>
        /// Gets the angle of the edge from 0 to 360 degrees, calculated from the source to the target.
        /// </summary>
        /// <returns>The angle in degrees (0 to 360)</returns>
        public float GetAngle()
        {
            float deltaX = (float) (target.lon - source.lon);
            float deltaY = (float) (target.lat - source.lat);

            // Calculate the angle in radians
            float angleInRadians = Mathf.Atan2(deltaY, deltaX);

            // Convert the angle to degrees
            float angleInDegrees = angleInRadians * Mathf.Rad2Deg;

            // Normalize the angle to be between 0 and 360
            if (angleInDegrees < 0)
            {
                angleInDegrees += 270;
            }

            return angleInDegrees;
        }
    }

}