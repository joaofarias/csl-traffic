using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ICities;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using System.IO;

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

                Debug.Log("Traffic++: Loading road data!");
                byte[] data = serializableDataManager.LoadData(LANE_DATA_ID);
                if (data == null)
                {
                    Debug.Log("Traffic++: No road data to load!");
                    return;
                }

                MemoryStream memStream = new MemoryStream();
                memStream.Write(data, 0, data.Length);
                memStream.Position = 0;

                BinaryFormatter binaryFormatter = new BinaryFormatter();
                try
                {
                    RoadManager.sm_lanes = (Lane[]) binaryFormatter.Deserialize(memStream);
                    foreach (Lane lane in RoadManager.sm_lanes)
                    {
                        if (lane == null)
                            continue;

                        lane.UpdateArrows();
                    }
                }
                catch (Exception e)
                {
                    Debug.Log("Traffic++: Unexpected " + e.GetType().Name + " loading road data.");
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

                Debug.Log("Traffic++: Saving road data!");
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                MemoryStream memStream = new MemoryStream();
                try
                {
                    binaryFormatter.Serialize(memStream, RoadManager.sm_lanes);
                    serializableDataManager.SaveData(LANE_DATA_ID, memStream.ToArray());
                }
                catch (Exception e)
                {
                    Debug.Log("Traffic++: Unexpected " + e.GetType().Name + " saving road data.");
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

            All             = Int32.MaxValue
        }

        [Serializable]
        public class Lane
        {
            public const ushort CONTROL_BIT = 2048;

            public uint m_laneId;
            private ushort m_nodeId;
            private List<uint> m_laneConnections = new List<uint>();
            public VehicleType m_vehicleTypes = VehicleType.All;
            public float m_speed;

            public bool AddConnection(uint laneId)
            {
                if (m_laneConnections.Contains(laneId))
                    return false;

                m_laneConnections.Add(laneId);
                UpdateArrows();

                return true;
            }

            public bool RemoveConnection(uint laneId)
            {
                if (m_laneConnections.Remove(laneId))
                {
                    UpdateArrows();
                    return true;
                }

                return false;
            }

            public uint[] GetConnectionsAsArray()
            {
                return m_laneConnections.ToArray();
            }

            public int ConnectionCount()
            {
                return m_laneConnections.Count();
            }

            public bool ConnectsTo(uint laneId)
            {
                return m_laneConnections.Count == 0 || m_laneConnections.Contains(laneId);
            }

            void VerifyConnections()
            {
                uint[] connections = GetConnectionsAsArray();
                foreach (uint laneId in connections)
                {
                    NetLane lane = NetManager.instance.m_lanes.m_buffer[laneId];
                    if ((lane.m_flags & CONTROL_BIT) != CONTROL_BIT)
                        m_laneConnections.Remove(laneId);
                }
            }

            public void UpdateArrows()
            {
                VerifyConnections();
                NetLane lane = NetManager.instance.m_lanes.m_buffer[m_laneId];
                NetSegment segment = NetManager.instance.m_segments.m_buffer[lane.m_segment];

                if (m_nodeId == 0 && !FindNode(segment))
                    return;

                if (m_laneConnections.Count == 0)
                {
                    SetDefaultArrows(lane.m_segment, ref NetManager.instance.m_segments.m_buffer[lane.m_segment]);
                    return;
                }

                NetLane.Flags flags = (NetLane.Flags)lane.m_flags;
                flags &= ~(NetLane.Flags.LeftForwardRight);

                Vector3 segDir = segment.GetDirection(m_nodeId);
                foreach (uint connection in m_laneConnections)
                {
                    ushort seg = NetManager.instance.m_lanes.m_buffer[connection].m_segment;
                    Vector3 dir = NetManager.instance.m_segments.m_buffer[seg].GetDirection(m_nodeId);
                    if (Vector3.Angle(segDir, dir) > 120f)
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
                {
                    lane.m_vehicleTypes = netInfoLane.m_allowedVehicleTypes;
                    lane.m_speed = netInfoLane.m_speedLimit;
                }
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

        public static void ToggleVehicleRestriction(uint laneId, VehicleType vehicleType)
        {
            GetLane(laneId).m_vehicleTypes ^= vehicleType;
        }

        #endregion
    }
}
