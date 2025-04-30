using model;
using System.Collections.Generic;
using model.graph;
using UnityEngine;

namespace controller
{
    public class BuildingManager : MonoBehaviour
    {
        private Dictionary<string, Building> buildings = new Dictionary<string, Building>();

        // Store the current active building and floor
        private Building activeBuilding;
        private GameObject activeFloorObject;   //the model shown

        
        public Building GetActiveBuilding()
        {
            return activeBuilding;
        }
        
        
        public Graph GetActiveGraph()
        {
            return activeBuilding.graph;
        }

        void Awake()
        {
            LoadBuildingConfigs();
            SpawnBuildingFloor("h4", 3); 
            // default one at the start, to initialize activeBuilding
            // so it is not null anywhere else
            
            Debug.Log($"Buildings Manager script initialized");
            Debug.Log($"activeBuilding is: {activeBuilding.buildingName}" );
        }

        void LoadBuildingConfigs()
        {
            // Get all config.json files in the Resources/Buildings folder
            TextAsset[] configFiles = Resources.LoadAll<TextAsset>("Buildings");

            foreach (var configFile in configFiles)
            {
                if (!configFile.name.EndsWith("config")) continue; // Unity strips ".json" from file.name

                // Deserialize the JSON into BuildingConfig
                BuildingConfig buildingConfig = JsonUtility.FromJson<BuildingConfig>(configFile.text);
                if (buildingConfig == null)
                {
                    Debug.LogError("Failed to deserialize the building config.");
                    return;
                }
                
                string graphFileLocation = $"Buildings/{buildingConfig.graph}".Replace(".json", "");
                TextAsset jsonFile = Resources.Load<TextAsset>(graphFileLocation); 
                if (jsonFile == null)
                {
                    Debug.LogError($"graph.json not found in Resources/Buildings! Expected file path: {graphFileLocation}.json");
                }
                
                Graph graph = new Graph(jsonFile.text);

                // Convert to a Building object
                List<Building.Floor> floors = new List<Building.Floor>();
                foreach (var floorConfig in buildingConfig.floors)
                {
                    string wallsPrefab = floorConfig.walls.Replace(".obj", "");
                    string groundPrefab = floorConfig.ground.Replace(".obj", "");

                    floors.Add(new Building.Floor
                    {
                        level = floorConfig.level,
                        walls = wallsPrefab,
                        ground = groundPrefab
                    });
                }
                
                
                Building building = new Building(buildingConfig.building, graph, floors);
                buildings[buildingConfig.building] = building;

                
                

                Debug.Log($"Loaded building: {buildingConfig.building}");
            }
        }

        public Building GetBuilding(string buildingName)
        {
            if (buildings.ContainsKey(buildingName))
            {
                return buildings[buildingName];
            }
            else
            {
                Debug.LogError($"Building {buildingName} not found!");
                return null;
            }
        }

        // Spawn a new floor, replacing the old one if any
        public void SpawnBuildingFloor(string buildingName, int floorLevel)
        {
            Building building = GetBuilding(buildingName);

            if (building != null)
            {
                // If there's already an active floor, destroy the old one
                if (activeFloorObject != null)
                {
                    Destroy(activeFloorObject);
                }

                // Create a new parent object for the building (optional)
                GameObject buildingObject = new GameObject(buildingName);
                activeBuilding = building; // Set the active building
                building.SpawnFloor(floorLevel, buildingObject.transform); // Spawn the floor
                activeFloorObject = buildingObject; // Store the spawned floor object
            }
        }
    }

    [System.Serializable]
    public class BuildingConfig
    {
        public string building;
        public string graph;
        public Floor[] floors;

        [System.Serializable]
        public class Floor
        {
            public int level;
            public string walls;
            public string ground;
        }
    }
}
