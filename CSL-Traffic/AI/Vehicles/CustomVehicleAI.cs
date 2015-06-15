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
            Vector4 targetPosition = vehicleData.m_targetPos0;
            targetPosition.w = 1000f;
            float minDistance = minSqrDistanceA;
            uint pathId = vehicleData.m_path;
            byte positionIdx = vehicleData.m_pathPositionIndex;
            byte pathOffset = vehicleData.m_lastPathOffset;
            if (positionIdx == 255)
            {
                positionIdx = 0;
                if (index <= 0)
                {
                    vehicleData.m_pathPositionIndex = 0;
                }
                if (!Singleton<PathManager>.instance.m_pathUnits.m_buffer[(int)((UIntPtr)pathId)].CalculatePathPositionOffset(positionIdx >> 1, targetPosition, out pathOffset))
                {
                    (vehicleAI as IVehicle).InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                    return;
                }
            }
            PathUnit.Position position;
            if (!instance.m_pathUnits.m_buffer[(int)((UIntPtr)pathId)].GetPosition(positionIdx >> 1, out position))
            {
                (vehicleAI as IVehicle).InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                return;
            }
            NetInfo info = instance2.m_segments.m_buffer[position.m_segment].Info;
            if (info.m_lanes.Length <= position.m_lane)
            {
                (vehicleAI as IVehicle).InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                return;
            }
            uint laneId = PathManager.GetLaneID(position);
            NetInfo.Lane lane = info.m_lanes[position.m_lane];
            Bezier3 bezier;
            while (true)
            {
                if ((positionIdx & 1) == 0)
                {
                    if (lane.m_laneType != NetInfo.LaneType.Cargo)
                    {
                        // Whats the point of this?
                        bool flag = true;
                        while (pathOffset != position.m_offset)
                        {
                            if (flag)
                            {
                                flag = false;
                            }
                            else
                            {
                                float distance = Mathf.Sqrt(minDistance) - Vector3.Distance(targetPosition, refPos);
                                int offset;
                                if (distance < 0f)
                                {
                                    offset = 4;
                                }
                                else
                                {
                                    offset = 4 + Mathf.CeilToInt(distance * 256f / (instance2.m_lanes.m_buffer[(int)((UIntPtr)laneId)].m_length + 1f));
                                }
                                if (pathOffset > position.m_offset)
                                {
                                    pathOffset = (byte)Mathf.Max(pathOffset - offset, position.m_offset);
                                }
                                else
                                {
                                    if (pathOffset < position.m_offset)
                                    {
                                        pathOffset = (byte)Mathf.Min(pathOffset + offset, position.m_offset);
                                    }
                                }
                            }
                            Vector3 vehiclePosition;
                            Vector3 vehicleDirection;
                            float vehicleMaxSpeed;
                            (vehicleAI as IVehicle).CalculateSegmentPosition(vehicleID, ref vehicleData, position, laneId, pathOffset, out vehiclePosition, out vehicleDirection, out vehicleMaxSpeed);
                            vehicleMaxSpeed = RestrictSpeed(vehicleMaxSpeed, laneId, vehicleData.Info);
                            targetPosition.Set(vehiclePosition.x, vehiclePosition.y, vehiclePosition.z, Mathf.Min(targetPosition.w, vehicleMaxSpeed));
                            float sqrMagnitude = (vehiclePosition - refPos).sqrMagnitude;
                            if (sqrMagnitude >= minDistance)
                            {
                                if (index <= 0)
                                {
                                    vehicleData.m_lastPathOffset = pathOffset;
                                }
                                vehicleData.SetTargetPos(index++, targetPosition);
                                minDistance = minSqrDistanceB;
                                refPos = targetPosition;
                                targetPosition.w = 1000f;
                                if (index == max)
                                {
                                    return;
                                }
                            }
                        }
                    }
                    positionIdx += 1;
                    pathOffset = 0;
                    if (index <= 0)
                    {
                        vehicleData.m_pathPositionIndex = positionIdx;
                        vehicleData.m_lastPathOffset = pathOffset;
                    }
                }
                int nextPosIdx = (positionIdx >> 1) + 1;
                uint nextPathId = pathId;
                if (nextPosIdx >= instance.m_pathUnits.m_buffer[(int)((UIntPtr)pathId)].m_positionCount)
                {
                    nextPosIdx = 0;
                    nextPathId = instance.m_pathUnits.m_buffer[(int)((UIntPtr)pathId)].m_nextPathUnit;
                    if (nextPathId == 0u)
                    {
                        if (index <= 0)
                        {
                            Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
                            vehicleData.m_path = 0u;
                        }
                        targetPosition.w = 1f;
                        vehicleData.SetTargetPos(index++, targetPosition);
                        return;
                    }
                }
                PathUnit.Position position2;
                if (!instance.m_pathUnits.m_buffer[(int)((UIntPtr)nextPathId)].GetPosition(nextPosIdx, out position2))
                {
                    (vehicleAI as IVehicle).InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                    return;
                }
                NetInfo info2 = instance2.m_segments.m_buffer[position2.m_segment].Info;
                if (info2.m_lanes.Length <= position2.m_lane)
                {
                    (vehicleAI as IVehicle).InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                    return;
                }
                uint lane2Id = PathManager.GetLaneID(position2);
                NetInfo.Lane lane2 = info2.m_lanes[position2.m_lane];
                ushort startNode = instance2.m_segments.m_buffer[position.m_segment].m_startNode;
                ushort endNode = instance2.m_segments.m_buffer[position.m_segment].m_endNode;
                ushort startNode2 = instance2.m_segments.m_buffer[position2.m_segment].m_startNode;
                ushort endNode2 = instance2.m_segments.m_buffer[position2.m_segment].m_endNode;
                if (startNode2 != startNode && startNode2 != endNode && endNode2 != startNode && endNode2 != endNode && ((instance2.m_nodes.m_buffer[startNode].m_flags | instance2.m_nodes.m_buffer[endNode].m_flags) & NetNode.Flags.Disabled) == NetNode.Flags.None && ((instance2.m_nodes.m_buffer[startNode2].m_flags | instance2.m_nodes.m_buffer[endNode2].m_flags) & NetNode.Flags.Disabled) != NetNode.Flags.None)
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
                        if ((vehicleAI as IVehicle).ParkVehicle(vehicleID, ref vehicleData, position, nextPathId, nextPosIdx << 1, out offset2))
                        {
                            if (offset2 != offset)
                            {
                                if (index <= 0)
                                {
                                    vehicleData.m_pathPositionIndex = (byte)(vehicleData.m_pathPositionIndex & -2);
                                    vehicleData.m_lastPathOffset = offset;
                                }
                                position.m_offset = offset2;
                                instance.m_pathUnits.m_buffer[(int)((UIntPtr)pathId)].SetPosition(positionIdx >> 1, position);
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
                if ((byte)(lane2.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.Cargo)) == 0)
                {
                    (vehicleAI as IVehicle).InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                    return;
                }
                if (lane2.m_vehicleType != vehicleAI.m_info.m_vehicleType && (vehicleAI as IVehicle).NeedChangeVehicleType(vehicleID, ref vehicleData, position2, lane2Id, lane2.m_vehicleType, ref targetPosition))
                {
                    float sqrMagnitude3 = (targetPosition - (Vector4)refPos).sqrMagnitude;
                    if (sqrMagnitude3 >= minDistance)
                    {
                        vehicleData.SetTargetPos(index++, targetPosition);
                    }
                    if (index <= 0)
                    {
                        if (nextPosIdx == 0)
                        {
                            Singleton<PathManager>.instance.ReleaseFirstUnit(ref vehicleData.m_path);
                        }
                        vehicleData.m_pathPositionIndex = (byte)(nextPosIdx << 1);
                        PathUnit.CalculatePathPositionOffset(lane2Id, targetPosition, out vehicleData.m_lastPathOffset);
                        if (vehicleID != 0 && !(vehicleAI as IVehicle).ChangeVehicleType(vehicleID, ref vehicleData, position2, lane2Id))
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
                byte newOffset = 0;
                if ((vehicleData.m_flags & Vehicle.Flags.Flying) != Vehicle.Flags.None)
                {
                    newOffset = (byte)((position2.m_offset < 128) ? 255 : 0);
                }
                else
                {
                    if (laneId != lane2Id && lane.m_laneType != NetInfo.LaneType.Cargo)
                    {
                        if (!RoadManager.instance.CheckLaneConnection(laneId, lane2Id, (vehicleAI as IVehicle).VehicleType))
                        {
                            (vehicleAI as IVehicle).InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                            return;
                        }

                        PathUnit.CalculatePathPositionOffset(lane2Id, targetPosition, out newOffset);
                        bezier = default(Bezier3);
                        Vector3 vector3;
                        float newSpeed;
                        (vehicleAI as IVehicle).CalculateSegmentPosition(vehicleID, ref vehicleData, position, laneId, position.m_offset, out bezier.a, out vector3, out newSpeed);
                        newSpeed = RestrictSpeed(newSpeed, laneId, vehicleData.Info);
                        bool isFirstVehicle = pathOffset == 0;
                        if (isFirstVehicle)
                        {
                            if ((vehicleData.m_flags & Vehicle.Flags.Reversed) != Vehicle.Flags.None)
                            {
                                isFirstVehicle = (vehicleData.m_trailingVehicle == 0);
                            }
                            else
                            {
                                isFirstVehicle = (vehicleData.m_leadingVehicle == 0);
                            }
                        }
                        Vector3 newDirection;
                        float newMaxSpeed;
                        if (isFirstVehicle)
                        {
                            PathUnit.Position nextPosition;
                            if (!instance.m_pathUnits.m_buffer[(int)((UIntPtr)nextPathId)].GetNextPosition(nextPosIdx, out nextPosition))
                            {
                                nextPosition = default(PathUnit.Position);
                            }
                            (vehicleAI as IVehicle).CalculateSegmentPosition(vehicleID, ref vehicleData, nextPosition, position2, lane2Id, newOffset, position, laneId, position.m_offset, out bezier.d, out newDirection, out newMaxSpeed);
                            newMaxSpeed = RestrictSpeed(newMaxSpeed, lane2Id, vehicleData.Info);
                        }
                        else
                        {
                            (vehicleAI as IVehicle).CalculateSegmentPosition(vehicleID, ref vehicleData, position2, lane2Id, newOffset, out bezier.d, out newDirection, out newMaxSpeed);
                            newMaxSpeed = RestrictSpeed(newMaxSpeed, lane2Id, vehicleData.Info);
                        }
                        if (newMaxSpeed < 0.01f || (instance2.m_segments.m_buffer[position2.m_segment].m_flags & NetSegment.Flags.Flooded) != NetSegment.Flags.None)
                        {
                            if (index <= 0)
                            {
                                vehicleData.m_lastPathOffset = pathOffset;
                            }
                            targetPosition = bezier.a;
                            targetPosition.w = 0f;
                            while (index < max)
                            {
                                vehicleData.SetTargetPos(index++, targetPosition);
                            }
                        }
                        if (position.m_offset == 0)
                        {
                            vector3 = -vector3;
                        }
                        if (newOffset < position2.m_offset)
                        {
                            newDirection = -newDirection;
                        }
                        vector3.Normalize();
                        newDirection.Normalize();
                        float middlePointDistance;
                        NetSegment.CalculateMiddlePoints(bezier.a, vector3, bezier.d, newDirection, true, true, out bezier.b, out bezier.c, out middlePointDistance);
                        if (middlePointDistance > 1f)
                        {
                            ushort nextNodeId;
                            if (newOffset == 0)
                            {
                                nextNodeId = instance2.m_segments.m_buffer[position2.m_segment].m_startNode;
                            }
                            else
                            {
                                if (newOffset == 255)
                                {
                                    nextNodeId = instance2.m_segments.m_buffer[position2.m_segment].m_endNode;
                                }
                                else
                                {
                                    nextNodeId = 0;
                                }
                            }
                            float curve = 1.57079637f * (1f + Vector3.Dot(vector3, newDirection));
                            if (middlePointDistance > 1f)
                            {
                                curve /= middlePointDistance;
                            }
                            newMaxSpeed = Mathf.Min(newMaxSpeed, (vehicleAI as IVehicle).CalculateTargetSpeed(vehicleID, ref vehicleData, 1000f, curve));
                            while (pathOffset < 255)
                            {
                                float newDistance = Mathf.Sqrt(minDistance) - Vector3.Distance(targetPosition, refPos);
                                int nextOffset;
                                if (newDistance < 0f)
                                {
                                    nextOffset = 8;
                                }
                                else
                                {
                                    nextOffset = 8 + Mathf.CeilToInt(newDistance * 256f / (middlePointDistance + 1f));
                                }
                                pathOffset = (byte)Mathf.Min(pathOffset + nextOffset, 255);
                                Vector3 a2 = bezier.Position(pathOffset * CustomPathFind.WEIGHT_FACTOR);
                                targetPosition.Set(a2.x, a2.y, a2.z, Mathf.Min(targetPosition.w, newMaxSpeed));
                                float sqrMagnitude2 = (a2 - refPos).sqrMagnitude;
                                if (sqrMagnitude2 >= minDistance)
                                {
                                    if (index <= 0)
                                    {
                                        vehicleData.m_lastPathOffset = pathOffset;
                                    }
                                    if (nextNodeId != 0)
                                    {
                                        (vehicleAI as IVehicle).UpdateNodeTargetPos(vehicleID, ref vehicleData, nextNodeId, ref instance2.m_nodes.m_buffer[nextNodeId], ref targetPosition, index);
                                    }
                                    vehicleData.SetTargetPos(index++, targetPosition);
                                    minDistance = minSqrDistanceB;
                                    refPos = targetPosition;
                                    targetPosition.w = 1000f;
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
                        PathUnit.CalculatePathPositionOffset(lane2Id, targetPosition, out newOffset);
                    }
                }
                if (index <= 0)
                {
                    if (nextPosIdx == 0)
                    {
                        Singleton<PathManager>.instance.ReleaseFirstUnit(ref vehicleData.m_path);
                    }
                    if (nextPosIdx >= instance.m_pathUnits.m_buffer[(int)((UIntPtr)nextPathId)].m_positionCount - 1 && instance.m_pathUnits.m_buffer[(int)((UIntPtr)nextPathId)].m_nextPathUnit == 0u && vehicleID != 0)
                    {
                        (vehicleAI as IVehicle).ArrivingToDestination(vehicleID, ref vehicleData);
                    }
                }
                pathId = nextPathId;
                positionIdx = (byte)(nextPosIdx << 1);
                pathOffset = newOffset;
                if (index <= 0)
                {
                    vehicleData.m_pathPositionIndex = positionIdx;
                    vehicleData.m_lastPathOffset = pathOffset;
                    vehicleData.m_flags = ((vehicleData.m_flags & ~(Vehicle.Flags.OnGravel | Vehicle.Flags.Underground | Vehicle.Flags.Transition)) | info2.m_setVehicleFlags);
                }
                position = position2;
                laneId = lane2Id;
                lane = lane2;
            }
        }

        public static float RestrictSpeed(float calculatedSpeed, uint laneId, VehicleInfo info)
        {
            if (calculatedSpeed == 0f)
                return calculatedSpeed;

            float speedLimit = RoadManager.instance.m_lanes[laneId].m_speed;
            float curve = NetManager.instance.m_lanes.m_buffer[laneId].m_curve;

            float a = 1000f / (1f + curve * 1000f / info.m_turning) + 2f;
            float b = 8f * speedLimit;
            return Mathf.Min(Mathf.Min(a, b), info.m_maxSpeed);
        }
    }
}
