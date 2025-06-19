using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using model;
using model.graph;
using UnityEngine;
using Edge = model.graph.Edge;

namespace controller
{
    public class GraphManager : MonoBehaviour
    {
        public Registry registry;

        public GameObject stairsArrowPrefab;

        private List<Edge> currentPath;
        private int recalculated = 0;
        
        public bool navigationActive = false;

        void Start()
        {
            Debug.Log("Graph Manager script initialized");

            // Initialize navigation dialog
            registry.navigationDialog.OnNavigationRequested += OnNavigationRequested;

            StartCoroutine(UpdatePath());
        }

        public float GetHeading()
        {
            float angle = currentPath?.FirstOrDefault()?.GetAngle() ?? 0f;
            angle += 90;
            return angle;
        }

        public void OnNavigateButtonPressed()
        {
            registry.navigationDialog.ShowDialog();
            registry.cameraController.inMenu = true;
        }

        private void OnNavigationRequested(string destination)
        {
            registry.cameraController.GotoPrediction();
            Vertex fromVertex = GetStart();
            Navigate(fromVertex, destination);
            registry.cameraController.inMenu = false;
        }

        public async void Navigate(Vertex fromVertex, string toRoom)
        {
            Debug.Log($"Navigating from {fromVertex} to {toRoom}");

            if (currentPath?.Count > 6 && currentPath.Last().target.HasRoomNamed(toRoom) && recalculated < 5)
            {
                recalculated++;
                // Only partly calculate the current path (way faster if the user just moved a bit)
                Debug.Log("Partially recalculating path");
                // Removing the first few edges, and just recalculating them for faster calculations
                currentPath = await registry.buildingManager.GetActiveGraph()
                    .FindShortestPathToTargetEdgesAsync(fromVertex, currentPath.GetRange(5, currentPath.Count - 5));
            }
            else
            {
                recalculated = 0;
                currentPath = await registry.buildingManager.GetActiveGraph().FindShortestPathByNameAsync(fromVertex, toRoom);
                Debug.Log("Completely calculating path");
            }

            InterpolateStart();
            navigationActive = true;

            PlotCurrentPath();
            Debug.Log($"Path found with length {currentPath.Count}");
        }

        public Vertex GetStart()
        {
            List<Vertex> verts = registry.buildingManager.GetActiveGraph().GetVertices();
            Position pos = registry.GetPositionFilter().GetEstimate();

            // Filter vertices on correct floor and sort
            verts = verts
                .Where(v => v.floor == pos.Floor)
                .OrderBy(v =>
                {
                    double dx = v.lat - pos.X;
                    double dy = v.lon - pos.Y;
                    return dx * dx + dy * dy;
                })
                .ToList();

            // Find all vertices with rooms that contain the user's position
            List<Vertex> candidates = verts
                .Where(v => v.rooms.Any(r => r.IsPointInside(pos.X, pos.Y)))
                .ToList();

            if (candidates.Count > 0)
            {
                Vertex closest = candidates
                    .OrderBy(v =>
                    {
                        double dx = v.lat - pos.X;
                        double dy = v.lon - pos.Y;
                        return dx * dx + dy * dy;
                    })
                    .First();

                registry.topMenu.UpdateCurrentRoomDisplay(string.Join(", ", closest.rooms.Where(r => r.IsPointInside(pos.X, pos.Y)).Select(r => r.name)));

                return closest;
            }

            // Fallback to closest vertex if no room matches
            if (verts.Count > 0)
            {
                return verts[0];
            }
            
            Debug.Log("getStart retuning null, no pos found");
            return null;
        }

        /// <summary>
        /// Makes the start of the path better, by adding a temporary vertex at the user's position, using parts of existing PathData objects if possible
        /// </summary>
        private void InterpolateStart()
        {
            // check if we have a path to work with
            if (currentPath == null || currentPath.Count == 0)
                return;

            Position pos = registry.GetPositionFilter().GetEstimate();
            Edge firstEdge = currentPath[0];

            // Source and target vertices always share exactly one room
            Room commonRoom = firstEdge.source.rooms.First(r1 => firstEdge.target.rooms.Any(r2 => r2.id == r1.id));

            Room currentRoom = null; // The room the user is in 
            Vertex nextVertex = null; // vertex we'll connect to after potential removal
            
            if (commonRoom.IsPointInside(pos.X, pos.Y))
            {
                // In this case the user is in the same room as the first path
                nextVertex = firstEdge.target; // save the target before removing
                currentPath.RemoveAt(0);
                currentRoom = commonRoom;
            }
            else
            {
                nextVertex = firstEdge.source; // keep the source
                currentRoom = firstEdge.source.GetOtherRoom(commonRoom);
            }

            // check again after potential removal
            if (currentPath.Count == 0)
            {
                // just add direct connection if no path left
                Vertex tempVertex1 = new Vertex(pos.X, pos.Y, pos.Floor, "userPositionVertex", new List<Room>());
                List<Point> pathPoints1 = new List<Point>();
                currentPath.Add(new Edge(tempVertex1, nextVertex, new PathData(0, pathPoints1)));
                return;
            }

            // Now finding an optimal path within the room to "attach" to
            // This represents all edges within the current room, which could be used for a nice path
            List<Edge> possibleCloseEdges = nextVertex.GetEdgesToRoom(currentRoom);

            Vertex tempVertex = new Vertex(pos.X, pos.Y, pos.Floor, "userPositionVertex", new List<Room>());

            // Finding a path that gets close to the user, cutting it there and adding it to the path:
            if (possibleCloseEdges.Count > 0)
            {
                Edge closestEdge = possibleCloseEdges[0];
                Point closestPoint = closestEdge.path.points[0];
                double closestDistSq = double.PositiveInfinity;

                foreach (Edge e in possibleCloseEdges)
                {
                    Point localClosestPoint = GetClosestPoint(e.path.points, pos);
                    double distSq = CalculateDistance(localClosestPoint, pos);

                    if (distSq < closestDistSq)
                    {
                        closestDistSq = distSq;
                        closestPoint = localClosestPoint;
                        closestEdge = e;
                    }
                }

                List<Point> subPath = null;
                int idx = closestEdge.path.points.IndexOf(closestPoint);
                if (idx >= 0)
                {
                    // Get all points from start to idx inclusive
                    subPath = closestEdge.path.points.GetRange(0, idx + 1);
                }

                if (subPath != null)
                {
                    subPath.Reverse();
                    currentPath.Insert(0,
                        new Edge(
                            new Vertex(subPath[0].lat, subPath[0].lon, pos.Floor, "partialPathVertex", new List<Room>()),
                            currentPath[0].source, new PathData(0, subPath)));
                }
            }

            // Add a direct line from the position to the start (to the door or the partialPathVertex created earlier)
            List<Point> pathPoints = new List<Point>();
            currentPath.Insert(0, new Edge(tempVertex, currentPath[0].source, new PathData(0, pathPoints)));
        }

