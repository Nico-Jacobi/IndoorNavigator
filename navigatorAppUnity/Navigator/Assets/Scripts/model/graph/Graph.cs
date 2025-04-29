using UnityEngine;
using System.Collections.Generic;
using System;

namespace model.graph
{
    [System.Serializable]
    class JsonGraphData //temporary class only used for easy parsing from the json
    {
        public bool bidirectional;
        //todo add metadata such as origin, level, .obj-name here
        public List<JsonVertex> vertices;
        public List<JsonEdge> edges;
    }
    
    [System.Serializable]
    class JsonVertex //temporary class only used for easy parsing from the json
    {
        public int id;
        public double lat;
        public double lon;
        public int floor;
        public string name;
        public List<string> rooms;
    }
    
    [System.Serializable]
    class JsonEdge //temporary class only used for easy parsing from the json
    {
        public int v1;
        public int v2;
        public PathData path;
    }
    
    
    
    public class Graph
    {
        private List<Vertex> vertices;
        private List<Edge> edges;
        public HashSet<string> allRoomsSet;
        
    
        public Graph(string jsonData)    {
            vertices = new List<Vertex>();
            edges = new List<Edge>();
            allRoomsSet = new HashSet<string>();
    
    
            JsonGraphData graphData = JsonUtility.FromJson<JsonGraphData>(jsonData);
                        
            // Process the parsed data, as the data is stored slightly different from the json
            if (graphData != null)
            {
                foreach (JsonVertex vertex in graphData.vertices) 
                {
                    vertices.Add(new Vertex(vertex.lat, vertex.lon, vertex.floor, vertex.name, vertex.rooms));
                    foreach (string room in vertex.rooms)
                    {
                        allRoomsSet.Add(room);
                    }
                }
                
                foreach (var edge in graphData.edges)
                {
                    
                    Edge e1 = new Edge(vertices[edge.v1], vertices[edge.v2], edge.path);
                    edges.Add(e1);
                    vertices[edge.v1].edges.Add(e1);
                    
                    if (graphData.bidirectional)
                    {
                        Edge e2 = new Edge(vertices[edge.v2], vertices[edge.v1], edge.path.GetReverseCopy());
                        edges.Add(e2);
                        vertices[edge.v2].edges.Add(e2);
                    }
                }
                Debug.Log($"Loaded graph with {graphData.vertices.Count} vertices and {graphData.edges.Count} edges");
            }
            
        }
    
        
        
    
        public List<Vertex> GetVertices()
        {
            return vertices;
        }
    
        
        // Find shortest path by room name
        public List<Edge> FindShortestPathByName(Vertex start, string targetName)
        {
            Dictionary<Vertex, double> distances = new Dictionary<Vertex, double>();
            Dictionary<Vertex, Vertex> previous = new Dictionary<Vertex, Vertex>();
            Dictionary<Vertex, Edge> connectingEdges = new Dictionary<Vertex, Edge>();
            HashSet<Vertex> visited = new HashSet<Vertex>();
    
            // Initialize distances
            foreach (var vertex in vertices)
            {
                distances[vertex] = double.PositiveInfinity;
                previous[vertex] = null;
                connectingEdges[vertex] = null;
            }
            distances[start] = 0;
    
            while (true)
            {
                Vertex current = null;
                double minDistance = double.PositiveInfinity;
    
                // Find unvisited vertex with minimum distance
                foreach (var vertex in vertices)
                {
                    if (!visited.Contains(vertex) && distances[vertex] < minDistance)
                    {
                        current = vertex;
                        minDistance = distances[vertex];
                    }
                }
    
                // No vertex found
                if (current == null)
                {
                    Debug.Log($"No path found from {start.name} to {targetName}");
                    throw new Exception($"No path found from {(start.rooms.Count > 0 ? start.rooms[0] : "unknown")} to {targetName}");
                }
    
                // Check if we found the target room
                if (current.rooms.Contains(targetName))
                {
                    List<Edge> pathEdges = new List<Edge>();
                    Vertex currentVertex = current;
    
                    while (previous[currentVertex] != null)
                    {
                        Edge edge = connectingEdges[currentVertex];
    
                        if (edge == null)
                        {
                            Debug.Log("Error reconstructing path: no connecting edge found");
                            throw new Exception("Error reconstructing path: no connecting edge found");
                        }
    
                        pathEdges.Insert(0, edge);
                        currentVertex = previous[currentVertex];
                    }
    
                    Debug.Log("Path reconstruction complete");
                    return OptimizePath(pathEdges);
                }
    
                // Mark as visited
                visited.Add(current);
    
                // If we've reached infinity distance, there's no path to remaining vertices
                if (double.IsInfinity(distances[current]))
                {
                    Debug.Log($"No path found from {start.name} to {targetName} (infinity reached)");
                    throw new Exception($"No path found from {start.name} to {targetName}");
                }
    
                // Update distances to neighbors
                foreach (Edge edge in current.edges)
                {
                    Vertex neighbor = edge.target;
                    if (visited.Contains(neighbor))
                    {
                        continue;
                    }
    
                    double newDist = distances[current] + edge.path.weight;
                    if (newDist < distances[neighbor])
                    {
                        distances[neighbor] = newDist;
                        previous[neighbor] = current;
                        connectingEdges[neighbor] = edge;
                    }
                }
            }
        }
        
        
        // fixes indirect paths which djikstra can return in this case, as a "normal" path is not always the shortest
        // eg sometimes it visits but doesent use doors in a room, to be faster (which cant be directly stopped at that point)
      public List<Edge> OptimizePath(List<Edge> pathEdges)
        {
            if (pathEdges.Count == 0) return new List<Edge>();
            if (pathEdges.Count == 1) return new List<Edge>(pathEdges);
    
            List<Edge> cleanedEdges = new List<Edge>();
            int i = 0;
    
            while (i < pathEdges.Count)
            {
                // Get the current vertex we're starting from
                Vertex current = pathEdges[i].source;
    
                // Look ahead as far as possible
                int furthestIndex = -1;
                Edge directEdge = null;
    
                // Try to find the furthest vertex we can skip to
                for (int j = i + 1; j < pathEdges.Count; j++)
                {
                    Vertex candidate = pathEdges[j].target;
    
                    // Check if there's a common room (meaning we might be able to directly connect)
                    bool shareRoom = false;
                    foreach (string r in candidate.rooms)
                    {
                        if (current.rooms.Contains(r))
                        {
                            shareRoom = true;
                            break;
                        }
                    }
    
                    // If they share a room, check if there's a direct edge
                    if (shareRoom)
                    {
                        foreach (Edge e in current.edges)
                        {
                            if (e.target == candidate)
                            {
                                // Found a potential skip - save this as our furthest candidate so far
                                furthestIndex = j;
                                directEdge = e;
                                break;
                            }
                        }
                    }
                }
    
                if (furthestIndex != -1 && directEdge != null)
                {
                    // found a vertex we can skip to - add the direct edge
                    cleanedEdges.Add(directEdge);
    
                    // Continue from the vertex after our furthest skipped vertex
                    i = furthestIndex + 1;
                }
                else
                {
                    // No skippable vertices found, add the current edge and move to the next
                    cleanedEdges.Add(pathEdges[i]);
                    i++;
                }
    
                // Handle the last edge if we haven't processed it yet
                if (i == pathEdges.Count - 1)
                {
                    cleanedEdges.Add(pathEdges[i]);
                    i++;
                }
            }
    
            return cleanedEdges;
        }
    }
}
