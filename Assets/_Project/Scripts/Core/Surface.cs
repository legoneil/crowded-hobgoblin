using UnityEngine;

namespace DuvalDrive
{
    public enum SurfaceType { Road, ParkingLot, Sidewalk, Grass }

    /// <summary>
    /// Marks ground the car can drive on. Anything the car touches without
    /// a Surface counts as a collision.
    /// </summary>
    public class Surface : MonoBehaviour
    {
        public SurfaceType type = SurfaceType.Road;
    }
}
