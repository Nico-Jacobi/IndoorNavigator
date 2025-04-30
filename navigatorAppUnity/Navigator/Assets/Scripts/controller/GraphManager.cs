using System.Collections.Generic;
using controller;
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
            List<string> names = verts.ConvertAll(v => $"{v.floor}");
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

            Positioning pos = wifiManager.GetPositioning();
            foreach (Edge e in path)
            {
                if (e.source.floor == pos.GetFloor())
                {
                    e.path.Plot(height:pos.GetFloorHeight()+0.5f);
                }
            }

            //todo plot 
            //todo normalize coordinates of graph
        }


    }
}