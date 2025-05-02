using SQLite;

namespace model.Database
{
    namespace Plugins
    {
        [Table("WifiInfos")]
        public class WifiInfo
        {
            private const float ExpectedMinSignal = -95f;
            private const float ExpectedMaxSignal = -30f;

            [PrimaryKey, AutoIncrement]
            public int Id { get; set; }

            public string Bssid { get; set; }

            private float _signalStrength;

            public float SignalStrength
            {
                get => _signalStrength;
                set => _signalStrength = (value - ExpectedMinSignal) / (ExpectedMaxSignal - ExpectedMinSignal); // Normalize between 0 and 1
            }

            public int CoordinateId { get; set; }

            // Parameterless constructor
            public WifiInfo() {}

            
        }
    }
}