using System.Collections.Generic;
using System.IO;
using System.Linq;
using model.Database.Plugins;
using SQLite;
using UnityEngine;
using Newtonsoft.Json;  //this one can word with nested things, its better

namespace Plugins
{
    public class SQLiteExample : MonoBehaviour
    {
        private SQLiteConnection db;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            InitializeDatabase();
            //ShowDatabaseContents();
        }

        private void InitializeDatabase()
        {
            string dbPath = Path.Combine(Application.persistentDataPath, "mydatabase.db");
            db = new SQLiteConnection(dbPath);

            // Create tables if they don't exist
            db.CreateTable<Coordinate>();
            db.CreateTable<WifiInfo>();

            Debug.Log("Database initialized at: " + dbPath);

            // If the database is empty, load data from JSON
            if (!db.Table<Coordinate>().Any())
            {
                ImportFromJson();
            }
        }

        private void ImportFromJson(string jsonPath="default_wifidata")
        {
            var json = Resources.Load<TextAsset>(jsonPath); // don't add .json
            print(json.text);
            
            // Replace JsonUtility with JsonConvert from Newtonsoft.Json
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


        /// <summary>
        /// Inserts a Coordinate and its related WifiInfos
        /// </summary>
        public void InsertCoordinateWithWifiInfos(Coordinate coord, List<WifiInfo> wifiInfos)
        {
            db.Insert(coord); // insert coordinate first
            foreach (var wifi in wifiInfos)
            {
                wifi.CoordinateId = coord.Id;
                db.Insert(wifi);
            }

            Debug.Log($"Inserted coordinate with {wifiInfos.Count} wifi entries.");
        }

        /// <summary>
        /// Gets all Coordinates and their associated WifiInfos
        /// </summary>
        public List<Coordinate> GetAllCoordinatesWithWifiInfos()
        {
            var coords = db.Table<Coordinate>().ToList();
            foreach (var coord in coords)
            {
                coord.WifiInfos = db.Table<WifiInfo>().Where(w => w.CoordinateId == coord.Id).ToList();
            }

            return coords;
        }

        private void DeleteDatabase()
        {
            string dbPath = Path.Combine(Application.persistentDataPath, "mydatabase.db");

            // Check if the database file exists
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
                Debug.Log("Database deleted.");
            }
            else
            {
                Debug.Log("No database found to delete.");
            }
        }


        private void ShowDatabaseContents()
        {
            // Retrieve all coordinates and their associated WifiInfos
            var coordinates = db.Table<Coordinate>().ToList();

            foreach (var coord in coordinates)
            {
                Debug.Log(
                    $"Coordinate ID: {coord.Id}, X: {coord.X}, Y: {coord.Y}, Floor: {coord.Floor}, Building: {coord.BuildingName}");

                // Retrieve all WifiInfos for this coordinate
                var wifiInfos = db.Table<WifiInfo>().Where(w => w.CoordinateId == coord.Id).ToList();

                foreach (var wifi in wifiInfos)
                {
                    Debug.Log(
                        $"    WifiInfo BSSID: {wifi.Bssid}, Signal Strength: {wifi.SignalStrength}, Coordinate ID: {wifi.CoordinateId}");
                }
            }
        }
    }
}