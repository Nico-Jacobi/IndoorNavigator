using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using model.Database;
using model.Database.Plugins;
using SQLite;
using UnityEngine;
using Newtonsoft.Json;

namespace controller
{
    public class SQLiteDatabase : MonoBehaviour
    {
        private SQLiteConnection db;

        // Cache only last accessed building
        private string cachedBuildingName = null;
        private List<Coordinate> cachedCoordinates = null;

        // Cache all BSSID-to-building mapping
        private Dictionary<string, HashSet<string>> bssidToBuildings = new();




        void Awake()
        {
            //DeleteDatabase(); // just for debugging
            //DontDestroyOnLoad(gameObject);
            InitializeDatabase();
        }
        
        
        private void InitializeDatabase()
        {
            string dbPath = Path.Combine(Application.persistentDataPath, "WifiData.db");
            db = new SQLiteConnection(dbPath);

            db.CreateTable<Coordinate>();
            db.CreateTable<WifiInfo>();

            Debug.Log("Database initialized at: " + dbPath);

            if (!db.Table<Coordinate>().Any())
            {
                //ImportFromJson(); //this dataset seems to be broken or not up to date (leads to less precise positioning)
            }

            // Build full BSSID-to-building map
            foreach (var coord in db.Table<Coordinate>())
            {
                var wifiInfos = db.Table<WifiInfo>().Where(w => w.CoordinateId == coord.Id).ToList();

                foreach (var wifi in wifiInfos)
                {
                    if (!bssidToBuildings.ContainsKey(wifi.Bssid))
                        bssidToBuildings[wifi.Bssid] = new();
                    bssidToBuildings[wifi.Bssid].Add(coord.BuildingName);
                }
            }
        }

        /// <summary>
        /// Closes and deletes the entire SQLite database file.
        /// Use with caution. Cannot be undone.
        /// </summary>
        public void DeleteDatabase()
        {
            db?.Close(); // Make sure the connection is closed
            db = null;

            string dbPath = Path.Combine(Application.persistentDataPath, "WifiData.db");
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
                cachedBuildingName = null;
                cachedCoordinates = null;
                bssidToBuildings.Clear();
                Debug.Log("Database file deleted.");
            }
            else
            {
                Debug.LogWarning("Database file not found.");
            }
        }

        private void ImportFromJson(string jsonPath = "default_wifidata")
        {
            string json;

            if (jsonPath == "default_wifidata")
            {
                TextAsset jsonFile = Resources.Load<TextAsset>(jsonPath);
                if (jsonFile == null)
                {
                    Debug.LogError("Could not load JSON from Resources: " + jsonPath);
                    return;
                }

                json = jsonFile.text;
            }
            else
            {
                if (!File.Exists(jsonPath))
                {
                    Debug.LogError("File not found at path: " + jsonPath);
                    return;
                }

                json = File.ReadAllText(jsonPath);
            }

            var coords = JsonConvert.DeserializeObject<CoordinateListWrapper>(json);

            foreach (var coord in coords.Coordinates)
            {
                db.Insert(coord);
                foreach (var wifi in coord.WifiInfos)
                {
                    wifi.CoordinateId = coord.Id;
                    db.Insert(wifi);
                }
            }

            Debug.Log($"Imported {coords.Coordinates.Count} coordinates from: {jsonPath}");
        }


        public void PickFileAndImport()
        {
            // Allow only JSON files
            string[] fileTypes = new[] { "application/json" };

            NativeFilePicker.PickFile((path) =>
            {
                if (path == null)
                {
                    Debug.Log("User cancelled import");
                    return;
                }

                Debug.Log("Importing from: " + path);
                ImportFromJson(path);
            }, fileTypes);
        }


        public void ExportWithSimpleFilename()
        {
            string filename = "wifi_data_" + DateTime.Now.ToString("yyyyMMdd_HHmmssfff") + ".json";

            var coordinates = db.Table<Coordinate>().ToList();
            foreach (var coord in coordinates)
                coord.WifiInfos = db.Table<WifiInfo>().Where(w => w.CoordinateId == coord.Id).ToList();

            var wrapper = new CoordinateListWrapper { Coordinates = coordinates };

            bool success = IOManager.SaveAsJson(wrapper, filename);

            if (success)
                Debug.Log($"Exported to Downloads folder as {filename}");
            else
                Debug.LogError("Export failed.");
        }




        public void InsertCoordinateWithWifiInfos(Coordinate coord)
        {
            db.Insert(coord);

            foreach (var wifi in coord.WifiInfos)
            {
                Debug.Log($"Inserting WiFi: BSSID={wifi.Bssid}, Signal={wifi.SignalStrength}");

                wifi.CoordinateId = coord.Id;
                db.Insert(wifi);

                if (!bssidToBuildings.ContainsKey(wifi.Bssid))
                    bssidToBuildings[wifi.Bssid] = new();
                bssidToBuildings[wifi.Bssid].Add(coord.BuildingName);
            }

            // Update single-building cache if it matches
            if (coord.BuildingName == cachedBuildingName)
                cachedCoordinates.Add(coord);

            Debug.Log($"Inserted coordinate with {coord.WifiInfos.Count} wifi entries.");
        }

        
        public List<Coordinate> GetAllCoordinatesWithWifiInfos()
        {
            return db.Table<Coordinate>().ToList().Select(coord =>
            {
                coord.WifiInfos = db.Table<WifiInfo>().Where(w => w.CoordinateId == coord.Id).ToList();
                return coord;
            }).ToList();
        }

