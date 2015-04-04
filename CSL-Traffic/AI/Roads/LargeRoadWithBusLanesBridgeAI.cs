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
    class LargeRoadWithBusLanesBridgeAI : RoadBridgeAI
    {
        public static bool sm_initialized;
        public static void Initialize(NetCollection collection, Transform customPrefabs)
        {
            if (sm_initialized)
                return;

            Initialize(collection, customPrefabs, "Large Road Bridge", "Large Road Bridge With Bus Lanes");
            Initialize(collection, customPrefabs, "Large Road Elevated", "Large Road Elevated With Bus Lanes");

            sm_initialized = true;
        }

        static void Initialize(NetCollection collection, Transform customPrefabs, string prefabName, string instanceName)
        {
            Debug.Log("Traffic++: Initializing " + instanceName);

            NetInfo originalRoadBridge = collection.m_prefabs.Where(p => p.name == prefabName).FirstOrDefault();
            if (originalRoadBridge == null)
                throw new KeyNotFoundException(prefabName + " was not found on " + collection.name);

            GameObject instance = GameObject.Instantiate<GameObject>(originalRoadBridge.gameObject);
            instance.name = instanceName;

            MethodInfo initMethod = typeof(NetCollection).GetMethod("InitializePrefabs", BindingFlags.Static | BindingFlags.NonPublic);
            if ((CSLTraffic.Options & OptionsManager.ModOptions.GhostMode) == OptionsManager.ModOptions.GhostMode)
            {
                instance.transform.SetParent(originalRoadBridge.transform.parent);
                Initializer.QueuePrioritizedLoadingAction((IEnumerator)initMethod.Invoke(null, new object[] { collection.name, new NetInfo[] { instance.GetComponent<NetInfo>() }, new string[] { } }));
                sm_initialized = true;
                return;
            }

            instance.transform.SetParent(customPrefabs);
            GameObject.Destroy(instance.GetComponent<RoadBridgeAI>());
            instance.AddComponent<LargeRoadWithBusLanesBridgeAI>();

            NetInfo largeRoadBridge = instance.GetComponent<NetInfo>();
            largeRoadBridge.m_prefabInitialized = false;
            largeRoadBridge.m_netAI = null;

            Initializer.QueuePrioritizedLoadingAction((IEnumerator)initMethod.Invoke(null, new object[] { collection.name, new[] { largeRoadBridge }, new string[] { } }));

            Initializer.QueueLoadingAction(() =>
            {
                largeRoadBridge.m_lanes[2].m_laneType = (NetInfo.LaneType)((byte)64);
                largeRoadBridge.m_lanes[3].m_laneType = (NetInfo.LaneType)((byte)64);

                if (Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic == SimulationMetaData.MetaBool.True)
                {
                    largeRoadBridge.m_lanes[2].m_direction = NetInfo.Direction.Forward;
                    largeRoadBridge.m_lanes[3].m_direction = NetInfo.Direction.Backward;
                }
            });
            
        }

        public override void InitializePrefab()
        {
            base.InitializePrefab();

            this.m_trafficLights = true;
            this.m_noiseAccumulation = 24;
            this.m_noiseRadius = 50;
            this.m_constructionCost = 24000;
            this.m_maintenanceCost = 1980;
            this.m_elevationCost = 2000;

            if (name == "Large Road Bridge With Bus Lanes")
            {
                this.m_middlePillarOffset = 43.5f;
                this.m_doubleLength = true;
                this.m_canModify = false;
            }
            else
            {
                this.m_doubleLength = false;
                this.m_canModify = true;
            }

            try
            {
                NetInfo largeRoad = PrefabCollection<NetInfo>.FindLoaded("Large Road With Bus Lanes");
                if (largeRoad == null)
                    throw new KeyNotFoundException("Can't find LargeRoad in PrefabCollection.");
                LargeRoadWithBusLanesAI largeRoadWithBusLanes = largeRoad.GetComponent<LargeRoadWithBusLanesAI>();
                if (largeRoadWithBusLanes == null)
                    throw new KeyNotFoundException("Large Road With Bus Lanes prefab does not have a LargeRoadWithBusLanesAI.");

                if (name == "Large Road Bridge With Bus Lanes")
                    largeRoadWithBusLanes.m_bridgeInfo = this.m_info;
                else
                    largeRoadWithBusLanes.m_elevatedInfo = this.m_info;

                if (name == "Large Road Bridge With Bus Lanes")
                {
                    GameObject pillarPrefab = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.name == "LargeRoadBridgeSuspensionPillar").FirstOrDefault();
                    if (pillarPrefab == null)
                        throw new KeyNotFoundException("Can't find LargeRoadBridgeSuspensionPillar.");

                    this.m_middlePillarInfo = pillarPrefab.GetComponent<BuildingInfo>();
                }
                else
                {
                    GameObject pillarPrefab = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.name == "HighwayBridgePillar").FirstOrDefault();
                    if (pillarPrefab == null)
                        throw new KeyNotFoundException("Can't find HighwayBridgePillar.");

                    this.m_bridgePillarInfo = pillarPrefab.GetComponent<BuildingInfo>();
                }

                Debug.Log("Traffic++: " + name + " initialized.");
            }
            catch (KeyNotFoundException knf)
            {
                Debug.Log("Traffic++: Error initializing Large Road Bridge AI: " + knf.Message + "\n");
            }
            catch (Exception e)
            {
                Debug.Log("Traffic++: Unexpected " + e.GetType().Name + " initializing Large Road Bridge AI: " + e.Message + "\n" + e.StackTrace + "\n");
            }
        }
    }
}
