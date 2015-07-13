using ColossalFramework;
using ColossalFramework.Math;
using CSL_Traffic.Extensions;
using System;
using System.Threading;
using UnityEngine;

namespace CSL_Traffic
{
	/*
	 * The PathManager is needed to use the CustomPathFind class that is where the real magic happens.
	 * There's some work to do here as I have some old code that isn't used anymore.
	 */
	public class CustomPathManager : PathManager
	{
		CustomPathFind[] m_pathFinds;

		protected override void Awake()
		{
			PathFind[] originalPathFinds = GetComponents<PathFind>();
			m_pathFinds = new CustomPathFind[originalPathFinds.Length];
			for (int i = 0; i < originalPathFinds.Length; i++)
			{
				Destroy(originalPathFinds[i]);
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

		public bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPos, PathUnit.Position endPos, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, RoadManager.VehicleType vehicleType)
		{
			PathUnit.Position position = default(PathUnit.Position);
			return this.CreatePath(out unit, ref randomizer, buildIndex, startPos, position, endPos, position, position, laneTypes, vehicleTypes, maxLength, false, false, false, false, vehicleType);
		}

		public bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, RoadManager.VehicleType vehicleType)
		{
			return this.CreatePath(out unit, ref randomizer, buildIndex, startPosA, startPosB, endPosA, endPosB, default(PathUnit.Position), laneTypes, vehicleTypes, maxLength, false, false, false, false, vehicleType);
		}

		public bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, bool skipQueue, RoadManager.VehicleType vehicleType)
		{
			return this.CreatePath(out unit, ref randomizer, buildIndex, startPosA, startPosB, endPosA, endPosB, default(PathUnit.Position), laneTypes, vehicleTypes, maxLength, isHeavyVehicle, ignoreBlocked, stablePath, skipQueue, vehicleType);
		}

