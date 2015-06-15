using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using CSL_Traffic.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace CSL_Traffic
{
    /*
     * This is the class responsible for pathfinding. It's all in here since none of the methods can be overwritten.
     * There's a lot of small changes here and there to make it generate a correct path for the service vehicles using pedestrian paths.
     */
    class CustomPathFind : PathFind
    {
        private struct BufferItem
        {
            public PathUnit.Position m_position;
            public float m_comparisonValue;
            public float m_methodDistance;
            public uint m_laneID;
            public NetInfo.Direction m_direction;
            public NetInfo.LaneType m_lanesUsed;
        }

        FieldInfo fi_pathUnits;
        private Array32<PathUnit> m_pathUnits
        {
            get { return (Array32<PathUnit>)fi_pathUnits.GetValue(this); }
            set { fi_pathUnits.SetValue(this, value); }
        }
        FieldInfo fi_queueFirst;
        private uint m_queueFirst
        {
            get { return (uint)fi_queueFirst.GetValue(this); }
            set { fi_queueFirst.SetValue(this, value); }
        }
        FieldInfo fi_queueLast;
        private uint m_queueLast
        {
            get { return (uint)fi_queueLast.GetValue(this); }
            set { fi_queueLast.SetValue(this, value); }
        }
        FieldInfo fi_calculating;
        private uint m_calculating
        {
            get { return (uint)fi_calculating.GetValue(this); }
            set { fi_calculating.SetValue(this, value); }
        }
        FieldInfo fi_queueLock;
        private object m_queueLock
        {
            get { return fi_queueLock.GetValue(this); }
            set { fi_queueLock.SetValue(this, value); }
        }
        private object m_bufferLock;
        FieldInfo fi_pathFindThread;
        private Thread m_pathFindThread
        {
            get { return (Thread)fi_pathFindThread.GetValue(this); }
            set { fi_pathFindThread.SetValue(this, value); }
        }
        FieldInfo fi_terminated;
        private bool m_terminated
        {
            get { return (bool)fi_terminated.GetValue(this); }
            set { fi_terminated.SetValue(this, value); }
        }

        private int m_bufferMinPos;
        private int m_bufferMaxPos;
        private uint[] m_laneLocation;
        private PathUnit.Position[] m_laneTarget;
        private BufferItem[] m_buffer;
        private int[] m_bufferMin;
        private int[] m_bufferMax;
        private float m_maxLength;
        private uint m_startLaneA;
        private uint m_startLaneB;
        private uint m_endLaneA;
        private uint m_endLaneB;
        private uint m_vehicleLane;
        private byte m_startOffsetA;
        private byte m_startOffsetB;
        private byte m_vehicleOffset;
        private bool m_isHeavyVehicle;
        private bool m_ignoreBlocked;
        private bool m_stablePath;
        private Randomizer m_pathRandomizer;
        private uint m_pathFindIndex;
        private NetInfo.LaneType m_laneTypes;
        private VehicleInfo.VehicleType m_vehicleTypes;
        private RoadManager.VehicleType m_vehicleType;
        private Dictionary<uint, RoadManager.VehicleType> m_pathVehicleType;
        private bool m_prioritizeBusLanes;

        private void Awake()
        {
            Type pathFindType = typeof(PathFind);
            fi_pathUnits = pathFindType.GetFieldByName("m_pathUnits");
            fi_queueFirst = pathFindType.GetFieldByName("m_queueFirst");
            fi_queueLast = pathFindType.GetFieldByName("m_queueLast");
            fi_calculating = pathFindType.GetFieldByName("m_calculating");
            fi_queueLock = pathFindType.GetFieldByName("m_queueLock");
            fi_pathFindThread = pathFindType.GetFieldByName("m_pathFindThread");
            fi_terminated = pathFindType.GetFieldByName("m_terminated");

            m_pathfindProfiler = new ThreadProfiler();
            m_laneLocation = new uint[262144];
            m_laneTarget = new PathUnit.Position[262144];
            m_buffer = new BufferItem[65536];
            m_bufferMin = new int[1024];
            m_bufferMax = new int[1024];
            m_queueLock = new object();
            m_pathVehicleType = new Dictionary<uint, RoadManager.VehicleType>();
            m_bufferLock = Singleton<PathManager>.instance.m_bufferLock;
            m_pathUnits = Singleton<PathManager>.instance.m_pathUnits;
            m_pathFindThread = new Thread(new ThreadStart(PathFindThread));
            m_pathFindThread.Name = "Pathfind";
            m_pathFindThread.Priority = SimulationManager.SIMULATION_PRIORITY;
            m_pathFindThread.Start();
            if (!m_pathFindThread.IsAlive)
            {
                CODebugBase<LogChannel>.Error(LogChannel.Core, "Path find thread failed to start!");
            }
        }

        private void OnDestroy()
        {
            while (!Monitor.TryEnter(m_queueLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
            {
            }
            try
            {
                m_terminated = true;
                Monitor.PulseAll(m_queueLock);
            }
            finally
            {
                Monitor.Exit(m_queueLock);
            }
        }
        public bool CalculatePath(uint unit, bool skipQueue, RoadManager.VehicleType vehicleType)
        {
            if (Singleton<PathManager>.instance.AddPathReference(unit))
            {
                while (!Monitor.TryEnter(m_queueLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
                {
                }
                try
                {
                    if (skipQueue)
                    {
                        if (m_queueLast == 0u)
                        {
                            m_queueLast = unit;
                        }
                        else
                        {
                            m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_nextPathUnit = m_queueFirst;
                        }
                        m_queueFirst = unit;
                    }
                    else
                    {
                        if (m_queueLast == 0u)
                        {
                            m_queueFirst = unit;
                        }
                        else
                        {
                            m_pathUnits.m_buffer[(int)((UIntPtr)m_queueLast)].m_nextPathUnit = unit;
                        }
                        m_queueLast = unit;
                    }

                    m_pathVehicleType[unit] = vehicleType;

                    PathUnit[] expr_BD_cp_0 = m_pathUnits.m_buffer;
                    UIntPtr expr_BD_cp_1 = (UIntPtr)unit;
                    expr_BD_cp_0[(int)expr_BD_cp_1].m_pathFindFlags = (byte)(expr_BD_cp_0[(int)expr_BD_cp_1].m_pathFindFlags | 1);
                    m_queuedPathFindCount++;
                    Monitor.Pulse(m_queueLock);
                }
                finally
                {
                    Monitor.Exit(m_queueLock);
                }
                return true;
            }
            return false;
        }

        //public void WaitForAllPaths()
        //{
        //    while (!Monitor.TryEnter(this.m_queueLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
        //    {
        //    }
        //    try
        //    {
        //        while ((this.m_queueFirst != 0u || this.m_calculating != 0u) && !this.m_terminated)
        //        {
        //            Monitor.Wait(this.m_queueLock);
        //        }
        //    }
        //    finally
        //    {
        //        Monitor.Exit(this.m_queueLock);
        //    }
        //}

        private void PathFindImplementation(uint unit, ref PathUnit data)
        {
            NetManager instance = Singleton<NetManager>.instance;
            m_laneTypes = (NetInfo.LaneType)m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_laneTypes;
            m_vehicleTypes = (VehicleInfo.VehicleType)m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_vehicleTypes;
            m_maxLength = m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_length;
            m_pathFindIndex = (m_pathFindIndex + 1u & 32767u);
            m_pathRandomizer = new Randomizer(unit);
            m_isHeavyVehicle = ((m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_simulationFlags & 16) != 0);
            m_ignoreBlocked = ((m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_simulationFlags & 32) != 0);
            m_stablePath = ((m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_simulationFlags & 64) != 0);

            if (!m_pathVehicleType.TryGetValue(unit, out m_vehicleType))
            {
                //if ((m_laneTypes & NetInfo.LaneType.Pedestrian) == NetInfo.LaneType.Pedestrian)
                m_vehicleType = RoadManager.VehicleType.PassengerCar;
                //else
                //	m_vehicleType = RoadManager.VehicleType.None;
            }
            if ((CSLTraffic.Options & OptionsManager.ModOptions.ImprovedAI) == OptionsManager.ModOptions.ImprovedAI)
                m_prioritizeBusLanes = (m_vehicleType & (RoadManager.VehicleType.Bus | RoadManager.VehicleType.Emergency)) != RoadManager.VehicleType.None;
            else
                m_prioritizeBusLanes = false;

            int num = m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_positionCount & 15;
            int num2 = m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_positionCount >> 4;
            BufferItem bufferItem;
            if (data.m_position00.m_segment != 0 && num >= 1)
            {
                //if (NetManager.instance.m_segments.m_buffer[data.m_position00.m_segment].Info == null)
                //{
                //	this.m_pathUnits.m_buffer[unit].m_pathFindFlags |= 8;
                //	return;
                //}

                m_startLaneA = PathManager.GetLaneID(data.m_position00);
                m_startOffsetA = data.m_position00.m_offset;
                bufferItem.m_laneID = m_startLaneA;
                bufferItem.m_position = data.m_position00;
                GetLaneDirection(data.m_position00, out bufferItem.m_direction, out bufferItem.m_lanesUsed);
                bufferItem.m_comparisonValue = 0f;

            }
            else
            {
                m_startLaneA = 0u;
                m_startOffsetA = 0;
                bufferItem = default(BufferItem);
            }
            BufferItem bufferItem2;
            if (data.m_position02.m_segment != 0 && num >= 3)
            {
                //if (NetManager.instance.m_segments.m_buffer[data.m_position02.m_segment].Info == null)
                //{
                //	this.m_pathUnits.m_buffer[unit].m_pathFindFlags |= 8;
                //	return;
                //}

                m_startLaneB = PathManager.GetLaneID(data.m_position02);
                m_startOffsetB = data.m_position02.m_offset;
                bufferItem2.m_laneID = m_startLaneB;
                bufferItem2.m_position = data.m_position02;
                GetLaneDirection(data.m_position02, out bufferItem2.m_direction, out bufferItem2.m_lanesUsed);
                bufferItem2.m_comparisonValue = 0f;
            }
            else
            {
                m_startLaneB = 0u;
                m_startOffsetB = 0;
                bufferItem2 = default(BufferItem);
            }
            BufferItem bufferItem3;
            if (data.m_position01.m_segment != 0 && num >= 2)
            {
                //if (NetManager.instance.m_segments.m_buffer[data.m_position01.m_segment].Info == null)
                //{
                //	this.m_pathUnits.m_buffer[unit].m_pathFindFlags |= 8;
                //	return;
                //}

                m_endLaneA = PathManager.GetLaneID(data.m_position01);
                bufferItem3.m_laneID = m_endLaneA;
                bufferItem3.m_position = data.m_position01;
                GetLaneDirection(data.m_position01, out bufferItem3.m_direction, out bufferItem3.m_lanesUsed);
                bufferItem3.m_methodDistance = 0f;
                bufferItem3.m_comparisonValue = 0f;
            }
            else
            {
                m_endLaneA = 0u;
                bufferItem3 = default(BufferItem);
            }
            BufferItem bufferItem4;
            if (data.m_position03.m_segment != 0 && num >= 4)
            {
                //if (NetManager.instance.m_segments.m_buffer[data.m_position03.m_segment].Info == null)
                //{
                //	this.m_pathUnits.m_buffer[unit].m_pathFindFlags |= 8;
                //	return;
                //}

                m_endLaneB = PathManager.GetLaneID(data.m_position03);
                bufferItem4.m_laneID = m_endLaneB;
                bufferItem4.m_position = data.m_position03;
                GetLaneDirection(data.m_position03, out bufferItem4.m_direction, out bufferItem4.m_lanesUsed);
                bufferItem4.m_methodDistance = 0f;
                bufferItem4.m_comparisonValue = 0f;
            }
            else
            {
                m_endLaneB = 0u;
                bufferItem4 = default(BufferItem);
            }
            if (data.m_position11.m_segment != 0 && num2 >= 1)
            {
                m_vehicleLane = PathManager.GetLaneID(data.m_position11);
                m_vehicleOffset = data.m_position11.m_offset;
            }
            else
            {
                m_vehicleLane = 0u;
                m_vehicleOffset = 0;
            }
            BufferItem bufferItem5 = default(BufferItem);
            byte b = 0;
            m_bufferMinPos = 0;
            m_bufferMaxPos = -1;
            if (m_pathFindIndex == 0u)
            {
                uint num3 = 4294901760u;
                for (int i = 0; i < 262144; i++)
                {
                    m_laneLocation[i] = num3;
                }
            }
            for (int j = 0; j < 1024; j++)
            {
                m_bufferMin[j] = 0;
                m_bufferMax[j] = -1;
            }
            if (bufferItem3.m_position.m_segment != 0)
            {
                m_bufferMax[0]++;
                m_buffer[++m_bufferMaxPos] = bufferItem3;
            }
            if (bufferItem4.m_position.m_segment != 0)
            {
                m_bufferMax[0]++;
                m_buffer[++m_bufferMaxPos] = bufferItem4;
            }
            bool flag = false;
            while (m_bufferMinPos <= m_bufferMaxPos)
            {
                int num4 = m_bufferMin[m_bufferMinPos];
                int num5 = m_bufferMax[m_bufferMinPos];
                if (num4 > num5)
                {
                    m_bufferMinPos++;
                }
                else
                {
                    m_bufferMin[m_bufferMinPos] = num4 + 1;
                    BufferItem bufferItem6 = m_buffer[(m_bufferMinPos << 6) + num4];
                    if (bufferItem6.m_position.m_segment == bufferItem.m_position.m_segment && bufferItem6.m_position.m_lane == bufferItem.m_position.m_lane)
                    {
                        if ((byte)(bufferItem6.m_direction & NetInfo.Direction.Forward) != 0 && bufferItem6.m_position.m_offset >= m_startOffsetA)
                        {
                            bufferItem5 = bufferItem6;
                            b = m_startOffsetA;
                            flag = true;
                            break;
                        }
                        if ((byte)(bufferItem6.m_direction & NetInfo.Direction.Backward) != 0 && bufferItem6.m_position.m_offset <= m_startOffsetA)
                        {
                            bufferItem5 = bufferItem6;
                            b = m_startOffsetA;
                            flag = true;
                            break;
                        }
                    }
                    if (bufferItem6.m_position.m_segment == bufferItem2.m_position.m_segment && bufferItem6.m_position.m_lane == bufferItem2.m_position.m_lane)
                    {
                        if ((byte)(bufferItem6.m_direction & NetInfo.Direction.Forward) != 0 && bufferItem6.m_position.m_offset >= m_startOffsetB)
                        {
                            bufferItem5 = bufferItem6;
                            b = m_startOffsetB;
                            flag = true;
                            break;
                        }
                        if ((byte)(bufferItem6.m_direction & NetInfo.Direction.Backward) != 0 && bufferItem6.m_position.m_offset <= m_startOffsetB)
                        {
                            bufferItem5 = bufferItem6;
                            b = m_startOffsetB;
                            flag = true;
                            break;
                        }
                    }
                    if ((byte)(bufferItem6.m_direction & NetInfo.Direction.Forward) != 0)
                    {
                        ushort startNode = instance.m_segments.m_buffer[bufferItem6.m_position.m_segment].m_startNode;
                        ProcessItem(bufferItem6, startNode, ref instance.m_nodes.m_buffer[startNode], 0, false);
                    }
                    if ((byte)(bufferItem6.m_direction & NetInfo.Direction.Backward) != 0)
                    {
                        ushort endNode = instance.m_segments.m_buffer[bufferItem6.m_position.m_segment].m_endNode;
                        ProcessItem(bufferItem6, endNode, ref instance.m_nodes.m_buffer[endNode], 255, false);
                    }
                    int num6 = 0;
                    ushort num7 = instance.m_lanes.m_buffer[(int)((UIntPtr)bufferItem6.m_laneID)].m_nodes;
                    if (num7 != 0)
                    {
                        ushort startNode2 = instance.m_segments.m_buffer[bufferItem6.m_position.m_segment].m_startNode;
                        ushort endNode2 = instance.m_segments.m_buffer[bufferItem6.m_position.m_segment].m_endNode;
                        bool flag2 = ((instance.m_nodes.m_buffer[startNode2].m_flags | instance.m_nodes.m_buffer[endNode2].m_flags) & NetNode.Flags.Disabled) != NetNode.Flags.None;
                        while (num7 != 0)
                        {
                            NetInfo.Direction direction = NetInfo.Direction.None;
                            byte laneOffset = instance.m_nodes.m_buffer[num7].m_laneOffset;
                            if (laneOffset <= bufferItem6.m_position.m_offset)
                            {
                                direction |= NetInfo.Direction.Forward;
                            }
                            if (laneOffset >= bufferItem6.m_position.m_offset)
                            {
                                direction |= NetInfo.Direction.Backward;
                            }
                            if ((byte)(bufferItem6.m_direction & direction) != 0 && (!flag2 || (instance.m_nodes.m_buffer[num7].m_flags & NetNode.Flags.Disabled) != NetNode.Flags.None))
                            {
                                ProcessItem(bufferItem6, num7, ref instance.m_nodes.m_buffer[num7], laneOffset, true);
                            }
                            num7 = instance.m_nodes.m_buffer[num7].m_nextLaneNode;
                            if (++num6 == 32768)
                            {
                                break;
                            }
                        }
                    }
                }
            }
            if (!flag)
            {
                PathUnit[] expr_8D5_cp_0 = m_pathUnits.m_buffer;
                UIntPtr expr_8D5_cp_1 = (UIntPtr)unit;
                expr_8D5_cp_0[(int)expr_8D5_cp_1].m_pathFindFlags = (byte)(expr_8D5_cp_0[(int)expr_8D5_cp_1].m_pathFindFlags | 8);
                return;
            }
            float num8 = bufferItem5.m_comparisonValue * m_maxLength;
            m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_length = num8;
            uint num9 = unit;
            int num10 = 0;
            int num11 = 0;
            PathUnit.Position position = bufferItem5.m_position;
            if ((position.m_segment != bufferItem3.m_position.m_segment || position.m_lane != bufferItem3.m_position.m_lane || position.m_offset != bufferItem3.m_position.m_offset) && (position.m_segment != bufferItem4.m_position.m_segment || position.m_lane != bufferItem4.m_position.m_lane || position.m_offset != bufferItem4.m_position.m_offset))
            {
                if (b != position.m_offset)
                {
                    PathUnit.Position position2 = position;
                    position2.m_offset = b;
                    m_pathUnits.m_buffer[(int)((UIntPtr)num9)].SetPosition(num10++, position2);
                }
                m_pathUnits.m_buffer[(int)((UIntPtr)num9)].SetPosition(num10++, position);
                position = m_laneTarget[(int)((UIntPtr)bufferItem5.m_laneID)];
            }
            for (int k = 0; k < 262144; k++)
            {
                m_pathUnits.m_buffer[(int)((UIntPtr)num9)].SetPosition(num10++, position);
                if ((position.m_segment == bufferItem3.m_position.m_segment && position.m_lane == bufferItem3.m_position.m_lane && position.m_offset == bufferItem3.m_position.m_offset) || (position.m_segment == bufferItem4.m_position.m_segment && position.m_lane == bufferItem4.m_position.m_lane && position.m_offset == bufferItem4.m_position.m_offset))
                {
                    m_pathUnits.m_buffer[(int)((UIntPtr)num9)].m_positionCount = (byte)num10;
                    num11 += num10;
                    if (num11 != 0)
                    {
                        num9 = m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_nextPathUnit;
                        num10 = m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_positionCount;
                        int num12 = 0;
                        while (num9 != 0u)
                        {
                            m_pathUnits.m_buffer[(int)((UIntPtr)num9)].m_length = num8 * (num11 - num10) / num11;
                            num10 += m_pathUnits.m_buffer[(int)((UIntPtr)num9)].m_positionCount;
                            num9 = m_pathUnits.m_buffer[(int)((UIntPtr)num9)].m_nextPathUnit;
                            if (++num12 >= 262144)
                            {
                                CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                                break;
                            }
                        }
                    }
                    PathUnit[] expr_BE2_cp_0 = m_pathUnits.m_buffer;
                    UIntPtr expr_BE2_cp_1 = (UIntPtr)unit;
                    expr_BE2_cp_0[(int)expr_BE2_cp_1].m_pathFindFlags = (byte)(expr_BE2_cp_0[(int)expr_BE2_cp_1].m_pathFindFlags | 4);
                    return;
                }
                if (num10 == 12)
                {
                    while (!Monitor.TryEnter(m_bufferLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
                    {
                    }
                    uint num13;
                    try
                    {
                        if (!m_pathUnits.CreateItem(out num13, ref m_pathRandomizer))
                        {
                            PathUnit[] expr_CE1_cp_0 = m_pathUnits.m_buffer;
                            UIntPtr expr_CE1_cp_1 = (UIntPtr)unit;
                            expr_CE1_cp_0[(int)expr_CE1_cp_1].m_pathFindFlags = (byte)(expr_CE1_cp_0[(int)expr_CE1_cp_1].m_pathFindFlags | 8);
                            return;
                        }
                        m_pathUnits.m_buffer[(int)((UIntPtr)num13)] = m_pathUnits.m_buffer[(int)((UIntPtr)num9)];
                        m_pathUnits.m_buffer[(int)((UIntPtr)num13)].m_referenceCount = 1;
                        m_pathUnits.m_buffer[(int)((UIntPtr)num13)].m_pathFindFlags = 4;
                        m_pathUnits.m_buffer[(int)((UIntPtr)num9)].m_nextPathUnit = num13;
                        m_pathUnits.m_buffer[(int)((UIntPtr)num9)].m_positionCount = (byte)num10;
                        num11 += num10;
                        Singleton<PathManager>.instance.m_pathUnitCount = (int)(m_pathUnits.ItemCount() - 1u);
                    }
                    finally
                    {
                        Monitor.Exit(m_bufferLock);
                    }
                    num9 = num13;
                    num10 = 0;
                }
                uint laneID = PathManager.GetLaneID(position);
                position = m_laneTarget[(int)((UIntPtr)laneID)];
            }
            PathUnit[] expr_D65_cp_0 = m_pathUnits.m_buffer;
            UIntPtr expr_D65_cp_1 = (UIntPtr)unit;
            expr_D65_cp_0[(int)expr_D65_cp_1].m_pathFindFlags = (byte)(expr_D65_cp_0[(int)expr_D65_cp_1].m_pathFindFlags | 8);
        }
        private void ProcessItem(BufferItem item, ushort nodeID, ref NetNode node, byte connectOffset, bool isMiddle)
        {
            NetManager instance = Singleton<NetManager>.instance;
            bool flag = false;
            bool flag2 = false;
            int num = 0;
            NetInfo info = instance.m_segments.m_buffer[item.m_position.m_segment].Info;
            if (item.m_position.m_lane < info.m_lanes.Length)
            {
                NetInfo.Lane lane = info.m_lanes[item.m_position.m_lane];
                flag = (lane.m_laneType == NetInfo.LaneType.Pedestrian);
                flag2 = (lane.m_laneType == NetInfo.LaneType.Vehicle && lane.m_vehicleType == VehicleInfo.VehicleType.Bicycle);
                if ((byte)(lane.m_finalDirection & NetInfo.Direction.Forward) != 0)
                {
                    num = lane.m_similarLaneIndex;
                }
                else
                {
                    num = lane.m_similarLaneCount - lane.m_similarLaneIndex - 1;
                }
            }
            if (isMiddle)
            {
                for (int i = 0; i < 8; i++)
                {
                    ushort segment = node.GetSegment(i);
                    if (segment != 0)
                    {
                        ProcessItem(item, nodeID, segment, ref instance.m_segments.m_buffer[segment], ref num, connectOffset, !flag, flag);
                    }
                }
            }
            else if (flag)
            {
                ushort segment2 = item.m_position.m_segment;
                int lane2 = item.m_position.m_lane;
                if (node.Info.m_class.m_service != ItemClass.Service.Beautification)
                {
                    bool flag3 = (node.m_flags & (NetNode.Flags.End | NetNode.Flags.Bend | NetNode.Flags.Junction)) != NetNode.Flags.None;
                    int laneIndex;
                    int laneIndex2;
                    uint num2;
                    uint num3;
                    instance.m_segments.m_buffer[segment2].GetLeftAndRightLanes(nodeID, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, lane2, out laneIndex, out laneIndex2, out num2, out num3);
                    ushort num4 = segment2;
                    ushort num5 = segment2;
                    if (num2 == 0u || num3 == 0u)
                    {
                        ushort leftSegment;
                        ushort rightSegment;
                        instance.m_segments.m_buffer[segment2].GetLeftAndRightSegments(nodeID, out leftSegment, out rightSegment);
                        int num6 = 0;
                        while (leftSegment != 0 && leftSegment != segment2 && num2 == 0u)
                        {
                            int num7;
                            int num8;
                            uint num9;
                            uint num10;
                            instance.m_segments.m_buffer[leftSegment].GetLeftAndRightLanes(nodeID, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, -1, out num7, out num8, out num9, out num10);
                            if (num10 != 0u)
                            {
                                num4 = leftSegment;
                                laneIndex = num8;
                                num2 = num10;
                            }
                            else
                            {
                                leftSegment = instance.m_segments.m_buffer[leftSegment].GetLeftSegment(nodeID);
                            }
                            if (++num6 == 8)
                            {
                                break;
                            }
                        }
                        num6 = 0;
                        while (rightSegment != 0 && rightSegment != segment2 && num3 == 0u)
                        {
                            int num11;
                            int num12;
                            uint num13;
                            uint num14;
                            instance.m_segments.m_buffer[rightSegment].GetLeftAndRightLanes(nodeID, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, -1, out num11, out num12, out num13, out num14);
                            if (num13 != 0u)
                            {
                                num5 = rightSegment;
                                laneIndex2 = num11;
                                num3 = num13;
                            }
                            else
                            {
                                rightSegment = instance.m_segments.m_buffer[rightSegment].GetRightSegment(nodeID);
                            }
                            if (++num6 == 8)
                            {
                                break;
                            }
                        }
                    }
                    if (num2 != 0u && (num4 != segment2 || flag3))
                    {
                        ProcessItem(item, nodeID, num4, ref instance.m_segments.m_buffer[num4], connectOffset, laneIndex, num2);
                    }
                    if (num3 != 0u && num3 != num2 && (num5 != segment2 || flag3))
                    {
                        ProcessItem(item, nodeID, num5, ref instance.m_segments.m_buffer[num5], connectOffset, laneIndex2, num3);
                    }
                    int laneIndex3;
                    uint lane3;
                    if ((m_vehicleTypes & VehicleInfo.VehicleType.Bicycle) != VehicleInfo.VehicleType.None && instance.m_segments.m_buffer[segment2].GetClosestLane(item.m_position.m_lane, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Bicycle, out laneIndex3, out lane3))
                    {
                        ProcessItem(item, nodeID, segment2, ref instance.m_segments.m_buffer[segment2], connectOffset, laneIndex3, lane3);
                    }
                }
                else
                {
                    for (int j = 0; j < 8; j++)
                    {
                        ushort segment3 = node.GetSegment(j);
                        if (segment3 != 0 && segment3 != segment2)
                        {
                            ProcessItem(item, nodeID, segment3, ref instance.m_segments.m_buffer[segment3], ref num, connectOffset, false, true);
                        }
                    }
                }
                NetInfo.LaneType laneType = m_laneTypes & ~NetInfo.LaneType.Pedestrian;
                VehicleInfo.VehicleType vehicleType = m_vehicleTypes & ~VehicleInfo.VehicleType.Bicycle;
                laneType &= ~(item.m_lanesUsed & NetInfo.LaneType.Vehicle);
                int num15;
                uint lane4;
                if (laneType != NetInfo.LaneType.None && vehicleType != VehicleInfo.VehicleType.None && instance.m_segments.m_buffer[segment2].GetClosestLane(lane2, laneType, vehicleType, out num15, out lane4))
                {
                    NetInfo.Lane lane5 = info.m_lanes[num15];
                    byte connectOffset2;
                    if ((instance.m_segments.m_buffer[segment2].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None == ((byte)(lane5.m_finalDirection & NetInfo.Direction.Backward) != 0))
                    {
                        connectOffset2 = 1;
                    }
                    else
                    {
                        connectOffset2 = 254;
                    }
                    ProcessItem(item, nodeID, segment2, ref instance.m_segments.m_buffer[segment2], connectOffset2, num15, lane4);
                }
            }
            else
            {
                bool flag4 = (node.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
                bool flag5 = (byte)(m_laneTypes & NetInfo.LaneType.Pedestrian) != 0;
                bool enablePedestrian = false;
                byte connectOffset3 = 0;
                if (flag5)
                {
                    if (flag2)
                    {
                        connectOffset3 = connectOffset;
                        enablePedestrian = (node.Info.m_class.m_service == ItemClass.Service.Beautification);
                    }
                    else if (m_vehicleLane != 0u)
                    {
                        if (m_vehicleLane != item.m_laneID)
                        {
                            flag5 = false;
                        }
                        else
                        {
                            connectOffset3 = m_vehicleOffset;
                        }
                    }
                    else if (m_stablePath)
                    {
                        connectOffset3 = 128;
                    }
                    else
                    {
                        connectOffset3 = (byte)m_pathRandomizer.UInt32(1u, 254u);
                    }
                }
                ushort num16 = instance.m_segments.m_buffer[item.m_position.m_segment].GetRightSegment(nodeID);
                for (int k = 0; k < 8; k++)
                {
                    if (num16 == 0 || num16 == item.m_position.m_segment)
                    {
                        break;
                    }
                    if (ProcessItem(item, nodeID, num16, ref instance.m_segments.m_buffer[num16], ref num, connectOffset, true, enablePedestrian))
                    {
                        flag4 = true;
                    }
                    num16 = instance.m_segments.m_buffer[num16].GetRightSegment(nodeID);
                }
                if (flag4)
                {
                    num16 = item.m_position.m_segment;
                    ProcessItem(item, nodeID, num16, ref instance.m_segments.m_buffer[num16], ref num, connectOffset, true, false);
                }
                if (flag5)
                {
                    num16 = item.m_position.m_segment;
                    int laneIndex4;
                    uint lane6;
                    if (instance.m_segments.m_buffer[num16].GetClosestLane(item.m_position.m_lane, NetInfo.LaneType.Pedestrian, m_vehicleTypes, out laneIndex4, out lane6))
                    {
                        ProcessItem(item, nodeID, num16, ref instance.m_segments.m_buffer[num16], connectOffset3, laneIndex4, lane6);
                    }
                }
            }
            if (node.m_lane != 0u)
            {
                bool targetDisabled = (node.m_flags & NetNode.Flags.Disabled) != NetNode.Flags.None;
                ushort segment4 = instance.m_lanes.m_buffer[(int)((UIntPtr)node.m_lane)].m_segment;
                if (segment4 != 0 && segment4 != item.m_position.m_segment)
                {
                    ProcessItem(item, nodeID, targetDisabled, segment4, ref instance.m_segments.m_buffer[segment4], node.m_lane, node.m_laneOffset, connectOffset);
                }
            }
        }

        private float CalculateLaneSpeed(byte startOffset, byte endOffset, ref NetSegment segment, NetInfo.Lane laneInfo, uint laneId)
        {
            float speedLimit = RoadManager.instance.m_lanes[laneId].m_speed;
            //float speedLimit = laneInfo.m_speedLimit;

            NetInfo.Direction direction = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? laneInfo.m_finalDirection : NetInfo.InvertDirection(laneInfo.m_finalDirection);
            if ((byte)(direction & NetInfo.Direction.Avoid) == 0)
            {
                //return laneInfo.m_speedLimit;
                return speedLimit;
            }
            if (endOffset > startOffset && direction == NetInfo.Direction.AvoidForward)
            {
                //return laneInfo.m_speedLimit * 0.1f;
                return speedLimit * 0.1f;
            }
            if (endOffset < startOffset && direction == NetInfo.Direction.AvoidBackward)
            {
                //return laneInfo.m_speedLimit * 0.1f;
                return speedLimit * 0.1f;
            }
            //return laneInfo.m_speedLimit * 0.2f;
            return speedLimit * 0.2f;
        }
        private void ProcessItem(BufferItem item, ushort targetNode, bool targetDisabled, ushort segmentID, ref NetSegment segment, uint lane, byte offset, byte connectOffset)
        {
            if ((segment.m_flags & (NetSegment.Flags.PathFailed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None)
            {
                return;
            }
            NetManager instance = Singleton<NetManager>.instance;
            if (targetDisabled && ((instance.m_nodes.m_buffer[segment.m_startNode].m_flags | instance.m_nodes.m_buffer[segment.m_endNode].m_flags) & NetNode.Flags.Disabled) == NetNode.Flags.None)
            {
                return;
            }
            NetInfo info = segment.Info;
            NetInfo info2 = instance.m_segments.m_buffer[item.m_position.m_segment].Info;
            int num = info.m_lanes.Length;
            uint num2 = segment.m_lanes;
            float num3 = 1f;
            float num4 = 1f;
            NetInfo.LaneType laneType = NetInfo.LaneType.None;
            if (item.m_position.m_lane < info2.m_lanes.Length)
            {
                uint l = instance.m_segments.m_buffer[item.m_position.m_segment].m_lanes;
                for (int n = 0; l != 0 && n < item.m_position.m_lane; ++n)
                    l = instance.m_lanes.m_buffer[l].m_nextLane;

                NetInfo.Lane lane2 = info2.m_lanes[item.m_position.m_lane];
                //num3 = lane2.m_speedLimit;
                num3 = RoadManager.instance.m_lanes[l].m_speed;
                laneType = lane2.m_laneType;
                num4 = CalculateLaneSpeed(connectOffset, item.m_position.m_offset, ref instance.m_segments.m_buffer[item.m_position.m_segment], lane2, l);
            }
            float averageLength = instance.m_segments.m_buffer[item.m_position.m_segment].m_averageLength;
            float num5 = Mathf.Abs((int)(connectOffset - item.m_position.m_offset)) * 0.003921569f * averageLength;
            float num6 = item.m_methodDistance + num5;
            float num7 = item.m_comparisonValue + num5 / (num4 * m_maxLength);
            Vector3 b = instance.m_lanes.m_buffer[(int)((UIntPtr)item.m_laneID)].CalculatePosition(connectOffset * 0.003921569f);
            int num8 = 0;
            while (num8 < num && num2 != 0u)
            {
                if (lane == num2)
                {
                    NetInfo.Lane lane3 = info.m_lanes[num8];
                    if (lane3.CheckType(m_laneTypes, m_vehicleTypes))
                    {
                        Vector3 a = instance.m_lanes.m_buffer[(int)((UIntPtr)lane)].CalculatePosition(offset * 0.003921569f);
                        float num9 = Vector3.Distance(a, b);
                        BufferItem item2;
                        item2.m_position.m_segment = segmentID;
                        item2.m_position.m_lane = (byte)num8;
                        item2.m_position.m_offset = offset;
                        if (laneType != lane3.m_laneType)
                        {
                            item2.m_methodDistance = 0f;
                        }
                        else
                        {
                            item2.m_methodDistance = num6 + num9;
                        }
                        if (lane3.m_laneType != NetInfo.LaneType.Pedestrian || item2.m_methodDistance < 1000f)
                        {
                            item2.m_comparisonValue = num7 + num9 / ((num3 + RoadManager.instance.m_lanes[lane].m_speed/*lane3.m_speedLimit*/) * 0.5f * m_maxLength);
                            if (lane == m_startLaneA)
                            {
                                float num10 = CalculateLaneSpeed(m_startOffsetA, item2.m_position.m_offset, ref segment, lane3, lane);
                                float num11 = Mathf.Abs((int)(item2.m_position.m_offset - m_startOffsetA)) * 0.003921569f;
                                item2.m_comparisonValue += num11 * segment.m_averageLength / (num10 * m_maxLength);
                            }
                            if (lane == m_startLaneB)
                            {
                                float num12 = CalculateLaneSpeed(m_startOffsetB, item2.m_position.m_offset, ref segment, lane3, lane);
                                float num13 = Mathf.Abs((int)(item2.m_position.m_offset - m_startOffsetB)) * 0.003921569f;
                                item2.m_comparisonValue += num13 * segment.m_averageLength / (num12 * m_maxLength);
                            }
                            if ((segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None)
                            {
                                item2.m_direction = NetInfo.InvertDirection(lane3.m_finalDirection);
                            }
                            else
                            {
                                item2.m_direction = lane3.m_finalDirection;
                            }
                            item2.m_laneID = lane;
                            item2.m_lanesUsed = (item.m_lanesUsed | lane3.m_laneType);
                            AddBufferItem(item2, item.m_position);
                        }
                    }
                    return;
                }
                num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
                num8++;
            }
        }
        private bool ProcessItem(BufferItem item, ushort targetNode, ushort segmentID, ref NetSegment segment, ref int currentTargetIndex, byte connectOffset, bool enableVehicle, bool enablePedestrian)
        {
            bool result = false;
            if ((segment.m_flags & (NetSegment.Flags.PathFailed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None)
            {
                return result;
            }
            NetManager instance = Singleton<NetManager>.instance;
            NetInfo info = segment.Info;
            NetInfo info2 = instance.m_segments.m_buffer[item.m_position.m_segment].Info;
            int num = info.m_lanes.Length;
            uint num2 = segment.m_lanes;
            NetInfo.Direction direction = (targetNode != segment.m_startNode) ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
            NetInfo.Direction direction2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? direction : NetInfo.InvertDirection(direction);
            float num3 = 0.01f - Mathf.Min(info.m_maxTurnAngleCos, info2.m_maxTurnAngleCos);
            if (num3 < 1f)
            {
                Vector3 vector;
                if (targetNode == instance.m_segments.m_buffer[item.m_position.m_segment].m_startNode)
                {
                    vector = instance.m_segments.m_buffer[item.m_position.m_segment].m_startDirection;
                }
                else
                {
                    vector = instance.m_segments.m_buffer[item.m_position.m_segment].m_endDirection;
                }
                Vector3 vector2;
                if ((byte)(direction & NetInfo.Direction.Forward) != 0)
                {
                    vector2 = segment.m_endDirection;
                }
                else
                {
                    vector2 = segment.m_startDirection;
                }
                float num4 = vector.x * vector2.x + vector.z * vector2.z;
                if (num4 >= num3)
                {
                    return result;
                }
            }
            float num5 = 1f;
            float num6 = 1f;
            NetInfo.LaneType laneType = NetInfo.LaneType.None;
            VehicleInfo.VehicleType vehicleType = VehicleInfo.VehicleType.None;
            if (item.m_position.m_lane < info2.m_lanes.Length)
            {
                uint l = instance.m_segments.m_buffer[item.m_position.m_segment].m_lanes;
                for (int n = 0; l != 0 && n < item.m_position.m_lane; ++n)
                    l = instance.m_lanes.m_buffer[l].m_nextLane;

                NetInfo.Lane lane = info2.m_lanes[item.m_position.m_lane];
                laneType = lane.m_laneType;
                vehicleType = lane.m_vehicleType;
                //num5 = lane.m_speedLimit;
                //num6 = this.CalculateLaneSpeed(connectOffset, item.m_position.m_offset, ref instance.m_segments.m_buffer[(int)item.m_position.m_segment], lane, 0);
                num5 = RoadManager.instance.m_lanes[l].m_speed;
                num6 = CalculateLaneSpeed(connectOffset, item.m_position.m_offset, ref instance.m_segments.m_buffer[item.m_position.m_segment], lane, l);
            }
            float num7 = instance.m_segments.m_buffer[item.m_position.m_segment].m_averageLength;
            if (!m_stablePath)
            {
                Randomizer randomizer = new Randomizer(m_pathFindIndex << 16 | item.m_position.m_segment);
                num7 *= (randomizer.Int32(900, 1000 + (int)(instance.m_segments.m_buffer[(int)item.m_position.m_segment].m_trafficDensity * 10)) + m_pathRandomizer.Int32(20u)) * 0.001f;
            }
            if (m_isHeavyVehicle && (instance.m_segments.m_buffer[item.m_position.m_segment].m_flags & NetSegment.Flags.HeavyBan) != NetSegment.Flags.None)
            {
                num7 *= 10f;
            }
            float num8 = Mathf.Abs((int)(connectOffset - item.m_position.m_offset)) * 0.003921569f * num7;
            float num9 = item.m_methodDistance + num8;
            float num10 = item.m_comparisonValue + num8 / (num6 * m_maxLength);
            Vector3 b = instance.m_lanes.m_buffer[(int)((UIntPtr)item.m_laneID)].CalculatePosition(connectOffset * 0.003921569f);
            int num11 = currentTargetIndex;
            bool flag = (instance.m_nodes.m_buffer[targetNode].m_flags & NetNode.Flags.Transition) != NetNode.Flags.None;
            NetInfo.LaneType laneType2 = m_laneTypes;
            VehicleInfo.VehicleType vehicleType2 = m_vehicleTypes;
            if (!enableVehicle)
            {
                vehicleType2 &= VehicleInfo.VehicleType.Bicycle;
                if (vehicleType2 == VehicleInfo.VehicleType.None)
                {
                    laneType2 &= ~NetInfo.LaneType.Vehicle;
                }
            }
            if (!enablePedestrian)
            {
                laneType2 &= ~NetInfo.LaneType.Pedestrian;
            }
            int num12 = 0;
            while (num12 < num && num2 != 0u)
            {
                NetInfo.Lane lane2 = info.m_lanes[num12];
                bool canConnect = true;
                if ((instance.m_lanes.m_buffer[num2].m_flags & RoadManager.Lane.CONTROL_BIT) != 0)
                    canConnect = RoadManager.instance.CheckLaneConnection(num2, item.m_laneID, m_vehicleType);
                if ((byte)(lane2.m_finalDirection & direction2) != 0 && canConnect)
                {
                    if (lane2.CheckType(laneType2, vehicleType2) && (segmentID != item.m_position.m_segment || num12 != item.m_position.m_lane) && (byte)(lane2.m_finalDirection & direction2) != 0)
                    {
                        Vector3 a;
                        if ((byte)(direction & NetInfo.Direction.Forward) != 0)
                        {
                            a = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_bezier.d;
                        }
                        else
                        {
                            a = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_bezier.a;
                        }
                        float num13 = Vector3.Distance(a, b);
                        if (flag)
                        {
                            num13 *= 2f;
                        }
                        if (m_prioritizeBusLanes)
                        {
                            NetInfoLane customLane2 = lane2 as NetInfoLane;
                            if (customLane2 != null && customLane2.m_specialLaneType == NetInfoLane.SpecialLaneType.BusLane)
                            {
                                num13 /= 10f;
                            }
                        }
                        float num14 = num13 / ((num5 + RoadManager.instance.m_lanes[num2].m_speed /*lane2.m_speedLimit*/) * 0.5f * m_maxLength);
                        BufferItem item2;
                        item2.m_position.m_segment = segmentID;
                        item2.m_position.m_lane = (byte)num12;
                        item2.m_position.m_offset = (byte)(((direction & NetInfo.Direction.Forward) == 0) ? 0 : 255);
                        if (laneType != lane2.m_laneType)
                        {
                            item2.m_methodDistance = 0f;
                        }
                        else
                        {
                            item2.m_methodDistance = num9 + num13;
                        }
                        if (lane2.m_laneType != NetInfo.LaneType.Pedestrian || item2.m_methodDistance < 1000f)
                        {
                            item2.m_comparisonValue = num10 + num14;
                            if (num2 == m_startLaneA)
                            {
                                float num15 = CalculateLaneSpeed(m_startOffsetA, item2.m_position.m_offset, ref segment, lane2, num2);
                                float num16 = Mathf.Abs((int)(item2.m_position.m_offset - m_startOffsetA)) * 0.003921569f;
                                item2.m_comparisonValue += num16 * segment.m_averageLength / (num15 * m_maxLength);
                            }
                            if (num2 == m_startLaneB)
                            {
                                float num17 = CalculateLaneSpeed(m_startOffsetB, item2.m_position.m_offset, ref segment, lane2, num2);
                                float num18 = Mathf.Abs((int)(item2.m_position.m_offset - m_startOffsetB)) * 0.003921569f;
                                item2.m_comparisonValue += num18 * segment.m_averageLength / (num17 * m_maxLength);
                            }
                            if (!m_ignoreBlocked && (segment.m_flags & NetSegment.Flags.Blocked) != NetSegment.Flags.None && lane2.m_laneType == NetInfo.LaneType.Vehicle)
                            {
                                item2.m_comparisonValue += 0.1f;
                                result = true;
                            }
                            item2.m_direction = direction;
                            item2.m_lanesUsed = (item.m_lanesUsed | lane2.m_laneType);
                            item2.m_laneID = num2;
                            if (lane2.m_laneType == laneType && lane2.m_vehicleType == vehicleType)
                            {
                                int firstTarget = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_firstTarget;
                                int lastTarget = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_lastTarget;
                                if (currentTargetIndex < firstTarget || currentTargetIndex >= lastTarget)
                                {
                                    item2.m_comparisonValue += Mathf.Max(1f, num13 * 3f - 3f) / ((num5 + RoadManager.instance.m_lanes[num2].m_speed/* lane2.m_speedLimit*/) * 0.5f * m_maxLength);
                                }
                            }
                            AddBufferItem(item2, item.m_position);
                        }
                    }
                }
                else if (lane2.m_laneType == laneType && lane2.m_vehicleType == vehicleType)
                {
                    num11++;
                }
                num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
                num12++;
            }
            currentTargetIndex = num11;
            return result;
        }
        private void ProcessItem(BufferItem item, ushort targetNode, ushort segmentID, ref NetSegment segment, byte connectOffset, int laneIndex, uint lane)
        {
            if ((segment.m_flags & (NetSegment.Flags.PathFailed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None)
            {
                return;
            }
            NetManager instance = Singleton<NetManager>.instance;
            NetInfo info = segment.Info;
            NetInfo info2 = instance.m_segments.m_buffer[item.m_position.m_segment].Info;
            int num = info.m_lanes.Length;
            float num2;
            byte offset;
            if (segmentID == item.m_position.m_segment)
            {
                Vector3 b = instance.m_lanes.m_buffer[(int)((UIntPtr)item.m_laneID)].CalculatePosition(connectOffset * 0.003921569f);
                Vector3 a = instance.m_lanes.m_buffer[(int)((UIntPtr)lane)].CalculatePosition(connectOffset * 0.003921569f);
                num2 = Vector3.Distance(a, b);
                offset = connectOffset;
            }
            else
            {
                NetInfo.Direction direction = (targetNode != segment.m_startNode) ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
                Vector3 b2 = instance.m_lanes.m_buffer[(int)((UIntPtr)item.m_laneID)].CalculatePosition(connectOffset * 0.003921569f);
                Vector3 a2;
                if ((byte)(direction & NetInfo.Direction.Forward) != 0)
                {
                    a2 = instance.m_lanes.m_buffer[(int)((UIntPtr)lane)].m_bezier.d;
                }
                else
                {
                    a2 = instance.m_lanes.m_buffer[(int)((UIntPtr)lane)].m_bezier.a;
                }
                num2 = Vector3.Distance(a2, b2);
                offset = (byte)(((direction & NetInfo.Direction.Forward) == 0) ? 0 : 255);
            }
            float num3 = 1f;
            float num4 = 1f;
            NetInfo.LaneType laneType = NetInfo.LaneType.None;
            if (item.m_position.m_lane < info2.m_lanes.Length)
            {
                uint l = instance.m_segments.m_buffer[item.m_position.m_segment].m_lanes;
                for (int n = 0; l != 0 && n < item.m_position.m_lane; ++n)
                    l = instance.m_lanes.m_buffer[l].m_nextLane;

                NetInfo.Lane lane2 = info2.m_lanes[item.m_position.m_lane];
                //num3 = lane2.m_speedLimit;
                num3 = RoadManager.instance.m_lanes[l].m_speed;
                laneType = lane2.m_laneType;
                num4 = CalculateLaneSpeed(connectOffset, item.m_position.m_offset, ref instance.m_segments.m_buffer[item.m_position.m_segment], lane2, l);
            }
            float averageLength = instance.m_segments.m_buffer[item.m_position.m_segment].m_averageLength;
            float num5 = Mathf.Abs((int)(connectOffset - item.m_position.m_offset)) * 0.003921569f * averageLength;
            float num6 = item.m_methodDistance + num5;
            float num7 = item.m_comparisonValue + num5 / (num4 * m_maxLength);
            if (laneIndex < num)
            {
                NetInfo.Lane lane3 = info.m_lanes[laneIndex];
                BufferItem item2;
                item2.m_position.m_segment = segmentID;
                item2.m_position.m_lane = (byte)laneIndex;
                item2.m_position.m_offset = offset;
                if (laneType != lane3.m_laneType)
                {
                    item2.m_methodDistance = 0f;
                }
                else
                {
                    if (item.m_methodDistance == 0f)
                    {
                        num7 += 100f / (0.25f * m_maxLength);
                    }
                    item2.m_methodDistance = num6 + num2;
                }
                if (lane3.m_laneType != NetInfo.LaneType.Pedestrian || item2.m_methodDistance < 1000f)
                {
                    item2.m_comparisonValue = num7 + num2 / ((num3 + RoadManager.instance.m_lanes[lane].m_speed /*lane3.m_speedLimit*/) * 0.25f * m_maxLength);
                    if (lane == m_startLaneA)
                    {
                        float num8 = CalculateLaneSpeed(m_startOffsetA, item2.m_position.m_offset, ref segment, lane3, lane);
                        float num9 = Mathf.Abs((int)(item2.m_position.m_offset - m_startOffsetA)) * 0.003921569f;
                        item2.m_comparisonValue += num9 * segment.m_averageLength / (num8 * m_maxLength);
                    }
                    if (lane == m_startLaneB)
                    {
                        float num10 = CalculateLaneSpeed(m_startOffsetB, item2.m_position.m_offset, ref segment, lane3, lane);
                        float num11 = Mathf.Abs((int)(item2.m_position.m_offset - m_startOffsetB)) * 0.003921569f;
                        item2.m_comparisonValue += num11 * segment.m_averageLength / (num10 * m_maxLength);
                    }
                    if ((segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None)
                    {
                        item2.m_direction = NetInfo.InvertDirection(lane3.m_finalDirection);
                    }
                    else
                    {
                        item2.m_direction = lane3.m_finalDirection;
                    }
                    item2.m_laneID = lane;
                    item2.m_lanesUsed = (item.m_lanesUsed | lane3.m_laneType);
                    AddBufferItem(item2, item.m_position);
                }
            }
        }
        private void AddBufferItem(BufferItem item, PathUnit.Position target)
        {
            uint num = m_laneLocation[(int)((UIntPtr)item.m_laneID)];
            uint num2 = num >> 16;
            int num3 = (int)(num & 65535u);
            int num6;
            if (num2 == m_pathFindIndex)
            {
                if (item.m_comparisonValue >= m_buffer[num3].m_comparisonValue)
                {
                    return;
                }
                int num4 = num3 >> 6;
                int num5 = num3 & -64;
                if (num4 < m_bufferMinPos || (num4 == m_bufferMinPos && num5 < m_bufferMin[num4]))
                {
                    return;
                }
                num6 = Mathf.Max(Mathf.RoundToInt(item.m_comparisonValue * 1024f), m_bufferMinPos);
                if (num6 == num4)
                {
                    m_buffer[num3] = item;
                    m_laneTarget[(int)((UIntPtr)item.m_laneID)] = target;
                    return;
                }
                int num7 = num4 << 6 | m_bufferMax[num4]--;
                BufferItem bufferItem = m_buffer[num7];
                m_laneLocation[(int)((UIntPtr)bufferItem.m_laneID)] = num;
                m_buffer[num3] = bufferItem;
            }
            else
            {
                num6 = Mathf.Max(Mathf.RoundToInt(item.m_comparisonValue * 1024f), m_bufferMinPos);
            }
            if (num6 >= 1024)
            {
                return;
            }
            while (m_bufferMax[num6] == 63)
            {
                num6++;
                if (num6 == 1024)
                {
                    return;
                }
            }
            if (num6 > m_bufferMaxPos)
            {
                m_bufferMaxPos = num6;
            }
            num3 = (num6 << 6 | ++m_bufferMax[num6]);
            m_buffer[num3] = item;
            m_laneLocation[(int)((UIntPtr)item.m_laneID)] = (m_pathFindIndex << 16 | (uint)num3);
            m_laneTarget[(int)((UIntPtr)item.m_laneID)] = target;
        }
        private void GetLaneDirection(PathUnit.Position pathPos, out NetInfo.Direction direction, out NetInfo.LaneType type)
        {
            NetManager instance = Singleton<NetManager>.instance;
            //if (instance == null)
            //	Logger.LogInfo("GetLaneDirection -> instance is null!\n");
            NetInfo info = instance.m_segments.m_buffer[pathPos.m_segment].Info;
            //if (info == null)
            //	Logger.LogInfo("GetLaneDirection -> info is null!\n");
            //else if (info.m_lanes == null)
            //	Logger.LogInfo("GetLaneDirection -> info.m_lanes is null!\n");
            if (info.m_lanes.Length > pathPos.m_lane)
            {
                direction = info.m_lanes[pathPos.m_lane].m_finalDirection;
                type = info.m_lanes[pathPos.m_lane].m_laneType;
                if ((instance.m_segments.m_buffer[pathPos.m_segment].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None)
                {
                    direction = NetInfo.InvertDirection(direction);
                }
            }
            else
            {
                direction = NetInfo.Direction.None;
                type = NetInfo.LaneType.None;
            }
        }
        private void PathFindThread()
        {
            Stopwatch stopwatch = new Stopwatch();
            long count = 0;
            long totalMs = 0;
            while (true)
            {
                while (!Monitor.TryEnter(m_queueLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
                {
                }
                try
                {
                    while (m_queueFirst == 0u && !m_terminated)
                    {
                        Monitor.Wait(m_queueLock);
                    }
                    if (m_terminated)
                    {
                        break;
                    }
                    m_calculating = m_queueFirst;
                    m_queueFirst = m_pathUnits.m_buffer[(int)((UIntPtr)m_calculating)].m_nextPathUnit;
                    if (m_queueFirst == 0u)
                    {
                        m_queueLast = 0u;
                        m_queuedPathFindCount = 0;
                    }
                    else
                    {
                        m_queuedPathFindCount--;
                    }
                    m_pathUnits.m_buffer[(int)((UIntPtr)m_calculating)].m_nextPathUnit = 0u;
                    m_pathUnits.m_buffer[(int)((UIntPtr)m_calculating)].m_pathFindFlags = (byte)(m_pathUnits.m_buffer[(int)((UIntPtr)m_calculating)].m_pathFindFlags & -2 | 2);
                }
                finally
                {
                    Monitor.Exit(m_queueLock);
                }
                try
                {
                    m_pathfindProfiler.BeginStep();
                    try
                    {
                        stopwatch.Reset();
                        stopwatch.Start();
                        PathFindImplementation(m_calculating, ref m_pathUnits.m_buffer[(int)((UIntPtr)m_calculating)]);
                        stopwatch.Stop();
                        totalMs += stopwatch.ElapsedMilliseconds;
                        count++;
                        if (count == 10000)
                        {
                            System.IO.File.AppendAllText("TimeThread" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n\nMs\nTime to calculate 10,000 Paths: " + totalMs + " ms\nAverage time/path: " + ((double)totalMs / count) + "ms\n");
                        }
                    }
                    finally
                    {
                        m_pathfindProfiler.EndStep();
                    }
                }
                catch (Exception ex)
                {
                    UIView.ForwardException(ex);
                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Path find error: " + ex.Message/* + " - " + m_vehicleType + " - " + m_vehicleTypes*/ + "\n" + ex.StackTrace);
                    PathUnit[] expr_1A0_cp_0 = m_pathUnits.m_buffer;
                    UIntPtr expr_1A0_cp_1 = (UIntPtr)m_calculating;
                    expr_1A0_cp_0[(int)expr_1A0_cp_1].m_pathFindFlags = (byte)(expr_1A0_cp_0[(int)expr_1A0_cp_1].m_pathFindFlags | 8);
                }
                while (!Monitor.TryEnter(m_queueLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
                {
                }
                try
                {
                    m_pathUnits.m_buffer[(int)((UIntPtr)m_calculating)].m_pathFindFlags = (byte)(m_pathUnits.m_buffer[(int)((UIntPtr)m_calculating)].m_pathFindFlags & -3);
                    Singleton<PathManager>.instance.ReleasePath(m_calculating);
                    m_calculating = 0u;
                    Monitor.Pulse(m_queueLock);
                }
                finally
                {
                    Monitor.Exit(m_queueLock);
                }
            }

            System.IO.File.AppendAllText("TimeThread" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n\nMs\nTime to calculate " + count + " Paths: " + totalMs + " ms\nAverage time/path: " + ((double)totalMs / count) + "ms\n");
        }
    }
}
