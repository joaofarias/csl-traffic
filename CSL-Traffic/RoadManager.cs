using ICities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using UnityEngine;

namespace CSL_Traffic
{
    public static class RoadManager
    {
        public class Data : SerializableDataExtensionBase
        {
            const string LANE_DATA_ID = "Traffic++_RoadManager_Lanes";
            
            public override void OnLoadData()
            {
                if ((CSLTraffic.Options & OptionsManager.ModOptions.BetaTestRoadCustomizerTool) == OptionsManager.ModOptions.None || (CSLTraffic.Options & OptionsManager.ModOptions.GhostMode) == OptionsManager.ModOptions.GhostMode)
                    return;
                

                Logger.LogInfo("Loading road data. Time: " + Time.realtimeSinceStartup);
                byte[] data = serializableDataManager.LoadData(LANE_DATA_ID);
                if (data == null)
                {
                    Logger.LogInfo("No road data to load.");
                    return;
                }

                MemoryStream memStream = new MemoryStream();
                memStream.Write(data, 0, data.Length);
                memStream.Position = 0;

                BinaryFormatter binaryFormatter = new BinaryFormatter();
                try
                {
                    RoadManager.sm_lanes = (Lane[]) binaryFormatter.Deserialize(memStream);
                    
                    FastList<ushort> nodesList = new FastList<ushort>();
                    foreach (Lane lane in RoadManager.sm_lanes)
                    {
                        if (lane == null)
                            continue;

                        if ((CSLTraffic.Options & OptionsManager.ModOptions.FixCargoTrucksNotSpawning) == OptionsManager.ModOptions.FixCargoTrucksNotSpawning && lane.m_vehicleTypes == (VehicleType.ServiceVehicles | VehicleType.PassengerCar))
                            lane.m_vehicleTypes = VehicleType.All;

                        lane.UpdateArrows();
                        if (lane.ConnectionCount() > 0)
                            nodesList.Add(lane.m_nodeId);

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

                    }

                    RoadCustomizerTool customizerTool = ToolsModifierControl.GetTool<RoadCustomizerTool>();
                    foreach (ushort nodeId in nodesList)
                        customizerTool.SetNodeMarkers(nodeId);

                    Logger.LogInfo("Finished loading road data. Time: " + Time.realtimeSinceStartup);
                }
                catch (Exception e)
                {
                    Logger.LogError("Unexpected " + e.GetType().Name + " loading road data.");
                }
                finally
                {
                    memStream.Close();
                }
            }

