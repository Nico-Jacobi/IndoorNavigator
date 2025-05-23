using System;
using SQLite;
using System.Collections.Generic;
using System.Linq;
using model.Database.Plugins;
using UnityEngine;

namespace model.Database
{
    [Table("Coordinates")]
    public class Coordinate
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public int Floor { get; set; }
        public string BuildingName { get; set; }

        [Ignore]
        public List<WifiInfo> WifiInfos { get; set; } = new();
        
        [Ignore]
        public Dictionary<string, WifiInfo> WifiInfoMap => WifiInfos.ToDictionary(w => w.Bssid, w => w);
        
        
        public override string ToString()
        {
            string wifiDetails = string.Join(", ",
                WifiInfoMap.Select(kv => $"{kv.Key}: {kv.Value.SignalStrength}dBm"));

            return $"Coordinate (Id={Id}, X={X}, Y={Y}, Floor={Floor}, Building='{BuildingName}') | WiFi: [{wifiDetails}]";
        }
        
        
        /// <summary>
        /// Compare the Wi-Fi similarity between this Coordinate and another Coordinate.
        /// </summary>
        public float CompareWifiSimilarity(Coordinate other)
        {
            var allBssids = WifiInfoMap.Keys.Union(other.WifiInfoMap.Keys).ToList();

            List<float> thisFeatureVector = new List<float>();
            List<float> otherFeatureVector = new List<float>();

            foreach (var bssid in allBssids)
            {
                float defaultMissing = -100f;
                float penaltyFactor = 0.3f;
                float thisSignalStrength = WifiInfoMap.TryGetValue(bssid, out var val1) ? val1.SignalStrength : defaultMissing * penaltyFactor;
                float otherSignalStrength = other.WifiInfoMap.TryGetValue(bssid, out var val2) ? val2.SignalStrength : defaultMissing * penaltyFactor;


                thisFeatureVector.Add(thisSignalStrength);
                otherFeatureVector.Add(otherSignalStrength);
            }

            // Calculate the difference sum
            float differenceSum = 0.0f;
            for (int i = 0; i < thisFeatureVector.Count; i++)
            {
                differenceSum += Math.Abs(thisFeatureVector[i] - otherFeatureVector[i]);
            }

            return differenceSum;
        }
        
        
        /// <summary>
        /// Checks if this Coordinate shares at least one BSSID with another Coordinate.
        /// </summary>
        public bool HasCommonBssid(Coordinate other)
        {
            foreach (var bssid in WifiInfoMap.Keys)
            {
                if (other.WifiInfoMap.ContainsKey(bssid))
                {
                    Debug.Log($"Common bssid is: {bssid}");
                    return true;
                }
            }
            return false;
        }

    }
    
    
    [System.Serializable]
    public class CoordinateListWrapper
    {
        public List<Coordinate> Coordinates;
    }
    
}