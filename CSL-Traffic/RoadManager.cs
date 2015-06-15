using ColossalFramework;
using ColossalFramework.IO;
using ColossalFramework.Math;
using ICities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using UnityEngine;

namespace CSL_Traffic
{
    public class RoadManager : Singleton<RoadManager>
    {
        public class Data : SerializableDataExtensionBase
        {
            const string LEGACY_LANE_DATA_ID = "Traffic++_RoadManager_Lanes";
            const string LANE_DATA_ID = "Traffic++_RoadManagerData_Lanes";
            const uint VERSION = 1;
            
            public override void OnLoadData()
            {
                if ((CSLTraffic.Options & OptionsManager.ModOptions.GhostMode) == OptionsManager.ModOptions.GhostMode)
                {
                    instance.InitializeData();
                    return;
                }

                Logger.LogInfo("Loading road data. Time: " + Time.realtimeSinceStartup);

                byte[] data = serializableDataManager.LoadData(LANE_DATA_ID);
                if (data == null)
                {
                    if (!HandleLegacyData())
                        instance.InitializeData();
                    return;
                }

                int index = 0;
                uint version = BitConverter.ToUInt32(data, index);
                index += 4;

                RoadManager roadManager = instance;
                FastList<ushort> nodesList = new FastList<ushort>();
                int length = data.Length;
                while (index < length)
                {
                    uint laneId = BitConverter.ToUInt32(data, index);
                    index += 4;
                    ushort nodeId = BitConverter.ToUInt16(data, index);
                    index += 2;
                    float speed = BitConverter.ToSingle(data, index);
                    index += 4;
                    int vehicleType = BitConverter.ToInt32(data, index);
                    index += 4;

                    int connectionsCount = BitConverter.ToInt32(data, index);
                    index += 4;
                    HashSet<uint> connectionsOut = new HashSet<uint>();
                    for (int i = 0; i < connectionsCount; i++)
                    {
                        connectionsOut.Add(BitConverter.ToUInt32(data, index));
                        index += 4;
                    }
                    if (connectionsCount > 0)
                        nodesList.Add(nodeId);

                    connectionsCount = BitConverter.ToInt32(data, index);
                    index += 4;
                    HashSet<uint> connectionsIn = new HashSet<uint>();
                    for (int i = 0; i < connectionsCount; i++)
                    {
                        connectionsIn.Add(BitConverter.ToUInt32(data, index));
                        index += 4;
                    }

                    roadManager.m_lanes[laneId] = new Lane(laneId, (VehicleType)vehicleType, speed, nodeId, connectionsOut, connectionsIn);
                }

                RoadCustomizerTool customizerTool = ToolsModifierControl.GetTool<RoadCustomizerTool>();
                foreach (ushort nodeId in nodesList)
                    customizerTool.SetNodeMarkers(nodeId);

                for (int i = 0; i < roadManager.m_lanes.Length; i++)
                {
                    if (roadManager.m_lanes[i].m_isAlive)
                        roadManager.m_lanes[i].UpdateArrows();
                }
            }

            public override void OnSaveData()
            {
                if ((CSLTraffic.Options & OptionsManager.ModOptions.GhostMode) == OptionsManager.ModOptions.GhostMode)
                    return;

                Logger.LogInfo("Saving road data!");

                List<byte> data = new List<byte>();
                data.AddRange(BitConverter.GetBytes(VERSION));

                RoadManager roadManager = instance;
                int length = roadManager.m_lanes.Length;
                for (int i = 0; i < length; i++)
                {
                    if (!roadManager.m_lanes[i].m_isAlive)
                        continue;

                    Lane lane = roadManager.m_lanes[i];
                    data.AddRange(BitConverter.GetBytes(lane.m_laneId));
                    data.AddRange(BitConverter.GetBytes(lane.m_nodeId));
                    data.AddRange(BitConverter.GetBytes(lane.m_speed));
                    data.AddRange(BitConverter.GetBytes((int)lane.m_vehicleTypes));

                    uint[] connections = lane.GetConnectionsAsArray();
                    data.AddRange(BitConverter.GetBytes(connections.Length));
                    for (int j = 0; j < connections.Length; j++)
                        data.AddRange(BitConverter.GetBytes(connections[j]));

                    connections = lane.GetConnectionsInAsArray();
                    data.AddRange(BitConverter.GetBytes(connections.Length));
                    for (int j = 0; j < connections.Length; j++)
                        data.AddRange(BitConverter.GetBytes(connections[j]));
                }
                serializableDataManager.SaveData(LANE_DATA_ID, data.ToArray());
            }

