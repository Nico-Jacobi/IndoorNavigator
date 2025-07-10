using model;
using UnityEngine;


namespace controller
{
    public interface PositionFilter
    {
        void UpdateWithWifi(Position rawWifiPrediction);
        void UpdateWithIMU(Vector2 acceleration, float headingDegrees);
        
        Position GetEstimate();
        Vector2 GetEstimatedVelocity();

        
        //bool IsInitialized { get; }
    }
}