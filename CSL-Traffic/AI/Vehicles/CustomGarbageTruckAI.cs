using ColossalFramework;
using System;
using UnityEngine;

namespace CSL_Traffic
{
    /*
     * The AI for garbage truck using pedestrian paths. Again, there's a few small changes to make it use them (having them in the path is not enough).
     * The movement happens on SimulationStep.
     */
    public class CustomGarbageTruckAI : GarbageTruckAI, IVehicle
    {
        public override void SimulationStep(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics)
        {
            if ((CSLTraffic.Options & OptionsManager.ModOptions.UseRealisticSpeeds) == OptionsManager.ModOptions.UseRealisticSpeeds)
            {
                if (CustomCarAI.sm_speedData[vehicleID].speedMultiplier == 0 || CustomCarAI.sm_speedData[vehicleID].currentPath != vehicleData.m_path)
                {
                    CustomCarAI.sm_speedData[vehicleID].currentPath = vehicleData.m_path;
                    CustomCarAI.sm_speedData[vehicleID].SetRandomSpeedMultiplier(0.75f, 0.95f);
                }
                CustomCarAI.sm_speedData[vehicleID].ApplySpeedMultiplier(this.m_info);
            }
            

            if ((vehicleData.m_flags & Vehicle.Flags.TransferToSource) != Vehicle.Flags.None)
            {
                if ((int)vehicleData.m_transferSize < this.m_cargoCapacity)
                {
                    this.TryCollectGarbage(vehicleID, ref vehicleData, ref frameData);
                }
                if ((int)vehicleData.m_transferSize >= this.m_cargoCapacity && (vehicleData.m_flags & Vehicle.Flags.GoingBack) == Vehicle.Flags.None && vehicleData.m_targetBuilding != 0)
                {
                    this.SetTarget(vehicleID, ref vehicleData, 0);
                }
            }
            CustomCarAI.SimulationStep(this, vehicleID, ref vehicleData, ref frameData, leaderID, ref leaderData, lodPhysics);
            if ((vehicleData.m_flags & Vehicle.Flags.Arriving) != Vehicle.Flags.None && vehicleData.m_targetBuilding != 0 && (vehicleData.m_flags & (Vehicle.Flags.WaitingPath | Vehicle.Flags.GoingBack | Vehicle.Flags.WaitingTarget)) == Vehicle.Flags.None)
            {
                this.ArriveAtTarget(vehicleID, ref vehicleData);
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
            return CustomCarAI.StartPathFind(this, vehicleID, ref vehicleData, startPos, endPos, startBothWays, endBothWays, RoadManager.VehicleType.GarbageTruck);
        }

        /*
         * Private unmodified methods
         */

        private void TryCollectGarbage(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData)
        {
            if ((vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) == Vehicle.Flags.Underground)
            {
                return;
            }
            Vector3 position = frameData.m_position;
            float num = position.x - 32f;
            float num2 = position.z - 32f;
            float num3 = position.x + 32f;
            float num4 = position.z + 32f;
            int num5 = Mathf.Max((int)((num - 72f) / 64f + 135f), 0);
            int num6 = Mathf.Max((int)((num2 - 72f) / 64f + 135f), 0);
            int num7 = Mathf.Min((int)((num3 + 72f) / 64f + 135f), 269);
            int num8 = Mathf.Min((int)((num4 + 72f) / 64f + 135f), 269);
            BuildingManager instance = Singleton<BuildingManager>.instance;
            for (int i = num6; i <= num8; i++)
            {
                for (int j = num5; j <= num7; j++)
                {
                    ushort num9 = instance.m_buildingGrid[i * 270 + j];
                    int num10 = 0;
                    while (num9 != 0)
                    {
                        this.TryCollectGarbage(vehicleID, ref vehicleData, ref frameData, num9, ref instance.m_buildings.m_buffer[(int)num9]);
                        num9 = instance.m_buildings.m_buffer[(int)num9].m_nextGridBuilding;
                        if (++num10 >= 32768)
                        {
                            CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                            break;
                        }
                    }
                }
            }
        }

        private void TryCollectGarbage(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort buildingID, ref Building building)
        {
            Vector3 a = building.CalculateSidewalkPosition();
            if (Vector3.SqrMagnitude(a - frameData.m_position) < 1024f)
            {
                int num = Mathf.Min(0, (int)vehicleData.m_transferSize - this.m_cargoCapacity);
                if (num == 0)
                {
                    return;
                }
                BuildingInfo info = building.Info;
                info.m_buildingAI.ModifyMaterialBuffer(buildingID, ref building, (TransferManager.TransferReason)vehicleData.m_transferType, ref num);
                if (num != 0)
                {
                    vehicleData.m_transferSize += (ushort)Mathf.Max(0, -num);
                }
            }
        }

        private bool ArriveAtTarget(ushort vehicleID, ref Vehicle data)
        {
            if (data.m_targetBuilding == 0)
            {
                return true;
            }
            int num = 0;
            if ((data.m_flags & Vehicle.Flags.TransferToTarget) != Vehicle.Flags.None)
            {
                num = (int)data.m_transferSize;
            }
            if ((data.m_flags & Vehicle.Flags.TransferToSource) != Vehicle.Flags.None)
            {
                num = Mathf.Min(0, (int)data.m_transferSize - this.m_cargoCapacity);
            }
            BuildingManager instance = Singleton<BuildingManager>.instance;
            BuildingInfo info = instance.m_buildings.m_buffer[(int)data.m_targetBuilding].Info;
            info.m_buildingAI.ModifyMaterialBuffer(data.m_targetBuilding, ref instance.m_buildings.m_buffer[(int)data.m_targetBuilding], (TransferManager.TransferReason)data.m_transferType, ref num);
            if ((data.m_flags & Vehicle.Flags.TransferToTarget) != Vehicle.Flags.None)
            {
                data.m_transferSize = (ushort)Mathf.Clamp((int)data.m_transferSize - num, 0, (int)data.m_transferSize);
            }
            if ((data.m_flags & Vehicle.Flags.TransferToSource) != Vehicle.Flags.None)
            {
                data.m_transferSize += (ushort)Mathf.Max(0, -num);
            }
            this.SetTarget(vehicleID, ref data, 0);
            return false;
        }

        private bool ShouldReturnToSource(ushort vehicleID, ref Vehicle data)
        {
            if (data.m_sourceBuilding != 0)
            {
                BuildingManager instance = Singleton<BuildingManager>.instance;
                if ((instance.m_buildings.m_buffer[(int)data.m_sourceBuilding].m_productionRate == 0 || (instance.m_buildings.m_buffer[(int)data.m_sourceBuilding].m_flags & (Building.Flags.Downgrading | Building.Flags.BurnedDown)) != Building.Flags.None) && instance.m_buildings.m_buffer[(int)data.m_sourceBuilding].m_fireIntensity == 0)
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