            bool HandleLegacyData()
            {
                byte[] data = serializableDataManager.LoadData(LEGACY_LANE_DATA_ID);
                if (data == null)
                    return false;

                Logger.LogInfo("Loading legacy road data.");

                RoadManager roadManager = instance;

                MemoryStream memStream = new MemoryStream();
                memStream.Write(data, 0, data.Length);
                memStream.Position = 0;

                BinaryFormatter binaryFormatter = new BinaryFormatter();
                binaryFormatter.Binder = new LegacyBinder();
                try
                {
                    LegacyLane[] lanes = (LegacyLane[])binaryFormatter.Deserialize(memStream);

                    FastList<ushort> nodesList = new FastList<ushort>();
                    foreach (LegacyLane legacyLane in lanes)
                    {
                        if (legacyLane == null)
                            continue;

                        Lane lane = new Lane(legacyLane.m_laneId, legacyLane.m_vehicleTypes, legacyLane.m_speed, legacyLane.m_nodeId);

                        if (lane.m_speed == 0)
                        {
                            NetSegment segment = NetManager.instance.m_segments.m_buffer[NetManager.instance.m_lanes.m_buffer[lane.m_laneId].m_segment];
                            NetInfo info = segment.Info;
                            uint l = segment.m_lanes;
                            int n = 0;
                            while (l != lane.m_laneId && n < info.m_lanes.Length)
                            {
                                l = NetManager.instance.m_lanes.m_buffer[l].m_nextLane;
                                n++;
                            }

                            if (n < info.m_lanes.Length)
                                lane.m_speed = info.m_lanes[n].m_speedLimit;
                        }

                        roadManager.m_lanes[lane.m_laneId] = lane;
                    }

                    foreach (LegacyLane legacyLane in lanes)
                    {
                        if (legacyLane == null)
                            continue;

                        foreach (uint connection in legacyLane.m_laneConnections)
                        {
                            roadManager.m_lanes[legacyLane.m_laneId].AddConnection(connection);
                        }

                        roadManager.m_lanes[legacyLane.m_laneId].UpdateArrows();
                        if (roadManager.m_lanes[legacyLane.m_laneId].ConnectionCount() > 0)
                            nodesList.Add(roadManager.m_lanes[legacyLane.m_laneId].m_nodeId);
                    }

                    RoadCustomizerTool customizerTool = ToolsModifierControl.GetTool<RoadCustomizerTool>();
                    foreach (ushort nodeId in nodesList)
                        customizerTool.SetNodeMarkers(nodeId);

                    Logger.LogInfo("Finished loading road data. Time: " + Time.realtimeSinceStartup);
                }
                catch (Exception e)
                {
                    Logger.LogInfo("Unexpected " + e.GetType().Name + " loading road data.");
                }
                finally
                {
                    memStream.Close();
                }
                return false;
            }
        }

        [Flags]
        public enum VehicleType
        {
            None                = 0,

            Ambulance           = 1,
            Bus                 = 2,
            CargoTruck          = 4,
            FireTruck           = 8,
            GarbageTruck        = 16,
            Hearse              = 32,
            PassengerCar        = 64,
            PoliceCar           = 128,

            Emergency           = 32768,
            EmergencyVehicles   = Emergency | Ambulance | FireTruck | PoliceCar,
            ServiceVehicles     = EmergencyVehicles | Bus | GarbageTruck | Hearse,

            All                 = ServiceVehicles | PassengerCar | CargoTruck
        }

        public struct Lane
        {
            public const ushort CONTROL_BIT = 2048;

            public uint m_laneId;
            public ushort m_nodeId;
            private HashSet<uint> m_connectionsOut;
            private HashSet<uint> m_connectionsIn;
            public VehicleType m_vehicleTypes;
            public float m_speed;
            public bool m_isAlive;
            private volatile bool m_lockConnections;

            public Lane(uint laneId, VehicleType vehicleTypes = VehicleType.All, float speedLimit = 1f, ushort nodeId = 0)
                : this(laneId, vehicleTypes, speedLimit, nodeId, new HashSet<uint>(), new HashSet<uint>())
            {
                //this.m_laneId = laneId;
                //this.m_nodeId = nodeId;
                //this.m_connectionsOut = new HashSet<uint>();
                //this.m_connectionsIn = new HashSet<uint>();
                //this.m_vehicleTypes = vehicleTypes;
                //this.m_speed = speedLimit;
                //this.m_lockConnections = false;
                //this.m_isAlive = true;
            }

