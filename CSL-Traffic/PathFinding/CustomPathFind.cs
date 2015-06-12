using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using CSL_Traffic.Extensions;
using UnityEngine;

namespace CSL_Traffic
{
    /*
     * This is the class responsible for pathfinding. It's all in here since none of the methods can be overwritten.
     * There's a lot of small changes here and there to make it generate a correct path for the service vehicles using pedestrian paths.
     */

    internal class CustomPathFind : PathFind
    {
        private const float WEIGHT_FACTOR = 0.003921569f;
        private FieldInfo fi_calculating;
        private FieldInfo fi_pathFindThread;
        private FieldInfo fi_pathUnits;
        private FieldInfo fi_queueFirst;
        private FieldInfo fi_queueLast;
        private FieldInfo fi_queueLock;
        private FieldInfo fi_terminated;
        private BufferItem[] m_buffer;
        private object m_bufferLock;
        private int[] m_bufferMax;
        private int m_bufferMaxPos;
        private int[] m_bufferMin;
        private int m_bufferMinPos;
        private uint m_endLaneA;
        private uint m_endLaneB;
        private bool m_ignoreBlocked;
        private bool m_isHeavyVehicle;
        private uint[] m_laneLocation;
        private PathUnit.Position[] m_laneTarget;
        private NetInfo.LaneType m_laneTypes;
        private float m_maxLength;
        private uint m_pathFindIndex;
        private Randomizer m_pathRandomizer;
        private Dictionary<uint, RoadManager.VehicleType> m_pathVehicleType;
        private bool m_prioritizeBusLanes;
        private bool m_stablePath;
        private uint m_startLaneA;
        private uint m_startLaneB;
        private byte m_startOffsetA;
        private byte m_startOffsetB;
        private uint m_vehicleLane;
        private byte m_vehicleOffset;
        private RoadManager.VehicleType m_vehicleType;
        private VehicleInfo.VehicleType m_vehicleTypes;

        private Array32<PathUnit> m_pathUnits
        {
            get { return (Array32<PathUnit>) fi_pathUnits.GetValue(this); }
            set { fi_pathUnits.SetValue(this, value); }
        }

        private uint m_queueFirst
        {
            get { return (uint) fi_queueFirst.GetValue(this); }
            set { fi_queueFirst.SetValue(this, value); }
        }

        private uint m_queueLast
        {
            get { return (uint) fi_queueLast.GetValue(this); }
            set { fi_queueLast.SetValue(this, value); }
        }

        private uint m_calculating
        {
            get { return (uint) fi_calculating.GetValue(this); }
            set { fi_calculating.SetValue(this, value); }
        }

        private object m_queueLock
        {
            get { return fi_queueLock.GetValue(this); }
            set { fi_queueLock.SetValue(this, value); }
        }

        private Thread m_pathFindThread
        {
            get { return (Thread) fi_pathFindThread.GetValue(this); }
            set { fi_pathFindThread.SetValue(this, value); }
        }

        private bool m_terminated
        {
            get { return (bool) fi_terminated.GetValue(this); }
            set { fi_terminated.SetValue(this, value); }
        }

