using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Threading;
using UnityEngine;

namespace CSL_Traffic
{
	class BusTransportLineAI : TransportLineAI
	{
		public override void InitializePrefab()
		{
			// CHECKME: is this needed?
			this.m_publicTransportAccumulation = 50;
			this.m_netService = ItemClass.Service.Road;

			base.InitializePrefab();

			Logger.LogInfo("" + name + " initialized.");
		}

		public override void SimulationStep(ushort segmentID, ref NetSegment data)
		{
			NetManager instance = Singleton<NetManager>.instance;
			if ((instance.m_nodes.m_buffer[(int)data.m_startNode].m_flags & NetNode.Flags.Temporary) == NetNode.Flags.None)
			{
				if (data.m_path == 0u || (ulong)(Singleton<SimulationManager>.instance.m_currentFrameIndex >> 8 & 15u) == (ulong)((long)(segmentID & 15)))
				{
					BusTransportLineAI.StartPathFind(segmentID, ref data, this.m_netService, this.m_vehicleType, false);
				}
				else
				{
					BusTransportLineAI.UpdatePath(segmentID, ref data, this.m_netService, this.m_vehicleType, false);
				}
			}
		}

		public new static bool StartPathFind(ushort segmentID, ref NetSegment data, ItemClass.Service netService, VehicleInfo.VehicleType vehicleType, bool skipQueue)
		{
			if (data.m_path != 0u)
			{
				Singleton<PathManager>.instance.ReleasePath(data.m_path);
				data.m_path = 0u;
			}
			NetManager instance = Singleton<NetManager>.instance;
			if ((instance.m_nodes.m_buffer[(int)data.m_startNode].m_flags & NetNode.Flags.Ambiguous) != NetNode.Flags.None)
			{
				for (int i = 0; i < 8; i++)
				{
					ushort segment = instance.m_nodes.m_buffer[(int)data.m_startNode].GetSegment(i);
					if (segment != 0 && segment != segmentID && instance.m_segments.m_buffer[(int)segment].m_path != 0u)
					{
						return true;
					}
				}
			}
			if ((instance.m_nodes.m_buffer[(int)data.m_endNode].m_flags & NetNode.Flags.Ambiguous) != NetNode.Flags.None)
			{
				for (int j = 0; j < 8; j++)
				{
					ushort segment2 = instance.m_nodes.m_buffer[(int)data.m_endNode].GetSegment(j);
					if (segment2 != 0 && segment2 != segmentID && instance.m_segments.m_buffer[(int)segment2].m_path != 0u)
					{
						return true;
					}
				}
			}
			Vector3 position = instance.m_nodes.m_buffer[(int)data.m_startNode].m_position;
			Vector3 position2 = instance.m_nodes.m_buffer[(int)data.m_endNode].m_position;
			PathUnit.Position startPosA;
			PathUnit.Position startPosB;
			float num;
			float num2;
			if (!PathManager.FindPathPosition(position, netService, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, true, false, 32f, out startPosA, out startPosB, out num, out num2))
			{
				return true;
			}
			PathUnit.Position endPosA;
			PathUnit.Position endPosB;
			float num3;
			float num4;
			if (!PathManager.FindPathPosition(position2, netService, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, true, false, 32f, out endPosA, out endPosB, out num3, out num4))
			{
				return true;
			}
			if ((instance.m_nodes.m_buffer[(int)data.m_startNode].m_flags & NetNode.Flags.Fixed) != NetNode.Flags.None)
			{
				startPosB = default(PathUnit.Position);
			}
			if ((instance.m_nodes.m_buffer[(int)data.m_endNode].m_flags & NetNode.Flags.Fixed) != NetNode.Flags.None)
			{
				endPosB = default(PathUnit.Position);
			}
			startPosA.m_offset = 128;
			startPosB.m_offset = 128;
			endPosA.m_offset = 128;
			endPosB.m_offset = 128;
			bool stopLane = BusTransportLineAI.GetStopLane(ref startPosA, vehicleType);
			bool stopLane2 = BusTransportLineAI.GetStopLane(ref startPosB, vehicleType);
			bool stopLane3 = BusTransportLineAI.GetStopLane(ref endPosA, vehicleType);
			bool stopLane4 = BusTransportLineAI.GetStopLane(ref endPosB, vehicleType);
			if ((!stopLane && !stopLane2) || (!stopLane3 && !stopLane4))
			{
				return true;
			}
			uint path;
			bool createPathResult;
			CustomPathManager customPathManager = Singleton<PathManager>.instance as CustomPathManager;
			if (customPathManager != null)
				createPathResult = customPathManager.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, vehicleType, 20000f, false, true, true, skipQueue, RoadManager.VehicleType.Bus);
			else
                createPathResult = Singleton<PathManager>.instance.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, vehicleType, 20000f, false, true, true, skipQueue);
			if (createPathResult)
			{
				if (startPosA.m_segment != 0 && startPosB.m_segment != 0)
				{
					NetNode[] expr_2D9_cp_0 = instance.m_nodes.m_buffer;
					ushort expr_2D9_cp_1 = data.m_startNode;
					expr_2D9_cp_0[(int)expr_2D9_cp_1].m_flags = (expr_2D9_cp_0[(int)expr_2D9_cp_1].m_flags | NetNode.Flags.Ambiguous);
				}
				else
				{
					NetNode[] expr_305_cp_0 = instance.m_nodes.m_buffer;
					ushort expr_305_cp_1 = data.m_startNode;
					expr_305_cp_0[(int)expr_305_cp_1].m_flags = (expr_305_cp_0[(int)expr_305_cp_1].m_flags & ~NetNode.Flags.Ambiguous);
				}
				if (endPosA.m_segment != 0 && endPosB.m_segment != 0)
				{
					NetNode[] expr_344_cp_0 = instance.m_nodes.m_buffer;
					ushort expr_344_cp_1 = data.m_endNode;
					expr_344_cp_0[(int)expr_344_cp_1].m_flags = (expr_344_cp_0[(int)expr_344_cp_1].m_flags | NetNode.Flags.Ambiguous);
				}
				else
				{
					NetNode[] expr_370_cp_0 = instance.m_nodes.m_buffer;
					ushort expr_370_cp_1 = data.m_endNode;
					expr_370_cp_0[(int)expr_370_cp_1].m_flags = (expr_370_cp_0[(int)expr_370_cp_1].m_flags & ~NetNode.Flags.Ambiguous);
				}
				data.m_path = path;
				data.m_flags |= NetSegment.Flags.WaitingPath;
				return false;
			}
			return true;
		}

		public new static bool UpdatePath(ushort segmentID, ref NetSegment data, ItemClass.Service netService, VehicleInfo.VehicleType vehicleType, bool skipQueue)
		{
			if (data.m_path == 0u)
			{
				return BusTransportLineAI.StartPathFind(segmentID, ref data, netService, vehicleType, skipQueue);
			}
			if ((data.m_flags & NetSegment.Flags.WaitingPath) == NetSegment.Flags.None)
			{
				return true;
			}
			PathManager instance = Singleton<PathManager>.instance;
			NetManager instance2 = Singleton<NetManager>.instance;
			byte pathFindFlags = instance.m_pathUnits.m_buffer[(int)((UIntPtr)data.m_path)].m_pathFindFlags;
			if ((pathFindFlags & 4) != 0)
			{
				bool flag = false;
				PathUnit.Position pathPos;
				if (instance.m_pathUnits.m_buffer[(int)((UIntPtr)data.m_path)].GetPosition(0, out pathPos))
				{
					flag = TransportLineAI.CheckNodePosition(data.m_startNode, pathPos);
				}
				if (instance.m_pathUnits.m_buffer[(int)((UIntPtr)data.m_path)].GetLastPosition(out pathPos))
				{
					TransportLineAI.CheckNodePosition(data.m_endNode, pathPos);
				}
				float length = instance.m_pathUnits.m_buffer[(int)((UIntPtr)data.m_path)].m_length;
				if (length != data.m_averageLength)
				{
					data.m_averageLength = length;
					ushort transportLine = instance2.m_nodes.m_buffer[(int)data.m_startNode].m_transportLine;
					if (transportLine != 0)
					{
						Singleton<TransportManager>.instance.UpdateLine(transportLine);
					}
				}
				if (data.m_lanes != 0u)
				{
					instance2.m_lanes.m_buffer[(int)((UIntPtr)data.m_lanes)].m_length = data.m_averageLength * ((!flag) ? 1f : 0.75f);
				}
				data.m_flags &= ~NetSegment.Flags.WaitingPath;
				data.m_flags &= ~NetSegment.Flags.PathFailed;
				return true;
			}
			if ((pathFindFlags & 8) != 0)
			{
				if (data.m_averageLength == 0f)
				{
					Vector3 position = instance2.m_nodes.m_buffer[(int)data.m_startNode].m_position;
					Vector3 position2 = instance2.m_nodes.m_buffer[(int)data.m_endNode].m_position;
					data.m_averageLength = Vector3.Distance(position, position2);
				}
				data.m_flags &= ~NetSegment.Flags.WaitingPath;
				data.m_flags |= NetSegment.Flags.PathFailed;
				return true;
			}
			return false;
		}

		private static bool GetStopLane(ref PathUnit.Position pos, VehicleInfo.VehicleType vehicleType)
		{
			if (pos.m_segment != 0)
			{
				NetManager instance = Singleton<NetManager>.instance;
				int num;
				uint num2;
                if (instance.m_segments.m_buffer[(int)pos.m_segment].GetClosestLane((int)pos.m_lane, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, vehicleType, out num, out num2))
				{
					pos.m_lane = (byte)num;
					return true;
				}
			}
			pos = default(PathUnit.Position);
			return false;
		}


		// from TransportLine

		public static bool UpdateMeshData(ref TransportLine transportLine, ushort lineID)
		{
			//return transportLine.UpdateMeshData(lineID);
			bool flag = true;
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			float num4 = 0f;
			TransportManager instance = Singleton<TransportManager>.instance;
			NetManager instance2 = Singleton<NetManager>.instance;
			PathManager instance3 = Singleton<PathManager>.instance;
			ushort stops = transportLine.m_stops;
			ushort num5 = stops;
			int num6 = 0;
			while (num5 != 0)
			{
				ushort num7 = 0;
				for (int i = 0; i < 8; i++)
				{
					ushort segment = instance2.m_nodes.m_buffer[(int)num5].GetSegment(i);
					if (segment != 0 && instance2.m_segments.m_buffer[(int)segment].m_startNode == num5)
					{
						uint path = instance2.m_segments.m_buffer[(int)segment].m_path;
						if (path != 0u)
						{
							byte pathFindFlags = instance3.m_pathUnits.m_buffer[(int)((UIntPtr)path)].m_pathFindFlags;
							if ((pathFindFlags & 4) != 0)
							{
								if (!TransportLine.CalculatePathSegmentCount(path, ref num2, ref num3, ref num4))
								{
									TransportInfo info = transportLine.Info;
									BusTransportLineAI.StartPathFind(segment, ref instance2.m_segments.m_buffer[(int)segment], info.m_netService, info.m_vehicleType, (transportLine.m_flags & TransportLine.Flags.Temporary) != TransportLine.Flags.None);
									flag = false;
								}
							}
							else if ((pathFindFlags & 8) == 0)
							{
								flag = false;
							}
						}
						num7 = instance2.m_segments.m_buffer[(int)segment].m_endNode;
						break;
					}
				}
				num++;
				num2++;
				num5 = num7;
				if (num5 == stops)
				{
					break;
				}
				if (!flag)
				{
					break;
				}
				if (++num6 >= 32768)
				{
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
			if (!flag)
			{
				return flag;
			}
			RenderGroup.MeshData meshData = new RenderGroup.MeshData();
			meshData.m_vertices = new Vector3[num2 * 8];
			meshData.m_normals = new Vector3[num2 * 8];
			meshData.m_tangents = new Vector4[num2 * 8];
			meshData.m_uvs = new Vector2[num2 * 8];
			meshData.m_uvs2 = new Vector2[num2 * 8];
			meshData.m_colors = new Color32[num2 * 8];
			meshData.m_triangles = new int[num2 * 30];
			TransportManager.LineSegment[] array = new TransportManager.LineSegment[num];
			Bezier3[] array2 = new Bezier3[num3];
			int num8 = 0;
			int num9 = 0;
			int num10 = 0;
			float lengthScale = Mathf.Ceil(num4 / 64f) / num4;
			float num11 = 0f;
			num5 = stops;
			Vector3 vector = new Vector3(100000f, 100000f, 100000f);
			Vector3 vector2 = new Vector3(-100000f, -100000f, -100000f);
			num6 = 0;
			while (num5 != 0)
			{
				ushort num12 = 0;
				for (int j = 0; j < 8; j++)
				{
					ushort segment2 = instance2.m_nodes.m_buffer[(int)num5].GetSegment(j);
					if (segment2 != 0 && instance2.m_segments.m_buffer[(int)segment2].m_startNode == num5)
					{
						uint path2 = instance2.m_segments.m_buffer[(int)segment2].m_path;
						if (path2 != 0u && (instance3.m_pathUnits.m_buffer[(int)((UIntPtr)path2)].m_pathFindFlags & 4) != 0)
						{
							array[num8].m_curveStart = num10;
							Vector3 vector3;
							Vector3 vector4;
							TransportLine.FillPathSegments(path2, meshData, array2, ref num9, ref num10, ref num11, lengthScale, out vector3, out vector4);
							vector = Vector3.Min(vector, vector3);
							vector2 = Vector3.Max(vector2, vector4);
							array[num8].m_bounds.SetMinMax(vector3, vector4);
							array[num8].m_curveEnd = num10;
						}
						num12 = instance2.m_segments.m_buffer[(int)segment2].m_endNode;
						break;
					}
				}
				TransportLine.FillPathNode(instance2.m_nodes.m_buffer[(int)num5].m_position, meshData, num9);
				num8++;
				num9++;
				num5 = num12;
				if (num5 == stops)
				{
					break;
				}
				if (++num6 >= 32768)
				{
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
			while (!Monitor.TryEnter(instance.m_lineMeshData, SimulationManager.SYNCHRONIZE_TIMEOUT))
			{
			}
			try
			{
				instance.m_lineMeshData[(int)lineID] = meshData;
				instance.m_lineSegments[(int)lineID] = array;
				instance.m_lineCurves[(int)lineID] = array2;
				transportLine.m_bounds.SetMinMax(vector, vector2);
			}
			finally
			{
				Monitor.Exit(instance.m_lineMeshData);
			}

			return flag;
		}

		public static bool UpdatePaths(ref TransportLine transportLine, ushort lineID)
		{
			bool flag = true;
			NetManager instance = Singleton<NetManager>.instance;
			ushort stops = transportLine.m_stops;
			ushort num = stops;
			int num2 = 0;
			while (num != 0)
			{
				ushort num3 = 0;
				for (int i = 0; i < 8; i++)
				{
					ushort segment = instance.m_nodes.m_buffer[(int)num].GetSegment(i);
					if (segment != 0 && instance.m_segments.m_buffer[(int)segment].m_startNode == num)
					{
						TransportInfo info = transportLine.Info;
						if (!BusTransportLineAI.UpdatePath(segment, ref instance.m_segments.m_buffer[(int)segment], info.m_netService, info.m_vehicleType, (transportLine.m_flags & TransportLine.Flags.Temporary) != TransportLine.Flags.None))
						{
							flag = false;
						}
						num3 = instance.m_segments.m_buffer[(int)segment].m_endNode;
						break;
					}
				}
				num = num3;
				if (num == stops)
				{
					break;
				}
				if (!flag)
				{
					break;
				}
				if (++num2 >= 32768)
				{
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
			return flag;
		}
	}
}
