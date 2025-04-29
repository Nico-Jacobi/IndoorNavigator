using System;

namespace model.graph
{
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