using ColossalFramework;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CSL_Traffic
{
	class CustomBusAI : BusAI, IVehicle
	{
		public static bool sm_initialized;

		public static void Initialize(VehicleCollection collection, Transform customPrefabs)
		{
			if (sm_initialized)
				return;
			VehicleInfo originalBus = collection.m_prefabs.Where(p => p.name.Contains("Bus")).FirstOrDefault();
			if (originalBus == null)
				throw new KeyNotFoundException("Bus was not found on " + collection.name);

			GameObject instance = GameObject.Instantiate<GameObject>(originalBus.gameObject);
			instance.name = "Bus";
			instance.transform.SetParent(customPrefabs);
			
            BusAI busAI = instance.GetComponent<BusAI>();
            TransportInfo transportInfo = busAI.m_transportInfo;
            
			GameObject.Destroy(instance.GetComponent<BusAI>());
			CustomBusAI customBusAI = instance.AddComponent<CustomBusAI>();
            customBusAI.m_transportInfo = transportInfo;

			VehicleInfo bus = instance.GetComponent<VehicleInfo>();
			bus.m_prefabInitialized = false;
			bus.m_vehicleAI = null;

			MethodInfo initMethod = typeof(VehicleCollection).GetMethod("InitializePrefabs", BindingFlags.Static | BindingFlags.NonPublic);
			Singleton<LoadingManager>.instance.QueueLoadingAction((IEnumerator)initMethod.Invoke(null, new object[] { collection.name, new[] { bus }, new string[] { "Bus" } }));

			sm_initialized = true;
		}

		public override void InitializeAI()
		{
			base.InitializeAI();            

#if DEBUG
			System.IO.File.AppendAllText("Debug.txt", "Initializing Bus AI.\n");
#endif
		}

        public override void SimulationStep(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics)
        {
            if ((vehicleData.m_flags & Vehicle.Flags.Stopped) != Vehicle.Flags.None)
            {
                vehicleData.m_waitCounter += 1;
                if (this.CanLeave(vehicleID, ref vehicleData))
                {
                    vehicleData.m_flags &= ~Vehicle.Flags.Stopped;
                    vehicleData.m_flags |= Vehicle.Flags.Leaving;
                    vehicleData.m_waitCounter = 0;
                }
            }
            CustomCarAI.SimulationStep(this, vehicleID, ref vehicleData, ref frameData, leaderID, ref leaderData, lodPhysics);
            if ((vehicleData.m_flags & Vehicle.Flags.GoingBack) == Vehicle.Flags.None && this.ShouldReturnToSource(vehicleID, ref vehicleData))
            {
                this.SetTransportLine(vehicleID, ref vehicleData, 0);
            }
        }

        protected override bool StartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays)
        {
            VehicleInfo info = this.m_info;
            NetInfo.LaneType laneTypes = NetInfo.LaneType.Vehicle | ((NetInfo.LaneType)((byte)32)) | ((NetInfo.LaneType)((byte)64));
            PathUnit.Position startPosA;
            PathUnit.Position startPosB;
            float num;
            float num2;
            PathUnit.Position endPosA;
            PathUnit.Position endPosB;
            float num3;
            float num4;
            if (CustomPathManager.FindPathPosition(startPos, ItemClass.Service.Road, laneTypes, info.m_vehicleType, 32f, out startPosA, out startPosB, out num, out num2) && CustomPathManager.FindPathPosition(endPos, ItemClass.Service.Road, laneTypes, info.m_vehicleType, 32f, out endPosA, out endPosB, out num3, out num4))
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
                if ((Singleton<PathManager>.instance as CustomPathManager).CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, laneTypes, info.m_vehicleType, 20000f, this.IsHeavyVehicle(), this.IgnoreBlocked(vehicleID, ref vehicleData), false, false, true))
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
