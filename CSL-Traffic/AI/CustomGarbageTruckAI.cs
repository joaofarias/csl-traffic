using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PedestrianZoning
{
	/*
	 * The AI for garbage truck using pedestrian paths. Again, there's a few small changes to make it use them (having them in the path is not enough).
	 * The movement happens on SimulationStep.
	 */
	public class CustomGarbageTruckAI : GarbageTruckAI
	{
		void Awake()
		{
			this.m_cargoCapacity = 20000;
		}

		public override void SimulationStep(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics)
		{
			if ((vehicleData.m_flags & Vehicle.Flags.TransferToSource) != Vehicle.Flags.None)
			{
				if ((int)vehicleData.m_transferSize < this.m_cargoCapacity)
				{
					this.TryCollectGarbage(vehicleID, ref vehicleData, ref frameData);
				}
				if ((int)vehicleData.m_transferSize >= this.m_cargoCapacity && (vehicleData.m_flags & Vehicle.Flags.GoingBack) == Vehicle.Flags.None && vehicleData.m_targetBuilding != 0)
				{
					this.SetTarget(vehicleID, ref vehicleData, 0);
				}
			}
			SimulationStepCarAI(vehicleID, ref vehicleData, ref frameData, leaderID, ref leaderData, lodPhysics);
			if ((vehicleData.m_flags & Vehicle.Flags.Arriving) != Vehicle.Flags.None && vehicleData.m_targetBuilding != 0 && (vehicleData.m_flags & (Vehicle.Flags.WaitingPath | Vehicle.Flags.GoingBack | Vehicle.Flags.WaitingTarget)) == Vehicle.Flags.None)
			{
				this.ArriveAtTarget(vehicleID, ref vehicleData);
			}
			if ((vehicleData.m_flags & (Vehicle.Flags.TransferToSource | Vehicle.Flags.GoingBack)) == Vehicle.Flags.TransferToSource && this.ShouldReturnToSource(vehicleID, ref vehicleData))
			{
				this.SetTarget(vehicleID, ref vehicleData, 0);
			}
		}

		// from CarAI
		public void SimulationStepCarAI(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics)
		{
			uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
			frameData.m_position += frameData.m_velocity * 0.5f;
			frameData.m_swayPosition += frameData.m_swayVelocity * 0.5f;
			float acceleration = this.m_info.m_acceleration;
			float braking = this.m_info.m_braking;
			float magnitude = frameData.m_velocity.magnitude;
			Vector3 vector = vehicleData.m_targetPos0 - (Vector4)frameData.m_position;
			float sqrMagnitude = vector.sqrMagnitude;
			float num = (magnitude + acceleration) * (0.5f + 0.5f * (magnitude + acceleration) / braking) + this.m_info.m_generatedInfo.m_size.z * 0.5f;
			float num2 = Mathf.Max(magnitude + acceleration, 5f);
			if (lodPhysics >= 2 && (ulong)(currentFrameIndex >> 4 & 3u) == (ulong)((long)(vehicleID & 3)))
			{
				num2 *= 2f;
			}
			float num3 = Mathf.Max((num - num2) / 3f, 1f);
			float num4 = num2 * num2;
			float num5 = num3 * num3;
			int i = 0;
			bool flag = false;
			if ((sqrMagnitude < num4 || vehicleData.m_targetPos3.w < 0.01f) && (leaderData.m_flags & (Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped)) == Vehicle.Flags.None)
			{
				if (leaderData.m_path != 0u)
				{
					UpdatePathTargetPositions(vehicleID, ref vehicleData, frameData.m_position, ref i, 4, num4, num5);
					if ((leaderData.m_flags & Vehicle.Flags.Spawned) == Vehicle.Flags.None)
					{
						frameData = vehicleData.m_frame0;
						return;
					}
				}
				if ((leaderData.m_flags & Vehicle.Flags.WaitingPath) == Vehicle.Flags.None)
				{
					while (i < 4)
					{
						float minSqrDistance;
						Vector3 refPos;
						if (i == 0)
						{
							minSqrDistance = num4;
							refPos = frameData.m_position;
							flag = true;
						}
						else
						{
							minSqrDistance = num5;
							refPos = vehicleData.GetTargetPos(i - 1);
						}
						int num6 = i;
						this.UpdateBuildingTargetPositions(vehicleID, ref vehicleData, refPos, leaderID, ref leaderData, ref i, minSqrDistance);
						if (i == num6)
						{
							break;
						}
					}
					if (i != 0)
					{
						Vector4 targetPos = vehicleData.GetTargetPos(i - 1);
						while (i < 4)
						{
							vehicleData.SetTargetPos(i++, targetPos);
						}
					}
				}
				vector = vehicleData.m_targetPos0 - (Vector4)frameData.m_position;
				sqrMagnitude = vector.sqrMagnitude;
			}
			if (leaderData.m_path != 0u && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == Vehicle.Flags.None)
			{
				NetManager instance = Singleton<NetManager>.instance;
				byte b = leaderData.m_pathPositionIndex;
				byte lastPathOffset = leaderData.m_lastPathOffset;
				if (b == 255)
				{
					b = 0;
				}
				float num7 = 1f + leaderData.CalculateTotalLength(leaderID);
				PathManager instance2 = Singleton<PathManager>.instance;
				PathUnit.Position pathPos;
				if (instance2.m_pathUnits.m_buffer[(int)((UIntPtr)leaderData.m_path)].GetPosition(b >> 1, out pathPos))
				{
					instance.m_segments.m_buffer[(int)pathPos.m_segment].AddTraffic(Mathf.RoundToInt(num7 * 2.5f));
					bool flag2 = false;
					if ((b & 1) == 0 || lastPathOffset == 0)
					{
						uint laneID = PathManager.GetLaneID(pathPos);
						if (laneID != 0u)
						{
							Vector3 b2 = instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculatePosition((float)pathPos.m_offset * 0.003921569f);
							float num8 = 0.5f * magnitude * magnitude / this.m_info.m_braking + this.m_info.m_generatedInfo.m_size.z * 0.5f;
							if (Vector3.Distance(frameData.m_position, b2) >= num8 - 1f)
							{
								instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].ReserveSpace(num7);
								flag2 = true;
							}
						}
					}
					if (!flag2 && instance2.m_pathUnits.m_buffer[(int)((UIntPtr)leaderData.m_path)].GetNextPosition(b >> 1, out pathPos))
					{
						uint laneID2 = PathManager.GetLaneID(pathPos);
						if (laneID2 != 0u)
						{
							instance.m_lanes.m_buffer[(int)((UIntPtr)laneID2)].ReserveSpace(num7);
						}
					}
				}
				if ((ulong)(currentFrameIndex >> 4 & 15u) == (ulong)((long)(leaderID & 15)))
				{
					bool flag3 = false;
					uint path = leaderData.m_path;
					int num9 = b >> 1;
					int j = 0;
					while (j < 5)
					{
						bool flag4;
						if (PathUnit.GetNextPosition(ref path, ref num9, out pathPos, out flag4))
						{
							uint laneID3 = PathManager.GetLaneID(pathPos);
							if (laneID3 != 0u && !instance.m_lanes.m_buffer[(int)((UIntPtr)laneID3)].CheckSpace(num7))
							{
								j++;
								continue;
							}
						}
						if (flag4)
						{
							this.InvalidPath(vehicleID, ref vehicleData, leaderID, ref leaderData);
						}
						flag3 = true;
						break;
					}
					if (!flag3)
					{
						leaderData.m_flags |= Vehicle.Flags.Congestion;
					}
				}
			}
			float num10;
			if ((leaderData.m_flags & Vehicle.Flags.Stopped) != Vehicle.Flags.None)
			{
				num10 = 0f;
			}
			else
			{
				num10 = vehicleData.m_targetPos0.w;
			}
			Quaternion rotation = Quaternion.Inverse(frameData.m_rotation);
			vector = rotation * vector;
			Vector3 vector2 = rotation * frameData.m_velocity;
			Vector3 a = Vector3.forward;
			Vector3 vector3 = Vector3.zero;
			Vector3 zero = Vector3.zero;
			float num11 = 0f;
			float num12 = 0f;
			bool flag5 = false;
			float num13 = 0f;
			if (sqrMagnitude > 1f)
			{
				a = VectorUtils.NormalizeXZ(vector, out num13);
				if (num13 > 1f)
				{
					Vector3 vector4 = vector;
					num2 = Mathf.Max(magnitude, 2f);
					num4 = num2 * num2;
					if (sqrMagnitude > num4)
					{
						vector4 *= num2 / Mathf.Sqrt(sqrMagnitude);
					}
					bool flag6 = false;
					if (vector4.z < Mathf.Abs(vector4.x))
					{
						if (vector4.z < 0f)
						{
							flag6 = true;
						}
						float num14 = Mathf.Abs(vector4.x);
						if (num14 < 1f)
						{
							vector4.x = Mathf.Sign(vector4.x);
							if (vector4.x == 0f)
							{
								vector4.x = 1f;
							}
							num14 = 1f;
						}
						vector4.z = num14;
					}
					float b3;
					a = VectorUtils.NormalizeXZ(vector4, out b3);
					num13 = Mathf.Min(num13, b3);
					float num15 = 1.57079637f * (1f - a.z);
					if (num13 > 1f)
					{
						num15 /= num13;
					}
					float num16 = num13;
					if (vehicleData.m_targetPos0.w < 0.1f)
					{
						num10 = this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1000f, num15);
						num10 = Mathf.Min(num10, CalculateMaxSpeed(num16, Mathf.Min(vehicleData.m_targetPos0.w, vehicleData.m_targetPos1.w), braking * 0.9f));
					}
					else
					{
						num10 = Mathf.Min(num10, this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1000f, num15));
						num10 = Mathf.Min(num10, CalculateMaxSpeed(num16, vehicleData.m_targetPos1.w, braking * 0.9f));
					}
					num16 += VectorUtils.LengthXZ(vehicleData.m_targetPos1 - vehicleData.m_targetPos0);
					num10 = Mathf.Min(num10, CalculateMaxSpeed(num16, vehicleData.m_targetPos2.w, braking * 0.9f));
					num16 += VectorUtils.LengthXZ(vehicleData.m_targetPos2 - vehicleData.m_targetPos1);
					num10 = Mathf.Min(num10, CalculateMaxSpeed(num16, vehicleData.m_targetPos3.w, braking * 0.9f));
					num16 += VectorUtils.LengthXZ(vehicleData.m_targetPos3 - vehicleData.m_targetPos2);
					if (vehicleData.m_targetPos3.w < 0.01f)
					{
						num16 = Mathf.Max(0f, num16 - this.m_info.m_generatedInfo.m_size.z * 0.5f);
					}
					num10 = Mathf.Min(num10, CalculateMaxSpeed(num16, 0f, braking * 0.9f));
					if (!DisableCollisionCheck(leaderID, ref leaderData))
					{
						this.CheckOtherVehicles(vehicleID, ref vehicleData, ref frameData, ref num10, ref flag5, ref zero, num, braking * 0.9f, lodPhysics);
					}
					if (flag6)
					{
						num10 = -num10;
					}
					if (num10 < magnitude)
					{
						float num17 = Mathf.Max(acceleration, Mathf.Min(braking, magnitude));
						num11 = Mathf.Max(num10, magnitude - num17);
					}
					else
					{
						float num18 = Mathf.Max(acceleration, Mathf.Min(braking, -magnitude));
						num11 = Mathf.Min(num10, magnitude + num18);
					}
				}
			}
			else
			{
				if (magnitude < 0.1f && flag && this.ArriveAtDestination(leaderID, ref leaderData))
				{
					leaderData.Unspawn(leaderID);
					if (leaderID == vehicleID)
					{
						frameData = leaderData.m_frame0;
					}
					return;
				}
			}
			if ((leaderData.m_flags & Vehicle.Flags.Stopped) == Vehicle.Flags.None && num10 < 0.1f)
			{
				flag5 = true;
			}
			if (flag5)
			{
				vehicleData.m_blockCounter = (byte)Mathf.Min((int)(vehicleData.m_blockCounter + 1), 255);
			}
			else
			{
				vehicleData.m_blockCounter = 0;
			}
			if (num13 > 1f)
			{
				num12 = Mathf.Asin(a.x) * Mathf.Sign(num11);
				vector3 = a * num11;
			}
			else
			{
				num11 = 0f;
				Vector3 b4 = Vector3.ClampMagnitude(vector * 0.5f - vector2, braking);
				vector3 = vector2 + b4;
			}
			bool flag7 = (currentFrameIndex + (uint)leaderID & 16u) != 0u;
			Vector3 a2 = vector3 - vector2;
			Vector3 vector5 = frameData.m_rotation * vector3;
			frameData.m_velocity = vector5 + zero;
			frameData.m_position += frameData.m_velocity * 0.5f;
			frameData.m_swayVelocity = frameData.m_swayVelocity * (1f - this.m_info.m_dampers) - a2 * (1f - this.m_info.m_springs) - frameData.m_swayPosition * this.m_info.m_springs;
			frameData.m_swayPosition += frameData.m_swayVelocity * 0.5f;
			frameData.m_steerAngle = num12;
			frameData.m_travelDistance += vector3.z;
			frameData.m_lightIntensity.x = 5f;
			frameData.m_lightIntensity.y = ((a2.z >= -0.1f) ? 0.5f : 5f);
			frameData.m_lightIntensity.z = ((num12 >= -0.1f || !flag7) ? 0f : 5f);
			frameData.m_lightIntensity.w = ((num12 <= 0.1f || !flag7) ? 0f : 5f);
			if ((vehicleData.m_flags & Vehicle.Flags.Parking) != Vehicle.Flags.None && num13 <= 1f && flag)
			{
				Vector3 forward = vehicleData.m_targetPos1 - vehicleData.m_targetPos0;
				if (forward.sqrMagnitude > 0.01f)
				{
					frameData.m_rotation = Quaternion.LookRotation(forward);
				}
			}
			else
			{
				if (num11 > 0.1f)
				{
					if (vector5.sqrMagnitude > 0.01f)
					{
						frameData.m_rotation = Quaternion.LookRotation(vector5);
					}
				}
				else
				{
					if (num11 < -0.1f && vector5.sqrMagnitude > 0.01f)
					{
						frameData.m_rotation = Quaternion.LookRotation(-vector5);
					}
				}
			}
			//base.SimulationStep(vehicleID, ref vehicleData, ref frameData, leaderID, ref leaderData, lodPhysics);
		}

		protected override bool StartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays)
		{
			VehicleInfo info = this.m_info;
			PathUnit.Position startPosA;
			PathUnit.Position startPosB;
			float num;
			float num2;
			PathUnit.Position endPosA;
			PathUnit.Position endPosB;
			float num3;
			float num4;
			if (PathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | ((NetInfo.LaneType)((byte)32)), info.m_vehicleType, 32f, out startPosA, out startPosB, out num, out num2) && PathManager.FindPathPosition(endPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | ((NetInfo.LaneType)((byte)32)), info.m_vehicleType, 32f, out endPosA, out endPosB, out num3, out num4)) 
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
				if ((Singleton<PathManager>.instance as CustomPathManager).CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle | ((NetInfo.LaneType)((byte)32)), info.m_vehicleType, 20000f, this.IsHeavyVehicle(), this.IgnoreBlocked(vehicleID, ref vehicleData), false, false, true))
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

		// from vehicleAI
		protected new void UpdatePathTargetPositions(ushort vehicleID, ref Vehicle vehicleData, Vector3 refPos, ref int index, int max, float minSqrDistanceA, float minSqrDistanceB)
		{
			PathManager instance = Singleton<PathManager>.instance;
			NetManager instance2 = Singleton<NetManager>.instance;
			Vector4 vector = vehicleData.m_targetPos0;
			vector.w = 1000f;
			float num = minSqrDistanceA;
			uint num2 = vehicleData.m_path;
			byte b = vehicleData.m_pathPositionIndex;
			byte b2 = vehicleData.m_lastPathOffset;
			if (b == 255)
			{
				b = 0;
				if (index <= 0)
				{
					vehicleData.m_pathPositionIndex = 0;
				}
				if (!Singleton<PathManager>.instance.m_pathUnits.m_buffer[(int)((UIntPtr)num2)].CalculatePathPositionOffset(b >> 1, vector, out b2))
				{
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}
			}
			PathUnit.Position position;
			if (!instance.m_pathUnits.m_buffer[(int)((UIntPtr)num2)].GetPosition(b >> 1, out position))
			{
				this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
				return;
			}
			NetInfo info = instance2.m_segments.m_buffer[(int)position.m_segment].Info;
			if (info.m_lanes.Length <= (int)position.m_lane)
			{
				this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
				return;
			}
			uint num3 = PathManager.GetLaneID(position);
			NetInfo.Lane lane = info.m_lanes[(int)position.m_lane];
			Bezier3 bezier;
			while (true)
			{
				if ((b & 1) == 0)
				{
					if (lane.m_laneType != NetInfo.LaneType.Cargo)
					{
						bool flag = true;
						while (b2 != position.m_offset)
						{
							if (flag)
							{
								flag = false;
							}
							else
							{
								float num4 = Mathf.Sqrt(num) - Vector3.Distance(vector, refPos);
								int num5;
								if (num4 < 0f)
								{
									num5 = 4;
								}
								else
								{
									num5 = 4 + Mathf.CeilToInt(num4 * 256f / (instance2.m_lanes.m_buffer[(int)((UIntPtr)num3)].m_length + 1f));
								}
								if (b2 > position.m_offset)
								{
									b2 = (byte)Mathf.Max((int)b2 - num5, (int)position.m_offset);
								}
								else
								{
									if (b2 < position.m_offset)
									{
										b2 = (byte)Mathf.Min((int)b2 + num5, (int)position.m_offset);
									}
								}
							}
							Vector3 a;
							Vector3 vector2;
							float b3;
							this.CalculateSegmentPosition(vehicleID, ref vehicleData, position, num3, b2, out a, out vector2, out b3);
							vector.Set(a.x, a.y, a.z, Mathf.Min(vector.w, b3));
							float sqrMagnitude = (a - refPos).sqrMagnitude;
							if (sqrMagnitude >= num)
							{
								if (index <= 0)
								{
									vehicleData.m_lastPathOffset = b2;
								}
								vehicleData.SetTargetPos(index++, vector);
								num = minSqrDistanceB;
								refPos = vector;
								vector.w = 1000f;
								if (index == max)
								{
									return;
								}
							}
						}
					}
					b += 1;
					b2 = 0;
					if (index <= 0)
					{
						vehicleData.m_pathPositionIndex = b;
						vehicleData.m_lastPathOffset = b2;
					}
				}
				int num6 = (b >> 1) + 1;
				uint num7 = num2;
				if (num6 >= (int)instance.m_pathUnits.m_buffer[(int)((UIntPtr)num2)].m_positionCount)
				{
					num6 = 0;
					num7 = instance.m_pathUnits.m_buffer[(int)((UIntPtr)num2)].m_nextPathUnit;
					if (num7 == 0u)
					{
						if (index <= 0)
						{
							Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
							vehicleData.m_path = 0u;
						}
						vector.w = 1f;
						vehicleData.SetTargetPos(index++, vector);
						return;
					}
				}
				PathUnit.Position position2;
				if (!instance.m_pathUnits.m_buffer[(int)((UIntPtr)num7)].GetPosition(num6, out position2))
				{
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}
				NetInfo info2 = instance2.m_segments.m_buffer[(int)position2.m_segment].Info;
				if (info2.m_lanes.Length <= (int)position2.m_lane)
				{
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}
				uint laneID = PathManager.GetLaneID(position2);
				NetInfo.Lane lane2 = info2.m_lanes[(int)position2.m_lane];
				ushort startNode = instance2.m_segments.m_buffer[(int)position.m_segment].m_startNode;
				ushort endNode = instance2.m_segments.m_buffer[(int)position.m_segment].m_endNode;
				ushort startNode2 = instance2.m_segments.m_buffer[(int)position2.m_segment].m_startNode;
				ushort endNode2 = instance2.m_segments.m_buffer[(int)position2.m_segment].m_endNode;
				if (startNode2 != startNode && startNode2 != endNode && endNode2 != startNode && endNode2 != endNode && ((instance2.m_nodes.m_buffer[(int)startNode].m_flags | instance2.m_nodes.m_buffer[(int)endNode].m_flags) & NetNode.Flags.Disabled) == NetNode.Flags.None && ((instance2.m_nodes.m_buffer[(int)startNode2].m_flags | instance2.m_nodes.m_buffer[(int)endNode2].m_flags) & NetNode.Flags.Disabled) != NetNode.Flags.None)
				{
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}
				if (lane2.m_laneType == NetInfo.LaneType.Pedestrian)
				{
					if (vehicleID != 0 && (vehicleData.m_flags & Vehicle.Flags.Parking) == Vehicle.Flags.None)
					{
						byte offset = position.m_offset;
						byte offset2 = position.m_offset;
						if (this.ParkVehicle(vehicleID, ref vehicleData, position, num7, num6 << 1, out offset2))
						{
							if (offset2 != offset)
							{
								if (index <= 0)
								{
									vehicleData.m_pathPositionIndex = (byte)((int)vehicleData.m_pathPositionIndex & -2);
									vehicleData.m_lastPathOffset = offset;
								}
								position.m_offset = offset2;
								instance.m_pathUnits.m_buffer[(int)((UIntPtr)num2)].SetPosition(b >> 1, position);
							}
							vehicleData.m_flags |= Vehicle.Flags.Parking;
						}
						else
						{
							this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
						}
					}
					return;
				}
				if ((byte)(lane2.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.Cargo | ((NetInfo.LaneType)((byte)32)))) == 0)
				{
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}
				if (lane2.m_vehicleType != this.m_info.m_vehicleType && this.NeedChangeVehicleType(vehicleID, ref vehicleData, position2, laneID, lane2.m_vehicleType, ref vector))
				{
					float sqrMagnitude3 = (vector - (Vector4)refPos).sqrMagnitude;
					if (sqrMagnitude3 >= num)
					{
						vehicleData.SetTargetPos(index++, vector);
					}
					if (index <= 0)
					{
						if (num6 == 0)
						{
							Singleton<PathManager>.instance.ReleaseFirstUnit(ref vehicleData.m_path);
						}
						vehicleData.m_pathPositionIndex = (byte)(num6 << 1);
						PathUnit.CalculatePathPositionOffset(laneID, vector, out vehicleData.m_lastPathOffset);
						if (vehicleID != 0 && !this.ChangeVehicleType(vehicleID, ref vehicleData, position2, laneID))
						{
							this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
						}
					}
					return;
				}
				if (position2.m_segment != position.m_segment && vehicleID != 0)
				{
					vehicleData.m_flags &= ~Vehicle.Flags.Leaving;
				}
				byte b4 = 0;
				if ((vehicleData.m_flags & Vehicle.Flags.Flying) != Vehicle.Flags.None)
				{
					b4 = (byte)((position2.m_offset < 128) ? 255 : 0);
				}
				else
				{
					if (num3 != laneID && lane.m_laneType != NetInfo.LaneType.Cargo)
					{
						PathUnit.CalculatePathPositionOffset(laneID, vector, out b4);
						bezier = default(Bezier3);
						Vector3 vector3;
						float num8;
						this.CalculateSegmentPosition(vehicleID, ref vehicleData, position, num3, position.m_offset, out bezier.a, out vector3, out num8);
						bool flag2 = b2 == 0;
						if (flag2)
						{
							if ((vehicleData.m_flags & Vehicle.Flags.Reversed) != Vehicle.Flags.None)
							{
								flag2 = (vehicleData.m_trailingVehicle == 0);
							}
							else
							{
								flag2 = (vehicleData.m_leadingVehicle == 0);
							}
						}
						Vector3 vector4;
						float num9;
						if (flag2)
						{
							PathUnit.Position nextPosition;
							if (!instance.m_pathUnits.m_buffer[(int)((UIntPtr)num7)].GetNextPosition(num6, out nextPosition))
							{
								nextPosition = default(PathUnit.Position);
							}
							this.CalculateSegmentPosition(vehicleID, ref vehicleData, nextPosition, position2, laneID, b4, position, num3, position.m_offset, out bezier.d, out vector4, out num9);
						}
						else
						{
							this.CalculateSegmentPosition(vehicleID, ref vehicleData, position2, laneID, b4, out bezier.d, out vector4, out num9);
						}
						if (num9 < 0.01f || (instance2.m_segments.m_buffer[(int)position2.m_segment].m_flags & NetSegment.Flags.Flooded) != NetSegment.Flags.None)
						{
							if (index <= 0)
							{
								vehicleData.m_lastPathOffset = b2;
							}
							vector = bezier.a;
							vector.w = 0f;
							while (index < max)
							{
								vehicleData.SetTargetPos(index++, vector);
							}
						}
						if (position.m_offset == 0)
						{
							vector3 = -vector3;
						}
						if (b4 < position2.m_offset)
						{
							vector4 = -vector4;
						}
						vector3.Normalize();
						vector4.Normalize();
						float num10;
						NetSegment.CalculateMiddlePoints(bezier.a, vector3, bezier.d, vector4, true, true, out bezier.b, out bezier.c, out num10);
						if (num10 > 1f)
						{
							ushort num11;
							if (b4 == 0)
							{
								num11 = instance2.m_segments.m_buffer[(int)position2.m_segment].m_startNode;
							}
							else
							{
								if (b4 == 255)
								{
									num11 = instance2.m_segments.m_buffer[(int)position2.m_segment].m_endNode;
								}
								else
								{
									num11 = 0;
								}
							}
							float num12 = 1.57079637f * (1f + Vector3.Dot(vector3, vector4));
							if (num10 > 1f)
							{
								num12 /= num10;
							}
							num9 = Mathf.Min(num9, this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1000f, num12));
							while (b2 < 255)
							{
								float num13 = Mathf.Sqrt(num) - Vector3.Distance(vector, refPos);
								int num14;
								if (num13 < 0f)
								{
									num14 = 8;
								}
								else
								{
									num14 = 8 + Mathf.CeilToInt(num13 * 256f / (num10 + 1f));
								}
								b2 = (byte)Mathf.Min((int)b2 + num14, 255);
								Vector3 a2 = bezier.Position((float)b2 * 0.003921569f);
								vector.Set(a2.x, a2.y, a2.z, Mathf.Min(vector.w, num9));
								float sqrMagnitude2 = (a2 - refPos).sqrMagnitude;
								if (sqrMagnitude2 >= num)
								{
									if (index <= 0)
									{
										vehicleData.m_lastPathOffset = b2;
									}
									if (num11 != 0)
									{
										this.UpdateNodeTargetPos(vehicleID, ref vehicleData, num11, ref instance2.m_nodes.m_buffer[(int)num11], ref vector, index);
									}
									vehicleData.SetTargetPos(index++, vector);
									num = minSqrDistanceB;
									refPos = vector;
									vector.w = 1000f;
									if (index == max)
									{
										return;
									}
								}
							}
						}
					}
					else
					{
						PathUnit.CalculatePathPositionOffset(laneID, vector, out b4);
					}
				}
				if (index <= 0)
				{
					if (num6 == 0)
					{
						Singleton<PathManager>.instance.ReleaseFirstUnit(ref vehicleData.m_path);
					}
					if (num6 >= (int)(instance.m_pathUnits.m_buffer[(int)((UIntPtr)num7)].m_positionCount - 1) && instance.m_pathUnits.m_buffer[(int)((UIntPtr)num7)].m_nextPathUnit == 0u && vehicleID != 0)
					{
						this.ArrivingToDestination(vehicleID, ref vehicleData);
					}
				}
				num2 = num7;
				b = (byte)(num6 << 1);
				b2 = b4;
				if (index <= 0)
				{
					vehicleData.m_pathPositionIndex = b;
					vehicleData.m_lastPathOffset = b2;
					vehicleData.m_flags = ((vehicleData.m_flags & ~Vehicle.Flags.OnGravel) | info2.m_setVehicleFlags);
				}
				position = position2;
				num3 = laneID;
				lane = lane2;
			}	
		}

		private void TryCollectGarbage(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData)
		{
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
						this.TryCollectGarbage(vehicleID, ref vehicleData, ref frameData, num9, ref instance.m_buildings.m_buffer[(int)num9]);
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

		private void TryCollectGarbage(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort buildingID, ref Building building)
		{
			Vector3 a = building.CalculateSidewalkPosition();
			if (Vector3.SqrMagnitude(a - frameData.m_position) < 1024f)
			{
				int num = Mathf.Min(0, (int)vehicleData.m_transferSize - this.m_cargoCapacity);
				if (num == 0)
				{
					return;
				}
				BuildingInfo info = building.Info;
				info.m_buildingAI.ModifyMaterialBuffer(buildingID, ref building, (TransferManager.TransferReason)vehicleData.m_transferType, ref num);
				if (num != 0)
				{
					vehicleData.m_transferSize += (ushort)Mathf.Max(0, -num);
				}
			}
		}

		private bool ArriveAtTarget(ushort vehicleID, ref Vehicle data)
		{
			if (data.m_targetBuilding == 0)
			{
				return true;
			}
			int num = 0;
			if ((data.m_flags & Vehicle.Flags.TransferToTarget) != Vehicle.Flags.None)
			{
				num = (int)data.m_transferSize;
			}
			if ((data.m_flags & Vehicle.Flags.TransferToSource) != Vehicle.Flags.None)
			{
				num = Mathf.Min(0, (int)data.m_transferSize - this.m_cargoCapacity);
			}
			BuildingManager instance = Singleton<BuildingManager>.instance;
			BuildingInfo info = instance.m_buildings.m_buffer[(int)data.m_targetBuilding].Info;
			info.m_buildingAI.ModifyMaterialBuffer(data.m_targetBuilding, ref instance.m_buildings.m_buffer[(int)data.m_targetBuilding], (TransferManager.TransferReason)data.m_transferType, ref num);
			if ((data.m_flags & Vehicle.Flags.TransferToTarget) != Vehicle.Flags.None)
			{
				data.m_transferSize = (ushort)Mathf.Clamp((int)data.m_transferSize - num, 0, (int)data.m_transferSize);
			}
			if ((data.m_flags & Vehicle.Flags.TransferToSource) != Vehicle.Flags.None)
			{
				data.m_transferSize += (ushort)Mathf.Max(0, -num);
			}
			this.SetTarget(vehicleID, ref data, 0);
			return false;
		}

		private bool ShouldReturnToSource(ushort vehicleID, ref Vehicle data)
		{
			if (data.m_sourceBuilding != 0)
			{
				BuildingManager instance = Singleton<BuildingManager>.instance;
				if ((instance.m_buildings.m_buffer[(int)data.m_sourceBuilding].m_productionRate == 0 || (instance.m_buildings.m_buffer[(int)data.m_sourceBuilding].m_flags & (Building.Flags.Downgrading | Building.Flags.BurnedDown)) != Building.Flags.None) && instance.m_buildings.m_buffer[(int)data.m_sourceBuilding].m_fireIntensity == 0)
				{
					return true;
				}
			}
			return false;
		}


		// from CarAI
		private float CalculateMaxSpeed(float targetDistance, float targetSpeed, float maxBraking)
		{
			float num = 0.5f * maxBraking;
			float num2 = num + targetSpeed;
			return Mathf.Sqrt(Mathf.Max(0f, num2 * num2 + 2f * targetDistance * maxBraking)) - num;
		}

		// from CarAI
		private static bool DisableCollisionCheck(ushort vehicleID, ref Vehicle vehicleData)
		{
			if ((vehicleData.m_flags & Vehicle.Flags.Arriving) != Vehicle.Flags.None)
			{
				float num = Mathf.Max(Mathf.Abs(vehicleData.m_targetPos3.x), Mathf.Abs(vehicleData.m_targetPos3.z));
				float num2 = 8640f;
				if (num > num2 - 100f)
				{
					return true;
				}
			}
			return false;
		}

		// from CarAI
		private void CheckOtherVehicles(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ref float maxSpeed, ref bool blocked, ref Vector3 collisionPush, float maxDistance, float maxBraking, int lodPhysics)
		{
			Vector3 vector = vehicleData.m_targetPos3 - (Vector4)frameData.m_position;
			Vector3 rhs = frameData.m_position + Vector3.ClampMagnitude(vector, maxDistance);
			Vector3 min = Vector3.Min(vehicleData.m_segment.Min(), rhs);
			Vector3 max = Vector3.Max(vehicleData.m_segment.Max(), rhs);
			VehicleManager instance = Singleton<VehicleManager>.instance;
			int num = Mathf.Max((int)((min.x - 10f) / 32f + 270f), 0);
			int num2 = Mathf.Max((int)((min.z - 10f) / 32f + 270f), 0);
			int num3 = Mathf.Min((int)((max.x + 10f) / 32f + 270f), 539);
			int num4 = Mathf.Min((int)((max.z + 10f) / 32f + 270f), 539);
			for (int i = num2; i <= num4; i++)
			{
				for (int j = num; j <= num3; j++)
				{
					ushort num5 = instance.m_vehicleGrid[i * 540 + j];
					int num6 = 0;
					while (num5 != 0)
					{
						num5 = this.CheckOtherVehicle(vehicleID, ref vehicleData, ref frameData, ref maxSpeed, ref blocked, ref collisionPush, maxBraking, num5, ref instance.m_vehicles.m_buffer[(int)num5], min, max, lodPhysics);
						if (++num6 > 16384)
						{
							CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
							break;
						}
					}
				}
			}
			if (lodPhysics == 0)
			{
				CitizenManager instance2 = Singleton<CitizenManager>.instance;
				float num7 = 0f;
				Vector3 vector2 = vehicleData.m_segment.b;
				Vector3 lhs = vehicleData.m_segment.b - vehicleData.m_segment.a;
				for (int k = 0; k < 4; k++)
				{
					Vector3 vector3 = vehicleData.GetTargetPos(k);
					Vector3 vector4 = vector3 - vector2;
					if (Vector3.Dot(lhs, vector4) > 0f)
					{
						float magnitude = vector4.magnitude;
						if (magnitude > 0.01f)
						{
							Segment3 segment = new Segment3(vector2, vector3);
							min = segment.Min();
							max = segment.Max();
							int num8 = Mathf.Max((int)((min.x - 3f) / 8f + 1080f), 0);
							int num9 = Mathf.Max((int)((min.z - 3f) / 8f + 1080f), 0);
							int num10 = Mathf.Min((int)((max.x + 3f) / 8f + 1080f), 2159);
							int num11 = Mathf.Min((int)((max.z + 3f) / 8f + 1080f), 2159);
							for (int l = num9; l <= num11; l++)
							{
								for (int m = num8; m <= num10; m++)
								{
									ushort num12 = instance2.m_citizenGrid[l * 2160 + m];
									int num13 = 0;
									while (num12 != 0)
									{
										num12 = this.CheckCitizen(segment, num7, magnitude, ref maxSpeed, ref blocked, maxBraking, num12, ref instance2.m_instances.m_buffer[(int)num12], min, max);
										if (++num13 > 65536)
										{
											CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
											break;
										}
									}
								}
							}
						}
						lhs = vector4;
						num7 += magnitude;
						vector2 = vector3;
					}
				}
			}
		}

		private ushort CheckOtherVehicle(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ref float maxSpeed, ref bool blocked, ref Vector3 collisionPush, float maxBraking, ushort otherID, ref Vehicle otherData, Vector3 min, Vector3 max, int lodPhysics)
		{
			if (otherID != vehicleID && vehicleData.m_leadingVehicle != otherID && vehicleData.m_trailingVehicle != otherID)
			{
				Vector3 vector;
				Vector3 vector2;
				if (lodPhysics >= 2)
				{
					vector = otherData.m_segment.Min();
					vector2 = otherData.m_segment.Max();
				}
				else
				{
					vector = Vector3.Min(otherData.m_segment.Min(), otherData.m_targetPos3);
					vector2 = Vector3.Max(otherData.m_segment.Max(), otherData.m_targetPos3);
				}
				if (min.x < vector2.x + 2f && min.y < vector2.y + 2f && min.z < vector2.z + 2f && vector.x < max.x + 2f && vector.y < max.y + 2f && vector.z < max.z + 2f)
				{
					Vehicle.Frame lastFrameData = otherData.GetLastFrameData();
					VehicleInfo info = otherData.Info;
					if (lodPhysics < 2)
					{
						float num2;
						float num3;
						float num = vehicleData.m_segment.DistanceSqr(otherData.m_segment, out num2, out num3);
						if (num < 4f)
						{
							Vector3 a = vehicleData.m_segment.Position(0.5f);
							Vector3 b = otherData.m_segment.Position(0.5f);
							Vector3 lhs = vehicleData.m_segment.b - vehicleData.m_segment.a;
							if (Vector3.Dot(lhs, a - b) < 0f)
							{
								collisionPush -= lhs.normalized * (0.1f - num * 0.025f);
							}
							else
							{
								collisionPush += lhs.normalized * (0.1f - num * 0.025f);
							}
							blocked = true;
						}
					}
					float num4 = frameData.m_velocity.magnitude + 0.01f;
					float num5 = lastFrameData.m_velocity.magnitude;
					float num6 = num5 * (0.5f + 0.5f * num5 / info.m_braking) + Mathf.Min(1f, num5);
					num5 += 0.01f;
					float num7 = 0f;
					Vector3 vector3 = vehicleData.m_segment.b;
					Vector3 lhs2 = vehicleData.m_segment.b - vehicleData.m_segment.a;
					for (int i = 0; i < 4; i++)
					{
						Vector3 vector4 = vehicleData.GetTargetPos(i);
						Vector3 vector5 = vector4 - vector3;
						if (Vector3.Dot(lhs2, vector5) > 0f)
						{
							float magnitude = vector5.magnitude;
							Segment3 segment = new Segment3(vector3, vector4);
							min = segment.Min();
							max = segment.Max();
							segment.a.y = segment.a.y * 0.5f;
							segment.b.y = segment.b.y * 0.5f;
							if (magnitude > 0.01f && min.x < vector2.x + 2f && min.y < vector2.y + 2f && min.z < vector2.z + 2f && vector.x < max.x + 2f && vector.y < max.y + 2f && vector.z < max.z + 2f)
							{
								Vector3 a2 = otherData.m_segment.a;
								a2.y *= 0.5f;
								float num8;
								if (segment.DistanceSqr(a2, out num8) < 4f)
								{
									float num9 = Vector3.Dot(lastFrameData.m_velocity, vector5) / magnitude;
									float num10 = num7 + magnitude * num8;
									if (num10 >= 0.01f)
									{
										num10 -= num9 + 3f;
										float num11 = Mathf.Max(0f, CalculateMaxSpeed(num10, num9, maxBraking));
										if (num11 < 0.01f)
										{
											blocked = true;
										}
										Vector3 rhs = Vector3.Normalize(otherData.m_targetPos0 - (Vector4)otherData.m_segment.a);
										float num12 = 1.2f - 1f / ((float)vehicleData.m_blockCounter * 0.02f + 0.5f);
										if (Vector3.Dot(vector5, rhs) > num12 * magnitude)
										{
											maxSpeed = Mathf.Min(maxSpeed, num11);
										}
									}
									break;
								}
								if (lodPhysics < 2)
								{
									float num13 = 0f;
									float num14 = num6;
									Vector3 vector6 = otherData.m_segment.b;
									Vector3 lhs3 = otherData.m_segment.b - otherData.m_segment.a;
									bool flag = false;
									int num15 = 0;
									while (num15 < 4 && num14 > 0.1f)
									{
										Vector3 vector7 = otherData.GetTargetPos(num15);
										Vector3 vector8 = Vector3.ClampMagnitude(vector7 - vector6, num14);
										if (Vector3.Dot(lhs3, vector8) > 0f)
										{
											vector7 = vector6 + vector8;
											float magnitude2 = vector8.magnitude;
											num14 -= magnitude2;
											Segment3 segment2 = new Segment3(vector6, vector7);
											segment2.a.y = segment2.a.y * 0.5f;
											segment2.b.y = segment2.b.y * 0.5f;
											if (magnitude2 > 0.01f)
											{
												float num17;
												float num18;
												float num16;
												if (otherID < vehicleID)
												{
													num16 = segment2.DistanceSqr(segment, out num17, out num18);
												}
												else
												{
													num16 = segment.DistanceSqr(segment2, out num18, out num17);
												}
												if (num16 < 4f)
												{
													float num19 = num7 + magnitude * num18;
													float num20 = num13 + magnitude2 * num17 + 0.1f;
													if (num19 >= 0.01f && num19 * num5 > num20 * num4)
													{
														float num21 = Vector3.Dot(lastFrameData.m_velocity, vector5) / magnitude;
														if (num19 >= 0.01f)
														{
															num19 -= num21 + 1f + otherData.Info.m_generatedInfo.m_size.z;
															float num22 = Mathf.Max(0f, CalculateMaxSpeed(num19, num21, maxBraking));
															if (num22 < 0.01f)
															{
																blocked = true;
															}
															maxSpeed = Mathf.Min(maxSpeed, num22);
														}
													}
													flag = true;
													break;
												}
											}
											lhs3 = vector8;
											num13 += magnitude2;
											vector6 = vector7;
										}
										num15++;
									}
									if (flag)
									{
										break;
									}
								}
							}
							lhs2 = vector5;
							num7 += magnitude;
							vector3 = vector4;
						}
					}
				}
			}
			return otherData.m_nextGridVehicle;
		}

		// from CarAI
		private ushort CheckCitizen(Segment3 segment, float lastLen, float nextLen, ref float maxSpeed, ref bool blocked, float maxBraking, ushort otherID, ref CitizenInstance otherData, Vector3 min, Vector3 max)
		{
			CitizenInfo info = otherData.Info;
			CitizenInstance.Frame lastFrameData = otherData.GetLastFrameData();
			Vector3 position = lastFrameData.m_position;
			Vector3 b = lastFrameData.m_position + lastFrameData.m_velocity;
			Segment3 segment2 = new Segment3(position, b);
			Vector3 vector = segment2.Min();
			vector.x -= info.m_radius;
			vector.z -= info.m_radius;
			Vector3 vector2 = segment2.Max();
			vector2.x += info.m_radius;
			vector2.y += info.m_height;
			vector2.z += info.m_radius;
			float num;
			float num2;
			if (min.x < vector2.x + 1f && min.y < vector2.y && min.z < vector2.z + 1f && vector.x < max.x + 1f && vector.y < max.y + 2f && vector.z < max.z + 1f && segment.DistanceSqr(segment2, out num, out num2) < (1f + info.m_radius) * (1f + info.m_radius))
			{
				float num3 = lastLen + nextLen * num;
				if (num3 >= 0.01f)
				{
					num3 -= 2f;
					float b2 = Mathf.Max(1f, CalculateMaxSpeed(num3, 0f, maxBraking));
					maxSpeed = Mathf.Min(maxSpeed, b2);
				}
			}
			return otherData.m_nextGridInstance;
		}
	}
}
