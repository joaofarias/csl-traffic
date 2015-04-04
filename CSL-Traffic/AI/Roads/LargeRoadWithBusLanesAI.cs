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
            
            Initialize(collection, customPrefabs, "Large Road", "Large Road With Bus Lanes");
            //Initialize(collection, customPrefabs, "Large Road Decoration Trees", "Large Road Decoration Trees With Bus Lanes");
            //Initialize(collection, customPrefabs, "Large Road Decoration Grass", "Large Road Decoration Grass With Bus Lanes");
            
            sm_initialized = true;
        }

        static void Initialize(NetCollection collection, Transform customPrefabs, string prefabName, string instanceName)
        {
            Debug.Log("Traffic++: Initializing " + instanceName);

            NetInfo originalLargeRoad = collection.m_prefabs.Where(p => p.name == prefabName).FirstOrDefault();
            if (originalLargeRoad == null)
                throw new KeyNotFoundException(prefabName + " was not found on " + collection.name);

            GameObject instance = GameObject.Instantiate<GameObject>(originalLargeRoad.gameObject);
            instance.name = instanceName;

            MethodInfo initMethod = typeof(NetCollection).GetMethod("InitializePrefabs", BindingFlags.Static | BindingFlags.NonPublic);
            if ((CSLTraffic.Options & OptionsManager.ModOptions.GhostMode) == OptionsManager.ModOptions.GhostMode)
            {
                instance.transform.SetParent(originalLargeRoad.transform.parent);
                Initializer.QueuePrioritizedLoadingAction((IEnumerator)initMethod.Invoke(null, new object[] { collection.name, new[] { instance.GetComponent<NetInfo>() }, new string[] { } }));
                sm_initialized = true;
                return;
            }

            instance.transform.SetParent(customPrefabs);
            GameObject.Destroy(instance.GetComponent<RoadAI>());
            instance.AddComponent<LargeRoadWithBusLanesAI>();

            NetInfo largeRoadWithBusLanes = instance.GetComponent<NetInfo>();
            largeRoadWithBusLanes.m_prefabInitialized = false;
            largeRoadWithBusLanes.m_netAI = null;

            Initializer.QueuePrioritizedLoadingAction((IEnumerator)initMethod.Invoke(null, new object[] { collection.name, new[] { largeRoadWithBusLanes }, new string[] { } }));

            Initializer.QueueLoadingAction(() =>
            {
                largeRoadWithBusLanes.m_UIPriority = 20;

                largeRoadWithBusLanes.m_lanes[4].m_laneType = (NetInfo.LaneType)((byte)64);
                largeRoadWithBusLanes.m_lanes[5].m_laneType = (NetInfo.LaneType)((byte)64);

                if (Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic == SimulationMetaData.MetaBool.True)
                {
                    largeRoadWithBusLanes.m_lanes[4].m_direction = NetInfo.Direction.Forward;
                    largeRoadWithBusLanes.m_lanes[5].m_direction = NetInfo.Direction.Backward;
                }
            });

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

            Debug.Log("Traffic++: " + name + " initialized.");
        }
    }
}