        private void Awake()
        {
            Type pathFindType = typeof (PathFind);
            fi_pathUnits = pathFindType.GetFieldByName("m_pathUnits");
            fi_queueFirst = pathFindType.GetFieldByName("m_queueFirst");
            fi_queueLast = pathFindType.GetFieldByName("m_queueLast");
            fi_calculating = pathFindType.GetFieldByName("m_calculating");
            fi_queueLock = pathFindType.GetFieldByName("m_queueLock");
            fi_pathFindThread = pathFindType.GetFieldByName("m_pathFindThread");
            fi_terminated = pathFindType.GetFieldByName("m_terminated");

            m_pathfindProfiler = new ThreadProfiler();
            m_laneLocation = new uint[NetManager.MAX_LANE_COUNT];
            m_laneTarget = new PathUnit.Position[NetManager.MAX_LANE_COUNT];
            m_buffer = new BufferItem[65536];
            m_bufferMin = new int[NetManager.MAX_ASSET_SEGMENTS];
            m_bufferMax = new int[NetManager.MAX_ASSET_SEGMENTS];
            m_queueLock = new object();
            m_pathVehicleType = new Dictionary<uint, RoadManager.VehicleType>();
            m_bufferLock = Singleton<PathManager>.instance.m_bufferLock;
            m_pathUnits = Singleton<PathManager>.instance.m_pathUnits;
            m_pathFindThread = new Thread(PathFindThread)
            {
                Name = "Pathfind",
                Priority = SimulationManager.SIMULATION_PRIORITY
            };
            m_pathFindThread.Start();
            if (!m_pathFindThread.IsAlive)
            {
                Logger.LogToFile("Path find thread failed to start!");
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

        public bool CalculatePath(uint unitId, bool skipQueue, RoadManager.VehicleType vehicleType)
        {
            if (Singleton<PathManager>.instance.AddPathReference(unitId))
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
                            m_queueLast = unitId;
                        }
                        else
                        {
                            m_pathUnits.m_buffer[unitId].m_nextPathUnit = m_queueFirst;
                        }
                        m_queueFirst = unitId;
                    }
                    else
                    {
                        if (m_queueLast == 0u)
                        {
                            m_queueFirst = unitId;
                        }
                        else
                        {
                            m_pathUnits.m_buffer[unitId].m_nextPathUnit = unitId;
                        }
                        m_queueLast = unitId;
                    }

                    m_pathVehicleType[unitId] = vehicleType;

                    m_pathUnits.m_buffer[unitId].m_pathFindFlags = m_pathUnits.m_buffer[unitId].m_pathFindFlags.SetFlags(PathUnit.FLAG_QUEUED);
                    ++m_queuedPathFindCount;
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
        private void PathFindImplementation(uint unitId, ref PathUnit data)
        {
            NetManager instance = Singleton<NetManager>.instance;

            // TODO: find out if we really have to save the values or if we can make them locals.
            m_laneTypes = (NetInfo.LaneType) m_pathUnits.m_buffer[unitId].m_laneTypes;
            m_vehicleTypes = (VehicleInfo.VehicleType) m_pathUnits.m_buffer[unitId].m_vehicleTypes;
            m_maxLength = m_pathUnits.m_buffer[unitId].m_length;
            m_pathFindIndex = (m_pathFindIndex + 1u & 32767u);
            m_pathRandomizer = new Randomizer(unitId);
            m_isHeavyVehicle = m_pathUnits.m_buffer[unitId].m_simulationFlags.IsFlagSet(PathUnit.FLAG_IS_HEAVY);
            m_ignoreBlocked = m_pathUnits.m_buffer[unitId].m_simulationFlags.IsFlagSet(PathUnit.FLAG_IGNORE_BLOCKED);
            m_stablePath = m_pathUnits.m_buffer[unitId].m_simulationFlags.IsFlagSet(PathUnit.FLAG_STABLE_PATH);

            if (!m_pathVehicleType.TryGetValue(unitId, out m_vehicleType))
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

            int posCount = m_pathUnits.m_buffer[unitId].m_positionCount & 15;
            int offset = m_pathUnits.m_buffer[unitId].m_positionCount >> 4;
            BufferItem bufferItem1;
            if (data.m_position00.m_segment != 0 && posCount >= 1)
            {
                //if (NetManager.instance.m_segments.m_buffer[data.m_position00.m_segment].Info == null)
                //{
                //	this.m_pathUnits.m_buffer[unitId].m_pathFindFlags |= PathUnit.FLAG_FAILED;
                //	return;
                //}

                m_startLaneA = PathManager.GetLaneID(data.m_position00);
                m_startOffsetA = data.m_position00.m_offset;
                bufferItem1.m_laneID = m_startLaneA;
                bufferItem1.m_position = data.m_position00;
                GetLaneDirection(data.m_position00, out bufferItem1.m_direction, out bufferItem1.m_lanesUsed);
                bufferItem1.m_comparisonValue = 0f;
            }
            else
            {
                m_startLaneA = 0u;
                m_startOffsetA = 0;
                bufferItem1 = default(BufferItem);
            }
            BufferItem bufferItem2;
            if (data.m_position02.m_segment != 0 && posCount >= 3)
            {
                //if (NetManager.instance.m_segments.m_buffer[data.m_position02.m_segment].Info == null)
                //{
                //	this.m_pathUnits.m_buffer[unitId].m_pathFindFlags |= PathUnit.FLAG_FAILED;
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
            if (data.m_position01.m_segment != 0 && posCount >= 2)
            {
                //if (NetManager.instance.m_segments.m_buffer[data.m_position01.m_segment].Info == null)
                //{
                //	this.m_pathUnits.m_buffer[unitId].m_pathFindFlags |= PathUnit.FLAG_FAILED;
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
            if (data.m_position03.m_segment != 0 && posCount >= 4)
            {
                //if (NetManager.instance.m_segments.m_buffer[data.m_position03.m_segment].Info == null)
                //{
                //	this.m_pathUnits.m_buffer[unitId].m_pathFindFlags |= PathUnit.FLAG_FAILED;
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
            if (data.m_position11.m_segment != 0 && offset >= 1)
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
            byte offsetSum = 0;
            m_bufferMinPos = 0;
            m_bufferMaxPos = -1;
            if (m_pathFindIndex == 0u)
            {
                const uint invalidPtr = 4294901760u;
                for (int i = 0; i < NetManager.MAX_LANE_COUNT; i++)
                {
                    m_laneLocation[i] = invalidPtr;
                }
            }
            // Clear buffer (i guess)
            for (int i = 0; i < NetManager.MAX_ASSET_SEGMENTS; i++)
            {
                m_bufferMin[i] = 0;
                m_bufferMax[i] = -1;
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
            bool found = false;
            while (m_bufferMinPos <= m_bufferMaxPos)
            {
                int min = m_bufferMin[m_bufferMinPos];
                int max = m_bufferMax[m_bufferMinPos];
                if (min > max)
                {
                    m_bufferMinPos++;
                }
                else
                {
                    m_bufferMin[m_bufferMinPos] = min + 1;
                    BufferItem bufferItem6 = m_buffer[(m_bufferMinPos << 6) + min];
                    if (bufferItem6.m_position.m_segment == bufferItem1.m_position.m_segment && bufferItem6.m_position.m_lane == bufferItem1.m_position.m_lane)
                    {
                        if (bufferItem6.m_direction.IsFlagSet(NetInfo.Direction.Forward) && bufferItem6.m_position.m_offset >= m_startOffsetA)
                        {
                            bufferItem5 = bufferItem6;
                            offsetSum = m_startOffsetA;
                            found = true;
                            break;
                        }
                        if (bufferItem6.m_direction.IsFlagSet(NetInfo.Direction.Backward) && bufferItem6.m_position.m_offset <= m_startOffsetA)
                        {
                            bufferItem5 = bufferItem6;
                            offsetSum = m_startOffsetA;
                            found = true;
                            break;
                        }
                    }
                    if (bufferItem6.m_position.m_segment == bufferItem2.m_position.m_segment && bufferItem6.m_position.m_lane == bufferItem2.m_position.m_lane)
                    {
                        if (bufferItem6.m_direction.IsFlagSet(NetInfo.Direction.Forward) && bufferItem6.m_position.m_offset >= m_startOffsetB)
                        {
                            bufferItem5 = bufferItem6;
                            offsetSum = m_startOffsetB;
                            found = true;
                            break;
                        }
                        if (bufferItem6.m_direction.IsFlagSet(NetInfo.Direction.Backward) && bufferItem6.m_position.m_offset <= m_startOffsetB)
                        {
                            bufferItem5 = bufferItem6;
                            offsetSum = m_startOffsetB;
                            found = true;
                            break;
                        }
                    }
                    if (bufferItem6.m_direction.IsFlagSet(NetInfo.Direction.Forward))
                    {
                        ushort startNode = instance.m_segments.m_buffer[bufferItem6.m_position.m_segment].m_startNode;
                        ProcessItem(bufferItem6, startNode, ref instance.m_nodes.m_buffer[startNode], 0, false);
                    }
                    if (bufferItem6.m_direction.IsFlagSet(NetInfo.Direction.Backward))
                    {
                        ushort endNode = instance.m_segments.m_buffer[bufferItem6.m_position.m_segment].m_endNode;
                        ProcessItem(bufferItem6, endNode, ref instance.m_nodes.m_buffer[endNode], 255, false);
                    }
                    int nodeCount = 0;
                    ushort nodeId = instance.m_lanes.m_buffer[bufferItem6.m_laneID].m_nodes;
                    // Node ID is always 0;
                    if (nodeId != 0)
                    {
                        ushort startNode = instance.m_segments.m_buffer[bufferItem6.m_position.m_segment].m_startNode;
                        ushort endNode = instance.m_segments.m_buffer[bufferItem6.m_position.m_segment].m_endNode;
                        bool isDisabled = (instance.m_nodes.m_buffer[startNode].m_flags | instance.m_nodes.m_buffer[endNode].m_flags).IsFlagSet(NetNode.Flags.Disabled);
                        while (nodeId != 0)
                        {
                            NetInfo.Direction direction = NetInfo.Direction.None;
                            byte laneOffset = instance.m_nodes.m_buffer[nodeId].m_laneOffset;
                            if (laneOffset <= bufferItem6.m_position.m_offset)
                            {
                                direction = direction.SetFlags(NetInfo.Direction.Forward);
                            }
                            if (laneOffset >= bufferItem6.m_position.m_offset)
                            {
                                direction = direction.SetFlags(NetInfo.Direction.Backward);
                            }
                            if (bufferItem6.m_direction.IsFlagSet(direction) && (!isDisabled || instance.m_nodes.m_buffer[nodeId].m_flags.IsFlagSet(NetNode.Flags.Disabled)))
                            {
                                ProcessItem(bufferItem6, nodeId, ref instance.m_nodes.m_buffer[nodeId], laneOffset, true);
                            }
                            nodeId = instance.m_nodes.m_buffer[nodeId].m_nextLaneNode;

                            // Max Node count i guess
                            if (++nodeCount == 32768)
                            {
                                break;
                            }
                        }
                    }
                }
            }
            if (!found)
            {
                m_pathUnits.m_buffer[unitId].m_pathFindFlags = m_pathUnits.m_buffer[unitId].m_pathFindFlags.SetFlags(PathUnit.FLAG_FAILED);
                return;
            }
            float length = bufferItem5.m_comparisonValue*m_maxLength;
            m_pathUnits.m_buffer[unitId].m_length = length;
            uint currentId = unitId;
            int posId = 0;
            int posOffset = 0;
            PathUnit.Position position = bufferItem5.m_position;
            if ((position.m_segment != bufferItem3.m_position.m_segment || position.m_lane != bufferItem3.m_position.m_lane || position.m_offset != bufferItem3.m_position.m_offset) &&
                (position.m_segment != bufferItem4.m_position.m_segment || position.m_lane != bufferItem4.m_position.m_lane || position.m_offset != bufferItem4.m_position.m_offset))
            {
                if (offsetSum != position.m_offset)
                {
                    PathUnit.Position position2 = position;
                    position2.m_offset = offsetSum;
                    m_pathUnits.m_buffer[currentId].SetPosition(posId++, position2);
                }
                m_pathUnits.m_buffer[currentId].SetPosition(posId++, position);
                position = m_laneTarget[bufferItem5.m_laneID];
            }
            for (int k = 0; k < NetManager.MAX_LANE_COUNT; k++)
            {
                m_pathUnits.m_buffer[currentId] = m_pathUnits.m_buffer[currentId];
                m_pathUnits.m_buffer[currentId].SetPosition(posId++, position);
                if ((position.m_segment == bufferItem3.m_position.m_segment && position.m_lane == bufferItem3.m_position.m_lane && position.m_offset == bufferItem3.m_position.m_offset) ||
                    (position.m_segment == bufferItem4.m_position.m_segment && position.m_lane == bufferItem4.m_position.m_lane && position.m_offset == bufferItem4.m_position.m_offset))
                {
                    m_pathUnits.m_buffer[currentId].m_positionCount = (byte) posId;
                    posOffset += posId;
                    if (posOffset != 0)
                    {
                        currentId = m_pathUnits.m_buffer[unitId].m_nextPathUnit;
                        posId = m_pathUnits.m_buffer[unitId].m_positionCount;
                        int laneItr = 0;
                        while (currentId != 0u)
                        {
                            m_pathUnits.m_buffer[currentId].m_length = length*(posOffset - posId)/posOffset;
                            posId += m_pathUnits.m_buffer[currentId].m_positionCount;
                            currentId = m_pathUnits.m_buffer[currentId].m_nextPathUnit;
                            if (++laneItr >= NetManager.MAX_LANE_COUNT)
                            {
                                Logger.LogToFile("PathFind:PathFindImplementation(): Invalid list detected!");
                                CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                                break;
                            }
                        }
                    }
                    m_pathUnits.m_buffer[unitId].m_pathFindFlags = m_pathUnits.m_buffer[unitId].m_pathFindFlags.SetFlags(PathUnit.FLAG_READY);
                    return;
                }
                if (posId == PathUnit.MAX_POSITIONS)
                {
                    while (!Monitor.TryEnter(m_bufferLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
                    {
                    }
                    uint newUnit;
                    try
                    {
                        if (!m_pathUnits.CreateItem(out newUnit, ref m_pathRandomizer))
                        {
                            Logger.LogToFile("PathFind:PathFindImplementation(): Could not create new Unit");
                            m_pathUnits.m_buffer[unitId].m_pathFindFlags = m_pathUnits.m_buffer[unitId].m_pathFindFlags.SetFlags(PathUnit.FLAG_FAILED);
                            return;
                        }
                        m_pathUnits.m_buffer[newUnit] = m_pathUnits.m_buffer[currentId];
                        m_pathUnits.m_buffer[newUnit].m_referenceCount = 1;
                        m_pathUnits.m_buffer[newUnit].m_pathFindFlags = PathUnit.FLAG_READY;
                        m_pathUnits.m_buffer[currentId].m_nextPathUnit = newUnit;
                        m_pathUnits.m_buffer[currentId].m_positionCount = (byte) posId;
                        posOffset += posId;
                        Singleton<PathManager>.instance.m_pathUnitCount = (int) (m_pathUnits.ItemCount() - 1u);
                    }
                    finally
                    {
                        Monitor.Exit(m_bufferLock);
                    }
                    currentId = newUnit;
                    posId = 0;
                }
                uint laneID = PathManager.GetLaneID(position);
                position = m_laneTarget[laneID];
            }
            m_pathUnits.m_buffer[unitId].m_pathFindFlags = m_pathUnits.m_buffer[unitId].m_pathFindFlags.SetFlags(PathUnit.FLAG_FAILED);
        }

        private void ProcessItem(BufferItem item, ushort nodeID, ref NetNode node, byte connectOffset, bool isMiddle)
        {
            NetManager instance = Singleton<NetManager>.instance;
            bool isPedestrian = false;
            bool isBicycle = false;
            int curLaneId = 0;
            NetInfo segmentInfo = instance.m_segments.m_buffer[item.m_position.m_segment].Info;
            if (item.m_position.m_lane < segmentInfo.m_lanes.Length)
            {
                NetInfo.Lane lane = segmentInfo.m_lanes[item.m_position.m_lane];

                // TODO: Do these checks even work? (those are flags)
                isPedestrian = (lane.m_laneType == NetInfo.LaneType.Pedestrian);
                isBicycle = (lane.m_laneType == NetInfo.LaneType.Vehicle && lane.m_vehicleType == VehicleInfo.VehicleType.Bicycle);

                curLaneId = lane.m_finalDirection.IsFlagSet(NetInfo.Direction.Forward) ? lane.m_similarLaneIndex : (lane.m_similarLaneCount - lane.m_similarLaneIndex - 1);
            }
            if (isMiddle)
            {
                for (int i = 0; i < 8; i++)
                {
                    ushort segment = node.GetSegment(i);
                    if (segment != 0)
                    {
                        ProcessItem(item, nodeID, segment, ref instance.m_segments.m_buffer[segment], ref curLaneId, connectOffset, !isPedestrian, isPedestrian);
                    }
                }
            }
            else if (isPedestrian)
            {
                ushort segment = item.m_position.m_segment;
                NetSegment netSegment = instance.m_segments.m_buffer[segment];
                int lane = item.m_position.m_lane;
                if (node.Info.m_class.m_service != ItemClass.Service.Beautification)
                {
                    bool canUse = (node.m_flags & (NetNode.Flags.End | NetNode.Flags.Bend | NetNode.Flags.Junction)) != NetNode.Flags.None;
                    int leftId;
                    int rightId;
                    uint leftLane;
                    uint rightLane;
                    netSegment.GetLeftAndRightLanes(nodeID, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, lane, out leftId, out rightId, out leftLane, out rightLane);
                    ushort curRightLane = segment;
                    ushort curLeftLane = segment;
                    if (leftLane == 0u || rightLane == 0u)
                    {
                        ushort leftSegment;
                        ushort rightSegment;
                        netSegment.GetLeftAndRightSegments(nodeID, out leftSegment, out rightSegment);
                        int segCount = 0;
                        while (leftSegment != 0 && leftSegment != segment && leftLane == 0u)
                        {
                            int leftId2;
                            int rightId2;
                            uint leftLane2;
                            uint rightLane2;
                            instance.m_segments.m_buffer[leftSegment].GetLeftAndRightLanes(nodeID, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, -1, out leftId2, out rightId2, out leftLane2, out rightLane2);
                            if (rightLane2 != 0u)
                            {
                                curRightLane = leftSegment;
                                leftId = rightId2;
                                leftLane = rightLane2;
                            }
                            else
                            {
                                leftSegment = instance.m_segments.m_buffer[leftSegment].GetLeftSegment(nodeID);
                            }
                            if (++segCount == 8)
                            {
                                break;
                            }
                        }
                        segCount = 0;
                        while (rightSegment != 0 && rightSegment != segment && rightLane == 0u)
                        {
                            int leftId2;
                            int rightId2;
                            uint leftLane2;
                            uint rightLane2;
                            instance.m_segments.m_buffer[rightSegment].GetLeftAndRightLanes(nodeID, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, -1, out leftId2, out rightId2, out leftLane2, out rightLane2);
                            if (leftLane2 != 0u)
                            {
                                curLeftLane = rightSegment;
                                rightId = leftId2;
                                rightLane = leftLane2;
                            }
                            else
                            {
                                rightSegment = instance.m_segments.m_buffer[rightSegment].GetRightSegment(nodeID);
                            }
                            if (++segCount == 8)
                            {
                                break;
                            }
                        }
                    }
                    if (leftLane != 0u && (curRightLane != segment || canUse))
                    {
                        ProcessItem(item, nodeID, curRightLane, ref instance.m_segments.m_buffer[curRightLane], connectOffset, leftId, leftLane);
                    }
                    if (rightLane != 0u && rightLane != leftLane && (curLeftLane != segment || canUse))
                    {
                        ProcessItem(item, nodeID, curLeftLane, ref instance.m_segments.m_buffer[curLeftLane], connectOffset, rightId, rightLane);
                    }
                    int laneId2;
                    uint lane2;
                    if ((m_vehicleTypes & VehicleInfo.VehicleType.Bicycle) != VehicleInfo.VehicleType.None && netSegment.GetClosestLane(item.m_position.m_lane, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Bicycle, out laneId2, out lane2))
                    {
                        ProcessItem(item, nodeID, segment, ref netSegment, connectOffset, laneId2, lane2);
                    }
                }
                else
                {
                    for (int j = 0; j < 8; j++)
                    {
                        ushort nextSegment = node.GetSegment(j);
                        if (nextSegment != 0 && nextSegment != segment)
                        {
                            ProcessItem(item, nodeID, nextSegment, ref instance.m_segments.m_buffer[nextSegment], ref curLaneId, connectOffset, false, true);
                        }
                    }
                }
                NetInfo.LaneType laneType = m_laneTypes & ~NetInfo.LaneType.Pedestrian;
                VehicleInfo.VehicleType vehicleType = m_vehicleTypes & ~VehicleInfo.VehicleType.Bicycle;
                laneType &= ~(item.m_lanesUsed & NetInfo.LaneType.Vehicle);
                int nextId;
                uint nextLane;
                if (laneType != NetInfo.LaneType.None && vehicleType != VehicleInfo.VehicleType.None && instance.m_segments.m_buffer[segment].GetClosestLane(lane, laneType, vehicleType, out nextId, out nextLane))
                {
                    NetInfo.Lane lane2 = segmentInfo.m_lanes[nextId];
                    byte connectOffset2;
                    if ((instance.m_segments.m_buffer[segment].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None == ((byte) (lane2.m_finalDirection & NetInfo.Direction.Backward) != 0))
                    {
                        connectOffset2 = 1;
                    }
                    else
                    {
                        connectOffset2 = 254;
                    }
                    ProcessItem(item, nodeID, segment, ref instance.m_segments.m_buffer[segment], connectOffset2, nextId, nextLane);
                }
            }
            else
            {
                bool hasWayOut = (node.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
                bool canUsePedestrian = (byte) (m_laneTypes & NetInfo.LaneType.Pedestrian) != 0;
                bool enablePedestrian = false;
                byte connectOffset3 = 0;
                if (canUsePedestrian)
                {
                    if (isBicycle)
                    {
                        connectOffset3 = connectOffset;
                        enablePedestrian = (node.Info.m_class.m_service == ItemClass.Service.Beautification);
                    }
                    else if (m_vehicleLane != 0u)
                    {
                        if (m_vehicleLane != item.m_laneID)
                        {
                            canUsePedestrian = false;
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
                        connectOffset3 = (byte) m_pathRandomizer.UInt32(1u, 254u);
                    }
                }
                ushort rightSegment = instance.m_segments.m_buffer[item.m_position.m_segment].GetRightSegment(nodeID);
                for (int i = 0; i < 8; i++)
                {
                    if (rightSegment == 0 || rightSegment == item.m_position.m_segment)
                    {
                        break;
                    }
                    if (ProcessItem(item, nodeID, rightSegment, ref instance.m_segments.m_buffer[rightSegment], ref curLaneId, connectOffset, true, enablePedestrian))
                    {
                        hasWayOut = true;
                    }
                    rightSegment = instance.m_segments.m_buffer[rightSegment].GetRightSegment(nodeID);
                }
                if (hasWayOut)
                {
                    rightSegment = item.m_position.m_segment;
                    ProcessItem(item, nodeID, rightSegment, ref instance.m_segments.m_buffer[rightSegment], ref curLaneId, connectOffset, true, false);
                }
                if (canUsePedestrian)
                {
                    rightSegment = item.m_position.m_segment;
                    int laneId2;
                    uint lane2;
                    if (instance.m_segments.m_buffer[rightSegment].GetClosestLane(item.m_position.m_lane, NetInfo.LaneType.Pedestrian, m_vehicleTypes, out laneId2, out lane2))
                    {
                        ProcessItem(item, nodeID, rightSegment, ref instance.m_segments.m_buffer[rightSegment], connectOffset3, laneId2, lane2);
                    }
                }
            }
            if (node.m_lane != 0u)
            {
                bool targetDisabled = (node.m_flags & NetNode.Flags.Disabled) != NetNode.Flags.None;
                ushort segment = instance.m_lanes.m_buffer[node.m_lane].m_segment;
                if (segment != 0 && segment != item.m_position.m_segment)
                {
                    ProcessItem(item, nodeID, targetDisabled, segment, ref instance.m_segments.m_buffer[segment], node.m_lane, node.m_laneOffset, connectOffset);
                }
            }
        }

        private float CalculateLaneSpeed(byte startOffset, byte endOffset, ref NetSegment segment, NetInfo.Lane laneInfo, uint laneId)
        {
            float speedLimit = RoadManager.instance.m_lanes[laneId].m_speed;
            //float speedLimit = laneInfo.m_speedLimit;

            NetInfo.Direction direction = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? laneInfo.m_finalDirection : NetInfo.InvertDirection(laneInfo.m_finalDirection);
            if ((byte) (direction & NetInfo.Direction.Avoid) == 0)
            {
                //return laneInfo.m_speedLimit;
                return speedLimit;
            }
            if (endOffset > startOffset && direction == NetInfo.Direction.AvoidForward)
            {
                //return laneInfo.m_speedLimit * 0.1f;
                return speedLimit*0.1f;
            }
            if (endOffset < startOffset && direction == NetInfo.Direction.AvoidBackward)
            {
                //return laneInfo.m_speedLimit * 0.1f;
                return speedLimit*0.1f;
            }
            //return laneInfo.m_speedLimit * 0.2f;
            return speedLimit*0.2f;
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
            uint laneId = segment.m_lanes;
            float maxSpeed = 1f;
            float laneSpeed = 1f;
            NetInfo.LaneType laneType = NetInfo.LaneType.None;
            if (item.m_position.m_lane < info2.m_lanes.Length)
            {
                uint l = instance.m_segments.m_buffer[item.m_position.m_segment].m_lanes;
                for (int n = 0; l != 0 && n < item.m_position.m_lane; ++n)
                    l = instance.m_lanes.m_buffer[l].m_nextLane;

                NetInfo.Lane lane2 = info2.m_lanes[item.m_position.m_lane];
                //num3 = lane2.m_speedLimit;
                maxSpeed = RoadManager.instance.m_lanes[l].m_speed;
                laneType = lane2.m_laneType;
                laneSpeed = CalculateLaneSpeed(connectOffset, item.m_position.m_offset, ref instance.m_segments.m_buffer[item.m_position.m_segment], lane2, l);
            }
            float averageLength = instance.m_segments.m_buffer[item.m_position.m_segment].m_averageLength;
            float weight = Mathf.Abs(connectOffset - item.m_position.m_offset)*WEIGHT_FACTOR*averageLength;
            float totalWeight = item.m_methodDistance + weight;
            float calculatedDistance = item.m_comparisonValue + weight/(laneSpeed*m_maxLength);
            Vector3 b = instance.m_lanes.m_buffer[item.m_laneID].CalculatePosition(connectOffset*WEIGHT_FACTOR);
            int laneItr = 0;
            while (laneItr < num && laneId != 0u)
            {
                if (lane == laneId)
                {
                    NetInfo.Lane curLane = info.m_lanes[laneItr];
                    if (curLane.CheckType(m_laneTypes, m_vehicleTypes))
                    {
                        Vector3 a = instance.m_lanes.m_buffer[lane].CalculatePosition(offset*WEIGHT_FACTOR);
                        float distance = Vector3.Distance(a, b);
                        BufferItem item2;
                        item2.m_position.m_segment = segmentID;
                        item2.m_position.m_lane = (byte) laneItr;
                        item2.m_position.m_offset = offset;
                        if (laneType != curLane.m_laneType)
                        {
                            item2.m_methodDistance = 0f;
                        }
                        else
                        {
                            item2.m_methodDistance = totalWeight + distance;
                        }
                        if (curLane.m_laneType != NetInfo.LaneType.Pedestrian || item2.m_methodDistance < 1000f)
                        {
                            item2.m_comparisonValue = calculatedDistance + distance/((maxSpeed + RoadManager.instance.m_lanes[lane].m_speed /*lane3.m_speedLimit*/)*0.5f*m_maxLength);
                            if (lane == m_startLaneA)
                            {
                                float laneSpeed2 = CalculateLaneSpeed(m_startOffsetA, item2.m_position.m_offset, ref segment, curLane, lane);
                                float laneWeight = Mathf.Abs(item2.m_position.m_offset - m_startOffsetA)*WEIGHT_FACTOR;
                                item2.m_comparisonValue += laneWeight*segment.m_averageLength/(laneSpeed2*m_maxLength);
                            }
                            if (lane == m_startLaneB)
                            {
                                float laneSpeed2 = CalculateLaneSpeed(m_startOffsetB, item2.m_position.m_offset, ref segment, curLane, lane);
                                float laneWeight2 = Mathf.Abs(item2.m_position.m_offset - m_startOffsetB)*WEIGHT_FACTOR;
                                item2.m_comparisonValue += laneWeight2*segment.m_averageLength/(laneSpeed2*m_maxLength);
                            }
                            if ((segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None)
                            {
                                item2.m_direction = NetInfo.InvertDirection(curLane.m_finalDirection);
                            }
                            else
                            {
                                item2.m_direction = curLane.m_finalDirection;
                            }
                            item2.m_laneID = lane;
                            item2.m_lanesUsed = (item.m_lanesUsed | curLane.m_laneType);
                            AddBufferItem(item2, item.m_position);
                        }
                    }
                    return;
                }
                laneId = instance.m_lanes.m_buffer[laneId].m_nextLane;
                laneItr++;
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
            NetSegment item2 = instance.m_segments.m_buffer[item.m_position.m_segment];
            NetInfo info2 = item2.Info;
            int laneCount = info.m_lanes.Length;
            uint laneId = segment.m_lanes;
            NetInfo.Direction direction = (targetNode != segment.m_startNode) ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
            NetInfo.Direction direction2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? direction : NetInfo.InvertDirection(direction);
            float angle = 0.01f - Mathf.Min(info.m_maxTurnAngleCos, info2.m_maxTurnAngleCos);
            if (angle < 1f)
            {
                Vector3 vector;
                if (targetNode == item2.m_startNode)
                {
                    vector = item2.m_startDirection;
                }
                else
                {
                    vector = item2.m_endDirection;
                }
                Vector3 vector2;
                if ((byte) (direction & NetInfo.Direction.Forward) != 0)
                {
                    vector2 = segment.m_endDirection;
                }
                else
                {
                    vector2 = segment.m_startDirection;
                }
                float dist = vector.x*vector2.x + vector.z*vector2.z;
                if (dist >= angle)
                {
                    return result;
                }
            }
            float maxlaneSpeed = 1f;
            float laneSpeed = 1f;
            NetInfo.LaneType laneType = NetInfo.LaneType.None;
            VehicleInfo.VehicleType vehicleType = VehicleInfo.VehicleType.None;
            if (item.m_position.m_lane < info2.m_lanes.Length)
            {
                uint l = item2.m_lanes;
                for (int n = 0; l != 0 && n < item.m_position.m_lane; ++n)
                    l = instance.m_lanes.m_buffer[l].m_nextLane;

                NetInfo.Lane lane = info2.m_lanes[item.m_position.m_lane];
                laneType = lane.m_laneType;
                vehicleType = lane.m_vehicleType;
                //num5 = lane.m_speedLimit;
                //num6 = this.CalculateLaneSpeed(connectOffset, item.m_position.m_offset, ref instance.m_segments.m_buffer[(int)item.m_position.m_segment], lane, 0);
                maxlaneSpeed = RoadManager.instance.m_lanes[l].m_speed;
                laneSpeed = CalculateLaneSpeed(connectOffset, item.m_position.m_offset, ref item2, lane, l);
            }
            float avgLength = item2.m_averageLength;
            if (!m_stablePath)
            {
                Randomizer randomizer = new Randomizer(m_pathFindIndex << 16 | item.m_position.m_segment);
                avgLength *= (randomizer.Int32(900, 1000 + instance.m_segments.m_buffer[item.m_position.m_segment].m_trafficDensity*10) + m_pathRandomizer.Int32(20u))*0.001f;
            }
            if (m_isHeavyVehicle && (item2.m_flags & NetSegment.Flags.HeavyBan) != NetSegment.Flags.None)
            {
                avgLength *= 10f;
            }
            float curDistance = Mathf.Abs(connectOffset - item.m_position.m_offset)*WEIGHT_FACTOR*avgLength;
            float distance = item.m_methodDistance + curDistance;
            float distanceWeight = item.m_comparisonValue + curDistance/(laneSpeed*m_maxLength);
            Vector3 position = instance.m_lanes.m_buffer[item.m_laneID].CalculatePosition(connectOffset*WEIGHT_FACTOR);
            int targetIndex = currentTargetIndex;
            bool isTransition = (instance.m_nodes.m_buffer[targetNode].m_flags & NetNode.Flags.Transition) != NetNode.Flags.None;
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
            int laneItr = 0;
            while (laneItr < laneCount && laneId != 0u)
            {
                NetInfo.Lane lane2 = info.m_lanes[laneItr];
                bool canConnect = true;
                if ((instance.m_lanes.m_buffer[laneId].m_flags & RoadManager.Lane.CONTROL_BIT) != 0)
                    canConnect = RoadManager.instance.CheckLaneConnection(laneId, item.m_laneID, m_vehicleType);
                if ((byte) (lane2.m_finalDirection & direction2) != 0 && canConnect)
                {
                    if (lane2.CheckType(laneType2, vehicleType2) && (segmentID != item.m_position.m_segment || laneItr != item.m_position.m_lane) && (byte) (lane2.m_finalDirection & direction2) != 0)
                    {
                        Vector3 bezier;
                        if ((byte) (direction & NetInfo.Direction.Forward) != 0)
                        {
                            bezier = instance.m_lanes.m_buffer[laneId].m_bezier.d;
                        }
                        else
                        {
                            bezier = instance.m_lanes.m_buffer[laneId].m_bezier.a;
                        }
                        float weight = Vector3.Distance(bezier, position);
                        if (isTransition)
                        {
                            weight *= 2f;
                        }
                        if (m_prioritizeBusLanes)
                        {
                            NetInfoLane customLane2 = lane2 as NetInfoLane;
                            if (customLane2 != null && customLane2.m_specialLaneType == NetInfoLane.SpecialLaneType.BusLane)
                            {
                                weight /= 10f;
                            }
                        }
                        float num14 = weight/((maxlaneSpeed + RoadManager.instance.m_lanes[laneId].m_speed /*lane2.m_speedLimit*/)*0.5f*m_maxLength);
                        BufferItem item3;
                        item3.m_position.m_segment = segmentID;
                        item3.m_position.m_lane = (byte) laneItr;
                        item3.m_position.m_offset = (byte) (((direction & NetInfo.Direction.Forward) == 0) ? 0 : 255);
                        if (laneType != lane2.m_laneType)
                        {
                            item3.m_methodDistance = 0f;
                        }
                        else
                        {
                            item3.m_methodDistance = distance + weight;
                        }
                        if (lane2.m_laneType != NetInfo.LaneType.Pedestrian || item3.m_methodDistance < 1000f)
                        {
                            item3.m_comparisonValue = distanceWeight + num14;
                            if (laneId == m_startLaneA)
                            {
                                float speed = CalculateLaneSpeed(m_startOffsetA, item3.m_position.m_offset, ref segment, lane2, laneId);
                                float laneWeight = Mathf.Abs(item3.m_position.m_offset - m_startOffsetA)*WEIGHT_FACTOR;
                                item3.m_comparisonValue += laneWeight*segment.m_averageLength/(speed*m_maxLength);
                            }
                            if (laneId == m_startLaneB)
                            {
                                float speed = CalculateLaneSpeed(m_startOffsetB, item3.m_position.m_offset, ref segment, lane2, laneId);
                                float laneWeight = Mathf.Abs(item3.m_position.m_offset - m_startOffsetB)*WEIGHT_FACTOR;
                                item3.m_comparisonValue += laneWeight*segment.m_averageLength/(speed*m_maxLength);
                            }
                            if (!m_ignoreBlocked && (segment.m_flags & NetSegment.Flags.Blocked) != NetSegment.Flags.None && lane2.m_laneType == NetInfo.LaneType.Vehicle)
                            {
                                item3.m_comparisonValue += 0.1f;
                                result = true;
                            }
                            item3.m_direction = direction;
                            item3.m_lanesUsed = (item.m_lanesUsed | lane2.m_laneType);
                            item3.m_laneID = laneId;
                            if (lane2.m_laneType == laneType && lane2.m_vehicleType == vehicleType)
                            {
                                int firstTarget = instance.m_lanes.m_buffer[laneId].m_firstTarget;
                                int lastTarget = instance.m_lanes.m_buffer[laneId].m_lastTarget;
                                if (currentTargetIndex < firstTarget || currentTargetIndex >= lastTarget)
                                {
                                    item3.m_comparisonValue += Mathf.Max(1f, weight*3f - 3f)/((maxlaneSpeed + RoadManager.instance.m_lanes[laneId].m_speed /* lane2.m_speedLimit*/)*0.5f*m_maxLength);
                                }
                            }
                            AddBufferItem(item3, item.m_position);
                        }
                    }
                }
                else if (lane2.m_laneType == laneType && lane2.m_vehicleType == vehicleType)
                {
                    targetIndex++;
                }
                laneId = instance.m_lanes.m_buffer[laneId].m_nextLane;
                laneItr++;
            }
            currentTargetIndex = targetIndex;
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
            int length = info.m_lanes.Length;
            float distance;
            byte offset;
            NetLane currentLane = instance.m_lanes.m_buffer[lane];
            if (segmentID == item.m_position.m_segment)
            {
                Vector3 position1 = instance.m_lanes.m_buffer[item.m_laneID].CalculatePosition(connectOffset*WEIGHT_FACTOR);
                Vector3 position2 = currentLane.CalculatePosition(connectOffset*WEIGHT_FACTOR);
                distance = Vector3.Distance(position2, position1);
                offset = connectOffset;
            }
            else
            {
                NetInfo.Direction direction = (targetNode != segment.m_startNode) ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
                Vector3 position = instance.m_lanes.m_buffer[item.m_laneID].CalculatePosition(connectOffset*WEIGHT_FACTOR);
                Vector3 bezier;
                if ((byte) (direction & NetInfo.Direction.Forward) != 0)
                {
                    bezier = currentLane.m_bezier.d;
                }
                else
                {
                    bezier = currentLane.m_bezier.a;
                }
                distance = Vector3.Distance(bezier, position);
                offset = (byte) (((direction & NetInfo.Direction.Forward) == 0) ? 0 : 255);
            }
            float maxSpeed = 1f;
            float currentSpeed = 1f;
            NetInfo.LaneType laneType = NetInfo.LaneType.None;
            if (item.m_position.m_lane < info2.m_lanes.Length)
            {
                uint l = instance.m_segments.m_buffer[item.m_position.m_segment].m_lanes;
                for (int n = 0; l != 0 && n < item.m_position.m_lane; ++n)
                    l = instance.m_lanes.m_buffer[l].m_nextLane;

                NetInfo.Lane lane2 = info2.m_lanes[item.m_position.m_lane];
                //num3 = lane2.m_speedLimit;
                maxSpeed = RoadManager.instance.m_lanes[l].m_speed;
                laneType = lane2.m_laneType;
                currentSpeed = CalculateLaneSpeed(connectOffset, item.m_position.m_offset, ref instance.m_segments.m_buffer[item.m_position.m_segment], lane2, l);
            }
            float averageLength = instance.m_segments.m_buffer[item.m_position.m_segment].m_averageLength;
            float currentDistance = Mathf.Abs(connectOffset - item.m_position.m_offset)*WEIGHT_FACTOR*averageLength;
            float totalDistance = item.m_methodDistance + currentDistance;
            float weight = item.m_comparisonValue + currentDistance/(currentSpeed*m_maxLength);
            if (laneIndex < length)
            {
                NetInfo.Lane newLane = info.m_lanes[laneIndex];
                BufferItem newItem;
                newItem.m_position.m_segment = segmentID;
                newItem.m_position.m_lane = (byte) laneIndex;
                newItem.m_position.m_offset = offset;
                if (laneType != newLane.m_laneType)
                {
                    newItem.m_methodDistance = 0f;
                }
                else
                {
                    if (item.m_methodDistance == 0f)
                    {
                        weight += 100f/(0.25f*m_maxLength);
                    }
                    newItem.m_methodDistance = totalDistance + distance;
                }
                if (newLane.m_laneType != NetInfo.LaneType.Pedestrian || newItem.m_methodDistance < 1000f)
                {
                    newItem.m_comparisonValue = weight + distance/((maxSpeed + RoadManager.instance.m_lanes[lane].m_speed /*lane3.m_speedLimit*/)*0.25f*m_maxLength);
                    if (lane == m_startLaneA)
                    {
                        float segmentMaxSpeed = CalculateLaneSpeed(m_startOffsetA, newItem.m_position.m_offset, ref segment, newLane, lane);
                        float segmentCurSpeed = Mathf.Abs(newItem.m_position.m_offset - m_startOffsetA)*WEIGHT_FACTOR;
                        newItem.m_comparisonValue += segmentCurSpeed*segment.m_averageLength/(segmentMaxSpeed*m_maxLength);
                    }
                    if (lane == m_startLaneB)
                    {
                        float segmentMaxSpeed = CalculateLaneSpeed(m_startOffsetB, newItem.m_position.m_offset, ref segment, newLane, lane);
                        float segmentCurSpeed = Mathf.Abs(newItem.m_position.m_offset - m_startOffsetB)*WEIGHT_FACTOR;
                        newItem.m_comparisonValue += segmentCurSpeed*segment.m_averageLength/(segmentMaxSpeed*m_maxLength);
                    }
                    if ((segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None)
                    {
                        newItem.m_direction = NetInfo.InvertDirection(newLane.m_finalDirection);
                    }
                    else
                    {
                        newItem.m_direction = newLane.m_finalDirection;
                    }
                    newItem.m_laneID = lane;
                    newItem.m_lanesUsed = (item.m_lanesUsed | newLane.m_laneType);
                    AddBufferItem(newItem, item.m_position);
                }
            }
        }

        private void AddBufferItem(BufferItem item, PathUnit.Position target)
        {
            uint num = m_laneLocation[item.m_laneID];
            uint num2 = num >> 16;
            int num3 = (int) (num & 65535u);
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
                num6 = Mathf.Max(Mathf.RoundToInt(item.m_comparisonValue*NetManager.MAX_ASSET_SEGMENTS), m_bufferMinPos);
                if (num6 == num4)
                {
                    m_buffer[num3] = item;
                    m_laneTarget[item.m_laneID] = target;
                    return;
                }
                int num7 = num4 << 6 | m_bufferMax[num4]--;
                BufferItem bufferItem = m_buffer[num7];
                m_laneLocation[bufferItem.m_laneID] = num;
                m_buffer[num3] = bufferItem;
            }
            else
            {
                num6 = Mathf.Max(Mathf.RoundToInt(item.m_comparisonValue*NetManager.MAX_ASSET_SEGMENTS), m_bufferMinPos);
            }
            if (num6 >= NetManager.MAX_ASSET_SEGMENTS)
            {
                return;
            }
            while (m_bufferMax[num6] == 63)
            {
                num6++;
                if (num6 == NetManager.MAX_ASSET_SEGMENTS)
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
            m_laneLocation[item.m_laneID] = (m_pathFindIndex << 16 | (uint) num3);
            m_laneTarget[item.m_laneID] = target;
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
                    m_queueFirst = m_pathUnits.m_buffer[m_calculating].m_nextPathUnit;
                    if (m_queueFirst == 0u)
                    {
                        m_queueLast = 0u;
                        m_queuedPathFindCount = 0;
                    }
                    else
                    {
                        m_queuedPathFindCount--;
                    }
                    m_pathUnits.m_buffer[m_calculating].m_nextPathUnit = 0u;
                    m_pathUnits.m_buffer[m_calculating].m_pathFindFlags = m_pathUnits.m_buffer[m_calculating].m_pathFindFlags.ClearFlags(PathUnit.FLAG_QUEUED).SetFlags(PathUnit.FLAG_CALCULATING);
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
                        PathFindImplementation(m_calculating, ref m_pathUnits.m_buffer[m_calculating]);
                        stopwatch.Stop();
                        totalMs += stopwatch.ElapsedMilliseconds;
                        count++;
                        if (count == 10000)
                        {
                            File.AppendAllText("TimeThread" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n\nMs\nTime to calculate 10,000 Paths: " + totalMs + " ms\nAverage time/path: " + ((double) totalMs/count) + "ms\n");
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
                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Path find error: " + ex.Message /* + " - " + m_vehicleType + " - " + m_vehicleTypes*/+ Environment.NewLine + ex.StackTrace);
                    Logger.LogToFile("PathFind:PathFindImplementation(): Path find error: " + ex.Message + Environment.NewLine + ex.StackTrace);
                    m_pathUnits.m_buffer[m_calculating].m_pathFindFlags = m_pathUnits.m_buffer[m_calculating].m_pathFindFlags.SetFlags(PathUnit.FLAG_FAILED);
                }
                while (!Monitor.TryEnter(m_queueLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
                {
                }
                try
                {
                    m_pathUnits.m_buffer[m_calculating].m_pathFindFlags = m_pathUnits.m_buffer[m_calculating].m_pathFindFlags.ClearFlags(PathUnit.FLAG_CALCULATING);
                    Singleton<PathManager>.instance.ReleasePath(m_calculating);
                    m_calculating = 0u;
                    Monitor.Pulse(m_queueLock);
                }
                finally
                {
                    Monitor.Exit(m_queueLock);
                }
            }

            Logger.LogToFile("TimeThread" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n\nMs\nTime to calculate " + count + " Paths: " + totalMs + " ms\nAverage time/path: " + ((double) totalMs/count) + "ms\n");
        }

        private struct BufferItem
        {
            public float m_comparisonValue;
            public NetInfo.Direction m_direction;
            public uint m_laneID;
            public NetInfo.LaneType m_lanesUsed;
            public float m_methodDistance;
            public PathUnit.Position m_position;
        }
    }
}