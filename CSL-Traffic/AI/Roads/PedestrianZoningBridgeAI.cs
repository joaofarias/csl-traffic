using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CSL_Traffic.Extensions;
using System.Reflection;
using System.Collections;

namespace CSL_Traffic
{
	class PedestrianZoningBridgeAI : PedestrianBridgeAI
	{
		public static bool sm_initialized;

		public static void Initialize(NetCollection collection, Transform customPrefabs)
		{
			if (sm_initialized)
				return;
			NetInfo originalPedestrianPath = collection.m_prefabs.Where(p => p.name == "Pedestrian Elevated").FirstOrDefault();
			if (originalPedestrianPath == null)
				throw new KeyNotFoundException("Pedestrian Elevated was not found on " + collection.name);

			GameObject instance = GameObject.Instantiate<GameObject>(originalPedestrianPath.gameObject); ;
			instance.name = "Zonable Pedestrian Elevated";
			instance.transform.SetParent(customPrefabs);
			GameObject.Destroy(instance.GetComponent<PedestrianBridgeAI>());
			instance.AddComponent<PedestrianZoningBridgeAI>();

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

			MethodInfo initMethod = typeof(NetCollection).GetMethod("InitializePrefabs", BindingFlags.Static | BindingFlags.NonPublic);
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
				PedestrianZoningPathAI zonablePathAI = zonablePath.GetComponent<PedestrianZoningPathAI>();
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

		public override void UpdateLaneConnection(ushort nodeID, ref NetNode data)
		{
			if ((data.m_flags & (NetNode.Flags.Temporary | NetNode.Flags.OnGround)) == NetNode.Flags.OnGround)
			{
				uint num = 0u;
				byte offset = 0;
				float num2 = 1E+10f;
				if ((data.m_flags & NetNode.Flags.ForbidLaneConnection) == NetNode.Flags.None)
				{
					PathUnit.Position pathPos;
					PathUnit.Position position;
					float num3;
					float num4;
					if (this.m_connectService1 != ItemClass.Service.None && PathManager.FindPathPosition(data.m_position, this.m_connectService1, (NetInfo.LaneType)((byte)32), VehicleInfo.VehicleType.None | VehicleInfo.VehicleType.Car, 16f, out pathPos, out position, out num3, out num4) && num3 < num2)
					{
						num2 = num3;
						num = PathManager.GetLaneID(pathPos);
						offset = pathPos.m_offset;
					}
					PathUnit.Position pathPos2;
					PathUnit.Position position2;
					float num5;
					float num6;
					if (this.m_connectService2 != ItemClass.Service.None && PathManager.FindPathPosition(data.m_position, this.m_connectService2, (NetInfo.LaneType)((byte)32), VehicleInfo.VehicleType.None | VehicleInfo.VehicleType.Car, 16f, out pathPos2, out position2, out num5, out num6) && num5 < num2)
					{
						num = PathManager.GetLaneID(pathPos2);
						offset = pathPos2.m_offset;
					}
				}
				if (num != data.m_lane)
				{
					if (data.m_lane != 0u)
					{
						this.RemoveLaneConnection(nodeID, ref data);
					}
					if (num != 0u)
					{
						this.AddLaneConnection(nodeID, ref data, num, offset);
					}
				}
			}
		}
		private void AddLaneConnection(ushort nodeID, ref NetNode data, uint laneID, byte offset)
		{
			NetManager instance = Singleton<NetManager>.instance;
			data.m_lane = laneID;
			data.m_laneOffset = offset;
			data.m_nextLaneNode = instance.m_lanes.m_buffer[(int)((UIntPtr)data.m_lane)].m_nodes;
			instance.m_lanes.m_buffer[(int)((UIntPtr)data.m_lane)].m_nodes = nodeID;
		}
		private void RemoveLaneConnection(ushort nodeID, ref NetNode data)
		{
			NetManager instance = Singleton<NetManager>.instance;
			ushort num = 0;
			ushort num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)data.m_lane)].m_nodes;
			int num3 = 0;
			while (num2 != 0)
			{
				if (num2 == nodeID)
				{
					if (num == 0)
					{
						instance.m_lanes.m_buffer[(int)((UIntPtr)data.m_lane)].m_nodes = data.m_nextLaneNode;
					}
					else
					{
						instance.m_nodes.m_buffer[(int)num].m_nextLaneNode = data.m_nextLaneNode;
					}
					break;
				}
				num = num2;
				num2 = instance.m_nodes.m_buffer[(int)num2].m_nextLaneNode;
				if (++num3 > 32768)
				{
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
			data.m_lane = 0u;
			data.m_laneOffset = 0;
			data.m_nextLaneNode = 0;
		}
	}
}
