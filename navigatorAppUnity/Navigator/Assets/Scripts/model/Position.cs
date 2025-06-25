using Newtonsoft.Json;

namespace model
{
    /// <summary>
    /// Represents a 2D position on a specific floor.
    /// </summary>
    public class Position
    {
        public float X { get; set; }
        public float Y { get; set; }
        public int Floor { get; set; }

        public Position(float x, float y, int floor)
        {
            X = x;
            Y = y;
            Floor = floor;
        }

        public float GetFloorHeight()
        {
            return Floor * 2.0f;
        }
        
        
        public override string ToString() => $"({X}, {Y}) on floor {Floor}";
        
        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented);

    }
    
}