            public Lane(uint laneId, VehicleType vehicleTypes, float speedLimit, ushort nodeId, HashSet<uint> connectionsOut, HashSet<uint> connectionsIn)
            {
                m_laneId = laneId;
                m_nodeId = nodeId;
                m_connectionsOut = connectionsOut;
                m_connectionsIn = connectionsIn;
                m_vehicleTypes = vehicleTypes;
                m_speed = speedLimit;
                m_lockConnections = false;
                m_isAlive = true;
            }

            public void Release() 
            {
                m_isAlive = false;
                m_laneId = m_nodeId = 0;
                m_vehicleTypes = VehicleType.All;
                m_speed = 1f;
                RemoveAllConnections();
            }

            public bool AddConnection(uint laneId)
            {
                if (laneId == m_laneId || m_connectionsOut == null)
                    return false;

                bool exists = false;
                m_lockConnections = true;
                lock (m_connectionsOut)
                {
                    exists = !m_connectionsOut.Add(laneId);
                }
                m_lockConnections = false;

                if (exists)
                    return false;

                NetManager.instance.m_lanes.m_buffer[m_laneId].m_flags |= CONTROL_BIT;

                instance.m_lanes[laneId].AddConnectionIn(m_laneId);

                UpdateArrows();

                return true;
            }

            private bool AddConnectionIn(uint laneId)
            {
                if (laneId == m_laneId || m_connectionsIn == null)
                    return false;

                bool result = false;
                lock (m_connectionsIn)
                {
                    result = m_connectionsIn.Add(laneId);
                }
                
                return result;
            }

            public bool RemoveConnection(uint laneId)
            {
                if (m_connectionsOut == null)
                    return false;

                bool removed = false;
                int count = -1;
                m_lockConnections = true;
                lock (m_connectionsOut)
                {
                    removed = m_connectionsOut.Remove(laneId);
                    count = m_connectionsOut.Count;
                }
                m_lockConnections = false;

                if (!removed)
                    return false;

                if (count == 0 && m_vehicleTypes == VehicleType.All)
                    NetManager.instance.m_lanes.m_buffer[m_laneId].m_flags = (ushort)(NetManager.instance.m_lanes.m_buffer[m_laneId].m_flags & ~CONTROL_BIT);

                instance.m_lanes[laneId].RemoveConnectionIn(m_laneId);

                UpdateArrows();

                return removed;
            }

            private bool RemoveConnectionIn(uint laneId)
            {
                if (m_connectionsIn == null)
                    return false;

                bool result = false;
                lock (m_connectionsIn)
                {
                    result = m_connectionsIn.Remove(laneId);
                }

                return result;
            }

            public void RemoveAllConnections()
            {
                if (m_connectionsOut == null)
                    return;

                uint[] connections = GetConnectionsAsArray();
                for (int i = 0; i < connections.Length; i++)
                    RemoveConnection(connections[i]);

                RoadManager roadManager = instance;
                connections = GetConnectionsInAsArray();
                for (int i = 0; i < connections.Length; i++)
                    roadManager.m_lanes[connections[i]].RemoveConnection(m_laneId);
            }

            public uint[] GetConnectionsAsArray()
            {
                if (m_connectionsOut == null)
                    return null;

                uint[] connections = null;
                lock (m_connectionsOut)
                {
                    connections = m_connectionsOut.ToArray();
                }
                
                return connections;
            }

            public uint[] GetConnectionsInAsArray()
            {
                if (m_connectionsIn == null)
                    return null;

                uint[] connections = null;
                lock (m_connectionsIn)
                {
                    connections = m_connectionsIn.ToArray();
                }

                return connections;
            }

            public int ConnectionCount()
            {
                if (m_connectionsOut == null)
                    return 0;

                int count = 0;
                lock (m_connectionsOut)
                {
                    count = m_connectionsOut.Count();
                }

                return count;
            }

            public bool ConnectsTo(uint laneId)
            {
                if (m_connectionsOut == null)
                    return true;

                // This is my attempt at avoiding locking unless strictly necessary, for performance reasons
                if (!m_lockConnections)
                    return m_connectionsOut.Count == 0 || m_connectionsOut.Contains(laneId);
                
                bool result = true;
                lock (m_connectionsOut)
                {
                    result = m_connectionsOut.Count == 0 || m_connectionsOut.Contains(laneId);
                }

                return result;
            }

