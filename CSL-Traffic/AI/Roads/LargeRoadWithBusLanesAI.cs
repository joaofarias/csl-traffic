using ColossalFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using CSL_Traffic.Extensions;

namespace CSL_Traffic
{
    class LargeRoadWithBusLanesAI : RoadAI
    {
        public static bool sm_initialized;

        public static void Initialize(NetCollection collection, Transform customPrefabs)
        {
            if (sm_initialized)
                return;

            NetInfo originalLargeRoad = collection.m_prefabs.Where(p => p.name == "Large Road").FirstOrDefault();
            if (originalLargeRoad == null)
                throw new KeyNotFoundException("Large Road was not found on " + collection.name);

            GameObject instance = GameObject.Instantiate<GameObject>(originalLargeRoad.gameObject); ;
            instance.name = "Large Road With Bus Lanes";

            MethodInfo initMethod = typeof(NetCollection).GetMethod("InitializePrefabs", BindingFlags.Static | BindingFlags.NonPublic);
            if ((CSLTraffic.Options & OptionsManager.ModOptions.GhostMode) == OptionsManager.ModOptions.GhostMode)
            {
                instance.transform.SetParent(originalLargeRoad.transform.parent);
                Singleton<LoadingManager>.instance.QueueLoadingAction((IEnumerator)initMethod.Invoke(null, new object[] { collection.name, new[] { instance.GetComponent<NetInfo>() }, new string[] { } }));
                return;
            }

            instance.transform.SetParent(customPrefabs);
            GameObject.Destroy(instance.GetComponent<RoadAI>());
            instance.AddComponent<LargeRoadWithBusLanesAI>();

            NetInfo largeRoadWithBusLanes = instance.GetComponent<NetInfo>();
            largeRoadWithBusLanes.m_prefabInitialized = false;
            largeRoadWithBusLanes.m_netAI = null;

            largeRoadWithBusLanes.m_lanes[4].m_laneType = (NetInfo.LaneType)((byte)64);
            largeRoadWithBusLanes.m_lanes[5].m_laneType = (NetInfo.LaneType)((byte)64);
            
            //NetInfo.Lane[] lanes = new NetInfo.Lane[3];

            //// Central Pedestrian lane
            //lanes[0] = largeRoadWithBusLanes.m_lanes[0];
            //lanes[0].m_width = 9f;
            //PropInfo lampProp = lanes[0].m_laneProps.m_props[0].m_prop;
            //lanes[0].m_laneProps = ScriptableObject.CreateInstance<NetLaneProps>();
            //lanes[0].m_laneProps.m_props = new NetLaneProps.Prop[2];
            //lanes[0].m_laneProps.m_props[0] = new NetLaneProps.Prop() { m_prop = lampProp, m_position = new Vector3(-4.75f, 0f, 0f), m_repeatDistance = 60f, m_segmentOffset = 0f };
            //lanes[0].m_laneProps.m_props[1] = new NetLaneProps.Prop() { m_prop = lampProp, m_position = new Vector3(4.75f, 0f, 0f), m_repeatDistance = 60f, m_segmentOffset = 30f };

            //// Backward Lane
            //lanes[1] = new NetInfo.Lane();
            //lanes[1].m_position = -1.5f;
            //lanes[1].m_width = 3f;
            //lanes[1].m_verticalOffset = 0f;
            //lanes[1].m_stopOffset = 0.1f;
            //lanes[1].m_speedLimit = 0.3f;
            //lanes[1].m_direction = NetInfo.Direction.Backward;
            //lanes[1].m_laneType = (NetInfo.LaneType)((byte)32);
            //lanes[1].m_vehicleType = VehicleInfo.VehicleType.Car;
            //lanes[1].m_laneProps = ScriptableObject.CreateInstance<NetLaneProps>();
            //lanes[1].m_allowStop = true;
            //lanes[1].m_useTerrainHeight = false;

            //// Forward Lane
            //lanes[2] = new NetInfo.Lane();
            //lanes[2].m_position = 1.5f;
            //lanes[2].m_width = 3f;
            //lanes[2].m_verticalOffset = 0f;
            //lanes[2].m_stopOffset = 0.1f;
            //lanes[2].m_speedLimit = 0.3f;
            //lanes[2].m_direction = NetInfo.Direction.Forward;
            //lanes[2].m_laneType = (NetInfo.LaneType)((byte)32);
            //lanes[2].m_vehicleType = VehicleInfo.VehicleType.Car;
            //lanes[2].m_laneProps = ScriptableObject.CreateInstance<NetLaneProps>();
            //lanes[2].m_allowStop = true;
            //lanes[2].m_useTerrainHeight = false;

            //largeRoadWithBusLanes.m_lanes = lanes;

            Singleton<LoadingManager>.instance.QueueLoadingAction((IEnumerator)initMethod.Invoke(null, new object[] { collection.name, new[] { largeRoadWithBusLanes }, new string[] { } }));

            sm_initialized = true;
        }
    }
}
