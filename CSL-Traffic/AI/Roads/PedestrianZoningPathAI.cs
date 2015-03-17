using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CSL_Traffic.Extensions;
using System.Reflection;
using System.Collections;

namespace CSL_Traffic
{
	/*
	 * Self explanatory. Most of the code (if not all, can't really remember if I changed anything) was taken
	 * from RoadAI.
	 */
	class PedestrianZoningPathAI : PedestrianPathAI
	{
		static bool sm_initialized;

		public bool m_enableZoning = true;

		public static void Initialize(NetCollection collection, Transform customPrefabs)
		{
			if (sm_initialized)
				return;

			NetInfo originalPedestrianPath = collection.m_prefabs.Where(p => p.name == "Pedestrian Pavement").FirstOrDefault();
			if (originalPedestrianPath == null)
				throw new KeyNotFoundException("Pedestrian Pavement was not found on " + collection.name);

			GameObject instance = GameObject.Instantiate<GameObject>(originalPedestrianPath.gameObject); ;
			instance.name = "Zonable Pedestrian Pavement";
			instance.transform.SetParent(customPrefabs);
			GameObject.Destroy(instance.GetComponent<PedestrianPathAI>());
			instance.AddComponent<PedestrianZoningPathAI>();

			NetInfo zonablePedestrianPath = instance.GetComponent<NetInfo>();
			zonablePedestrianPath.m_prefabInitialized = false;
			zonablePedestrianPath.m_netAI = null;
			zonablePedestrianPath.m_flattenTerrain = true;
			zonablePedestrianPath.m_halfWidth = 4f;
			zonablePedestrianPath.m_autoRemove = false;
			zonablePedestrianPath.m_flatJunctions = true;
			zonablePedestrianPath.m_surfaceLevel = 0f;
			zonablePedestrianPath.m_class = ScriptableObject.CreateInstance<ItemClass>();
			zonablePedestrianPath.m_class.m_service = ItemClass.Service.Road;
			zonablePedestrianPath.m_class.m_subService = ItemClass.SubService.None;
			zonablePedestrianPath.m_class.m_level = ItemClass.Level.Level1;
			typeof(NetInfo).GetFieldByName("m_UICategory").SetValue(zonablePedestrianPath, "RoadsSmall");

			// Pedestrian lane
			NetInfo.Lane[] lanes = new NetInfo.Lane[3];
			lanes[0] = zonablePedestrianPath.m_lanes[0];
			lanes[0].m_width = 6f;
			PropInfo lampProp = lanes[0].m_laneProps.m_props[0].m_prop;
			lanes[0].m_laneProps = ScriptableObject.CreateInstance<NetLaneProps>();
			lanes[0].m_laneProps.m_props = new NetLaneProps.Prop[1];
			lanes[0].m_laneProps.m_props[0] = new NetLaneProps.Prop() { m_prop = lampProp, m_position = new Vector3(-4f, 0f, 0f), m_repeatDistance = 30f };

			// Backward Lane
			lanes[1] = new NetInfo.Lane();
			lanes[1].m_position = -1.5f;
			lanes[1].m_width = 3f;
			lanes[1].m_verticalOffset = 0f;
			lanes[1].m_stopOffset = 0.1f;
			lanes[1].m_speedLimit = 0.3f;
			lanes[1].m_direction = NetInfo.Direction.Backward;
			lanes[1].m_laneType = (NetInfo.LaneType)((byte)32);
			lanes[1].m_vehicleType = VehicleInfo.VehicleType.Car;
			lanes[1].m_laneProps = ScriptableObject.CreateInstance<NetLaneProps>();
			lanes[1].m_allowStop = true;
			lanes[1].m_useTerrainHeight = false;

			// Forward Lane
			lanes[2] = new NetInfo.Lane();
			lanes[2].m_position = 1.5f;
			lanes[2].m_width = 3f;
			lanes[2].m_verticalOffset = 0f;
			lanes[2].m_stopOffset = 0.1f;
			lanes[2].m_speedLimit = 0.3f;
			lanes[2].m_direction = NetInfo.Direction.Forward;
			lanes[2].m_laneType = (NetInfo.LaneType)((byte)32);
			lanes[2].m_vehicleType = VehicleInfo.VehicleType.Car;
			lanes[2].m_laneProps = ScriptableObject.CreateInstance<NetLaneProps>();
			lanes[2].m_allowStop = true;
			lanes[2].m_useTerrainHeight = false;

			zonablePedestrianPath.m_lanes = lanes;

			MethodInfo initMethod = typeof(NetCollection).GetMethod("InitializePrefabs", BindingFlags.Static | BindingFlags.NonPublic);
			Singleton<LoadingManager>.instance.QueueLoadingAction((IEnumerator)initMethod.Invoke(null, new object[] { collection.name, new[] { zonablePedestrianPath }, new string[] { } }));
			
			sm_initialized = true;
		}