        public void DeleteCoordinate(int coordinateId)
        {
            var wifiInfos = db.Table<WifiInfo>().Where(w => w.CoordinateId == coordinateId).ToList();
            var coord = db.Table<Coordinate>().FirstOrDefault(c => c.Id == coordinateId);

            if (coord != null)
            {
                foreach (var wifi in wifiInfos)
                {
                    db.Delete(wifi);

                    // Remove building association for this BSSID if it exists
                    if (bssidToBuildings.TryGetValue(wifi.Bssid, out var buildings))
                    {
                        buildings.Remove(coord.BuildingName);
                        if (buildings.Count == 0)
                        {
                            bssidToBuildings.Remove(wifi.Bssid);
                        }
                    }
                }

                // Update cache if this coordinate belongs to the currently cached building
                if (cachedBuildingName == coord.BuildingName && cachedCoordinates != null)
                {
                    cachedCoordinates.RemoveAll(c => c.Id == coordinateId);

                    if (cachedCoordinates.Count == 0)
                    {
                        cachedBuildingName = null;
                        cachedCoordinates = null;
                    }
                }

                // Delete the coordinate from the database
                db.Delete(coord);
                Debug.Log($"Deleted coordinate with ID: {coordinateId}");
            }
            else
            {
                Debug.LogWarning($"Coordinate with ID {coordinateId} not found.");
            }
        }


        public List<Coordinate> GetCoordinatesForBuilding(string buildingName)
        {


            if (cachedBuildingName == buildingName && cachedCoordinates != null)
                return cachedCoordinates;

            var coords = db.Table<Coordinate>()
                .Where(c => c.BuildingName == buildingName)
                .ToList();

            //if (!coords.Any())
                //throw new ArgumentException($"no data found for Building '{buildingName}'.");

            foreach (var coord in coords)
                coord.WifiInfos = db.Table<WifiInfo>().Where(w => w.CoordinateId == coord.Id).ToList();

            cachedBuildingName = buildingName;
            cachedCoordinates = coords;

            return coords;
        }

        /// <summary>
        /// Returns the building name for the given BSSID. Logs if not found.
        /// </summary>
        public string GetBuildingForBssid(string bssid)
        {
            if (bssidToBuildings.TryGetValue(bssid, out var buildings) && buildings.Count > 0)
            {
                return buildings.First();
            }

            if (bssidToBuildings.Count == 0)
            {
                Debug.Log("bssid to buildings List is empty, unless the db is empty this shouldn´t happen");
                return null;
            }

            //Debug.Log("Wifi data doesn't match any recorded building"); its expected this happens with some bssids
            return null;
        }


        /// <summary>
        /// Prints the entire database contents to the Unity console.
        /// Useful for debugging and reviewing data.
        /// </summary>
        public void PrintDatabase()
        {
            Debug.Log("==== DATABASE CONTENT START ====");

            try
            {
                var allCoords = GetAllCoordinatesWithWifiInfos();
                Debug.Log($"Total Coordinates: {allCoords.Count}");

                var buildingGroups = allCoords.GroupBy(c => c.BuildingName);

                foreach (var group in buildingGroups)
                {
                    string buildingName = group.Key;
                    int coordCount = group.Count();

                    Debug.Log($"\n=== BUILDING: {buildingName} ({coordCount} coordinates) ===");

                    foreach (var coord in group)
                    {
                        Debug.Log($"  Coordinate ID: {coord.Id}");
                        Debug.Log($"    Position: ({coord.X}, {coord.Y}, {coord.Floor})");
                        Debug.Log($"    Floor: {coord.Floor}");
                        Debug.Log($"    Wifi Count: {coord.WifiInfos.Count}");

                        // Print first 3 WiFi entries (to avoid overwhelming the console)
                        int wifiCount = 0;
                        foreach (var wifi in coord.WifiInfos.OrderByDescending(w => w.SignalStrength))
                        {
                            wifiCount++;

                            Debug.Log($"      WiFi {wifiCount}: BSSID={wifi.Bssid}, Level={wifi.SignalStrength}dBm");
                            
                        }

                        Debug.Log("  ----------");
                    }
                }

                // Print BSSID-to-Building mapping stats
                Debug.Log($"\n=== BSSID MAPPING STATS ===");
                Debug.Log($"Total unique BSSIDs tracked: {bssidToBuildings.Count}");

                var bssidsInMultipleBuildings = bssidToBuildings
                    .Where(kvp => kvp.Value.Count > 1)
                    .ToList();

                Debug.Log($"BSSIDs found in multiple buildings: {bssidsInMultipleBuildings.Count}");

                if (bssidsInMultipleBuildings.Count > 0)
                {
                    Debug.Log("Sample multi-building BSSIDs:");
                    foreach (var item in bssidsInMultipleBuildings.Take(5))
                    {
                        Debug.Log($"  BSSID: {item.Key} → Buildings: {string.Join(", ", item.Value)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error printing database: {ex.Message}");
            }

            Debug.Log("==== DATABASE CONTENT END ====");


        }
    }
}
