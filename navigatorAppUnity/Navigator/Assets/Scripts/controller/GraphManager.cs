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


        void Start()
        {
            Debug.Log($"Graph Manager script initialized");


            PopulateDropdownFromVertices(fromField, buildingManager.activeBuilding.graph.GetVertices());
            PopulateDropdownFromStrings(toField, new List<string>(buildingManager.activeBuilding.graph.allRoomsSet));
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
            List<Vertex> verts = buildingManager.activeBuilding.graph.GetVertices();
            Vertex fromVertex = verts[fromField.value];
            string toRoom = toField.options[toField.value].text;

            Debug.Log($"Navigating from {fromVertex.name} to {toRoom}");

            List<Edge> path = buildingManager.activeBuilding.graph.FindShortestPathByName(fromVertex, toRoom);

            Debug.Log($" path found with lenght {path.Count}");

            foreach (Edge e in path)
            {
                e.path.Plot();
            }

            //todo plot 
            //todo normalize coordinates of graph
        }


    }
}