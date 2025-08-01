using model;
using UnityEngine;


namespace controller
{
    /// <summary>
    /// this is the interface the filters must implement
    /// which filter is active is managed by the registry
    /// </summary>
    public interface PositionFilter
    {
        void UpdateWithWifi(Position rawWifiPrediction);
        void UpdateWithIMU(Vector2 acceleration, float headingDegrees);
        
        Position GetEstimate();
        Vector2 GetEstimatedVelocity();

        
        //bool IsInitialized { get; }
    }
}