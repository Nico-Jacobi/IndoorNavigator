using SQLite;

namespace model.Database
{ 
    namespace Plugins
    {
        
        [Table("WifiInfos")]
        public class WifiInfo
        {
            [PrimaryKey, AutoIncrement]
            public int Id { get; set; }
            public string Bssid { get; set; }
            public float SignalStrength { get; set; }

            public int CoordinateId { get; set; }
        }
    }
}