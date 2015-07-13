using ColossalFramework;
using System;
using UnityEngine;

namespace CSL_Traffic
{
	class CustomPassengerCarAI : PassengerCarAI, IVehicle
	{
		public override void SimulationStep(ushort vehicleID, ref Vehicle data, Vector3 physicsLodRefPos)
		{
			if ((CSLTraffic.Options & OptionsManager.ModOptions.NoDespawn) == OptionsManager.ModOptions.NoDespawn)
				data.m_flags &= ~Vehicle.Flags.Congestion;

			base.SimulationStep(vehicleID, ref data, physicsLodRefPos);
		}

		public override void SimulationStep(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics)
		{
			if ((CSLTraffic.Options & OptionsManager.ModOptions.UseRealisticSpeeds) == OptionsManager.ModOptions.UseRealisticSpeeds)
			{
				if (CustomCarAI.sm_speedData[vehicleID].speedMultiplier == 0 || CustomCarAI.sm_speedData[vehicleID].currentPath != vehicleData.m_path)
				{
					CustomCarAI.sm_speedData[vehicleID].currentPath = vehicleData.m_path;
					CustomCarAI.sm_speedData[vehicleID].SetRandomSpeedMultiplier(0.6f, 1.4f);
				}
				CustomCarAI.sm_speedData[vehicleID].ApplySpeedMultiplier(this.m_info);
			}
			
			if ((vehicleData.m_flags & Vehicle.Flags.Stopped) != Vehicle.Flags.None)
			{
				vehicleData.m_waitCounter += 1;
				if (this.CanLeave(vehicleID, ref vehicleData))
				{
					vehicleData.m_flags &= ~Vehicle.Flags.Stopped;
					vehicleData.m_waitCounter = 0;
				}
			}
			CustomCarAI.SimulationStep(this, vehicleID, ref vehicleData, ref frameData, leaderID, ref leaderData, lodPhysics);

			if ((CSLTraffic.Options & OptionsManager.ModOptions.UseRealisticSpeeds) == OptionsManager.ModOptions.UseRealisticSpeeds)
			{
				CustomCarAI.sm_speedData[vehicleID].RestoreVehicleSpeed(this.m_info);
			}
		}

		protected override bool StartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays)
		{
			VehicleInfo info = this.m_info;
			ushort driverInstance = this.GetDriverInstance(vehicleID, ref vehicleData);
			if (driverInstance == 0)
			{
				return false;
			}
			CitizenManager instance = Singleton<CitizenManager>.instance;
			CitizenInfo info2 = instance.m_instances.m_buffer[(int)driverInstance].Info;
			NetInfo.LaneType laneTypes = NetInfo.LaneType.Vehicle | NetInfo.LaneType.Pedestrian;
			VehicleInfo.VehicleType vehicleType = this.m_info.m_vehicleType;
			bool allowUnderground = (vehicleData.m_flags & Vehicle.Flags.Underground) != Vehicle.Flags.None;
			PathUnit.Position startPosA;
			PathUnit.Position startPosB;
			float num;
			float num2;
			PathUnit.Position endPosA;
            //    if (PathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, allowUnderground, false, 32f, out startPosA, out startPosB, out num, out num2)                     && info2.m_citizenAI.FindPathPosition(driverInstance, ref instance.m_instances.m_buffer[(int)driverInstance], endPos, laneTypes, vehicleType, false, out endPosA))
            if (CustomPathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, allowUnderground, false, 32f, out startPosA, out startPosB, out num, out num2, RoadManager.VehicleType.PassengerCar) && FindPathPosition(driverInstance, ref instance.m_instances.m_buffer[(int)driverInstance], endPos, laneTypes, vehicleType, false, out endPosA))
			{
				if (!startBothWays || num < 10f)
				{
					startPosB = default(PathUnit.Position);
				}
				PathUnit.Position endPosB = default(PathUnit.Position);
				SimulationManager instance2 = Singleton<SimulationManager>.instance;
				uint path;
				bool createPathResult;
				CustomPathManager customPathManager = Singleton<PathManager>.instance as CustomPathManager;
				if (customPathManager != null)
					createPathResult = customPathManager.CreatePath(out path, ref instance2.m_randomizer, instance2.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, laneTypes, vehicleType, 20000f, RoadManager.VehicleType.PassengerCar);
				else
					createPathResult = Singleton<PathManager>.instance.CreatePath(out path, ref instance2.m_randomizer, instance2.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, laneTypes, vehicleType, 20000f);
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
		}

		// from CitizenAI
		public static bool FindPathPosition(ushort instanceID, ref CitizenInstance citizenData, Vector3 pos, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, bool allowUnderground, out PathUnit.Position position)
		{
			position = default(PathUnit.Position);
			float num = 1E+10f;
			PathUnit.Position position2;
			PathUnit.Position position3;
			float num2;
			float num3;
			if (CustomPathManager.FindPathPosition(pos, ItemClass.Service.Road, laneTypes, vehicleTypes, allowUnderground, false, 32f, out position2, out position3, out num2, out num3, RoadManager.VehicleType.PassengerCar) && num2 < num)
			{
				num = num2;
				position = position2;
			}
			PathUnit.Position position4;
			PathUnit.Position position5;
			float num4;
			float num5;
            if (CustomPathManager.FindPathPosition(pos, ItemClass.Service.Beautification, laneTypes, vehicleTypes, allowUnderground, false, 32f, out position4, out position5, out num4, out num5, RoadManager.VehicleType.PassengerCar) && num4 < num)
			{
				num = num4;
				position = position4;
			}
			PathUnit.Position position6;
			PathUnit.Position position7;
			float num6;
			float num7;
            if ((citizenData.m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None && CustomPathManager.FindPathPosition(pos, ItemClass.Service.PublicTransport, laneTypes, vehicleTypes, allowUnderground, false, 32f, out position6, out position7, out num6, out num7, RoadManager.VehicleType.PassengerCar) && num6 < num)
			{
				position = position6;
			}
			return position.m_segment != 0;
		}

		/*
		 * Private unmodified methods
		 */

		private ushort GetDriverInstance(ushort vehicleID, ref Vehicle data)
		{
			CitizenManager instance = Singleton<CitizenManager>.instance;
			uint num = data.m_citizenUnits;
			int num2 = 0;
			while (num != 0u)
			{
				uint nextUnit = instance.m_units.m_buffer[(int)((UIntPtr)num)].m_nextUnit;
				for (int i = 0; i < 5; i++)
				{
					uint citizen = instance.m_units.m_buffer[(int)((UIntPtr)num)].GetCitizen(i);
					if (citizen != 0u)
					{
						ushort instance2 = instance.m_citizens.m_buffer[(int)((UIntPtr)citizen)].m_instance;
						if (instance2 != 0)
						{
							return instance2;
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
			return 0;
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
