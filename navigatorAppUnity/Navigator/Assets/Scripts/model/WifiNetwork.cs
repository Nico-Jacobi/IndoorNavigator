namespace model
{
    public class WifiNetwork
    {
        public string SSID;
        public string BSSID;
        public int level;
        public int frequency;
        public string capabilities;
        public long timestamp;

        public override string ToString()
        {
            return $"SSID: {SSID}, BSSID: {BSSID}, Level: {level}, Frequency: {frequency}, Capabilities: {capabilities}, Timestamp: {timestamp}";
        }
    }

}