            public override void OnSaveData()
            {
                if ((CSLTraffic.Options & OptionsManager.ModOptions.BetaTestRoadCustomizerTool) == OptionsManager.ModOptions.None || (CSLTraffic.Options & OptionsManager.ModOptions.GhostMode) == OptionsManager.ModOptions.GhostMode)
                    return;

                Logger.LogInfo("Saving road data!");
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                MemoryStream memStream = new MemoryStream();
                try
                {
                    binaryFormatter.Serialize(memStream, RoadManager.sm_lanes);
                    serializableDataManager.SaveData(LANE_DATA_ID, memStream.ToArray());
                    Logger.LogInfo("Finished saving road data!");
                }
                catch (Exception e)
                {
                    Logger.LogError("Unexpected " + e.GetType().Name + " saving road data.");
                }
                finally
                {
                    memStream.Close();
                }
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

        [Serializable]
        public class Lane
        {
            public const ushort CONTROL_BIT = 2048;

            public uint m_laneId;
            public ushort m_nodeId;
            private List<uint> m_laneConnections = new List<uint>();
            public VehicleType m_vehicleTypes = VehicleType.All;
            public float m_speed = 1f;            

            public bool AddConnection(uint laneId)
            {
                bool exists = false;
                while (!Monitor.TryEnter(this.m_laneConnections, SimulationManager.SYNCHRONIZE_TIMEOUT))
                {
                }
                try
                {
                    if (m_laneConnections.Contains(laneId))
                        exists = true;
                    else
                        m_laneConnections.Add(laneId);
                }
                finally
                {
                    Monitor.Exit(this.m_laneConnections);
                }

                if (exists)
                    return false;

                UpdateArrows();

                return true;
            }

            public bool RemoveConnection(uint laneId)
            {
                bool result = false;
                while (!Monitor.TryEnter(this.m_laneConnections, SimulationManager.SYNCHRONIZE_TIMEOUT))
                {
                }
                try
                {
                    result = m_laneConnections.Remove(laneId);
                }
                finally
                {
                    Monitor.Exit(this.m_laneConnections);
                }

                if (result)
                    UpdateArrows();

                return result;
            }

            public uint[] GetConnectionsAsArray()
            {
                uint[] connections = null;
                while (!Monitor.TryEnter(this.m_laneConnections, SimulationManager.SYNCHRONIZE_TIMEOUT))
                {
                }
                try
                {
                    connections = m_laneConnections.ToArray();
                }
                finally
                {
                    Monitor.Exit(this.m_laneConnections);
                }
                return connections;
            }

            public int ConnectionCount()
            {
                int count = 0;
                while (!Monitor.TryEnter(this.m_laneConnections, SimulationManager.SYNCHRONIZE_TIMEOUT))
                {
                }
                try
                {
                    count = m_laneConnections.Count();
                }
                finally
                {
                    Monitor.Exit(this.m_laneConnections);
                }
                return count;
            }

            public bool ConnectsTo(uint laneId)
            {
                VerifyConnections();

                bool result = true;
                while (!Monitor.TryEnter(this.m_laneConnections, SimulationManager.SYNCHRONIZE_TIMEOUT))
                {
                }
                try
                {
                    result = m_laneConnections.Count == 0 || m_laneConnections.Contains(laneId);
                }
                finally
                {
                    Monitor.Exit(this.m_laneConnections);
                }

                return result;
            }

            void VerifyConnections()
            {
                uint[] connections = GetConnectionsAsArray();
                while (!Monitor.TryEnter(this.m_laneConnections, SimulationManager.SYNCHRONIZE_TIMEOUT))
                {
                }
                try
                {
                    foreach (uint laneId in connections)
                    {
                        NetLane lane = NetManager.instance.m_lanes.m_buffer[laneId];
                        if ((lane.m_flags & CONTROL_BIT) != CONTROL_BIT)
                            m_laneConnections.Remove(laneId);
                    }
                }
                finally
                {
                    Monitor.Exit(this.m_laneConnections);
                }
            }

            public void UpdateArrows()
            {
                VerifyConnections();
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

                uint laneId = segment.m_lanes;
                int laneCount = info.m_lanes.Length;
                for (int laneIndex = 0; laneIndex < laneCount && laneId != 0; laneIndex++)
                {
                    if (laneId != m_laneId && RoadManager.sm_lanes[laneId] != null && RoadManager.sm_lanes[laneId].ConnectionCount() > 0)
                        RoadManager.sm_lanes[laneId].UpdateArrows();

                    laneId = NetManager.instance.m_lanes.m_buffer[laneId].m_nextLane;
                }
            }
        }

        static Lane[] sm_lanes = new Lane[NetManager.MAX_LANE_COUNT];

        public static Lane CreateLane(uint laneId)
        {
            Lane lane = new Lane()
            {
                m_laneId = laneId
            };

            NetSegment segment = NetManager.instance.m_segments.m_buffer[NetManager.instance.m_lanes.m_buffer[laneId].m_segment];
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
                NetInfoLane netInfoLane = netInfo.m_lanes[laneIndex] as NetInfoLane;
                if (netInfoLane != null)
                    lane.m_vehicleTypes = netInfoLane.m_allowedVehicleTypes;

                lane.m_speed = netInfo.m_lanes[laneIndex].m_speedLimit;
            }

            NetManager.instance.m_lanes.m_buffer[laneId].m_flags |= Lane.CONTROL_BIT;

            sm_lanes[laneId] = lane;

            return lane;
        }

        public static Lane GetLane(uint laneId)
        {
            Lane lane = sm_lanes[laneId];
            if (lane == null || (NetManager.instance.m_lanes.m_buffer[laneId].m_flags & Lane.CONTROL_BIT) == 0)
                lane = CreateLane(laneId);

            return lane;
        }

        #region Lane Connections
        public static bool AddLaneConnection(uint laneId, uint connectionId)
        {
            Lane lane = GetLane(laneId);
            GetLane(connectionId); // makes sure lane information is stored

            return lane.AddConnection(connectionId);
        }

        public static bool RemoveLaneConnection(uint laneId, uint connectionId)
        {
            Lane lane = GetLane(laneId);

            return lane.RemoveConnection(connectionId);
        }

        public static uint[] GetLaneConnections(uint laneId)
        {
            Lane lane = GetLane(laneId);

            return lane.GetConnectionsAsArray();
        }

        public static bool CheckLaneConnection(uint from, uint to)
        {   
            Lane lane = GetLane(from);

            return lane.ConnectsTo(to);
        }
        #endregion

        #region Vehicle Restrictions
        public static bool CanUseLane(VehicleType vehicleType, uint laneId)
        {            
            return (GetLane(laneId).m_vehicleTypes & vehicleType) != VehicleType.None;
        }

        public static VehicleType GetVehicleRestrictions(uint laneId)
        {
            return GetLane(laneId).m_vehicleTypes;
        }

        public static void SetVehicleRestrictions(uint laneId, VehicleType vehicleRestrictions)
        {
            GetLane(laneId).m_vehicleTypes = vehicleRestrictions;
        }

        public static void ToggleVehicleRestriction(uint laneId, VehicleType vehicleType)
        {
            GetLane(laneId).m_vehicleTypes ^= vehicleType;
        }

        #endregion

        #region Lane Speeds

        public static float GetLaneSpeed(uint laneId)
        {
            return GetLane(laneId).m_speed;
        }

        public static void SetLaneSpeed(uint laneId, int speed)
        {
            GetLane(laneId).m_speed = (float)Math.Round(speed/50f, 2);
        }

        #endregion
    }
}
