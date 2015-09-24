using ColossalFramework;
using System;
using UnityEngine;

namespace CSL_Traffic
{
	class CustomPoliceCarAI : PoliceCarAI, IVehicle
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
						CustomCarAI.sm_speedData[vehicleID].SetRandomSpeedMultiplier(0.7f, 1.1f);
				}
				CustomCarAI.sm_speedData[vehicleID].ApplySpeedMultiplier(this.m_info);
			}

            if (this.m_info.m_class.m_level >= ItemClass.Level.Level4)
            {
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
            }
            else
            {
                frameData.m_blinkState = (((vehicleData.m_flags & Vehicle.Flags.Emergency2) == Vehicle.Flags.None) ? 0f : 10f);
                this.TryCollectCrime(vehicleID, ref vehicleData, ref frameData);
                CustomCarAI.SimulationStep(this, vehicleID, ref vehicleData, ref frameData, leaderID, ref leaderData, lodPhysics);
                if ((vehicleData.m_flags & Vehicle.Flags.Stopped) != Vehicle.Flags.None)
                {
                    if (this.CanLeave(vehicleID, ref vehicleData))
                    {
                        vehicleData.m_flags &= ~Vehicle.Flags.Stopped;
                        vehicleData.m_flags |= Vehicle.Flags.Leaving;
                    }
                }
                else if ((vehicleData.m_flags & Vehicle.Flags.Arriving) != Vehicle.Flags.None && vehicleData.m_targetBuilding != 0 && (vehicleData.m_flags & (Vehicle.Flags.Emergency2 | Vehicle.Flags.WaitingPath | Vehicle.Flags.GoingBack | Vehicle.Flags.WaitingTarget)) == Vehicle.Flags.None)
                {
                    this.ArriveAtTarget(vehicleID, ref vehicleData);
                }
                if ((vehicleData.m_flags & Vehicle.Flags.GoingBack) == Vehicle.Flags.None && this.ShouldReturnToSource(vehicleID, ref vehicleData))
                {
                    this.SetTarget(vehicleID, ref vehicleData, 0);
                }
            }

            if ((CSLTraffic.Options & OptionsManager.ModOptions.UseRealisticSpeeds) == OptionsManager.ModOptions.UseRealisticSpeeds)
			{
				CustomCarAI.sm_speedData[vehicleID].RestoreVehicleSpeed(this.m_info);
			}
		}

		protected override bool StartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays)
		{
			RoadManager.VehicleType vehicleType = RoadManager.VehicleType.PoliceCar;
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

		private void TryCollectCrime(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData)
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
						this.TryCollectCrime(vehicleID, ref vehicleData, ref frameData, num9, ref instance.m_buildings.m_buffer[(int)num9]);
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

		private void TryCollectCrime(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort buildingID, ref Building building)
		{
			Vector3 a = building.CalculateSidewalkPosition();
			if (Vector3.SqrMagnitude(a - frameData.m_position) < 1024f)
			{
				int num = -this.m_crimeCapacity;
				BuildingInfo info = building.Info;
				info.m_buildingAI.ModifyMaterialBuffer(buildingID, ref building, TransferManager.TransferReason.Crime, ref num);
			}
		}

		private bool ArriveAtTarget(ushort vehicleID, ref Vehicle data)
		{
			if (data.m_targetBuilding == 0)
			{
				return true;
			}
            if (this.m_info.m_class.m_level >= ItemClass.Level.Level4)
            {
                this.ArrestCriminals(vehicleID, ref data, data.m_targetBuilding);
                data.m_flags |= Vehicle.Flags.Stopped;
            }
            else
            {
                int num = -this.m_crimeCapacity;
                BuildingInfo info = Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)data.m_targetBuilding].Info;
                info.m_buildingAI.ModifyMaterialBuffer(data.m_targetBuilding, ref Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)data.m_targetBuilding], (TransferManager.TransferReason)data.m_transferType, ref num);
                if ((data.m_flags & Vehicle.Flags.Emergency2) != Vehicle.Flags.None)
                {
                    this.ArrestCriminals(vehicleID, ref data, data.m_targetBuilding);
                    for (int i = 0; i < this.m_policeCount; i++)
                    {
                        this.CreatePolice(vehicleID, ref data, Citizen.AgePhase.Adult0);
                    }
                    data.m_flags |= Vehicle.Flags.Stopped;
                }
            }
            this.SetTarget(vehicleID, ref data, 0);
			return false;
		}

        private void ArrestCriminals(ushort vehicleID, ref Vehicle vehicleData, ushort building)
        {
            if ((int)vehicleData.m_transferSize >= this.m_criminalCapacity)
            {
                return;
            }
            BuildingManager instance = Singleton<BuildingManager>.instance;
            CitizenManager instance2 = Singleton<CitizenManager>.instance;
            uint num = instance.m_buildings.m_buffer[(int)building].m_citizenUnits;
            int num2 = 0;
            while (num != 0u)
            {
                uint nextUnit = instance2.m_units.m_buffer[(int)((UIntPtr)num)].m_nextUnit;
                for (int i = 0; i < 5; i++)
                {
                    uint citizen = instance2.m_units.m_buffer[(int)((UIntPtr)num)].GetCitizen(i);
                    if (citizen != 0u && (instance2.m_citizens.m_buffer[(int)((UIntPtr)citizen)].Criminal || instance2.m_citizens.m_buffer[(int)((UIntPtr)citizen)].Arrested) && !instance2.m_citizens.m_buffer[(int)((UIntPtr)citizen)].Dead && instance2.m_citizens.m_buffer[(int)((UIntPtr)citizen)].GetBuildingByLocation() == building)
                    {
                        instance2.m_citizens.m_buffer[(int)((UIntPtr)citizen)].SetVehicle(citizen, vehicleID, 0u);
                        if (instance2.m_citizens.m_buffer[(int)((UIntPtr)citizen)].m_vehicle != vehicleID)
                        {
                            vehicleData.m_transferSize = (ushort)this.m_criminalCapacity;
                            return;
                        }
                        instance2.m_citizens.m_buffer[(int)((UIntPtr)citizen)].Arrested = true;
                        ushort instance3 = instance2.m_citizens.m_buffer[(int)((UIntPtr)citizen)].m_instance;
                        if (instance3 != 0)
                        {
                            instance2.ReleaseCitizenInstance(instance3);
                        }
                        instance2.m_citizens.m_buffer[(int)((UIntPtr)citizen)].CurrentLocation = Citizen.Location.Moving;
                        if ((int)(vehicleData.m_transferSize += 1) >= this.m_criminalCapacity)
                        {
                            return;
                        }
                    }
                }
                num = nextUnit;
                if (++num2 > 524288)
                {
                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                    break;
                }
            }
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

		private void CreatePolice(ushort vehicleID, ref Vehicle data, Citizen.AgePhase agePhase)
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
