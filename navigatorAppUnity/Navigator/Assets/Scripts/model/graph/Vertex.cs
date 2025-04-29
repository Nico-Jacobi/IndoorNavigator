using System;
using System.Collections.Generic;

namespace model.graph
{

    [Serializable]
    public class Vertex
    {
        public double lat;
        public double lon;
        public int floor;
        public string name;
        public List<string> rooms;
        public List<Edge> edges;

        public Vertex(double lat, double lon, int floor, string name, List<string> rooms = null)
        {
            this.lat = lat;
            this.lon = lon;
            this.floor = floor;
            this.name = name;
            this.rooms = rooms ?? new List<string>(); // Default to empty list if null is passed
            this.edges = new List<Edge>();
        }
    }
}