namespace model
{
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
            public string doors;
        }
    }
}