using model;
using System.Collections.Generic;
using System.Linq;
using model.Database;
using model.graph;
using UnityEngine;

namespace controller
{
    
    /// <summary>
    /// coordinated the shown building and floor
    /// also holds the building the user is in currently
    /// call this to make changes to the buildings
    /// </summary>
    public class BuildingManager : MonoBehaviour
    {
        private Dictionary<string, Building> buildings = new Dictionary<string, Building>();

        private Building currentBuilding;   //the currently detectedPosition is here
        private Building shownBuilding;
        private GameObject activeFloorObject; //the model shown
        private int activeFloorLevel = -1; // Track the active floor level
        private string currentRoom = "";

        public Registry registry;

        public Building GetActiveBuilding()
        {
            return currentBuilding;
        }
        
        public Building GetShownBuilding()
        {
            return shownBuilding;
        }

        public bool ShownEqualsActiveBuilding()
        {
            return currentBuilding == shownBuilding;
        }

        public int GetShownFloor()
        {
            return activeFloorLevel;
        }

        public Graph GetActiveGraph()
        {
            return currentBuilding?.graph;
        }
        

        public string GetCurrentRoom()
        {
            return currentRoom;
        }

        public void SetCurrentRoom(string value)
        {
            currentRoom = value;
            // Notify UI to update
            if (registry.topMenu != null)
            {
                registry.topMenu.UpdateCurrentRoomDisplay(currentRoom);
            }
        }

        public List<string> GetAvailableBuildingNames()
        {
            return buildings.Keys.ToList();
        }

        void Awake()
        {
            LoadBuildingConfigs();
            
            // Default building setup
            SpawnBuildingFloor("H4", 3, true);
            Debug.Log($"Buildings Manager script initialized");
            Debug.Log($"activeBuilding is: {currentBuilding?.buildingName}");
        }

        /// <summary>
        /// Will spawn the higher floor, if it exists
        /// </summary>
        public void IncreaseFloor()
        {
            if (shownBuilding == null) return;
            //Debug.Log("IncreaseFloor called");

            int currentIndex = GetFloorIndex(activeFloorLevel);
            if (currentIndex < shownBuilding.floors.Count - 1)
            {
                int nextFloorLevel = shownBuilding.floors[currentIndex + 1].level;
                SpawnBuildingFloor(shownBuilding.buildingName, nextFloorLevel);
            }

            registry.dataCollectionMode.Refresh();
            registry.graphManager.PlotCurrentPath();
            
            // Notify UI to update
            NotifyUIUpdate();
        }

        /// <summary>
        /// will spawn the floor below, if it exists
        /// </summary>
        public void DecreaseFloor()
        {
            if (shownBuilding == null) return;
            //Debug.Log("DecreaseFloor called");

            int currentIndex = GetFloorIndex(activeFloorLevel);
            if (currentIndex > 0)
            {
                int prevFloorLevel = shownBuilding.floors[currentIndex - 1].level;
                SpawnBuildingFloor(shownBuilding.buildingName, prevFloorLevel);
            }

            registry.dataCollectionMode.Refresh();
            registry.graphManager.PlotCurrentPath();
            
            // Notify UI to update
            NotifyUIUpdate();
        }

        /// <summary>
        /// checks if there is a floor above
        /// </summary>
        public bool CanIncreaseFloor()
        {
            if (shownBuilding == null) return false;
            int currentIndex = GetFloorIndex(activeFloorLevel);
            return currentIndex < shownBuilding.floors.Count -1;
        }

        
        /// <summary>
        /// checks if there is a floor below
        /// </summary>
        public bool CanDecreaseFloor()
        {
            if (shownBuilding == null) return false;
            int currentIndex = GetFloorIndex(activeFloorLevel);
            return currentIndex > 0;
        }

