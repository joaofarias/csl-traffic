using ColossalFramework;
using System;
using UnityEngine;

namespace CSL_Traffic
{
    class CustomFireTruckAI : FireTruckAI, IVehicle
    {
        public override void SimulationStep(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics)
        {
            if ((CSLTraffic.Options & OptionsManager.ModOptions.UseRealisticSpeeds) == OptionsManager.ModOptions.UseRealisticSpeeds)
            {
                if (CustomCarAI.sm_speedData[vehicleID].speedMultiplier == 0 || CustomCarAI.sm_speedData[vehicleID].currentPath != vehicleData.m_path)
                {
                    CustomCarAI.sm_speedData[vehicleID].currentPath = vehicleData.m_path;
                    if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) == Vehicle.Flags.Emergency2)
                        CustomCarAI.sm_speedData[vehicleID].SetRandomSpeedMultiplier(1f, 1.75f);
                    else
                        CustomCarAI.sm_speedData[vehicleID].SetRandomSpeedMultiplier(0.65f, 1f);
                }
                CustomCarAI.sm_speedData[vehicleID].ApplySpeedMultiplier(this.m_info);
            }
            

            frameData.m_blinkState = (((vehicleData.m_flags & (Vehicle.Flags.Emergency1 | Vehicle.Flags.Emergency2)) == Vehicle.Flags.None) ? 0f : 10f);
            CustomCarAI.SimulationStep(this, vehicleID, ref vehicleData, ref frameData, leaderID, ref leaderData, lodPhysics);
            bool flag = false;
            if (vehicleData.m_targetBuilding != 0)
            {
                BuildingManager instance = Singleton<BuildingManager>.instance;
                Vector3 a = instance.m_buildings.m_buffer[(int)vehicleData.m_targetBuilding].CalculateSidewalkPosition();
                flag = ((a - frameData.m_position).sqrMagnitude < 4096f);
                bool flag2 = (vehicleData.m_flags & Vehicle.Flags.Stopped) != Vehicle.Flags.None || frameData.m_velocity.sqrMagnitude < 0.0100000007f;
                if (flag && (vehicleData.m_flags & Vehicle.Flags.Emergency2) != Vehicle.Flags.None)
                {
                    vehicleData.m_flags = ((vehicleData.m_flags & ~Vehicle.Flags.Emergency2) | Vehicle.Flags.Emergency1);
                }
                if (flag && flag2)
                {
                    if (vehicleData.m_blockCounter > 8)
                    {
                        vehicleData.m_blockCounter = 8;
                    }
                    if (vehicleData.m_blockCounter == 8 && (vehicleData.m_flags & Vehicle.Flags.Stopped) == Vehicle.Flags.None)
                    {
                        this.ArriveAtTarget(leaderID, ref leaderData);
                    }
                    if (this.ExtinguishFire(vehicleID, ref vehicleData, vehicleData.m_targetBuilding, ref Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)vehicleData.m_targetBuilding]))
                    {
                        this.SetTarget(vehicleID, ref vehicleData, 0);
                    }
                }
                else
                {
                    if (instance.m_buildings.m_buffer[(int)vehicleData.m_targetBuilding].m_fireIntensity == 0)
                    {
                        this.SetTarget(vehicleID, ref vehicleData, 0);
                    }
                }
            }
            if ((vehicleData.m_flags & Vehicle.Flags.Stopped) != Vehicle.Flags.None && !flag && this.CanLeave(vehicleID, ref vehicleData))
            {
                vehicleData.m_flags &= ~Vehicle.Flags.Stopped;
                vehicleData.m_flags |= Vehicle.Flags.Leaving;
            }
            if ((vehicleData.m_flags & Vehicle.Flags.GoingBack) == Vehicle.Flags.None)
            {
                if (this.ShouldReturnToSource(vehicleID, ref vehicleData))
                {
                    this.SetTarget(vehicleID, ref vehicleData, 0);
                }
            }
            else
            {
                if ((ulong)(Singleton<SimulationManager>.instance.m_currentFrameIndex >> 4 & 15u) == (ulong)((long)(vehicleID & 15)) && !this.ShouldReturnToSource(vehicleID, ref vehicleData))
                {
                    TransferManager.TransferOffer offer = default(TransferManager.TransferOffer);
                    offer.Priority = 3;
                    offer.Vehicle = vehicleID;
                    offer.Position = frameData.m_position;
                    offer.Amount = 1;
                    offer.Active = true;
                    Singleton<TransferManager>.instance.AddIncomingOffer((TransferManager.TransferReason)vehicleData.m_transferType, offer);
                }
            }

            if ((CSLTraffic.Options & OptionsManager.ModOptions.UseRealisticSpeeds) == OptionsManager.ModOptions.UseRealisticSpeeds)
            {
                CustomCarAI.sm_speedData[vehicleID].RestoreVehicleSpeed(this.m_info);
            }
        }

        protected override bool StartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays)
        {
            RoadManager.VehicleType vehicleType = RoadManager.VehicleType.FireTruck;
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


        /*
         * Private unmodified methods
         */
        
        private bool ArriveAtTarget(ushort vehicleID, ref Vehicle data)
        {
            if (data.m_targetBuilding == 0)
            {
                Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleID);
                return true;
            }
            for (int i = 0; i < this.m_firemanCount; i++)
            {
                if (i < this.m_hoseCount)
                {
                    this.CreateFireman(vehicleID, ref data, Citizen.AgePhase.Adult1);
                }
                else
                {
                    this.CreateFireman(vehicleID, ref data, Citizen.AgePhase.Adult0);
                }
            }
            data.m_flags |= Vehicle.Flags.Stopped;
            return false;
        }

        private bool ExtinguishFire(ushort vehicleID, ref Vehicle data, ushort buildingID, ref Building buildingData)
        {
            int width = buildingData.Width;
            int length = buildingData.Length;
            int num = Mathf.Min(5000, (int)buildingData.m_fireIntensity * (width * length));
            if (num != 0 && Singleton<SimulationManager>.instance.m_randomizer.Int32(8u) == 0)
            {
                num = Mathf.Max(num - this.m_fireFightingRate, 0);
                num /= width * length;
                buildingData.m_fireIntensity = (byte)num;
                if (num == 0)
                {
                    if (data.m_sourceBuilding != 0)
                    {
                        int tempExport = (int)Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)data.m_sourceBuilding].m_tempExport;
                        Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)data.m_sourceBuilding].m_tempExport = (byte)Mathf.Min(tempExport + 1, 255);
                    }
                    Building.Flags flags = buildingData.m_flags;
                    if (buildingData.m_productionRate != 0)
                    {
                        buildingData.m_flags |= Building.Flags.Active;
                    }
                    Building.Flags flags2 = buildingData.m_flags;
                    Singleton<BuildingManager>.instance.UpdateBuildingRenderer(buildingID, buildingData.GetLastFrameData().m_fireDamage == 0);
                    if (flags2 != flags)
                    {
                        Singleton<BuildingManager>.instance.UpdateFlags(buildingID, flags2 ^ flags);
                    }
                    GuideController properties = Singleton<GuideManager>.instance.m_properties;
                    if (properties != null)
                    {
                        Singleton<BuildingManager>.instance.m_buildingOnFire.Deactivate(buildingID, false);
                    }
                }
            }
            return num == 0;
        }

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

        private void CreateFireman(ushort vehicleID, ref Vehicle data, Citizen.AgePhase agePhase)
        {
            SimulationManager instance = Singleton<SimulationManager>.instance;
            CitizenManager instance2 = Singleton<CitizenManager>.instance;
            CitizenInfo groupCitizenInfo = instance2.GetGroupCitizenInfo(ref instance.m_randomizer, this.m_info.m_class.m_service, Citizen.Gender.Male, Citizen.SubCulture.Generic, agePhase);
            if (groupCitizenInfo != null)
            {
                int family = instance.m_randomizer.Int32(256u);
                uint num = 0u;
                if (instance2.CreateCitizen(out num, 90, family, ref instance.m_randomizer, groupCitizenInfo.m_gender))
                {
                    ushort num2;
                    if (instance2.CreateCitizenInstance(out num2, ref instance.m_randomizer, groupCitizenInfo, num))
                    {
                        Vector3 randomDoorPosition = data.GetRandomDoorPosition(ref instance.m_randomizer, VehicleInfo.DoorType.Exit);
                        groupCitizenInfo.m_citizenAI.SetCurrentVehicle(num2, ref instance2.m_instances.m_buffer[(int)num2], 0, 0u, randomDoorPosition);
                        groupCitizenInfo.m_citizenAI.SetTarget(num2, ref instance2.m_instances.m_buffer[(int)num2], data.m_targetBuilding);
                        instance2.m_citizens.m_buffer[(int)((UIntPtr)num)].SetVehicle(num, vehicleID, 0u);
                    }
                    else
                    {
                        instance2.ReleaseCitizen(num);
                    }
                }
            }
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
