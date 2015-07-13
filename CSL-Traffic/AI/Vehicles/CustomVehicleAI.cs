using ColossalFramework;
using ColossalFramework.Math;
using System;
using UnityEngine;

namespace CSL_Traffic
{
    static class CustomVehicleAI
    {
        public static void UpdatePathTargetPositions(VehicleAI vehicleAI, ushort vehicleID, ref Vehicle vehicleData, Vector3 refPos, ref int index, int max, float minSqrDistanceA, float minSqrDistanceB)
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
                    (vehicleAI as IVehicle).InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                    return;
                }
            }
            PathUnit.Position position;
            if (!instance.m_pathUnits.m_buffer[(int)((UIntPtr)num2)].GetPosition(b >> 1, out position))
            {
                (vehicleAI as IVehicle).InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                return;
            }
            NetInfo info = instance2.m_segments.m_buffer[(int)position.m_segment].Info;
            if (info.m_lanes.Length <= (int)position.m_lane)
            {
                (vehicleAI as IVehicle).InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                return;
            }
            uint num3 = PathManager.GetLaneID(position);
            NetInfo.Lane lane = info.m_lanes[(int)position.m_lane];
            Bezier3 bezier;
            while (true)
            {
                if ((b & 1) == 0)
                {
                    if (lane.m_laneType != NetInfo.LaneType.CargoVehicle)
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
                                    num5 = 4 + Mathf.Max(0, Mathf.CeilToInt(num4 * 256f / (instance2.m_lanes.m_buffer[(int)((UIntPtr)num3)].m_length + 1f)));
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
                            (vehicleAI as IVehicle).CalculateSegmentPosition(vehicleID, ref vehicleData, position, num3, b2, out a, out vector2, out b3);
                            b3 = RestrictSpeed(b3, num3, vehicleData.Info);
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
                    (vehicleAI as IVehicle).InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                    return;
                }
                NetInfo info2 = instance2.m_segments.m_buffer[(int)position2.m_segment].Info;
                if (info2.m_lanes.Length <= (int)position2.m_lane)
                {
                    (vehicleAI as IVehicle).InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
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
                    (vehicleAI as IVehicle).InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                    return;
                }
                if (lane2.m_laneType == NetInfo.LaneType.Pedestrian)
                {
                    if (vehicleID != 0 && (vehicleData.m_flags & Vehicle.Flags.Parking) == Vehicle.Flags.None)
                    {
                        byte offset = position.m_offset;
                        byte offset2 = position.m_offset;
                        if ((vehicleAI as IVehicle).ParkVehicle(vehicleID, ref vehicleData, position, num7, num6 << 1, out offset2))
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
                            (vehicleAI as IVehicle).InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                        }
                    }
                    return;
                }
                if ((byte)(lane2.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.CargoVehicle | NetInfo.LaneType.TransportVehicle)) == 0)
                {
                    (vehicleAI as IVehicle).InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                    return;
                }
                if (lane2.m_vehicleType != vehicleAI.m_info.m_vehicleType && (vehicleAI as IVehicle).NeedChangeVehicleType(vehicleID, ref vehicleData, position2, laneID, lane2.m_vehicleType, ref vector))
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
                        if (vehicleID != 0 && !(vehicleAI as IVehicle).ChangeVehicleType(vehicleID, ref vehicleData, position2, laneID))
                        {
                            (vehicleAI as IVehicle).InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
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
                    if (num3 != laneID && lane.m_laneType != NetInfo.LaneType.CargoVehicle)
                    {
                        PathUnit.CalculatePathPositionOffset(laneID, vector, out b4);
                        bezier = default(Bezier3);
                        Vector3 vector3;
                        float num8;
                        (vehicleAI as IVehicle).CalculateSegmentPosition(vehicleID, ref vehicleData, position, num3, position.m_offset, out bezier.a, out vector3, out num8);
                        num8 = RestrictSpeed(num8, num3, vehicleData.Info);
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
                            (vehicleAI as IVehicle).CalculateSegmentPosition(vehicleID, ref vehicleData, nextPosition, position2, laneID, b4, position, num3, position.m_offset, out bezier.d, out vector4, out num9);
                            num9 = RestrictSpeed(num9, laneID, vehicleData.Info);
                        }
                        else
                        {
                            (vehicleAI as IVehicle).CalculateSegmentPosition(vehicleID, ref vehicleData, position2, laneID, b4, out bezier.d, out vector4, out num9);
                            num9 = RestrictSpeed(num9, laneID, vehicleData.Info);
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
                            num9 = Mathf.Min(num9, (vehicleAI as IVehicle).CalculateTargetSpeed(vehicleID, ref vehicleData, 1000f, num12));
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
                                    num14 = 8 + Mathf.Max(0, Mathf.CeilToInt(num13 * 256f / (num10 + 1f)));
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
                                        (vehicleAI as IVehicle).UpdateNodeTargetPos(vehicleID, ref vehicleData, num11, ref instance2.m_nodes.m_buffer[(int)num11], ref vector, index);
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
                        (vehicleAI as IVehicle).ArrivingToDestination(vehicleID, ref vehicleData);
                    }
                }
                num2 = num7;
                b = (byte)(num6 << 1);
                b2 = b4;
                if (index <= 0)
                {
                    vehicleData.m_pathPositionIndex = b;
                    vehicleData.m_lastPathOffset = b2;
                    vehicleData.m_flags = ((vehicleData.m_flags & ~(Vehicle.Flags.OnGravel | Vehicle.Flags.Underground | Vehicle.Flags.Transition)) | info2.m_setVehicleFlags);
                }
                position = position2;
                num3 = laneID;
                lane = lane2;
            }
        }

        public static float RestrictSpeed(float calculatedSpeed, uint laneId, VehicleInfo info)
        {
            if (calculatedSpeed == 0f || (CSLTraffic.Options & OptionsManager.ModOptions.BetaTestRoadCustomizerTool) == OptionsManager.ModOptions.None)
                return calculatedSpeed;

            float speedLimit = RoadManager.GetLaneSpeed(laneId);
            float curve = NetManager.instance.m_lanes.m_buffer[laneId].m_curve;

            float a = 1000f / (1f + curve * 1000f / info.m_turning) + 2f;
            float b = 8f * speedLimit;
            return Mathf.Min(Mathf.Min(a, b), info.m_maxSpeed);
        }
    }
}
