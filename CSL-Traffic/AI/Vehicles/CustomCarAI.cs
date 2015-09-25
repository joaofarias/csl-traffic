using ColossalFramework;
using ColossalFramework.Math;
using System;
using UnityEngine;

namespace CSL_Traffic
{
    static class CustomCarAI
    {
        public static SpeedData[] sm_speedData = new SpeedData[VehicleManager.MAX_VEHICLE_COUNT];
        public static void SimulationStep(CarAI carAI, ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics)
        {
            uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
            frameData.m_position += frameData.m_velocity * 0.5f;
            frameData.m_swayPosition += frameData.m_swayVelocity * 0.5f;
            float acceleration = carAI.m_info.m_acceleration;
            float braking = carAI.m_info.m_braking;
            float magnitude = frameData.m_velocity.magnitude;
            Vector3 vector = vehicleData.m_targetPos0 - (Vector4)frameData.m_position;
            float sqrMagnitude = vector.sqrMagnitude;
            float num = (magnitude + acceleration) * (0.5f + 0.5f * (magnitude + acceleration) / braking) + carAI.m_info.m_generatedInfo.m_size.z * 0.5f;
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
                    CustomVehicleAI.UpdatePathTargetPositions(carAI, vehicleID, ref vehicleData, frameData.m_position, ref i, 4, num4, num5);
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
                        carAI.UpdateBuildingTargetPositions(vehicleID, ref vehicleData, refPos, leaderID, ref leaderData, ref i, minSqrDistance);
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
                            float num8 = 0.5f * magnitude * magnitude / carAI.m_info.m_braking + carAI.m_info.m_generatedInfo.m_size.z * 0.5f;
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
                /* -------------------- Congestion Changes ------------------------- */
                // Not everything is new. Changes are commented
                if ((ulong)(currentFrameIndex >> 4 & 15u) == (ulong)((long)(leaderID & 15)))
                {
                    bool flag3 = false;
                    uint path = leaderData.m_path;
                    int num9 = b >> 1;
                    int j = 0, count = 0; // the count variable is used to keep track of how many of the next 5 lanes are congested
                    //int j = 0;
                    while (j < 5)
                    {
                        bool flag4;
                        if (PathUnit.GetNextPosition(ref path, ref num9, out pathPos, out flag4))
                        {
                            uint laneID3 = PathManager.GetLaneID(pathPos);
                            if (laneID3 != 0 && !instance.m_lanes.m_buffer[(int)((UIntPtr)laneID3)].CheckSpace(num7))
                            {
                                j++;
                                ++count; // this lane is congested so increase count
                                continue;
                            }
                        }
                        if (flag4)
                        {
                            (carAI as IVehicle).InvalidPath(vehicleID, ref vehicleData, leaderID, ref leaderData);
                            // flag it as not congested and set count to -1 so that it is neither congested nor completely clear
                            // this is needed here because, contrary to the default code, it does not leave the cycle below
                            if ((CSLTraffic.Options & OptionsManager.ModOptions.ImprovedAI) == OptionsManager.ModOptions.ImprovedAI)
                            {
                                flag3 = true;
                                count = -1;
                                break;
                            }
                        }
                        flag3 = true;
                        ++j;
                        if ((CSLTraffic.Options & OptionsManager.ModOptions.ImprovedAI) != OptionsManager.ModOptions.ImprovedAI)
                        {
                            break;
                        }
                        // the default code would leave the cycle at this point since it found a non congested lane.
                        // this has been changed so that vehicles detect congestions a few lanes in advance.
                        // I am yet to test the performance impact this particular "feature" has.
                    }

                    if ((CSLTraffic.Options & OptionsManager.ModOptions.ImprovedAI) == OptionsManager.ModOptions.ImprovedAI)
                    {
                        // if at least 2 out of the next 5 lanes are congested and it hasn't tried to find a new path yet, then calculates a new path and flags it as such
                        // the amounf of congested lanes necessary to calculate a new path can be tweaked to reduce the amount of new paths being calculated, if performance in bigger cities is severely affected
                        if (count >= 2 && (leaderData.m_flags & (Vehicle.Flags)1073741824) == 0)
                        {
                            leaderData.m_flags |= (Vehicle.Flags)1073741824;
                            (carAI as IVehicle).InvalidPath(vehicleID, ref vehicleData, leaderID, ref leaderData);
                        }
                        // if none of the next 5 lanes is congested and the vehicle has already searched for a new path, then it successfully avoided a congestion and the flag is cleared
                        else if (count == 0 && (leaderData.m_flags & (Vehicle.Flags)1073741824) != 0)
                        {
                            leaderData.m_flags &= ~((Vehicle.Flags)1073741824);
                        }
                        // default congestion behavior
                        else if (!flag3)
                            leaderData.m_flags |= Vehicle.Flags.Congestion;
                    }
                    else if (!flag3)
                    {
                        leaderData.m_flags |= Vehicle.Flags.Congestion;
                    }
                }
                /* ----------------------------------------------------------------- */
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
                        num10 = (carAI as IVehicle).CalculateTargetSpeed(vehicleID, ref vehicleData, 1000f, num15);
                        num10 = Mathf.Min(num10, CalculateMaxSpeed(num16, Mathf.Min(vehicleData.m_targetPos0.w, vehicleData.m_targetPos1.w), braking * 0.9f));
                    }
                    else
                    {
                        num10 = Mathf.Min(num10, (carAI as IVehicle).CalculateTargetSpeed(vehicleID, ref vehicleData, 1000f, num15));
                        num10 = Mathf.Min(num10, CalculateMaxSpeed(num16, vehicleData.m_targetPos1.w, braking * 0.9f));
                    }
                    num16 += VectorUtils.LengthXZ(vehicleData.m_targetPos1 - vehicleData.m_targetPos0);
                    num10 = Mathf.Min(num10, CalculateMaxSpeed(num16, vehicleData.m_targetPos2.w, braking * 0.9f));
                    num16 += VectorUtils.LengthXZ(vehicleData.m_targetPos2 - vehicleData.m_targetPos1);
                    num10 = Mathf.Min(num10, CalculateMaxSpeed(num16, vehicleData.m_targetPos3.w, braking * 0.9f));
                    num16 += VectorUtils.LengthXZ(vehicleData.m_targetPos3 - vehicleData.m_targetPos2);
                    if (vehicleData.m_targetPos3.w < 0.01f)
                    {
                        num16 = Mathf.Max(0f, num16 - carAI.m_info.m_generatedInfo.m_size.z * 0.5f);
                    }
                    num10 = Mathf.Min(num10, CalculateMaxSpeed(num16, 0f, braking * 0.9f));
                    if (!DisableCollisionCheck(leaderID, ref leaderData))
                    {
                        CustomCarAI.CheckOtherVehicles(carAI, vehicleID, ref vehicleData, ref frameData, ref num10, ref flag5, ref zero, num, braking * 0.9f, lodPhysics);
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
                if (magnitude < 0.1f && flag && carAI.ArriveAtDestination(leaderID, ref leaderData))
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
                if ((vehicleData.m_blockCounter == 100 || vehicleData.m_blockCounter == 150) && (CSLTraffic.Options & OptionsManager.ModOptions.NoDespawn) == OptionsManager.ModOptions.NoDespawn)
                    vehicleData.m_blockCounter++;
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
            frameData.m_swayVelocity = frameData.m_swayVelocity * (1f - carAI.m_info.m_dampers) - a2 * (1f - carAI.m_info.m_springs) - frameData.m_swayPosition * carAI.m_info.m_springs;
            frameData.m_swayPosition += frameData.m_swayVelocity * 0.5f;
            frameData.m_steerAngle = num12;
            frameData.m_travelDistance += vector3.z;
            frameData.m_lightIntensity.x = 5f;
            frameData.m_lightIntensity.y = ((a2.z >= -0.1f) ? 0.5f : 5f);
            frameData.m_lightIntensity.z = ((num12 >= -0.1f || !flag7) ? 0f : 5f);
            frameData.m_lightIntensity.w = ((num12 <= 0.1f || !flag7) ? 0f : 5f);
            frameData.m_underground = ((vehicleData.m_flags & Vehicle.Flags.Underground) != Vehicle.Flags.None);
            frameData.m_transition = ((vehicleData.m_flags & Vehicle.Flags.Transition) != Vehicle.Flags.None);
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
        }

