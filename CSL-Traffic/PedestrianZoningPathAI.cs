using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PedestrianZoning
{
	/*
	 * Self explanatory. Most of the code (if not all, can't really remember if I changed anything) was taken
	 * from RoadAI.
	 */
	class PedestrianZoningPathAI : PedestrianPathAI
	{
		public bool m_enableZoning = true;

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
	}
}
