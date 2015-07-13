using ColossalFramework;
using UnityEngine;

namespace CSL_Traffic
{
    class CustomAmbulanceAI : AmbulanceAI, IVehicle
    {
        public override void SimulationStep(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics)
        {
            if ((CSLTraffic.Options & OptionsManager.ModOptions.UseRealisticSpeeds) == OptionsManager.ModOptions.UseRealisticSpeeds)
            {
                if (CustomCarAI.sm_speedData[vehicleID].speedMultiplier == 0 || CustomCarAI.sm_speedData[vehicleID].currentPath != vehicleData.m_path)
                {
                    CustomCarAI.sm_speedData[vehicleID].currentPath = vehicleData.m_path;
                    if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) == Vehicle.Flags.Emergency2)
                        CustomCarAI.sm_speedData[vehicleID].SetRandomSpeedMultiplier(1f, 1.5f);
                    else
                        CustomCarAI.sm_speedData[vehicleID].SetRandomSpeedMultiplier(0.7f, 1.05f);
                }
                CustomCarAI.sm_speedData[vehicleID].ApplySpeedMultiplier(this.m_info);
            }
            

            frameData.m_blinkState = (((vehicleData.m_flags & Vehicle.Flags.Emergency2) == Vehicle.Flags.None) ? 0f : 10f);
            CustomCarAI.SimulationStep(this, vehicleID, ref vehicleData, ref frameData, leaderID, ref leaderData, lodPhysics);
            if ((vehicleData.m_flags & Vehicle.Flags.Stopped) != Vehicle.Flags.None && this.CanLeave(vehicleID, ref vehicleData))
            {
                vehicleData.m_flags &= ~Vehicle.Flags.Stopped;
                vehicleData.m_flags |= Vehicle.Flags.Leaving;
            }
            if ((vehicleData.m_flags & Vehicle.Flags.GoingBack) == Vehicle.Flags.None && this.ShouldReturnToSource(vehicleID, ref vehicleData))
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
            RoadManager.VehicleType vehicleType = RoadManager.VehicleType.Ambulance;
            if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != Vehicle.Flags.None)
                vehicleType |= RoadManager.VehicleType.Emergency;

            VehicleInfo info = this.m_info;
            bool allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != Vehicle.Flags.None;
            PathUnit.Position startPosA;
            PathUnit.Position startPosB;
            float num;
            float num2;
            PathUnit.Position endPosA;
            PathUnit.Position endPosB;
            float num3;
            float num4;
            if (CustomPathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, allowUnderground, false, 32f, out startPosA, out startPosB, out num, out num2, vehicleType) && CustomPathManager.FindPathPosition(endPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, false, false, 32f, out endPosA, out endPosB, out num3, out num4, vehicleType))
            {
                if (!startBothWays || num < 10f)
                {
                    startPosB = default(PathUnit.Position);
                }
                if (!endBothWays || num3 < 10f)
                {
                    endPosB = default(PathUnit.Position);
                }
                uint path;
                bool createPathResult;
                CustomPathManager customPathManager = Singleton<PathManager>.instance as CustomPathManager;
                if (customPathManager != null)
                    createPathResult = customPathManager.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, 20000f, this.IsHeavyVehicle(), this.IgnoreBlocked(vehicleID, ref vehicleData), false, false, vehicleType);
                else
                    createPathResult = Singleton<PathManager>.instance.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, 20000f, this.IsHeavyVehicle(), this.IgnoreBlocked(vehicleID, ref vehicleData), false, false);
                if (createPathResult)
                {
                    if (vehicleData.m_path != 0u)
                    {
                        Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
                    }
                    vehicleData.m_path = path;
                    vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
                    return true;
                }
            }
            return false;

            //return CustomCarAI.StartPathFind(this, vehicleID, ref vehicleData, startPos, endPos, startBothWays, endBothWays, vehicleType);
        }

        //protected override float CalculateTargetSpeed(ushort vehicleID, ref Vehicle data, float speedLimit, float curve)
        //{
        //    float targetSpeed = base.CalculateTargetSpeed(vehicleID, ref data, speedLimit, curve);

        //    if ((CSLTraffic.Options & OptionsManager.ModOptions.UseRealisticSpeeds) == OptionsManager.ModOptions.None)
        //        return targetSpeed;

        //    if (m_currentPath != data.m_path)
        //    {
        //        m_currentPath = data.m_path;

        //        if ((data.m_flags & Vehicle.Flags.Emergency2) == Vehicle.Flags.Emergency2)
        //            m_speedMutliplier = Random.Range(1f, 1.25f);
        //        else
        //            m_speedMutliplier = Random.Range(0.75f, 1f);
        //    }

        //    return targetSpeed * m_speedMutliplier;
        //}

        /*
         * Private unmodified methods
         */

        private bool ShouldReturnToSource(ushort vehicleID, ref Vehicle data)
        {
            if (data.m_sourceBuilding != 0)
            {
                BuildingManager instance = Singleton<BuildingManager>.instance;
                if ((instance.m_buildings.m_buffer[(int)data.m_sourceBuilding].m_flags & Building.Flags.Active) == Building.Flags.None && instance.m_buildings.m_buffer[(int)data.m_sourceBuilding].m_fireIntensity == 0)
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
