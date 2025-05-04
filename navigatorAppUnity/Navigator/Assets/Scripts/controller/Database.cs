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
            DontDestroyOnLoad(gameObject);
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
                ImportFromJson();
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


        private void ImportFromJson(string jsonPath = "default_wifidata")
        {
            var json = Resources.Load<TextAsset>(jsonPath);
            var coords = JsonConvert.DeserializeObject<CoordinateListWrapper>(json.text);

            foreach (var coord in coords.Coordinates)
            {
                db.Insert(coord);
                foreach (var wifi in coord.WifiInfos)
                {
                    wifi.CoordinateId = coord.Id;
                    db.Insert(wifi);
                }
            }

            Debug.Log($"Imported {coords.Coordinates.Count} coordinates from JSON.");
        }

        public void InsertCoordinateWithWifiInfos(Coordinate coord, List<WifiInfo> wifiInfos)
        {
            db.Insert(coord);
            coord.WifiInfos = wifiInfos;

            foreach (var wifi in wifiInfos)
            {
                wifi.CoordinateId = coord.Id;
                db.Insert(wifi);

                if (!bssidToBuildings.ContainsKey(wifi.Bssid))
                    bssidToBuildings[wifi.Bssid] = new();
                bssidToBuildings[wifi.Bssid].Add(coord.BuildingName);
            }

            // Update single-building cache if it matches
            if (coord.BuildingName == cachedBuildingName)
                cachedCoordinates.Add(coord);

            Debug.Log($"Inserted coordinate with {wifiInfos.Count} wifi entries.");
        }

        public List<Coordinate> GetAllCoordinatesWithWifiInfos()
        {
            return db.Table<Coordinate>().ToList().Select(coord =>
            {
                coord.WifiInfos = db.Table<WifiInfo>().Where(w => w.CoordinateId == coord.Id).ToList();
                return coord;
            }).ToList();
        }

        public List<Coordinate> GetCoordinatesForBuilding(string buildingName)
        {

            
            if (cachedBuildingName == buildingName && cachedCoordinates != null)
                return cachedCoordinates;

            var coords = db.Table<Coordinate>()
                           .Where(c => c.BuildingName == buildingName)
                           .ToList();

            if (!coords.Any())
                throw new ArgumentException($"no data found for Building '{buildingName}'.");

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

    }
}
