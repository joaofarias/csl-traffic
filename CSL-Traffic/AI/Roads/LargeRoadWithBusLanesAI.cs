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

#if DEBUG
            System.IO.File.AppendAllText("TrafficPP_Debug.txt", "Initializing Large Road With Bus Lanes AI.\n");
#endif

            NetInfo originalLargeRoad = collection.m_prefabs.Where(p => p.name == "Large Road").FirstOrDefault();
            if (originalLargeRoad == null)
                throw new KeyNotFoundException("Large Road was not found on " + collection.name);

            GameObject instance = GameObject.Instantiate<GameObject>(originalLargeRoad.gameObject);
            instance.name = "Large Road With Bus Lanes";

            MethodInfo initMethod = typeof(NetCollection).GetMethod("InitializePrefabs", BindingFlags.Static | BindingFlags.NonPublic);
            if ((CSLTraffic.Options & OptionsManager.ModOptions.GhostMode) == OptionsManager.ModOptions.GhostMode)
            {
                instance.transform.SetParent(originalLargeRoad.transform.parent);
                Singleton<LoadingManager>.instance.QueueLoadingAction((IEnumerator)initMethod.Invoke(null, new object[] { collection.name, new[] { instance.GetComponent<NetInfo>() }, new string[] { } }));
                sm_initialized = true;
                return;
            }

            instance.transform.SetParent(customPrefabs);
            GameObject.Destroy(instance.GetComponent<RoadAI>());
            LargeRoadWithBusLanesAI largeRoad = instance.AddComponent<LargeRoadWithBusLanesAI>();

            largeRoad.m_outsideConnection = originalLargeRoad.GetComponent<RoadAI>().m_outsideConnection;

            NetInfo largeRoadWithBusLanes = instance.GetComponent<NetInfo>();
            largeRoadWithBusLanes.m_prefabInitialized = false;
            largeRoadWithBusLanes.m_netAI = null;
            largeRoadWithBusLanes.m_UIPriority = 20;

            largeRoadWithBusLanes.m_lanes[4].m_laneType = (NetInfo.LaneType)((byte)64);
            largeRoadWithBusLanes.m_lanes[5].m_laneType = (NetInfo.LaneType)((byte)64);

            Singleton<LoadingManager>.instance.QueueLoadingAction((IEnumerator)initMethod.Invoke(null, new object[] { collection.name, new[] { largeRoadWithBusLanes }, new string[] { } }));

            sm_initialized = true;
        }

        public override void InitializePrefab()
        {
            base.InitializePrefab();

            this.m_trafficLights = true;
            this.m_noiseAccumulation = 24;
            this.m_noiseRadius = 50;
            this.m_constructionCost = 8000;
            this.m_maintenanceCost = 662;
            this.m_enableZoning = true;

#if DEBUG
            System.IO.File.AppendAllText("TrafficPP_Debug.txt", "Large Road With Bus Lanes AI successfully initialized.\n");
#endif
        }
    }
}
