using System.Collections.Generic;
using model.graph;
using UnityEngine;
using TMPro;


public class GraphManager : MonoBehaviour
{
    public Graph graph;
    public TMP_Dropdown fromField;
    public TMP_Dropdown toField;


    void Awake()
    {
        Debug.Log($"Graph script initialized");


        TextAsset jsonFile = Resources.Load<TextAsset>("graph"); // No .json extension
        if (jsonFile != null)
        {
            graph = new Graph(jsonFile.text);
        }
        else
        {
            Debug.LogError("graph.json not found in Resources folder!");
        }
        
        PopulateDropdownFromVertices(fromField, graph.GetVertices());
        PopulateDropdownFromStrings(toField,  new List<string>(graph.allRoomsSet));
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
        List<Vertex> verts = graph.GetVertices();
        Vertex fromVertex = verts[fromField.value];
        string toRoom = toField.options[toField.value].text;

        Debug.Log($"Navigating from {fromVertex.name} to {toRoom}");

        List<Edge> path = graph.FindShortestPathByName(fromVertex, toRoom);
        
        Debug.Log($" path found with lenght {path.Count}");
        
        foreach (Edge e in path)
        {
            e.path.Plot();
        }
        
        //todo plot 
        //todo normalize coordinates of graph
    }


}