		public override void GetEffectRadius(out float radius, out bool capped, out UnityEngine.Color color)
		{
			if (this.m_enableZoning)
			{
				radius = Mathf.Max(8f, this.m_info.m_halfWidth) + 32f;
				capped = true;
				if (Singleton<InfoManager>.instance.CurrentMode != InfoManager.InfoMode.None)
				{
					color = Singleton<ToolManager>.instance.m_properties.m_validColorInfo;
					color.a *= 0.5f;
				}
				else
				{
					color = Singleton<ToolManager>.instance.m_properties.m_validColor;
					color.a *= 0.5f;
				}
			}
			else
			{
				radius = 0f;
				capped = false;
				color = new Color(0f, 0f, 0f, 0f);
			}
		}

		public override void CreateSegment(ushort segmentID, ref NetSegment data)
		{
			base.CreateSegment(segmentID, ref data);
			if (this.m_enableZoning)
			{
				this.CreateZoneBlocks(segmentID, ref data);
			}
		}

		public override float GetLengthSnap()
		{
			return (!this.m_enableZoning) ? 0f : 8f;
		}

		private void CreateZoneBlocks(ushort segment, ref NetSegment data)
		{
			NetManager instance = Singleton<NetManager>.instance;
			Randomizer randomizer = new Randomizer((int)segment);
			Vector3 position = instance.m_nodes.m_buffer[(int)data.m_startNode].m_position;
			Vector3 position2 = instance.m_nodes.m_buffer[(int)data.m_endNode].m_position;
			Vector3 startDirection = data.m_startDirection;
			Vector3 endDirection = data.m_endDirection;
			float num = startDirection.x * endDirection.x + startDirection.z * endDirection.z;
			bool flag = !NetSegment.IsStraight(position, startDirection, position2, endDirection);
			float num2 = Mathf.Max(8f, this.m_info.m_halfWidth);
			float num3 = 32f;
			if (flag)
			{
				float num4 = VectorUtils.LengthXZ(position2 - position);
				bool flag2 = startDirection.x * endDirection.z - startDirection.z * endDirection.x > 0f;
				bool flag3 = num < -0.8f || num4 > 50f;
				if (flag2)
				{
					num2 = -num2;
					num3 = -num3;
				}
				Vector3 vector = position - new Vector3(startDirection.z, 0f, -startDirection.x) * num2;
				Vector3 vector2 = position2 + new Vector3(endDirection.z, 0f, -endDirection.x) * num2;
				Vector3 vector3;
				Vector3 vector4;
				NetSegment.CalculateMiddlePoints(vector, startDirection, vector2, endDirection, true, true, out vector3, out vector4);
				if (flag3)
				{
					float num5 = num * 0.025f + 0.04f;
					float num6 = num * 0.025f + 0.06f;
					if (num < -0.9f)
					{
						num6 = num5;
					}
					Bezier3 bezier = new Bezier3(vector, vector3, vector4, vector2);
					vector = bezier.Position(num5);
					vector3 = bezier.Position(0.5f - num6);
					vector4 = bezier.Position(0.5f + num6);
					vector2 = bezier.Position(1f - num5);
				}
				else
				{
					Bezier3 bezier2 = new Bezier3(vector, vector3, vector4, vector2);
					vector3 = bezier2.Position(0.86f);
					vector = bezier2.Position(0.14f);
				}
				float num7;
				Vector3 vector5 = VectorUtils.NormalizeXZ(vector3 - vector, out num7);
				int num8 = Mathf.FloorToInt(num7 / 8f + 0.01f);
				float num9 = num7 * 0.5f + (float)(num8 - 8) * ((!flag2) ? -4f : 4f);
				if (num8 != 0)
				{
					float angle = (!flag2) ? Mathf.Atan2(vector5.x, -vector5.z) : Mathf.Atan2(-vector5.x, vector5.z);
					Vector3 position3 = vector + new Vector3(vector5.x * num9 - vector5.z * num3, 0f, vector5.z * num9 + vector5.x * num3);
					if (flag2)
					{
						Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockStartRight, ref randomizer, position3, angle, num8, data.m_buildIndex);
					}
					else
					{
						Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockStartLeft, ref randomizer, position3, angle, num8, data.m_buildIndex);
					}
				}
				if (flag3)
				{
					vector5 = VectorUtils.NormalizeXZ(vector2 - vector4, out num7);
					num8 = Mathf.FloorToInt(num7 / 8f + 0.01f);
					num9 = num7 * 0.5f + (float)(num8 - 8) * ((!flag2) ? -4f : 4f);
					if (num8 != 0)
					{
						float angle2 = (!flag2) ? Mathf.Atan2(vector5.x, -vector5.z) : Mathf.Atan2(-vector5.x, vector5.z);
						Vector3 position4 = vector4 + new Vector3(vector5.x * num9 - vector5.z * num3, 0f, vector5.z * num9 + vector5.x * num3);
						if (flag2)
						{
							Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndRight, ref randomizer, position4, angle2, num8, data.m_buildIndex + 1u);
						}
						else
						{
							Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndLeft, ref randomizer, position4, angle2, num8, data.m_buildIndex + 1u);
						}
					}
				}
				Vector3 vector6 = position + new Vector3(startDirection.z, 0f, -startDirection.x) * num2;
				Vector3 vector7 = position2 - new Vector3(endDirection.z, 0f, -endDirection.x) * num2;
				Vector3 b;
				Vector3 c;
				NetSegment.CalculateMiddlePoints(vector6, startDirection, vector7, endDirection, true, true, out b, out c);
				Bezier3 bezier3 = new Bezier3(vector6, b, c, vector7);
				Vector3 vector8 = bezier3.Position(0.5f);
				Vector3 vector9 = bezier3.Position(0.25f);
				vector9 = Line2.Offset(VectorUtils.XZ(vector6), VectorUtils.XZ(vector8), VectorUtils.XZ(vector9));
				Vector3 vector10 = bezier3.Position(0.75f);
				vector10 = Line2.Offset(VectorUtils.XZ(vector7), VectorUtils.XZ(vector8), VectorUtils.XZ(vector10));
				Vector3 vector11 = vector6;
				Vector3 a = vector7;
				float d;
				float num10;
				if (Line2.Intersect(VectorUtils.XZ(position), VectorUtils.XZ(vector6), VectorUtils.XZ(vector11 - vector9), VectorUtils.XZ(vector8 - vector9), out d, out num10))
				{
					vector6 = position + (vector6 - position) * d;
				}
				if (Line2.Intersect(VectorUtils.XZ(position2), VectorUtils.XZ(vector7), VectorUtils.XZ(a - vector10), VectorUtils.XZ(vector8 - vector10), out d, out num10))
				{
					vector7 = position2 + (vector7 - position2) * d;
				}
				if (Line2.Intersect(VectorUtils.XZ(vector11 - vector9), VectorUtils.XZ(vector8 - vector9), VectorUtils.XZ(a - vector10), VectorUtils.XZ(vector8 - vector10), out d, out num10))
				{
					vector8 = vector11 - vector9 + (vector8 - vector11) * d;
				}
				float num11;
				Vector3 vector12 = VectorUtils.NormalizeXZ(vector8 - vector6, out num11);
				int num12 = Mathf.FloorToInt(num11 / 8f + 0.01f);
				float num13 = num11 * 0.5f + (float)(num12 - 8) * ((!flag2) ? 4f : -4f);
				if (num12 != 0)
				{
					float angle3 = (!flag2) ? Mathf.Atan2(-vector12.x, vector12.z) : Mathf.Atan2(vector12.x, -vector12.z);
					Vector3 position5 = vector6 + new Vector3(vector12.x * num13 + vector12.z * num3, 0f, vector12.z * num13 - vector12.x * num3);
					if (flag2)
					{
						Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockStartLeft, ref randomizer, position5, angle3, num12, data.m_buildIndex);
					}
					else
					{
						Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockStartRight, ref randomizer, position5, angle3, num12, data.m_buildIndex);
					}
				}
				vector12 = VectorUtils.NormalizeXZ(vector7 - vector8, out num11);
				num12 = Mathf.FloorToInt(num11 / 8f + 0.01f);
				num13 = num11 * 0.5f + (float)(num12 - 8) * ((!flag2) ? 4f : -4f);
				if (num12 != 0)
				{
					float angle4 = (!flag2) ? Mathf.Atan2(-vector12.x, vector12.z) : Mathf.Atan2(vector12.x, -vector12.z);
					Vector3 position6 = vector8 + new Vector3(vector12.x * num13 + vector12.z * num3, 0f, vector12.z * num13 - vector12.x * num3);
					if (flag2)
					{
						Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndLeft, ref randomizer, position6, angle4, num12, data.m_buildIndex + 1u);
					}
					else
					{
						Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndRight, ref randomizer, position6, angle4, num12, data.m_buildIndex + 1u);
					}
				}
			}
			else
			{
				num2 += num3;
				Vector2 vector13 = new Vector2(position2.x - position.x, position2.z - position.z);
				float magnitude = vector13.magnitude;
				int num14 = Mathf.FloorToInt(magnitude / 8f + 0.1f);
				int num15 = (num14 <= 8) ? num14 : (num14 + 1 >> 1);
				int num16 = (num14 <= 8) ? 0 : (num14 >> 1);
				if (num15 > 0)
				{
					float num17 = Mathf.Atan2(startDirection.x, -startDirection.z);
					Vector3 position7 = position + new Vector3(startDirection.x * 32f - startDirection.z * num2, 0f, startDirection.z * 32f + startDirection.x * num2);
					Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockStartLeft, ref randomizer, position7, num17, num15, data.m_buildIndex);
					position7 = position + new Vector3(startDirection.x * (float)(num15 - 4) * 8f + startDirection.z * num2, 0f, startDirection.z * (float)(num15 - 4) * 8f - startDirection.x * num2);
					Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockStartRight, ref randomizer, position7, num17 + 3.14159274f, num15, data.m_buildIndex);
				}
				if (num16 > 0)
				{
					float num18 = magnitude - (float)num14 * 8f;
					float num19 = Mathf.Atan2(endDirection.x, -endDirection.z);
					Vector3 position8 = position2 + new Vector3(endDirection.x * (32f + num18) - endDirection.z * num2, 0f, endDirection.z * (32f + num18) + endDirection.x * num2);
					Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndLeft, ref randomizer, position8, num19, num16, data.m_buildIndex + 1u);
					position8 = position2 + new Vector3(endDirection.x * ((float)(num16 - 4) * 8f + num18) + endDirection.z * num2, 0f, endDirection.z * ((float)(num16 - 4) * 8f + num18) - endDirection.x * num2);
					Singleton<ZoneManager>.instance.CreateBlock(out data.m_blockEndRight, ref randomizer, position8, num19 + 3.14159274f, num16, data.m_buildIndex + 1u);
				}
			}
		}

		public override ToolBase.ToolErrors CheckBuildPosition(bool test, bool visualize, bool overlay, bool autofix, ref NetTool.ControlPoint startPoint, ref NetTool.ControlPoint middlePoint, ref NetTool.ControlPoint endPoint, out BuildingInfo ownerBuilding, out Vector3 ownerPosition, out Vector3 ownerDirection, out int productionRate)
		{
			ToolBase.ToolErrors toolErrors = base.CheckBuildPosition(test, visualize, overlay, autofix, ref startPoint, ref middlePoint, ref endPoint, out ownerBuilding, out ownerPosition, out ownerDirection, out productionRate);
			if (test)
			{
				if (this.m_enableZoning && !Singleton<ZoneManager>.instance.CheckLimits())
				{
					toolErrors |= ToolBase.ToolErrors.TooManyObjects;
				}
			}
			return toolErrors;
		}

		public override NetInfo GetInfo(float elevation, float length, bool incoming, bool outgoing, bool curved, bool enableDouble, ref ToolBase.ToolErrors errors)
		{
			if (incoming || outgoing)
			{
				int num;
				int num2;
				Singleton<BuildingManager>.instance.CalculateOutsideConnectionCount(this.m_info.m_class.m_service, this.m_info.m_class.m_subService, out num, out num2);
				if ((incoming && num >= 4) || (outgoing && num2 >= 4))
				{
					errors |= ToolBase.ToolErrors.TooManyConnections;
				}
			}
			if (this.m_invisible)
			{
				return this.m_info;
			}
			if (elevation > 255f)
			{
				errors |= ToolBase.ToolErrors.HeightTooHigh;
			}
			if (this.m_bridgeInfo != null && elevation > 25f && length > 45f && !curved && (enableDouble || !this.m_bridgeInfo.m_netAI.RequireDoubleSegments()))
			{
				return this.m_bridgeInfo;
			}
			if (this.m_elevatedInfo != null && elevation > 2f)
			{
				return this.m_elevatedInfo;
			}
			if (elevation > 4f)
			{
				errors |= ToolBase.ToolErrors.HeightTooHigh;
			}
			return this.m_info;
		}

		public override void UpdateLaneConnection(ushort nodeID, ref NetNode data)
		{
			uint num = 0u;
			byte offset = 0;
			float num2 = 1E+10f;
			if ((data.m_flags & NetNode.Flags.ForbidLaneConnection) == NetNode.Flags.None)
			{
				float maxDistance = 8f;
				if ((data.m_flags & NetNode.Flags.End) != NetNode.Flags.None)
				{
					maxDistance = 16f;
				}
				PathUnit.Position pathPos;
				PathUnit.Position position;
				float num3;
				float num4;
				if (this.m_connectService1 != ItemClass.Service.None && PathManager.FindPathPosition(data.m_position, this.m_connectService1, (NetInfo.LaneType)((byte)32), VehicleInfo.VehicleType.None | VehicleInfo.VehicleType.Car, maxDistance, out pathPos, out position, out num3, out num4) && num3 < num2)
				{
					num2 = num3;
					num = PathManager.GetLaneID(pathPos);
					offset = pathPos.m_offset;
				}
				PathUnit.Position pathPos2;
				PathUnit.Position position2;
				float num5;
				float num6;
				if (this.m_connectService2 != ItemClass.Service.None && PathManager.FindPathPosition(data.m_position, this.m_connectService2, (NetInfo.LaneType)((byte)32), VehicleInfo.VehicleType.None | VehicleInfo.VehicleType.Car, maxDistance, out pathPos2, out position2, out num5, out num6) && num5 < num2 && (this.m_canConnectTunnel || !Singleton<NetManager>.instance.m_segments.m_buffer[(int)pathPos2.m_segment].Info.m_netAI.IsUnderground()))
				{
					num = PathManager.GetLaneID(pathPos2);
					offset = pathPos2.m_offset;
				}
			}
			if (num != data.m_lane)
			{
				if (data.m_lane != 0u)
				{
					this.RemoveLaneConnection(nodeID, ref data);
				}
				if (num != 0u)
				{
					this.AddLaneConnection(nodeID, ref data, num, offset);
				}
			}
		}

		private void AddLaneConnection(ushort nodeID, ref NetNode data, uint laneID, byte offset)
		{
			NetManager instance = Singleton<NetManager>.instance;
			data.m_lane = laneID;
			data.m_laneOffset = offset;
			data.m_nextLaneNode = instance.m_lanes.m_buffer[(int)((UIntPtr)data.m_lane)].m_nodes;
			instance.m_lanes.m_buffer[(int)((UIntPtr)data.m_lane)].m_nodes = nodeID;
		}
		private void RemoveLaneConnection(ushort nodeID, ref NetNode data)
		{
			NetManager instance = Singleton<NetManager>.instance;
			ushort num = 0;
			ushort num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)data.m_lane)].m_nodes;
			int num3 = 0;
			while (num2 != 0)
			{
				if (num2 == nodeID)
				{
					if (num == 0)
					{
						instance.m_lanes.m_buffer[(int)((UIntPtr)data.m_lane)].m_nodes = data.m_nextLaneNode;
					}
					else
					{
						instance.m_nodes.m_buffer[(int)num].m_nextLaneNode = data.m_nextLaneNode;
					}
					break;
				}
				num = num2;
				num2 = instance.m_nodes.m_buffer[(int)num2].m_nextLaneNode;
				if (++num3 > 32768)
				{
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
			data.m_lane = 0u;
			data.m_laneOffset = 0;
			data.m_nextLaneNode = 0;
		}
	}
}