        /// <summary>
        /// changes the building to the given building
        /// </summary>
        public void ChangeBuilding(string buildingName)
        {
            Debug.Log($"Changing Building to {buildingName}");
            if (buildings.ContainsKey(buildingName))
            {
                activeFloorLevel = buildings[buildingName].floors[0].level;
                SpawnBuildingFloor(buildingName, buildings[buildingName].floors[0].level);
                
                registry.graphManager.CancelNavigation();
                
                if (ShownEqualsActiveBuilding())
                {
                    registry.positionMarker.ActivateMarker();
                }
                else
                {
                    registry.positionMarker.DeactivateMarker();
                }
            }
            else
            {
                Debug.LogError($"Building {buildingName} not found!");
            }
        }

        /// <summary>
        /// updates the ui manually
        /// </summary>
        private void NotifyUIUpdate()
        {
            if (registry.topMenu != null)
            {
                registry.topMenu.UpdateUI();
            }
        }

        /// <summary>
        /// gets the index of the floor in the shown building
        /// </summary>
        private int GetFloorIndex(int floorLevel)
        {
            if (shownBuilding == null) return 0;
            
            for (int i = 0; i < shownBuilding.floors.Count; i++)
            {
                if (shownBuilding.floors[i].level == floorLevel)
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>
        /// loads the buildings configs at startup
        /// </summary>
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
                    Debug.LogError(
                        $"graph.json not found in Resources/Buildings! Expected file path: {graphFileLocation}.json");
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
        public void UpdateBuilding(Coordinate wifiNetworks)
        {
            Dictionary<string, int> buildingCount = new();

            foreach (string bssid in wifiNetworks.WifiInfoMap.Keys)
            {
                string building = registry.database.GetBuildingForBssid(bssid);

                if (building == null)
                    continue;

                // Early return if the first match equals current building (for efficiency, as this will be the case most times anyway)
                if (currentBuilding != null && building == currentBuilding.buildingName)
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

                currentBuilding = GetBuilding(mostLikelyBuilding);
                NotifyUIUpdate();
                registry.graphManager.RefreshRoomOptions();
            }
            else
            {
                Debug.Log("Wifi data doesn't match any recorded building, staying at current");
                return;
            }
        }

        /// <summary>
        /// gets a building object by name
        /// </summary>
        public Building GetBuilding(string buildingName)
        {
            if (buildings.ContainsKey(buildingName))
            {
                return buildings[buildingName];
            }

            Debug.LogError($"Building {buildingName} not found!");
            return null;
        }

        /// <summary>
        /// spawn a specific floor of a specific building
        /// will also update the caches with data, and all necesarry field
        /// </summary>
        public void SpawnBuildingFloor(string buildingName, int floorLevel, bool updateCurrentBuilding = false)
        {
            // Check if the requested building and floor are already active
            if (shownBuilding != null &&
                shownBuilding.buildingName == buildingName &&
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
                if (updateCurrentBuilding)
                {
                    currentBuilding = building;
                }

                shownBuilding = building;
                activeFloorLevel = floorLevel;
                Debug.Log($"Set current floor to {floorLevel}");
                GameObject floor = building.SpawnFloor(floorLevel, buildingObject.transform);
                activeFloorObject = buildingObject;

                registry.positionMarker.UpdateMarkerInShownBuilding();
                NotifyUIUpdate();

                // set the camera to show the model, as this might not be where it was before
                if (floor != null)
                {
                    Bounds bounds = new Bounds(floor.transform.position, Vector3.zero);
                    Renderer[] renderers = floor.GetComponentsInChildren<Renderer>();
    
                    if (renderers.Length > 0)
                    {
                        foreach (Renderer r in renderers)
                            bounds.Encapsulate(r.bounds);
    
                        Position pos = new Position(bounds.center.x, bounds.center.y, floorLevel);
                        registry.positionMarker.MoveMarkerToPosition(pos);
                    }
                    else
                    {
                        Debug.LogWarning("No renderers found on spawned floor for bounds calculation.");
                    }
                }
                


                Debug.Log($"Spawned building {buildingName}, floor {floorLevel}");
            }
        }
    }
}