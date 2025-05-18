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
        public List<Room> rooms;
        public List<Edge> edges;

        public Vertex(double lat, double lon, int floor, string name, List<Room> rooms = null)
        {
            this.lat = lat;
            this.lon = lon;
            this.floor = floor;
            this.name = name;
            this.rooms = rooms ?? new List<Room>(); // Default to empty list if null is passed
            this.edges = new List<Edge>();
        }
        
        /// <summary>
        /// Checks if any room in this vertex has the given name.
        /// </summary>
        public bool HasRoomNamed(string roomName)
        {
            foreach (Room room in rooms)
            {
                if (room.name == roomName) // or room.name if Room has a name property
                    return true;
            }
            return false;
        }
        
        
        /// <summary>
        /// Returns the names of all rooms in this vertex.
        /// </summary>
        public List<string> GetRoomNames()
        {
            List<string> names = new();
            foreach (Room room in rooms)
            {
                names.Add(room.name); // or room.name if it exists
            }
            return names;
        }
        
        /// <summary>
        /// Returns all Edges to given Room.
        ///  Pass the same room as this to get room-internal edges
        /// </summary>
        public List<Edge> GetEdgesToRoom(Room givenRoom)
        {
            List<Edge> edgesToRoom = new();
            foreach (Edge e in edges)
            {

                foreach (Room r in e.target.rooms)
                {
                    if (r.id == givenRoom.id)
                    {
                        edgesToRoom.Add(e);
                    }
                }
            }
            return edgesToRoom;
        }

        /// <summary>
        /// Returns the room NOT given
        /// </summary>
        public Room GetOtherRoom(Room givenRoom)
        {
            foreach (var r in rooms)
            {
                if (r.id != givenRoom.id)
                    return r;
            }
            return null;
        }


    }
}