		public bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, PathUnit.Position vehiclePosition, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, bool skipQueue, RoadManager.VehicleType vehicleType)
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
			if (pathFind != null && pathFind.CalculatePath(unit, skipQueue, vehicleType))
			{
				return true;
			}
			this.ReleasePath(unit);
			return false;
		}

        public static bool FindPathPosition(Vector3 position, ItemClass.Service service, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleTypes, bool allowUnderground, bool requireConnect, float maxDistance, out PathUnit.Position pathPos, RoadManager.VehicleType vehicleType)
		{
			PathUnit.Position position2;
			float num;
			float num2;
			return CustomPathManager.FindPathPosition(position, service, laneType, vehicleTypes, allowUnderground, requireConnect, maxDistance, out pathPos, out position2, out num, out num2, vehicleType);
		}

		public static bool FindPathPosition(Vector3 position, ItemClass.Service service, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleTypes, bool allowUnderground, bool requireConnect, float maxDistance, out PathUnit.Position pathPosA, out PathUnit.Position pathPosB, out float distanceSqrA, out float distanceSqrB, RoadManager.VehicleType vehicleType)
		{
			Bounds bounds = new Bounds(position, new Vector3(maxDistance * 2f, maxDistance * 2f, maxDistance * 2f));
			int num = Mathf.Max((int)((bounds.min.x - 64f) / 64f + 135f), 0);
			int num2 = Mathf.Max((int)((bounds.min.z - 64f) / 64f + 135f), 0);
			int num3 = Mathf.Min((int)((bounds.max.x + 64f) / 64f + 135f), 269);
			int num4 = Mathf.Min((int)((bounds.max.z + 64f) / 64f + 135f), 269);
			NetManager instance = Singleton<NetManager>.instance;
			pathPosA.m_segment = 0;
			pathPosA.m_lane = 0;
			pathPosA.m_offset = 0;
			distanceSqrA = 1E+10f;
			pathPosB.m_segment = 0;
			pathPosB.m_lane = 0;
			pathPosB.m_offset = 0;
			distanceSqrB = 1E+10f;
			float num5 = maxDistance * maxDistance;
			for (int i = num2; i <= num4; i++)
			{
				for (int j = num; j <= num3; j++)
				{
					ushort num6 = instance.m_segmentGrid[i * 270 + j];
					int num7 = 0;
					while (num6 != 0)
					{
						NetInfo info = instance.m_segments.m_buffer[(int)num6].Info;
						if (info.m_class.m_service == service && (instance.m_segments.m_buffer[(int)num6].m_flags & NetSegment.Flags.Flooded) == NetSegment.Flags.None && (allowUnderground || !info.m_netAI.IsUnderground()))
						{
							ushort startNode = instance.m_segments.m_buffer[(int)num6].m_startNode;
							ushort endNode = instance.m_segments.m_buffer[(int)num6].m_endNode;
							Vector3 position2 = instance.m_nodes.m_buffer[(int)startNode].m_position;
							Vector3 position3 = instance.m_nodes.m_buffer[(int)endNode].m_position;
							float num8 = Mathf.Max(Mathf.Max(bounds.min.x - 64f - position2.x, bounds.min.z - 64f - position2.z), Mathf.Max(position2.x - bounds.max.x - 64f, position2.z - bounds.max.z - 64f));
							float num9 = Mathf.Max(Mathf.Max(bounds.min.x - 64f - position3.x, bounds.min.z - 64f - position3.z), Mathf.Max(position3.x - bounds.max.x - 64f, position3.z - bounds.max.z - 64f));
							Vector3 b;
							int num10;
							float num11;
							Vector3 b2;
							int num12;
							float num13;
							if ((num8 < 0f || num9 < 0f) && instance.m_segments.m_buffer[(int)num6].m_bounds.Intersects(bounds) && CustomPathManager.GetClosestLanePosition(instance.m_segments.m_buffer[(int)num6], position, laneType, vehicleTypes, requireConnect, out b, out num10, out num11, out b2, out num12, out num13, vehicleType))
							{
								float num14 = Vector3.SqrMagnitude(position - b);
								if (num14 < num5)
								{
									num5 = num14;
									pathPosA.m_segment = num6;
									pathPosA.m_lane = (byte)num10;
									pathPosA.m_offset = (byte)Mathf.Clamp(Mathf.RoundToInt(num11 * 255f), 0, 255);
									distanceSqrA = num14;
									num14 = Vector3.SqrMagnitude(position - b2);
									if (num12 == -1 || num14 >= maxDistance * maxDistance)
									{
										pathPosB.m_segment = 0;
										pathPosB.m_lane = 0;
										pathPosB.m_offset = 0;
										distanceSqrB = 1E+10f;
									}
									else
									{
										pathPosB.m_segment = num6;
										pathPosB.m_lane = (byte)num12;
										pathPosB.m_offset = (byte)Mathf.Clamp(Mathf.RoundToInt(num13 * 255f), 0, 255);
										distanceSqrB = num14;
									}
								}
							}
						}
						num6 = instance.m_segments.m_buffer[(int)num6].m_nextGridSegment;
						if (++num7 >= 32768)
						{
							CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
							break;
						}
					}
				}
			}
			return pathPosA.m_segment != 0;
		}

		// NetSegment.GetClosestLane -- it's only called by the PathManager
		public static bool GetClosestLanePosition(NetSegment seg, Vector3 point, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, bool requireConnect, out Vector3 positionA, out int laneIndexA, out float laneOffsetA, out Vector3 positionB, out int laneIndexB, out float laneOffsetB, RoadManager.VehicleType vehicleType)
		{
			positionA = point;
			laneIndexA = -1;
			laneOffsetA = 0f;
			positionB = point;
			laneIndexB = -1;
			laneOffsetB = 0f;
			if (seg.m_flags != NetSegment.Flags.None && seg.m_lanes != 0u)
			{
				NetInfo info = seg.Info;
				if (info.m_lanes != null)
				{
					float num = 1E+09f;
					float num2 = 1E+09f;
					uint num3 = seg.m_lanes;
					int num4 = 0;
					while (num4 < info.m_lanes.Length && num3 != 0u)
					{
						NetInfo.Lane lane = info.m_lanes[num4];
                        if (lane.CheckType(laneTypes, vehicleTypes) && (lane.m_allowConnect || !requireConnect) && RoadManager.CanUseLane(vehicleType, num3))
						{
							Vector3 vector;
							float num5;
							Singleton<NetManager>.instance.m_lanes.m_buffer[(int)((UIntPtr)num3)].GetClosestPosition(point, out vector, out num5);
							float num6 = Vector3.SqrMagnitude(point - vector);
							if (lane.m_finalDirection == NetInfo.Direction.Backward || lane.m_finalDirection == NetInfo.Direction.AvoidForward)
							{
								if (num6 < num2)
								{
									num2 = num6;
									positionB = vector;
									laneIndexB = num4;
									laneOffsetB = num5;
								}
							}
							else if (num6 < num)
							{
								num = num6;
								positionA = vector;
								laneIndexA = num4;
								laneOffsetA = num5;
							}
						}
						num3 = Singleton<NetManager>.instance.m_lanes.m_buffer[(int)((UIntPtr)num3)].m_nextLane;
						num4++;
					}
					if (num2 < num)
					{
						Vector3 vector2 = positionA;
						int num7 = laneIndexA;
						float num8 = laneOffsetA;
						positionA = positionB;
						laneIndexA = laneIndexB;
						laneOffsetA = laneOffsetB;
						positionB = vector2;
						laneIndexB = num7;
						laneOffsetB = num8;
					}
					if (!info.m_canCrossLanes)
					{
						positionB = point;
						laneIndexB = -1;
						laneOffsetB = 0f;
					}
				}
			}
			return laneIndexA != -1;
		}
	}
}
