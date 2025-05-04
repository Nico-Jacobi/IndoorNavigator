using model;
using System.Collections.Generic;
using System.Linq;
using model.graph;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace controller
{
    public class BuildingManager : MonoBehaviour
    {
        private Dictionary<string, Building> buildings = new Dictionary<string, Building>();

        // Store the current active building and floor
        private Building activeBuilding;
        private GameObject activeFloorObject;   //the model shown
        private int activeFloorLevel = -1;      // Track the active floor level

        public SQLiteDatabase database;
        public TMP_Dropdown buildingField;
        
        // New UI elements for floor navigation
        public Button increaseFloorButton;
        public Button decreaseFloorButton;
        public TMP_Text currentFloorText;

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
            
            // Initialize buttons
            if (increaseFloorButton != null)
                increaseFloorButton.onClick.AddListener(IncreaseFloor);
            
            if (decreaseFloorButton != null)
                decreaseFloorButton.onClick.AddListener(DecreaseFloor);
            
            // Setup building dropdown
            buildingField.ClearOptions();
            buildingField.AddOptions(buildings.Keys.ToList());
            buildingField.onValueChanged.AddListener(buildingFieldChanged);
            
            // Default building setup
            SpawnBuildingFloor("h4", 3);
            
            // Set the dropdown to match the current building
            int index = buildingField.options.FindIndex(option => option.text == "h4");
            if (index >= 0)
            {
                buildingField.value = index;
                buildingField.RefreshShownValue();
            }
            
            Debug.Log($"Buildings Manager script initialized");
            Debug.Log($"activeBuilding is: {activeBuilding.buildingName}");
            
            // Update floor text
            UpdateFloorText();
        }

        private void buildingFieldChanged(int index)
        {
            string buildingName = buildingField.options[index].text;
            SpawnBuildingFloor(buildingName, buildings[buildingName].floors[0].level);
        }
        
        private void IncreaseFloor()
        {
            if (activeBuilding == null) return;
            
            // Find the next floor level
            int currentIndex = GetFloorIndex(activeFloorLevel);
            if (currentIndex < activeBuilding.floors.Count - 1)
            {
                int nextFloorLevel = activeBuilding.floors[currentIndex + 1].level;
                SpawnBuildingFloor(activeBuilding.buildingName, nextFloorLevel);
            }
        }
        
        private void DecreaseFloor()
        {
            if (activeBuilding == null) return;
            
            // Find the previous floor level
            int currentIndex = GetFloorIndex(activeFloorLevel);
            if (currentIndex > 0)
            {
                int prevFloorLevel = activeBuilding.floors[currentIndex - 1].level;
                SpawnBuildingFloor(activeBuilding.buildingName, prevFloorLevel);
            }
        }
        
        private int GetFloorIndex(int floorLevel)
        {
            for (int i = 0; i < activeBuilding.floors.Count; i++)
            {
                if (activeBuilding.floors[i].level == floorLevel)
                {
                    return i;
                }
            }
            return 0;
        }
        
        private void UpdateFloorText()
        {
            if (currentFloorText != null && activeBuilding != null)
            {
                currentFloorText.text = $"Floor: {activeFloorLevel}";
            }
        }
        
        private void UpdateBuildingUI()
        {
            // Update building dropdown to match current building
            if (activeBuilding != null)
            {
                int index = buildingField.options.FindIndex(option => option.text == activeBuilding.buildingName);
                if (index >= 0 && buildingField.value != index)
                {
                    buildingField.SetValueWithoutNotify(index);
                    buildingField.RefreshShownValue();
                }
            }
            
            // Update floor text
            UpdateFloorText();
            
            // Update button interactability based on available floors
            if (activeBuilding != null)
            {
                int currentIndex = GetFloorIndex(activeFloorLevel);
                
                if (increaseFloorButton != null)
                    increaseFloorButton.interactable = (currentIndex < activeBuilding.floors.Count - 1);
                
                if (decreaseFloorButton != null)
                    decreaseFloorButton.interactable = (currentIndex > 0);
            }
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
                    string doorPrefab = floorConfig.doors.Replace(".obj", "");

                    floors.Add(new Building.Floor
                    {
                        level = floorConfig.level,
                        walls = wallsPrefab,
                        ground = groundPrefab,
                        doors = doorPrefab
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

        public void SpawnBuildingFloor(string buildingName, int floorLevel)
        {
            // Check if the requested building and floor are already active
            if (activeBuilding != null && 
                activeBuilding.buildingName == buildingName && 
                activeFloorLevel == floorLevel)
            {
                //Debug.Log($"Building {buildingName}, floor {floorLevel} already active - skipping respawn");
                return;
            }

            Building building = GetBuilding(buildingName);

            if (building != null)
            {
                // Use the first floor if no specific floor is requested
                if (floorLevel == -1)
                {
                    floorLevel = building.floors[0].level;
                }
                
                // Validate that the requested floor exists in this building
                bool floorExists = building.floors.Any(f => f.level == floorLevel);
                if (!floorExists)
                {
                    Debug.LogWarning($"Floor {floorLevel} not found in building {buildingName}. Using first available floor.");
                    floorLevel = building.floors[0].level;
                }
                
                // If there's already an active floor, destroy the old one
                if (activeFloorObject != null)
                {
                    Destroy(activeFloorObject);
                }

                // Create a new parent object for the building
                GameObject buildingObject = new GameObject(buildingName);
                activeBuilding = building; 
                
                activeFloorLevel = floorLevel; 
                building.SpawnFloor(floorLevel, buildingObject.transform);
                activeFloorObject = buildingObject; 
                
                // Update the UI to reflect the changes
                UpdateBuildingUI();
                
                Debug.Log($"Spawned building {buildingName}, floor {floorLevel}");
            }
        }
    }
}