        private double CalculateDistance(Point point1, Position pos)
        {
            return Math.Pow((point1.lat - pos.X), 2) + Math.Pow((point1.lon - pos.Y), 2);
        }

        private Point GetClosestPoint(List<Point> points, Position pos)
        {
            double dist = double.PositiveInfinity;
            Point closestPoint = null;

            foreach (Point point in points)
            {
                double currentDistance = CalculateDistance(point, pos);
                if (currentDistance < dist)
                {
                    dist = currentDistance;
                    closestPoint = point;
                }
            }

            return closestPoint;
        }

        private IEnumerator UpdatePath()
        {
            while (true)
            {
                Vertex fromVertex = GetStart();
                if (navigationActive)
                {
                    string currentDestination = registry.navigationDialog.GetSelectedDestination();
                    if (!string.IsNullOrEmpty(currentDestination))
                    {
                        Navigate(fromVertex, currentDestination); // Will overwrite currentPath
                    }
                }

                yield return new WaitForSeconds(5f);
            }
        }

        public void PlotCurrentPath()
        {
            foreach (GameObject obj in GameObject.FindGameObjectsWithTag("PlottedPath"))
                Destroy(obj);

            if (currentPath == null || currentPath.Count == 0)
            {
                navigationActive = false;
                return;
            }
            
            if (!registry.buildingManager.ShownEqualsActiveBuilding()) return;  //the path cant be shown as we are in the wrong building

            Color[] startColors = { Color.cyan, Color.magenta };
            Color[] endColors = { Color.magenta, Color.cyan };

            int idx = 0;
            while (idx < currentPath.Count)
            {
                if ((idx + 1 < currentPath.Count) && IsStairsEdge(currentPath[idx + 1]))
                {
                    int stairsStart = idx + 1;

                    while (stairsStart < currentPath.Count && IsStairsEdge(currentPath[stairsStart]))
                    {
                        Edge stairEdge = currentPath[stairsStart];

                        if (stairEdge.source.floor == registry.buildingManager.GetShownFloor())
                        {
                            float height = stairEdge.source.floor * 2 + 2f;
                            Vector3 arrowPos = new Vector3((float)stairEdge.source.lat, height, (float)stairEdge.source.lon);
                            Quaternion rotation = stairEdge.target.floor > stairEdge.source.floor
                                ? Quaternion.Euler(90f, 0f, 0f) // up
                                : Quaternion.Euler(-90f, 0f, 0f); // down

                            GameObject arrow = GameObject.Instantiate(stairsArrowPrefab, arrowPos + Vector3.up * 0.5f, rotation);
                            arrow.tag = "PlottedPath";
                        }

                        stairsStart++;
                    }

                    idx = stairsStart + 1;
                    continue;
                }

                Edge e = currentPath[idx];
                if (e.source.floor == registry.buildingManager.GetShownFloor())
                {
                    float height = registry.buildingManager.GetShownFloor() * 2 + 0.5f;

                    e.path.Plot(
                        height: height,
                        startColor: startColors[idx % 2],
                        endColor: endColors[idx % 2]
                    );
                }

                idx++;
            }
        }

        private bool IsStairsEdge(Edge e)
        {
            return e.source.floor != e.target.floor;
        }

        public void RefreshRoomOptions()
        {
            List<string> allRoomNames = new List<string>(registry.buildingManager.GetActiveGraph().allRoomsNames);
            registry.navigationDialog.RefreshOptions(allRoomNames);
        }
        
        
        public void CancelNavigation()
        {
            Debug.Log("Navigation cancelled");
            navigationActive = false;
            currentPath = null;
            recalculated = 0;
    
            foreach (GameObject obj in GameObject.FindGameObjectsWithTag("PlottedPath"))
            {
                Destroy(obj);
            }
    
        }
        
    }
}