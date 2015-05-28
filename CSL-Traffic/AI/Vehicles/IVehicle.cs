using UnityEngine;

namespace CSL_Traffic
{
    interface IVehicle
    {
        bool StartPathFind(ushort vehicleID, ref Vehicle vehicleData);
        void CalculateSegmentPosition(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position position, uint laneID, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed);
        void CalculateSegmentPosition(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position nextPosition, PathUnit.Position position, uint laneID, byte offset, PathUnit.Position prevPos, uint prevLaneID, byte prevOffset, out Vector3 pos, out Vector3 dir, out float maxSpeed);
        bool ParkVehicle(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position pathPos, uint nextPath, int nextPositionIndex, out byte segmentOffset);
        bool NeedChangeVehicleType(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position pathPos, uint laneID, VehicleInfo.VehicleType laneVehicleType, ref Vector4 target);
        bool ChangeVehicleType(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position pathPos, uint laneID);
        void UpdateNodeTargetPos(ushort vehicleID, ref Vehicle vehicleData, ushort nodeID, ref NetNode nodeData, ref Vector4 targetPos, int index);
        void ArrivingToDestination(ushort vehicleID, ref Vehicle vehicleData);
        float CalculateTargetSpeed(ushort vehicleID, ref Vehicle data, float speedLimit, float curve);
        void InvalidPath(ushort vehicleID, ref Vehicle vehicleData, ushort leaderID, ref Vehicle leaderData);
        bool IsHeavyVehicle();
        bool IgnoreBlocked(ushort vehicleID, ref Vehicle vehicleData);
    }
}
