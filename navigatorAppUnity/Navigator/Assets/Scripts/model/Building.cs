using model.graph;

namespace model
{
    using System.Collections.Generic;
    using UnityEngine;

    public class Building
    {
        public string buildingName;
        public Graph graph;
        public List<Floor> floors;

        public Building(string buildingName, Graph graph, List<Floor> floors)
        {
            this.buildingName = buildingName;
            this.graph = graph;
            this.floors = floors;
        }

        // Method to spawn a floor by its level
        public void SpawnFloor(int level, Transform parent)
        {
            Floor floor = floors.Find(f => f.level == level);

            if (floor != null)
            {
                // Load and instantiate prefabs for walls and ground
                GameObject wallsPrefab = Resources.Load<GameObject>($"Prefabs/{buildingName}/" + floor.walls);
                GameObject groundPrefab = Resources.Load<GameObject>($"Prefabs/{buildingName}/" + floor.ground);

                if (wallsPrefab != null && groundPrefab != null)
                {
                    // Spawn the floor components at the correct position
                    GameObject wallsInstance = Object.Instantiate(wallsPrefab, new Vector3(0, level * 2, 0), Quaternion.identity, parent);
                    GameObject groundInstance = Object.Instantiate(groundPrefab, new Vector3(0, level * 2 , 0), Quaternion.identity, parent);

                    // Optionally, make the parent the building object to keep the hierarchy clean
                    wallsInstance.transform.SetParent(parent);
                    groundInstance.transform.SetParent(parent);
                }
                else
                {
                    Debug.LogError($"Prefab missing for floor {level} in building {buildingName}, should be in {$"Prefabs/{buildingName}/" + floor.walls}");
                }
            }
            else
            {
                Debug.LogError($"Floor {level} not found for building {buildingName}");
            }
        }

        [System.Serializable]
        public class Floor
        {
            public int level;
            public string walls;
            public string ground;
        }
    }


}