using ColossalFramework.Math;
using System;
using System.Threading;
using CSL_Traffic.Extensions;

namespace CSL_Traffic
{
	/*
	 * The PathManager is needed to use the CustomPathFind class that is where the real magic happens.
	 * There's some work to do here as I have some old code that isn't used anymore.
	 */
	public class CustomPathManager : PathManager
	{
		CustomPathFind[] m_pathFinds;

		new void Awake()
		{
			PathFind[] originalPathFinds = GetComponents<PathFind>();
			m_pathFinds = new CustomPathFind[originalPathFinds.Length];
			for (int i = 0; i < originalPathFinds.Length; i++)
			{
				// CHECKME: I don't think this needs to be commented anymore. Need to test
				//Destroy(originalPathFinds[i]);
				m_pathFinds[i] = gameObject.AddComponent<CustomPathFind>();
			}
			typeof(PathManager).GetFieldByName("m_pathfinds").SetValue(this, m_pathFinds);
		}

		// copy values from original to new path manager
		public void SetOriginalValues(PathManager originalPathManager)
		{
			// members of SimulationManagerBase
			this.m_simulationProfiler = originalPathManager.m_simulationProfiler;
			this.m_drawCallData = originalPathManager.m_drawCallData;
			this.m_properties = originalPathManager.m_properties;
			
			// members of PathManager
			this.m_pathUnitCount = originalPathManager.m_pathUnitCount;
			this.m_renderPathGizmo = originalPathManager.m_renderPathGizmo;
			this.m_pathUnits = originalPathManager.m_pathUnits;
			this.m_bufferLock = originalPathManager.m_bufferLock;
		}

		public bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPos, PathUnit.Position endPos, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isServiceVehicle)
		{
			PathUnit.Position position = default(PathUnit.Position);
			return this.CreatePath(out unit, ref randomizer, buildIndex, startPos, position, endPos, position, position, laneTypes, vehicleTypes, maxLength, false, false, false, false, isServiceVehicle);
		}

		public bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isServiceVehicle)
		{
			return this.CreatePath(out unit, ref randomizer, buildIndex, startPosA, startPosB, endPosA, endPosB, default(PathUnit.Position), laneTypes, vehicleTypes, maxLength, false, false, false, false, isServiceVehicle);
		}

		public bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, bool skipQueue, bool isServiceVehicle)
		{
			return this.CreatePath(out unit, ref randomizer, buildIndex, startPosA, startPosB, endPosA, endPosB, default(PathUnit.Position), laneTypes, vehicleTypes, maxLength, isHeavyVehicle, ignoreBlocked, stablePath, skipQueue, isServiceVehicle);
		}
		
		public bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, PathUnit.Position vehiclePosition, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, bool skipQueue, bool isServiceVehicle)
		{
			while (!Monitor.TryEnter(this.m_bufferLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
			{
			}
			uint num;
			try
			{
				if (!this.m_pathUnits.CreateItem(out num, ref randomizer))
				{
					unit = 0u;
					bool result = false;
					return result;
				}
				this.m_pathUnitCount = (int)(this.m_pathUnits.ItemCount() - 1u);
			}
			finally
			{
				Monitor.Exit(this.m_bufferLock);
			}
			unit = num;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_simulationFlags = 1;
			if (isServiceVehicle)
			{
				PathUnit[] pathUnits = this.m_pathUnits.m_buffer;
				UIntPtr unitIndex = (UIntPtr)unit;
				pathUnits[(int)unitIndex].m_simulationFlags = (byte)(pathUnits[(int)unitIndex].m_simulationFlags | 8);
			}
			if (isHeavyVehicle)
			{
				PathUnit[] expr_92_cp_0 = this.m_pathUnits.m_buffer;
				UIntPtr expr_92_cp_1 = (UIntPtr)unit;
				expr_92_cp_0[(int)expr_92_cp_1].m_simulationFlags = (byte)(expr_92_cp_0[(int)expr_92_cp_1].m_simulationFlags | 16);
			}
			if (ignoreBlocked)
			{
				PathUnit[] expr_BB_cp_0 = this.m_pathUnits.m_buffer;
				UIntPtr expr_BB_cp_1 = (UIntPtr)unit;
				expr_BB_cp_0[(int)expr_BB_cp_1].m_simulationFlags = (byte)(expr_BB_cp_0[(int)expr_BB_cp_1].m_simulationFlags | 32);
			}
			if (stablePath)
			{
				PathUnit[] expr_E4_cp_0 = this.m_pathUnits.m_buffer;
				UIntPtr expr_E4_cp_1 = (UIntPtr)unit;
				expr_E4_cp_0[(int)expr_E4_cp_1].m_simulationFlags = (byte)(expr_E4_cp_0[(int)expr_E4_cp_1].m_simulationFlags | 64);
			}
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_pathFindFlags = 0;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_buildIndex = buildIndex;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position00 = startPosA;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position01 = endPosA;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position02 = startPosB;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position03 = endPosB;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position11 = vehiclePosition;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_nextPathUnit = 0u;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_laneTypes = (byte)laneTypes;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_vehicleTypes = (byte)vehicleTypes;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_length = maxLength;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_positionCount = 20;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_referenceCount = 1;
			int num2 = 10000000;
			CustomPathFind pathFind = null;
			for (int i = 0; i < this.m_pathFinds.Length; i++)
			{
				CustomPathFind pathFind2 = this.m_pathFinds[i];
				if (pathFind2.IsAvailable && pathFind2.m_queuedPathFindCount < num2)
				{
					num2 = pathFind2.m_queuedPathFindCount;
					pathFind = pathFind2;
				}
			}
			if (pathFind != null && pathFind.CalculatePath(unit, skipQueue))
			{
				return true;
			}
			this.ReleasePath(unit);
			return false;
		}
	} 
}
