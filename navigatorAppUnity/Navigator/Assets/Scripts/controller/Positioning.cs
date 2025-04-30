using Unity.VisualScripting;

namespace controller
{
    public class Positioning
    {
        // Private fields to store the current position
        private float currentX;
        private float currentY;
        private int currentFloor;
        private float floorHeight = 2.0f;

        // Constructor to initialize the starting position
        public Positioning(float x = 0.0f, float y = 0.0f, int floor = 3)
        {
            currentX = x;
            currentY = y;
            currentFloor = floor;
        }

        // Getter for currentX
        public float GetX()
        {
            return currentX;
        }



        // Getter for currentY
        public float GetY()
        {
            return currentY;
        }


        // Getter for currentFloor
        public int GetFloor()
        {
            return currentFloor;
        }
        
        public float GetFloorHeight()
        {
            return currentFloor * floorHeight;
        }


        // Method to update and return the current position
        public (float x, float y, int floor) FindPosition()
        {
            // You can update the position logic here if needed
            // For now, this simply returns the current position and updates it
            // (example of position change: just adding 1 to the x-coordinate for each call)
            currentX += 1.0f;  // Just a sample update for demo
            currentY += 1.0f;  // Just a sample update for demo

            return (currentX, currentY, currentFloor);
        }
        

        // Optional: Method to set a new position
        public void SetPosition(float x, float y, int floor)
        {
            currentX = x;
            currentY = y;
            currentFloor = floor;
        }
    }
}