        public static bool StartPathFind(CarAI carAI, ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, RoadManager.VehicleType vehicleType)
        {
            VehicleInfo info = carAI.m_info;
            bool allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != Vehicle.Flags.None;
            PathUnit.Position startPosA;
            PathUnit.Position startPosB;
            float num;
            float num2;
            PathUnit.Position endPosA;
            PathUnit.Position endPosB;
            float num3;
            float num4;
            if (CustomPathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, allowUnderground, false, 32f, out startPosA, out startPosB, out num, out num2, vehicleType) && CustomPathManager.FindPathPosition(endPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, false, false, 32f, out endPosA, out endPosB, out num3, out num4, vehicleType))
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
                bool createPathResult;
                CustomPathManager customPathManager = Singleton<PathManager>.instance as CustomPathManager;
                if (customPathManager != null)
                    createPathResult = customPathManager.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle, info.m_vehicleType, 20000f, (carAI as IVehicle).IsHeavyVehicle(), (carAI as IVehicle).IgnoreBlocked(vehicleID, ref vehicleData), false, false, vehicleType);
                else
                    createPathResult = Singleton<PathManager>.instance.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle, info.m_vehicleType, 20000f, (carAI as IVehicle).IsHeavyVehicle(), (carAI as IVehicle).IgnoreBlocked(vehicleID, ref vehicleData), false, false);
                if (createPathResult)
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

        private static float CalculateMaxSpeed(float targetDistance, float targetSpeed, float maxBraking)
        {
            float num = 0.5f * maxBraking;
            float num2 = num + targetSpeed;
            return Mathf.Sqrt(Mathf.Max(0f, num2 * num2 + 2f * targetDistance * maxBraking)) - num;
        }

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

        private static void CheckOtherVehicles(CarAI carAI, ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ref float maxSpeed, ref bool blocked, ref Vector3 collisionPush, float maxDistance, float maxBraking, int lodPhysics)
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
                        num5 = CustomCarAI.CheckOtherVehicle(vehicleID, ref vehicleData, ref frameData, ref maxSpeed, ref blocked, ref collisionPush, maxBraking, num5, ref instance.m_vehicles.m_buffer[(int)num5], min, max, lodPhysics);
                        if (++num6 > 16384)
                        {
                            CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                            break;
                        }
                    }
                }
            }
            if (lodPhysics == 0/* && (CSLTraffic.Options & OptionsManager.ModOptions.noStopForCrossing) != OptionsManager.ModOptions.noStopForCrossing*/)
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
                                        num12 = CustomCarAI.CheckCitizen(vehicleID, ref vehicleData, segment, num7, magnitude, ref maxSpeed, ref blocked, maxBraking, num12, ref instance2.m_instances.m_buffer[(int)num12], min, max);
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

        private static ushort CheckOtherVehicle(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ref float maxSpeed, ref bool blocked, ref Vector3 collisionPush, float maxBraking, ushort otherID, ref Vehicle otherData, Vector3 min, Vector3 max, int lodPhysics)
        {
            if (otherID != vehicleID && vehicleData.m_leadingVehicle != otherID && vehicleData.m_trailingVehicle != otherID)
            {
                VehicleInfo info = otherData.Info;
                if (info.m_vehicleType == VehicleInfo.VehicleType.Bicycle)
                {
                    return otherData.m_nextGridVehicle;
                }
                if (((vehicleData.m_flags | otherData.m_flags) & Vehicle.Flags.Transition) == Vehicle.Flags.None && (vehicleData.m_flags & Vehicle.Flags.Underground) != (otherData.m_flags & Vehicle.Flags.Underground))
                {
                    return otherData.m_nextGridVehicle;
                }
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

        // CHECKME: check if this method allows to make people get away from traffic
        private static ushort CheckCitizen(ushort vehicleID, ref Vehicle vehicleData, Segment3 segment, float lastLen, float nextLen, ref float maxSpeed, ref bool blocked, float maxBraking, ushort otherID, ref CitizenInstance otherData, Vector3 min, Vector3 max)
        {
            if ((vehicleData.m_flags & Vehicle.Flags.Transition) == Vehicle.Flags.None && (otherData.m_flags & CitizenInstance.Flags.Transition) == CitizenInstance.Flags.None && (vehicleData.m_flags & Vehicle.Flags.Underground) != Vehicle.Flags.None != ((otherData.m_flags & CitizenInstance.Flags.Underground) != CitizenInstance.Flags.None))
            {
                return otherData.m_nextGridInstance;
            }

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

        public struct SpeedData
        {
            public uint currentPath;
            public float speedMultiplier;

            public void SetRandomSpeedMultiplier(float rangeMin = 0.75f, float rangeMax = 1.25f)
            {
                speedMultiplier = UnityEngine.Random.Range(rangeMin, rangeMax);
            }

            public void ApplySpeedMultiplier(VehicleInfo vehicle)
            {
                vehicle.m_acceleration *= speedMultiplier;
                //vehicle.m_braking *= speedMultiplier;
                //vehicle.m_turning *= speedMultiplier;
                vehicle.m_maxSpeed *= speedMultiplier;
            }

            public void RestoreVehicleSpeed(VehicleInfo vehicle)
            {
                vehicle.m_acceleration /= speedMultiplier;
                //vehicle.m_braking /= speedMultiplier;
                //vehicle.m_turning /= speedMultiplier;
                vehicle.m_maxSpeed /= speedMultiplier;
            }
        }
    }
}