            public void UpdateArrows()
            {
                //VerifyConnections();
                NetLane lane = NetManager.instance.m_lanes.m_buffer[m_laneId];
                NetSegment segment = NetManager.instance.m_segments.m_buffer[lane.m_segment];

                if ((m_nodeId == 0 && !FindNode(segment)) || NetManager.instance.m_nodes.m_buffer[m_nodeId].CountSegments() <= 2)
                    return;

                if (ConnectionCount() == 0)
                {
                    SetDefaultArrows(lane.m_segment, ref NetManager.instance.m_segments.m_buffer[lane.m_segment]);
                    return;
                }

                NetLane.Flags flags = (NetLane.Flags)lane.m_flags;
                flags &= ~(NetLane.Flags.LeftForwardRight);

                Vector3 segDir = segment.GetDirection(m_nodeId);
                uint[] connections = GetConnectionsAsArray();
                foreach (uint connection in connections)
                {
                    ushort seg = NetManager.instance.m_lanes.m_buffer[connection].m_segment;
                    Vector3 dir = NetManager.instance.m_segments.m_buffer[seg].GetDirection(m_nodeId);
                    if (Vector3.Angle(segDir, dir) > 150f)
                    {
                        flags |= NetLane.Flags.Forward;
                    }
                    else 
                    {
                        
                        if (Vector3.Dot(Vector3.Cross(segDir, -dir), Vector3.up) > 0f)
                            flags |= NetLane.Flags.Right;
                        else
                            flags |= NetLane.Flags.Left;
                    }
                }

                NetManager.instance.m_lanes.m_buffer[m_laneId].m_flags = (ushort)flags;
            }

            bool FindNode(NetSegment segment)
            {
                uint laneId = segment.m_lanes;
                NetInfo info = segment.Info;
                int laneCount = info.m_lanes.Length;
                int laneIndex = 0;
                for (; laneIndex < laneCount && laneId != 0; laneIndex++)
                {
                    if (laneId == m_laneId)
                        break;
                    laneId = NetManager.instance.m_lanes.m_buffer[laneId].m_nextLane;
                }

                if (laneIndex < laneCount)
                {
                    NetInfo.Direction laneDir = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? info.m_lanes[laneIndex].m_finalDirection : NetInfo.InvertDirection(info.m_lanes[laneIndex].m_finalDirection);

                    if ((laneDir & (NetInfo.Direction.Forward | NetInfo.Direction.Avoid)) == NetInfo.Direction.Forward)
                        m_nodeId = segment.m_endNode;
                    else if ((laneDir & (NetInfo.Direction.Backward | NetInfo.Direction.Avoid)) == NetInfo.Direction.Backward)
                        m_nodeId = segment.m_startNode;
                    
                    return true;
                }

                return false;
            }

            void SetDefaultArrows(ushort seg, ref NetSegment segment)
            {
                NetInfo info = segment.Info;
                info.m_netAI.UpdateLanes(seg, ref segment, false);

                RoadManager roadManager = instance;
                uint laneId = segment.m_lanes;
                int laneCount = info.m_lanes.Length;
                for (int laneIndex = 0; laneIndex < laneCount && laneId != 0; laneIndex++)
                {
                    if (laneId != m_laneId && roadManager.m_lanes[laneId].ConnectionCount() > 0)
                        roadManager.m_lanes[laneId].UpdateArrows();

                    laneId = NetManager.instance.m_lanes.m_buffer[laneId].m_nextLane;
                }
            }
        }

        public Lane[] m_lanes;

        public RoadManager()
        {
            m_lanes = new Lane[NetManager.MAX_LANE_COUNT];
        }

        private void CreateLanes(uint firstLaneId, ushort segmentId)
        {
            NetSegment segment = NetManager.instance.m_segments.m_buffer[segmentId];
            NetInfo netInfo = segment.Info;
            int laneCount = netInfo.m_lanes.Length;
            int laneIndex = 0;
            for (uint l = firstLaneId; laneIndex < laneCount && l != 0; laneIndex++)
            {
                //if ((NetManager.instance.m_lanes.m_buffer[l].m_flags & Lane.CONTROL_BIT) == 0)
                if (!instance.m_lanes[l].m_isAlive)
                {
                    VehicleType vehicleTypes = VehicleType.All;

                    NetInfoLane netInfoLane = netInfo.m_lanes[laneIndex] as NetInfoLane;
                    if (netInfoLane != null)
                    {
                        vehicleTypes = netInfoLane.m_allowedVehicleTypes;
                        NetManager.instance.m_lanes.m_buffer[l].m_flags |= Lane.CONTROL_BIT;
                    }

                    instance.m_lanes[l] = new Lane(l, vehicleTypes, netInfo.m_lanes[laneIndex].m_speedLimit);
                }
                
                l = NetManager.instance.m_lanes.m_buffer[l].m_nextLane;
            }
        }

