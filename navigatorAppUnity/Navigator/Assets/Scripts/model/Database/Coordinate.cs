using SQLite;
using System.Collections.Generic;
using model.Database.Plugins;

namespace Plugins
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
    }
    
    [System.Serializable]
    public class CoordinateListWrapper
    {
        public List<Coordinate> Coordinates;
    }

    
}