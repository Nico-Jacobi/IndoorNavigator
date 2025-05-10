using model;
using System.Collections.Generic;
using System.Linq;
using model.Database;
using model.graph;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using view;

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
        public CameraController cameraController;
        public DataCollectionMode dataCollectionMode;
        
        // New UI elements for floor navigation
        public Button increaseFloorButton;
        public Button decreaseFloorButton;
        public TMP_Text currentFloorText;

        public Building GetActiveBuilding()
        {
            return activeBuilding;
        }
        
        public int GetShownFloor()
        {
            return activeFloorLevel;
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
            Debug.Log("IncreaseFloor called");

            int currentIndex = GetFloorIndex(activeFloorLevel);
            if (currentIndex < activeBuilding.floors.Count - 1)
            {
                int nextFloorLevel = activeBuilding.floors[currentIndex + 1].level;
                SpawnBuildingFloor(activeBuilding.buildingName, nextFloorLevel);
            }

            dataCollectionMode.Refresh();
            UpdateFloorButtons();
        }

        private void DecreaseFloor()
        {
            if (activeBuilding == null) return;
            dataCollectionMode.Refresh();
            Debug.Log("DecreaseFloor called");

            int currentIndex = GetFloorIndex(activeFloorLevel);
            if (currentIndex > 0)
            {
                int prevFloorLevel = activeBuilding.floors[currentIndex - 1].level;
                SpawnBuildingFloor(activeBuilding.buildingName, prevFloorLevel);
            }

            dataCollectionMode.Refresh();
            UpdateFloorButtons();
        }

        private void UpdateFloorButtons()
        {
            int currentFloor = GetFloorIndex(activeFloorLevel);
            int minFloor = int.MaxValue;
            int maxFloor = int.MinValue;
            
            foreach (Building.Floor floor in activeBuilding.floors)
            {

                if (floor.level < minFloor)
                {
                    minFloor = floor.level;
                }
                
                
                if (floor.level > maxFloor)
                {
                    maxFloor = floor.level;
                }
            }
            
            if (currentFloor+1 >= maxFloor)
            {
                increaseFloorButton.interactable = false;
            }
            else
            {
                increaseFloorButton.interactable = true;
            }

            if (currentFloor-1 <= minFloor)
            {
                decreaseFloorButton.interactable = false;
            }
            else
            {
                decreaseFloorButton.interactable = true;
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


        /// <summary>
        /// Updates the active building based on the most common BSSID matches from Wi-Fi data.
        /// If the first matched BSSID maps to the current building, we assume all others likely belong to the same and skip full evaluation.
        /// </summary>
        public void updateBuilding(Coordinate wifiNetworks)
        {
            Dictionary<string, int> buildingCount = new();

            foreach (string bssid in wifiNetworks.WifiInfoMap.Keys)
            {
                string building = database.GetBuildingForBssid(bssid);

                if (building == null)
                    continue;

                // Early return if the first match equals current building (for efficiency, as this will be the case most times anyway)
                if (activeBuilding != null && building == activeBuilding.buildingName)
                {
                    return;
                }

                if (!buildingCount.TryAdd(building, 1))
                    buildingCount[building]++;
            }

            if (buildingCount.Count > 0)
            {
                string mostLikelyBuilding = buildingCount
                    .OrderByDescending(kv => kv.Value)
                    .First().Key;

                activeBuilding = GetBuilding(mostLikelyBuilding);
            }
            else
            {
                Debug.Log("Wifi data doesn't match any recorded building, staying at current");
                return;
            }
        }

        public Building GetBuilding(string buildingName)
        {
            if (buildings.ContainsKey(buildingName))
            {
                return buildings[buildingName];
            }
            Debug.LogError($"Building {buildingName} not found!");
            return null;
            
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
                cameraController.MoveMarkerToPosition(null);

                Debug.Log($"Spawned building {buildingName}, floor {floorLevel}");
            }
            
        }
    }
}