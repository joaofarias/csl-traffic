using ColossalFramework;
using UnityEngine;

namespace CSL_Traffic
{
    class CustomHearseAI : HearseAI, IVehicle
    {
        public override void SimulationStep(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics)
        {
            if ((CSLTraffic.Options & OptionsManager.ModOptions.UseRealisticSpeeds) == OptionsManager.ModOptions.UseRealisticSpeeds)
            {
                if (CustomCarAI.sm_speedData[vehicleID].speedMultiplier == 0 || CustomCarAI.sm_speedData[vehicleID].currentPath != vehicleData.m_path)
                {
                    CustomCarAI.sm_speedData[vehicleID].currentPath = vehicleData.m_path;
                    CustomCarAI.sm_speedData[vehicleID].SetRandomSpeedMultiplier(0.7f, 1.15f);
                }
                CustomCarAI.sm_speedData[vehicleID].ApplySpeedMultiplier(this.m_info);
            }
            
            CustomCarAI.SimulationStep(this, vehicleID, ref vehicleData, ref frameData, leaderID, ref leaderData, lodPhysics);
            if ((vehicleData.m_flags & Vehicle.Flags.Stopped) != Vehicle.Flags.None && this.CanLeave(vehicleID, ref vehicleData))
            {
                vehicleData.m_flags &= ~Vehicle.Flags.Stopped;
                vehicleData.m_flags |= Vehicle.Flags.Leaving;
            }
            if ((vehicleData.m_flags & (Vehicle.Flags.TransferToSource | Vehicle.Flags.GoingBack)) == Vehicle.Flags.TransferToSource && this.ShouldReturnToSource(vehicleID, ref vehicleData))
            {
                this.SetTarget(vehicleID, ref vehicleData, 0);
            }

            if ((CSLTraffic.Options & OptionsManager.ModOptions.UseRealisticSpeeds) == OptionsManager.ModOptions.UseRealisticSpeeds)
            {
                CustomCarAI.sm_speedData[vehicleID].RestoreVehicleSpeed(this.m_info);
            }
        }

        protected override bool StartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays)
        {
            return CustomCarAI.StartPathFind(this, vehicleID, ref vehicleData, startPos, endPos, startBothWays, endBothWays, RoadManager.VehicleType.Hearse);
        }


        /*
         * Private unmodified methods
         */

        private bool ShouldReturnToSource(ushort vehicleID, ref Vehicle data)
        {
            if (data.m_sourceBuilding != 0)
            {
                BuildingManager instance = Singleton<BuildingManager>.instance;
                if ((instance.m_buildings.m_buffer[(int)data.m_sourceBuilding].m_flags & (Building.Flags.Active | Building.Flags.Downgrading)) != Building.Flags.Active && instance.m_buildings.m_buffer[(int)data.m_sourceBuilding].m_fireIntensity == 0)
                {
                    return true;
                }
            }
            return false;
        }


        /*
         * Interface Proxy Methods
         */

        public new bool StartPathFind(ushort vehicleID, ref Vehicle vehicleData)
        {
            return base.StartPathFind(vehicleID, ref vehicleData);
        }

        public new void CalculateSegmentPosition(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position position, uint laneID, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed)
        {
            base.CalculateSegmentPosition(vehicleID, ref vehicleData, position, laneID, offset, out pos, out dir, out maxSpeed);
        }

        public new void CalculateSegmentPosition(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position nextPosition, PathUnit.Position position, uint laneID, byte offset, PathUnit.Position prevPos, uint prevLaneID, byte prevOffset, out Vector3 pos, out Vector3 dir, out float maxSpeed)
        {
            base.CalculateSegmentPosition(vehicleID, ref vehicleData, nextPosition, position, laneID, offset, prevPos, prevLaneID, prevOffset, out pos, out dir, out maxSpeed);
        }

        public new bool ParkVehicle(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position pathPos, uint nextPath, int nextPositionIndex, out byte segmentOffset)
        {
            return base.ParkVehicle(vehicleID, ref vehicleData, pathPos, nextPath, nextPositionIndex, out segmentOffset);
        }

        public new bool NeedChangeVehicleType(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position pathPos, uint laneID, VehicleInfo.VehicleType laneVehicleType, ref Vector4 target)
        {
            return base.NeedChangeVehicleType(vehicleID, ref vehicleData, pathPos, laneID, laneVehicleType, ref target);
        }

        public new bool ChangeVehicleType(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position pathPos, uint laneID)
        {
            return base.ChangeVehicleType(vehicleID, ref vehicleData, pathPos, laneID);
        }

        public new void UpdateNodeTargetPos(ushort vehicleID, ref Vehicle vehicleData, ushort nodeID, ref NetNode nodeData, ref Vector4 targetPos, int index)
        {
            base.UpdateNodeTargetPos(vehicleID, ref vehicleData, nodeID, ref nodeData, ref targetPos, index);
        }

        public new void ArrivingToDestination(ushort vehicleID, ref Vehicle vehicleData)
        {
            base.ArrivingToDestination(vehicleID, ref vehicleData);
        }

        public new float CalculateTargetSpeed(ushort vehicleID, ref Vehicle data, float speedLimit, float curve)
        {
            return base.CalculateTargetSpeed(vehicleID, ref data, speedLimit, curve);
        }

        public new void InvalidPath(ushort vehicleID, ref Vehicle vehicleData, ushort leaderID, ref Vehicle leaderData)
        {
            base.InvalidPath(vehicleID, ref vehicleData, leaderID, ref leaderData);
        }

        public new bool IsHeavyVehicle()
        {
            return base.IsHeavyVehicle();
        }

        public new bool IgnoreBlocked(ushort vehicleID, ref Vehicle vehicleData)
        {
            return base.IgnoreBlocked(vehicleID, ref vehicleData);
        }
    }
}
