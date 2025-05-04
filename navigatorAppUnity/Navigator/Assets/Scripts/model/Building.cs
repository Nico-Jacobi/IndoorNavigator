using System.IO;
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
                string basePath = $"Prefabs/{buildingName}";
                GameObject wallsPrefab = Resources.Load<GameObject>(Path.Combine(basePath, floor.walls));
                GameObject groundPrefab = Resources.Load<GameObject>(Path.Combine(basePath, floor.ground));
                GameObject doorsPrefab =Resources.Load<GameObject>(Path.Combine(basePath,  floor.doors));
                
                if (wallsPrefab != null && groundPrefab != null && doorsPrefab != null)
                {
                    // Spawn the floor components at the correct position
                    GameObject wallsInstance = Object.Instantiate(wallsPrefab, new Vector3(0, level * 2, 0), Quaternion.identity, parent);
                    GameObject groundInstance = Object.Instantiate(groundPrefab, new Vector3(0, level * 2 , 0), Quaternion.identity, parent);
                    GameObject doorsInstance = Object.Instantiate(doorsPrefab, new Vector3(0, level * 2 , 0), Quaternion.identity, parent);
                    
                    // parent already passed in the instatiate call
                    //wallsInstance.transform.SetParent(parent);
                    //groundInstance.transform.SetParent(parent);
                    //doorsInstance.transform.SetParent(parent);
                    
                    
                    // Load and apply door material (the others default to a white material)
                    Material doorMaterial = Resources.Load<Material>("DoorMaterial");
                    if (doorMaterial != null)
                    {
                        Renderer[] doorRenderers = doorsInstance.GetComponentsInChildren<Renderer>();
                        foreach (Renderer renderer in doorRenderers)
                        {
                            renderer.material = doorMaterial;
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Door material not found at Resources/Materials/DoorMaterial");
                    }
                    
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
            public string doors;
        }
    }


}