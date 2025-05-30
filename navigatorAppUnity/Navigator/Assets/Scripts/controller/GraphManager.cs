using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using controller;
using model;
using model.graph;
using UnityEngine;
using TMPro;
using Unity.VisualScripting;
using view;
using Edge = model.graph.Edge;

namespace controller
{
    public class GraphManager : MonoBehaviour
    {
        public TMP_Dropdown toField;
        public RectTransform navigationDialog;
        public TMP_InputField searchField;

        public Registry registry;

        private List<string> allOptions;
        private List<Edge> currentPath;
        public bool navigationActive = false;

        public GameObject stairsArrowPrefab; 

        
        void Start()
        {
            Debug.Log("Graph Manager script initialized");

            allOptions = new List<string>(registry.buildingManager.GetActiveGraph().allRoomsNames);
            PopulateDropdownFromStrings(allOptions);

            StartCoroutine(UpdatePath());
            CloseNavigationDialog();

            searchField.onValueChanged.AddListener(OnSearchChanged);

        }

        public float GetHeading()
        {
            float angle = currentPath?.FirstOrDefault()?.GetAngle() ?? 0f;
            angle += 90;
            return angle;
        }



        private void PopulateDropdownFromStrings(List<string> options)
        {
            toField.ClearOptions();
            toField.AddOptions(options);
        }

        void OnSearchChanged(string input)
        {
            string lowerInput = input.ToLower();
            List<string> matches = allOptions
                .Where(option => option.ToLower().Contains(lowerInput))
                .ToList();

            PopulateDropdownFromStrings(matches);
        }


        public void OnNavigateButtonPressed()
        {
            navigationDialog.gameObject.SetActive(true);
            registry.cameraController.inMenu = true;
        }

        public void CloseNavigationDialog()
        {
            navigationDialog.gameObject.SetActive(false);
            registry.cameraController.inMenu = false;
        }

        private void OnStartNavigationButtonPressed()
        {
            registry.cameraController.GotoPrediction();
            Vertex fromVertex = GetStart();

            navigate(fromVertex);
            CloseNavigationDialog();
        }


        public async void navigate(Vertex fromVertex)
        {

            string toRoom = toField.options[toField.value].text;

            Debug.Log($"Navigating from {fromVertex.name} to {toRoom}");

            if (currentPath?.Count > 6 && currentPath.Last().target.HasRoomNamed(toField.options[toField.value].text))
            {
                //only partly calculate the current path (way faster if the user just moved a bit)
                Debug.Log("partially recalculating path");
                // removing the first few edges, and just recalculating them for faster calculations
                currentPath = await registry.buildingManager.GetActiveGraph()
                    .FindShortestPathToTargetEdgesAsync(fromVertex, currentPath.GetRange(5, currentPath.Count - 5));
            }
            else
            {
                currentPath = await registry.buildingManager.GetActiveGraph().FindShortestPathByNameAsync(fromVertex, toRoom);
                Debug.Log("completely calculating path");
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

            // Find all vertecies with rooms that contain the user's position
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

                registry.buildingManager.setCurrentRoom(string.Join(", ", closest.rooms.Where(r => r.IsPointInside(pos.X, pos.Y)).Select(r => r.name)));

                return closest;
            }

            // fallback to closest vertex if no room matches
            if (verts.Count > 0)
            {
                return verts[0];

            }
            return null;
        }


        /// <summary>
        /// makes the start of the path better, by adding a temporary vertex at the users position, using parts of existing PathData objects if possible
        /// </summary>
        private void InterpolateStart()
        {
            Position pos = registry.GetPositionFilter().GetEstimate();
            Edge firstEdge = currentPath[0];

            // source and target vertices always share exactly one room
            Room commonRoom = firstEdge.source.rooms.First(r1 => firstEdge.target.rooms.Any(r2 => r2.id == r1.id));

            Room currentRoom = null; //the room the user is in 
            if (commonRoom.IsPointInside(pos.X, pos.Y))
            {
                //in this case the user is in the same room as the first path
                currentPath.RemoveAt(0);
                currentRoom = commonRoom;
            }
            else
            {
                currentRoom = firstEdge.source.GetOtherRoom(commonRoom);
            }

            //currentRoom.Plot();

            // now finding a optimal path within the room to "attach" to

            //this represents all edges wihtin the current room, which could be used for a nice path
            List<Edge> possibleCloseEdges = currentPath[0].source.GetEdgesToRoom(currentRoom);


            Vertex tempVertex = new Vertex(pos.X, pos.Y, pos.Floor, "userPositionVertex", new List<Room>());


            // finding a path that gets close to the user, cutting it there and adding it to the path:
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

                subPath.Reverse();
                currentPath.Insert(0,
                    new Edge(
                        new Vertex(subPath[0].lat, subPath[0].lon, pos.Floor, "partialPathVertex", new List<Room>()),
                        currentPath[0].source, new PathData(0, subPath)));

            }


            // add a direct line from the pos to the start (to the door or the partialPathVertex created ealier)
            List<Point> PathPoints = new List<Point>();
            currentPath.Insert(0, new Edge(tempVertex, currentPath[0].source, new PathData(0, PathPoints)));

        }

        private double CalculateDistance(Point point1, Position pos)
        {
            return Math.Pow((point1.lat - pos.X), 2) + Math.Pow((point1.lon - pos.Y), 2);
        }

        private List<Point> GetPartialPathAfterPoint(List<Point> points, Point startPoint)
        {
            int index = points.IndexOf(startPoint);
            if (index != -1 && index + 1 < points.Count)
            {
                return points.Skip(index + 1).ToList();
            }

            return new List<Point>();
        }

        private Point GetClosestPoint(List<Point> points, Position pos)
        {
            double dist = Double.PositiveInfinity;
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
                    navigate(fromVertex); //will overwrite currentPath
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


    

    }
}