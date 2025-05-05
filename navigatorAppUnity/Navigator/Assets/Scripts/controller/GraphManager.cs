using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using controller;
using model;
using model.graph;
using UnityEngine;
using TMPro;

namespace controller
{
    public class GraphManager : MonoBehaviour
    {
        public TMP_Dropdown toField;
        public BuildingManager buildingManager;
        public WifiManager wifiManager;

        private List<Edge> currentPath;
        public bool navigationActive = false;

        void Start()
        {
            Debug.Log("Graph Manager script initialized");

            PopulateDropdownFromStrings(toField, new List<string>(buildingManager.GetActiveGraph().allRoomsSet));

            StartCoroutine(UpdatePath());
        }


        void PopulateDropdownFromStrings(TMP_Dropdown dropdown, List<string> options)
        {
            dropdown.ClearOptions();
            List<string> names = options.FindAll(v => v.Contains("Seminar"));
            dropdown.AddOptions(names);
        }

        public void OnNavigateButtonPressed()
        {
            Vertex fromVertex = GetStart();
            string toRoom = toField.options[toField.value].text;

            Debug.Log($"Navigating from {fromVertex.name} to {toRoom}");

            currentPath = buildingManager.GetActiveGraph().FindShortestPathByName(fromVertex, toRoom);

            InterpolateStart();
            navigationActive = true;

            PlotCurrentPath();
            Debug.Log($"Path found with length {currentPath.Count}");
        }

        public Vertex GetStart()
        {
            List<Vertex> verts = buildingManager.GetActiveGraph().GetVertices();
            Position pos = wifiManager.GetPosition();

            verts = verts
                .Where(v => v.floor == pos.Floor)
                .OrderBy(v =>
                {
                    double dx = v.lat - pos.X;
                    double dy = v.lon - pos.Y;
                    return dx * dx + dy * dy;
                })
                .ToList();

            return verts[0];
        }

        // make the currentPath better by making it start at the current position
        private void InterpolateStart()
        {
            Position pos = wifiManager.GetPosition();

            Point closestPoint = GetClosestPoint(currentPath[0].path.points, pos);
            if (closestPoint != null)
            {
                double distToStart =
                    CalculateDistance(new Point(currentPath[0].source.lat, currentPath[0].source.lon), pos);
                double distToClosest = CalculateDistance(closestPoint, pos);

                if (distToClosest < distToStart)
                {
                    List<Point> partialPath = GetPartialPathAfterPoint(currentPath[0].path.points, closestPoint);
                    Edge p = new Edge(new Vertex(pos.X, pos.Y, pos.Floor, "CurrentPosTemp"), currentPath[0].target,
                        new PathData(1, partialPath));
                    currentPath[0] = p;
                }
                else
                {
                    Edge closestEdge = GetClosestEdgeToPosition(pos);
                    List<Point> partialPath = GetPartialPathAfterPoint(closestEdge.path.points, closestPoint);
                    Edge p = new Edge(new Vertex(pos.X, pos.Y, pos.Floor, "CurrentPosTemp"), currentPath[0].source,
                        new PathData(1, partialPath));
                    currentPath.Insert(0, p);
                }
            }
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

        private Edge GetClosestEdgeToPosition(Position pos)
        {
            double dist = Double.PositiveInfinity;
            Point closestPoint = null;
            Edge closestEdge = null;

            foreach (Edge e in currentPath[0].source.edges)
            {
                foreach (Point point in e.path.points)
                {
                    double currentDistance = CalculateDistance(point, pos);
                    if (currentDistance < dist)
                    {
                        dist = currentDistance;
                        closestPoint = point;
                        closestEdge = e;
                    }
                }
            }

            return closestEdge;
        }


        private IEnumerator UpdatePath()
        {
            while (true)
            {
                if (navigationActive)
                {
                    OnNavigateButtonPressed(); //will overwrite currentPath
                    PlotCurrentPath();
                }

                yield return new WaitForSeconds(5f);
            }
        }


        public void PlotCurrentPath()
        {
            if (currentPath == null || currentPath.Count == 0)
            {
                navigationActive = false;
                return;
            }


            Position pos = wifiManager.GetPosition();
            Color[] startColors = { Color.cyan, Color.magenta };
            Color[] endColors = { Color.magenta, Color.cyan };

            int i = 0;
            foreach (Edge e in currentPath)
            {
                if (e.source.floor == pos.Floor)
                {
                    e.path.Plot(
                        height: pos.GetFloorHeight() + 0.5f,
                        startColor: startColors[i % 2],
                        endColor: endColors[i % 2],
                        smoothness: 100
                    );
                    i++;
                }
            }
        }
    }
}