        private void CreateLane(uint laneId, ushort segmentId)
        {
            //if ((NetManager.instance.m_lanes.m_buffer[laneId].m_flags & Lane.CONTROL_BIT) != 0)
            if (instance.m_lanes[laneId].m_isAlive)
                return;
            
            NetSegment segment = NetManager.instance.m_segments.m_buffer[segmentId];
            NetInfo netInfo = segment.Info;
            int laneCount = netInfo.m_lanes.Length;
            int laneIndex = 0;
            for (uint l = segment.m_lanes; laneIndex < laneCount && l != 0; laneIndex++)
            {
                if (l == laneId)
                    break;
                l = NetManager.instance.m_lanes.m_buffer[l].m_nextLane;
            }

            if (laneIndex < laneCount)
            {
                VehicleType vehicleTypes = VehicleType.All;

                NetInfoLane netInfoLane = netInfo.m_lanes[laneIndex] as NetInfoLane;
                if (netInfoLane != null)
                {
                    vehicleTypes = netInfoLane.m_allowedVehicleTypes;
                    NetManager.instance.m_lanes.m_buffer[laneId].m_flags |= Lane.CONTROL_BIT;
                }

                instance.m_lanes[laneId] = new Lane(laneId, vehicleTypes, netInfo.m_lanes[laneIndex].m_speedLimit);
            }
            else
            {
                Logger.LogWarning("Whoops!");
            }
        }

        public void InitializeData()
        {
            NetManager netManager = NetManager.instance;
            for (int i = 0; i < NetManager.MAX_LANE_COUNT; i++)
            {
                if (!m_lanes[i].m_isAlive && (netManager.m_lanes.m_buffer[i].m_flags & (ushort)NetLane.Flags.Created) != 0)
                    CreateLanes(netManager.m_segments.m_buffer[netManager.m_lanes.m_buffer[i].m_segment].m_lanes, netManager.m_lanes.m_buffer[i].m_segment);
            }
        }

        //public Lane GetLane(uint laneId)
        //{
        //    Lane lane = m_lanes[laneId];
        //    if (lane == null || (NetManager.instance.m_lanes.m_buffer[laneId].m_flags & Lane.CONTROL_BIT) == 0)
        //        lane = CreateLane(laneId);

        //    return lane;
        //}

        #region Lane Connections

        public bool AddLaneConnection(uint laneId, uint connectionId)
        {
            //Lane lane = GetLane(laneId);
            //GetLane(connectionId); // makes sure lane information is stored

            return m_lanes[laneId].AddConnection(connectionId);
        }

        public bool RemoveLaneConnection(uint laneId, uint connectionId)
        {
            //Lane lane = GetLane(laneId);

            return m_lanes[laneId].RemoveConnection(connectionId);
        }

        public uint[] GetLaneConnections(uint laneId)
        {
            //Lane lane = GetLane(laneId);

            return m_lanes[laneId].GetConnectionsAsArray();
        }

        public bool CheckLaneConnection(uint from, uint to, VehicleType vehicleType)
        {
            return m_lanes[from].ConnectsTo(to) && (m_lanes[from].m_vehicleTypes & m_lanes[to].m_vehicleTypes & vehicleType) != VehicleType.None;
        }
        #endregion

        #region Vehicle Restrictions
        public bool CanUseLane(VehicleType vehicleType, uint laneId)
        {
            return (m_lanes[laneId].m_vehicleTypes & vehicleType) != VehicleType.None;
        }

        public VehicleType GetVehicleRestrictions(uint laneId)
        {
            return m_lanes[laneId].m_vehicleTypes;
        }

        public void SetVehicleRestrictions(uint laneId, VehicleType vehicleRestrictions)
        {
            NetManager.instance.m_lanes.m_buffer[laneId].m_flags |= Lane.CONTROL_BIT;
            m_lanes[laneId].m_vehicleTypes = vehicleRestrictions;
        }

