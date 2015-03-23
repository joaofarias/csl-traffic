using ColossalFramework;
using CSL_Traffic.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace CSL_Traffic
{
	class ZonablePedestrianBridgeAI : RoadBridgeAI
	{
		public static bool sm_initialized;
		public static void Initialize(NetCollection collection, Transform customPrefabs)
		{
			if (ZonablePedestrianBridgeAI.sm_initialized)
				return;

            NetInfo originalPedestrianBridge = collection.m_prefabs.Where(p => p.name == "Pedestrian Elevated").FirstOrDefault();
			if (originalPedestrianBridge == null)
				throw new KeyNotFoundException("Pedestrian Elevated was not found on " + collection.name);

            GameObject instance = GameObject.Instantiate<GameObject>(originalPedestrianBridge.gameObject); ;
			instance.name = "Zonable Pedestrian Elevated";
            
            MethodInfo initMethod = typeof(NetCollection).GetMethod("InitializePrefabs", BindingFlags.Static | BindingFlags.NonPublic);
            if ((CSLTraffic.Options & OptionsManager.ModOptions.GhostMode) == OptionsManager.ModOptions.GhostMode)
            {
                instance.transform.SetParent(originalPedestrianBridge.transform.parent);
                Singleton<LoadingManager>.instance.QueueLoadingAction((IEnumerator)initMethod.Invoke(null, new object[] { collection.name, new NetInfo[] { instance.GetComponent<NetInfo>() }, new string[0]}));
                return;
            }

			instance.transform.SetParent(customPrefabs);
			GameObject.Destroy(instance.GetComponent<PedestrianBridgeAI>());
			instance.AddComponent<ZonablePedestrianBridgeAI>();

            NetInfo zonablePedestrianBridge = instance.GetComponent<NetInfo>();
            zonablePedestrianBridge.m_prefabInitialized = false;
            zonablePedestrianBridge.m_netAI = null;
            zonablePedestrianBridge.m_halfWidth = 4f;
            zonablePedestrianBridge.m_class = ScriptableObject.CreateInstance<ItemClass>();
            zonablePedestrianBridge.m_class.m_service = ItemClass.Service.Road;
            zonablePedestrianBridge.m_class.m_subService = ItemClass.SubService.None;
            zonablePedestrianBridge.m_class.m_level = ItemClass.Level.Level1;
            typeof(NetInfo).GetFieldByName("m_UICategory").SetValue(zonablePedestrianBridge, "RoadsSmall");

            // Pedestrian lane
            NetInfo.Lane[] lanes = new NetInfo.Lane[3];
            lanes[0] = zonablePedestrianBridge.m_lanes[0];
            lanes[0].m_width = 6f;
            //PropInfo lampProp = lanes[0].m_laneProps.m_props[0].m_prop;
            lanes[0].m_laneProps = ScriptableObject.CreateInstance<NetLaneProps>(); // TODO: Put the lamps back on bridges after resizing them
            //lanes[0].m_laneProps.m_props = new NetLaneProps.Prop[1];
            //lanes[0].m_laneProps.m_props[0] = new NetLaneProps.Prop() { m_prop = lampProp, m_position = new Vector3(-4f, 0f, 0f), m_repeatDistance = 30f };

            // Backward Lane
            lanes[1] = new NetInfo.Lane();
            lanes[1].m_position = -1.5f;
            lanes[1].m_width = 3f;
            lanes[1].m_verticalOffset = 0f;
            lanes[1].m_stopOffset = 0.1f;
            lanes[1].m_speedLimit = 0.3f;
            lanes[1].m_direction = NetInfo.Direction.Backward;
            lanes[1].m_laneType = (NetInfo.LaneType)((byte)32);
            lanes[1].m_vehicleType = VehicleInfo.VehicleType.Car;
            lanes[1].m_laneProps = ScriptableObject.CreateInstance<NetLaneProps>();
            lanes[1].m_allowStop = true;
            lanes[1].m_useTerrainHeight = false;

            // Forward Lane
            lanes[2] = new NetInfo.Lane();
            lanes[2].m_position = 1.5f;
            lanes[2].m_width = 3f;
            lanes[2].m_verticalOffset = 0f;
            lanes[2].m_stopOffset = 0.1f;
            lanes[2].m_speedLimit = 0.3f;
            lanes[2].m_direction = NetInfo.Direction.Forward;
            lanes[2].m_laneType = (NetInfo.LaneType)((byte)32);
            lanes[2].m_vehicleType = VehicleInfo.VehicleType.Car;
            lanes[2].m_laneProps = ScriptableObject.CreateInstance<NetLaneProps>();
            lanes[2].m_allowStop = true;
            lanes[2].m_useTerrainHeight = false;

            zonablePedestrianBridge.m_lanes = lanes;

            ColossalFramework.Singleton<LoadingManager>.instance.QueueLoadingAction((IEnumerator)initMethod.Invoke(null, new object[] { collection.name, new[] { zonablePedestrianBridge }, new string[] { } }));
						
			sm_initialized = true;
		}
		public override void InitializePrefab()
		{
			base.InitializePrefab();

			this.m_constructionCost = 2000;
			this.m_maintenanceCost = 250;
			
            try
			{
                NetInfo zonablePath = PrefabCollection<NetInfo>.FindLoaded("Zonable Pedestrian Pavement");
                if (zonablePath == null)
                    throw new KeyNotFoundException("Can't find Zonable Pedestrian Pavement in PrefabCollection.");
                ZonablePedestrianPathAI zonablePathAI = zonablePath.GetComponent<ZonablePedestrianPathAI>();
                if (zonablePathAI == null)
                    throw new KeyNotFoundException("Zonable Pedestrian Pavement prefab does not have a ZonablePedestrianPathAI.");
                zonablePathAI.m_elevatedInfo = this.m_info;
                zonablePathAI.m_bridgeInfo = this.m_info;

                GameObject pillarPrefab = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.name == "Pedestrian Elevated Pillar").FirstOrDefault();
                if (pillarPrefab == null)
                    throw new KeyNotFoundException("Can't find Pedestrian Elevated Pillar.");
                this.m_bridgePillarInfo = pillarPrefab.GetComponent<BuildingInfo>();
			}
			catch (KeyNotFoundException knf)
			{
#if DEBUG
                System.IO.File.AppendAllText("Debug.txt", "Error initializing Zonable Pedestrian Bridge AI: " + knf.Message + "\n");
#endif
			}
		}
	}
}
