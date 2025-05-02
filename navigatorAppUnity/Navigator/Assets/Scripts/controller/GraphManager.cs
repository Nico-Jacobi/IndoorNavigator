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
        public TMP_Dropdown fromField;
        public TMP_Dropdown toField;
        public BuildingManager buildingManager;
        public WifiManager wifiManager;

        void Start()
        {
            Debug.Log($"Graph Manager script initialized");


            PopulateDropdownFromVertices(fromField, buildingManager.GetActiveBuilding().graph.GetVertices());
            PopulateDropdownFromStrings(toField, new List<string>(buildingManager.GetActiveGraph().allRoomsSet));
        }


        void PopulateDropdownFromVertices(TMP_Dropdown dropdown, List<Vertex> verts)
        {
            dropdown.ClearOptions();
            List<string> names = verts
                .Where(v => v.rooms.Count > 0)
                .Select(v => $"{v.rooms[0]}")
                .ToList();
            dropdown.AddOptions(names);
        }

        void PopulateDropdownFromStrings(TMP_Dropdown dropdown, List<string> options)
        {
            dropdown.ClearOptions();
            List<string> names = options.FindAll(v => v.Contains("Seminar"));

            dropdown.AddOptions(names);
        }


        public void OnNavigateButtonPressed()
        {
            List<Vertex> verts = buildingManager.GetActiveGraph().GetVertices();
            Vertex fromVertex = verts[fromField.value];
            string toRoom = toField.options[toField.value].text;

            Debug.Log($"Navigating from {fromVertex.name} to {toRoom}");

            List<Edge> path = buildingManager.GetActiveGraph().FindShortestPathByName(fromVertex, toRoom);

            Debug.Log($" path found with lenght {path.Count}");

            Position pos = wifiManager.GetPosition();
            Color[] startColors = { Color.cyan, Color.magenta };
            Color[] endColors = { Color.magenta, Color.cyan };
            int i = 0;

            foreach (Edge e in path)
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