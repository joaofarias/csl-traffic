using ColossalFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace CSL_Traffic
{
	class CustomCargoTruckAI : CargoTruckAI, IVehicle
	{
		public static bool sm_initialized;

		public static void Initialize(VehicleCollection collection, Transform customPrefabs)
		{
			if (sm_initialized)
				return;

#if DEBUG
            System.IO.File.AppendAllText("TrafficPP_Debug.txt", "Initializing Cargo Truck AI.\n");
#endif

			int length = collection.m_prefabs.Length;
			VehicleInfo[] vehicles = new VehicleInfo[length];
			string[] vehicleNames = new string[length];
			for (int i = 0; i < length; i++)
			{
				VehicleInfo vehicleInfo = collection.m_prefabs[i];
				if (vehicleInfo == null)
					throw new KeyNotFoundException("Null prefab in the collection " + collection.name);

				vehicleNames[i] = vehicleInfo.name;

				GameObject gameObject = GameObject.Instantiate<GameObject>(vehicleInfo.gameObject);
				gameObject.name = vehicleNames[i];
				gameObject.transform.SetParent(customPrefabs);
				GameObject.Destroy(gameObject.GetComponent<CargoTruckAI>());
				gameObject.AddComponent<CustomCargoTruckAI>();
				vehicles[i] = gameObject.GetComponent<VehicleInfo>();
				vehicles[i].m_prefabInitialized = false;
				vehicles[i].m_vehicleAI = null;
			}


			MethodInfo method = typeof(VehicleCollection).GetMethod("InitializePrefabs", BindingFlags.NonPublic | BindingFlags.Static);
			Singleton<LoadingManager>.instance.QueueLoadingAction((IEnumerator)method.Invoke(null, new object[]{ collection.name, vehicles, vehicleNames }));
			
            CustomCargoTruckAI.sm_initialized = true;
		}
		public override void InitializeAI()
		{
			base.InitializeAI();
			this.m_cargoCapacity = ((name == "Lorry") ? 8000 : 6000);

#if DEBUG
            System.IO.File.AppendAllText("TrafficPP_Debug.txt", "Cargo Truck AI successfully initialized (" + name + ").\n");
#endif
		}
		public override void SimulationStep(ushort vehicleID, ref Vehicle vehicleData, ushort leaderID, ref Vehicle leaderData, int lodPhysics)
		{
            if ((vehicleData.m_flags & Vehicle.Flags.Spawned) != Vehicle.Flags.None)
            {
                Vehicle.Frame lastFrameData = vehicleData.GetLastFrameData();
                if (this.m_info.m_isLargeVehicle)
                {
                    int num = Mathf.Clamp((int)(lastFrameData.m_position.x / 320f + 27f), 0, 53);
                    int num2 = Mathf.Clamp((int)(lastFrameData.m_position.z / 320f + 27f), 0, 53);
                    CustomCarAI.SimulationStep(this, vehicleID, ref vehicleData, ref lastFrameData, leaderID, ref leaderData, lodPhysics);
                    int num3 = Mathf.Clamp((int)(lastFrameData.m_position.x / 320f + 27f), 0, 53);
                    int num4 = Mathf.Clamp((int)(lastFrameData.m_position.z / 320f + 27f), 0, 53);
                    if ((num3 != num || num4 != num2) && (vehicleData.m_flags & Vehicle.Flags.Spawned) != Vehicle.Flags.None)
                    {
                        Singleton<VehicleManager>.instance.RemoveFromGrid(vehicleID, ref vehicleData, true, num, num2);
                        Singleton<VehicleManager>.instance.AddToGrid(vehicleID, ref vehicleData, true, num3, num4);
                    }
                }
                else
                {
                    int num5 = Mathf.Clamp((int)(lastFrameData.m_position.x / 32f + 270f), 0, 539);
                    int num6 = Mathf.Clamp((int)(lastFrameData.m_position.z / 32f + 270f), 0, 539);
                    CustomCarAI.SimulationStep(this, vehicleID, ref vehicleData, ref lastFrameData, leaderID, ref leaderData, lodPhysics);
                    int num7 = Mathf.Clamp((int)(lastFrameData.m_position.x / 32f + 270f), 0, 539);
                    int num8 = Mathf.Clamp((int)(lastFrameData.m_position.z / 32f + 270f), 0, 539);
                    if ((num7 != num5 || num8 != num6) && (vehicleData.m_flags & Vehicle.Flags.Spawned) != Vehicle.Flags.None)
                    {
                        Singleton<VehicleManager>.instance.RemoveFromGrid(vehicleID, ref vehicleData, false, num5, num6);
                        Singleton<VehicleManager>.instance.AddToGrid(vehicleID, ref vehicleData, false, num7, num8);
                    }
                }
                if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) == Vehicle.Flags.Created)
                {
                    this.FrameDataUpdated(vehicleID, ref vehicleData, ref lastFrameData);
                    vehicleData.SetFrameData(Singleton<SimulationManager>.instance.m_currentFrameIndex, lastFrameData);
                }
            }
		}
		protected override bool StartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays)
		{
            if ((vehicleData.m_flags & (Vehicle.Flags.TransferToSource | Vehicle.Flags.GoingBack)) != Vehicle.Flags.None)
            {
                if ((CSLTraffic.Options & OptionsManager.ModOptions.AllowTrucksInPedestrianRoads) != OptionsManager.ModOptions.None)
                {
                    return CustomCarAI.StartPathFind(this, vehicleID, ref vehicleData, startPos, endPos, startBothWays, endBothWays, true);
                }
                return CustomCarAI.StartPathFind(this, vehicleID, ref vehicleData, startPos, endPos, startBothWays, endBothWays, false);
            }
            PathUnit.Position startPosA;
            PathUnit.Position startPosB;
            float num;
            float num2;
            NetInfo.LaneType laneTypes = NetInfo.LaneType.Vehicle;
            if ((CSLTraffic.Options & OptionsManager.ModOptions.AllowTrucksInPedestrianRoads) != OptionsManager.ModOptions.None)
            {
                laneTypes |= ((NetInfo.LaneType)((byte)32));
            }
            bool flag = PathManager.FindPathPosition(startPos, ItemClass.Service.Road, laneTypes, VehicleInfo.VehicleType.Car, 32f, out startPosA, out startPosB, out num, out num2);
            PathUnit.Position position;
            PathUnit.Position position2;
            float num3;
            float num4;
            if (PathManager.FindPathPosition(startPos, ItemClass.Service.PublicTransport, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship, 32f, out position, out position2, out num3, out num4))
            {
                if (!flag || num3 < num)
                {
                    startPosA = position;
                    startPosB = position2;
                    num = num3;
                    num2 = num4;
                }
                flag = true;
            }
            PathUnit.Position endPosA;
            PathUnit.Position endPosB;
            float num5;
            float num6;
            bool flag2 = PathManager.FindPathPosition(endPos, ItemClass.Service.Road, laneTypes, VehicleInfo.VehicleType.Car, 32f, out endPosA, out endPosB, out num5, out num6);
            PathUnit.Position position3;
            PathUnit.Position position4;
            float num7;
            float num8;
            if (PathManager.FindPathPosition(endPos, ItemClass.Service.PublicTransport, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship, 32f, out position3, out position4, out num7, out num8))
            {
                if (!flag2 || num7 < num5)
                {
                    endPosA = position3;
                    endPosB = position4;
                    num5 = num7;
                    num6 = num8;
                }
                flag2 = true;
            }
            if (flag && flag2)
            {
                CustomPathManager instance = (CustomPathManager)Singleton<PathManager>.instance;
                if (!startBothWays || num < 10f)
                {
                    startPosB = default(PathUnit.Position);
                }
                if (!endBothWays || num5 < 10f)
                {
                    endPosB = default(PathUnit.Position);
                }
                laneTypes = NetInfo.LaneType.Vehicle | NetInfo.LaneType.Cargo;
                if ((CSLTraffic.Options & OptionsManager.ModOptions.AllowTrucksInPedestrianRoads) != OptionsManager.ModOptions.None)
                {
                    laneTypes |= ((NetInfo.LaneType)((byte)32));
                }
                VehicleInfo.VehicleType vehicleTypes = VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship;
                uint path;
                if (instance.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, laneTypes, vehicleTypes, 20000f, this.IsHeavyVehicle(), this.IgnoreBlocked(vehicleID, ref vehicleData), false, false, true))
                {
                    if (vehicleData.m_path != 0u)
                    {
                        instance.ReleasePath(vehicleData.m_path);
                    }
                    vehicleData.m_path = path;
                    vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
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
