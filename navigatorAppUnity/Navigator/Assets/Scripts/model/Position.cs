namespace model
{
    /// <summary>
    /// Represents a 2D position on a specific floor.
    /// </summary>
    public class Position
    {
        public double X { get; set; }
        public double Y { get; set; }
        public int Floor { get; set; }

        public Position(double x, double y, int floor)
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
    }
    
}