using ColossalFramework;
using ColossalFramework.DataBinding;
using CSL_Traffic.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace CSL_Traffic
{
	class ZonablePedestrianPathAI : RoadAI
	{
		public static bool sm_initialized;

        public static void Initialize(NetCollection collection, Transform customPrefabs)
        {
            if (sm_initialized)
                return;

#if DEBUG
            System.IO.File.AppendAllText("TrafficPP_Debug.txt", "Initializing Zonable Pedestrian Path AI.\n");
#endif

            Initialize(collection, customPrefabs, "Pedestrian Pavement", "Zonable Pedestrian Pavement");
            Initialize(collection, customPrefabs, "Pedestrian Gravel", "Zonable Pedestrian Gravel");

            sm_initialized = true;
        }

        static void Initialize(NetCollection collection, Transform customPrefabs, string prefabName, string instanceName)
        {
            NetInfo originalPedestrianPath = collection.m_prefabs.Where(p => p.name == prefabName).FirstOrDefault();
            if (originalPedestrianPath == null)
                throw new KeyNotFoundException(prefabName + " was not found on " + collection.name);

            GameObject instance = GameObject.Instantiate<GameObject>(originalPedestrianPath.gameObject);
            instance.name = instanceName;

            MethodInfo initMethod = typeof(NetCollection).GetMethod("InitializePrefabs", BindingFlags.Static | BindingFlags.NonPublic);
            if ((CSLTraffic.Options & OptionsManager.ModOptions.GhostMode) == OptionsManager.ModOptions.GhostMode)
            {
                instance.transform.SetParent(originalPedestrianPath.transform.parent);
                Singleton<LoadingManager>.instance.QueueLoadingAction((IEnumerator)initMethod.Invoke(null, new object[] { collection.name, new[] { instance.GetComponent<NetInfo>() }, new string[] { } }));
                sm_initialized = true;
                return;
            }

            instance.transform.SetParent(customPrefabs);
            GameObject.Destroy(instance.GetComponent<PedestrianPathAI>());
            instance.AddComponent<ZonablePedestrianPathAI>();

            NetInfo zonablePedestrianPath = instance.GetComponent<NetInfo>();
            zonablePedestrianPath.m_prefabInitialized = false;
            zonablePedestrianPath.m_netAI = null;
            zonablePedestrianPath.m_flattenTerrain = true;
            zonablePedestrianPath.m_halfWidth = 5f;
            zonablePedestrianPath.m_autoRemove = false;
            zonablePedestrianPath.m_flatJunctions = true;
            zonablePedestrianPath.m_surfaceLevel = 0f;
            zonablePedestrianPath.m_segmentLength = 46f;
            zonablePedestrianPath.m_pavementWidth = 1.875f;
            zonablePedestrianPath.m_class = ScriptableObject.CreateInstance<ItemClass>();
            zonablePedestrianPath.m_class.m_service = ItemClass.Service.Road;
            zonablePedestrianPath.m_class.m_subService = ItemClass.SubService.None;
            zonablePedestrianPath.m_class.m_level = ItemClass.Level.Level1;
            typeof(NetInfo).GetFieldByName("m_UICategory").SetValue(zonablePedestrianPath, "RoadsSmall");

            if ((CSLTraffic.Options & OptionsManager.ModOptions.DisableCentralLaneOnPedestrianRoads) == OptionsManager.ModOptions.None)
            {
                NetInfo.Lane[] lanes = new NetInfo.Lane[3];

                // Central Pedestrian lane
                lanes[0] = zonablePedestrianPath.m_lanes[0];
                lanes[0].m_width = 9f;
                if (prefabName.Contains("Pavement"))
                {
                    PropInfo lampProp = lanes[0].m_laneProps.m_props[0].m_prop;
                    lanes[0].m_laneProps = ScriptableObject.CreateInstance<NetLaneProps>();
                    lanes[0].m_laneProps.m_props = new NetLaneProps.Prop[2];
                    lanes[0].m_laneProps.m_props[0] = new NetLaneProps.Prop() { m_prop = lampProp, m_position = new Vector3(-4.75f, 0f, 0f), m_repeatDistance = 60f, m_segmentOffset = 0f };
                    lanes[0].m_laneProps.m_props[1] = new NetLaneProps.Prop() { m_prop = lampProp, m_position = new Vector3(4.75f, 0f, 0f), m_repeatDistance = 60f, m_segmentOffset = 30f };
                }
                

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

                zonablePedestrianPath.m_lanes = lanes;
            }
            else
            {
                NetInfo.Lane[] lanes = new NetInfo.Lane[4];

                // Left Pedestrian lane
                lanes[0] = new NetInfo.Lane();
                lanes[0].m_position = -4f;
                lanes[0].m_width = 2f;
                lanes[0].m_verticalOffset = zonablePedestrianPath.m_lanes[0].m_verticalOffset;
                lanes[0].m_stopOffset = zonablePedestrianPath.m_lanes[0].m_stopOffset;
                lanes[0].m_speedLimit = zonablePedestrianPath.m_lanes[0].m_speedLimit;
                lanes[0].m_direction = NetInfo.Direction.Both;
                lanes[0].m_laneType = NetInfo.LaneType.Pedestrian;
                lanes[0].m_vehicleType = VehicleInfo.VehicleType.None;
                lanes[0].m_laneProps = ScriptableObject.CreateInstance<NetLaneProps>();
                if (prefabName.Contains("Pavement"))
                {
                    lanes[0].m_laneProps.m_props = new NetLaneProps.Prop[1];
                    lanes[0].m_laneProps.m_props[0] = new NetLaneProps.Prop() { m_prop = zonablePedestrianPath.m_lanes[0].m_laneProps.m_props[0].m_prop, m_position = new Vector3(-.75f, 0f, 0f), m_repeatDistance = 60f, m_segmentOffset = 0f };
                }
                lanes[0].m_allowStop = true;
                lanes[0].m_useTerrainHeight = false;


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

                // Right Pedestrian lane
                lanes[3] = new NetInfo.Lane();
                lanes[3].m_position = 4f;
                lanes[3].m_width = 2f;
                lanes[3].m_verticalOffset = zonablePedestrianPath.m_lanes[0].m_verticalOffset;
                lanes[3].m_stopOffset = zonablePedestrianPath.m_lanes[0].m_stopOffset;
                lanes[3].m_speedLimit = zonablePedestrianPath.m_lanes[0].m_speedLimit;
                lanes[3].m_direction = NetInfo.Direction.Both;
                lanes[3].m_laneType = NetInfo.LaneType.Pedestrian;
                lanes[3].m_vehicleType = VehicleInfo.VehicleType.None;
                lanes[3].m_laneProps = ScriptableObject.CreateInstance<NetLaneProps>();
                if (prefabName.Contains("Pavement"))
                {
                    lanes[3].m_laneProps.m_props = new NetLaneProps.Prop[1];
                    lanes[3].m_laneProps.m_props[0] = new NetLaneProps.Prop() { m_prop = zonablePedestrianPath.m_lanes[0].m_laneProps.m_props[0].m_prop, m_position = new Vector3(.75f, 0f, 0f), m_repeatDistance = 60f, m_segmentOffset = 30f };
                }
                lanes[3].m_allowStop = true;
                lanes[3].m_useTerrainHeight = false;

                zonablePedestrianPath.m_lanes = lanes;
            }

            Singleton<LoadingManager>.instance.QueueLoadingAction((IEnumerator)initMethod.Invoke(null, new object[] { collection.name, new[] { zonablePedestrianPath }, new string[] { } }));
        }

		public override void InitializePrefab()
		{
			base.InitializePrefab();

            if (name.Contains("Pavement"))
            {
                this.m_constructionCost = 2000;
                this.m_maintenanceCost = 250;
            }
            else
            {
                this.m_constructionCost = 1000;
                this.m_maintenanceCost = 150;
            }
			
			this.m_enableZoning = true;

			base.StartCoroutine(this.FixNodes());

#if DEBUG
            System.IO.File.AppendAllText("TrafficPP_Debug.txt", "Zonable Pedestrian Path AI successfully initialized (" + name + ").\n");
#endif
		}

		private IEnumerator FixNodes()
		{
			NetInfo netInfo;
			while ((netInfo = PrefabCollection<NetInfo>.FindLoaded("Gravel Road")) == null)
				yield return new WaitForSeconds(2f);

			this.m_info.m_nodes = new NetInfo.Node[1];
			this.m_info.m_nodes[0] = netInfo.m_nodes[1];
			this.m_info.InitializePrefab();

			NetNode[] buffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
			for (int i = 0; i < buffer.Length; i++)
			{
				buffer[i].UpdateNode((ushort)i);
			}	
		}

		public override string GetLocalizedTooltip()
		{
			return TooltipHelper.Format(new string[]
			{
				LocaleFormatter.Cost,
				LocaleFormatter.FormatCost(this.GetConstructionCost(), true),
				LocaleFormatter.Upkeep,
				LocaleFormatter.FormatUpkeep(this.GetMaintenanceCost(), true),
				LocaleFormatter.Speed,
				LocaleFormatter.FormatGeneric("AIINFO_SPEED", new object[]
				{
					15f
				})
			});
		}
	}
}