        public void ToggleVehicleRestriction(uint laneId, VehicleType vehicleType)
        {
            NetManager.instance.m_lanes.m_buffer[laneId].m_flags |= Lane.CONTROL_BIT;
            m_lanes[laneId].m_vehicleTypes ^= vehicleType;
        }

        #endregion

        #region Lane Speeds

        public float GetLaneSpeed(uint laneId)
        {
            return m_lanes[laneId].m_speed;
        }

        public void SetLaneSpeed(uint laneId, int speed)
        {
            m_lanes[laneId].m_speed = (float)Math.Round(speed / 50f, 2);
        }

        #endregion

        #region Redirected Methods

        public bool CreateLanes(out uint firstLane, ref Randomizer randomizer, ushort segment, int count)
        {
            firstLane = 0u;
            if (count == 0)
            {
                return true;
            }
            NetManager netManager = NetManager.instance;
            NetLane netLane = default(NetLane);
            uint num = 0u;
            for (int i = 0; i < count; i++)
            {
                uint num2;
                if (!netManager.m_lanes.CreateItem(out num2, ref randomizer))
                {
                    netManager.ReleaseLanes(firstLane);
                    firstLane = 0u;
                    return false;
                }
                if (i == 0)
                {
                    firstLane = num2;
                }
                else
                {
                    netLane.m_nextLane = num2;
                    netManager.m_lanes.m_buffer[(int)((UIntPtr)num)] = netLane;
                }
                netLane = default(NetLane);
                netLane.m_flags = 1;
                netLane.m_segment = segment;
                num = num2;
            }
            netManager.m_lanes.m_buffer[(int)((UIntPtr)num)] = netLane;
            netManager.m_laneCount = (int)(netManager.m_lanes.ItemCount() - 1u);

            if (count > 1)
                CreateLanes(firstLane, segment);
            else
                CreateLane(firstLane, segment);
            
            return true;
        }

        public void ReleaseLanes(uint firstLane)
        {
            RoadManager roadManager = instance;
            NetManager netManager = NetManager.instance;
            int num = 0;
            while (firstLane != 0u)
            {
                uint nextLane = netManager.m_lanes.m_buffer[(int)((UIntPtr)firstLane)].m_nextLane;
                //if (roadManager.m_lanes[firstLane].m_speed > 0.1f)
                    //roadManager.m_lanes[firstLane].RemoveAllConnections();
                roadManager.m_lanes[firstLane].Release();
                netManager.m_lanes.m_buffer[firstLane].m_flags = (ushort)(netManager.m_lanes.m_buffer[firstLane].m_flags & ~Lane.CONTROL_BIT);
                ReleaseLaneImplementation(firstLane, ref netManager.m_lanes.m_buffer[(int)((UIntPtr)firstLane)]);
                firstLane = nextLane;
                if (++num > NetManager.MAX_LANE_COUNT)
                {
                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                    break;
                }
            }
            netManager.m_laneCount = (int)(netManager.m_lanes.ItemCount() - 1u);
        }

        // Not redirected, simply called by ReleaseLanes above - It's here to avoid using Reflection
        private void ReleaseLaneImplementation(uint lane, ref NetLane data)
        {
            NetManager netManager = NetManager.instance;
            if (data.m_nodes != 0)
            {
                ushort num = data.m_nodes;
                data.m_nodes = 0;
                int num2 = 0;
                while (num != 0)
                {
                    ushort nextLaneNode = netManager.m_nodes.m_buffer[num].m_nextLaneNode;
                    netManager.m_nodes.m_buffer[num].m_nextLaneNode = 0;
                    netManager.m_nodes.m_buffer[num].m_lane = 0u;
                    netManager.m_nodes.m_buffer[num].m_laneOffset = 0;
                    netManager.UpdateNode(num, 0, 10);
                    num = nextLaneNode;
                    if (++num2 > 32768)
                    {
                        CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                        break;
                    }
                }
            }
            data = default(NetLane);
            netManager.m_lanes.ReleaseItem(lane);
        }

        #endregion

        #region Legacy

        [Serializable]
        public class LegacyLane
        {
            public uint m_laneId;
            public ushort m_nodeId;
            public List<uint> m_laneConnections;
            public VehicleType m_vehicleTypes;
            public float m_speed;
        }

        class LegacyBinder : SerializationBinder
        {
            public override Type BindToType(string assemblyName, string typeName)
            {
                typeName = typeName.Replace("Lane", "LegacyLane");

                return Type.GetType(String.Format("{0}, {1}", typeName, assemblyName));
            }
        }

        #endregion
    }
}
