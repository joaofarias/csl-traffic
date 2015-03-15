using ColossalFramework;
using ColossalFramework.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using PedestrianZoning.Extensions;

namespace PedestrianZoning
{
	/*
	 * TODO: Refactor this class
	 * This class needs to be completely refactored. Move intialization to their respective classes and
	 * make this class as generic as possible.
	 */
	class Initializer : MonoBehaviour
	{
		bool m_initialized;

		void Awake()
		{
			DontDestroyOnLoad(this);
		}

		void Start()
		{
			ReplacePathManager();
		}
		
		void Update()
		{
			if (!m_initialized)
				TryReplacePrefabs();
		}

		/*
		 * In here I'm changing the prefabs to have my classes. This way, every time the game instantiates
		 * a prefab that I've changed, that object will run my code.
		 * The prefabs aren't available at the moment of creation of this class, that's why I keep trying to
		 * run it on update. I want to make sure I make the switch as soon as they exist to prevent the game
		 * from instantianting objects without my code.
		 * This is only run to completion one time.
		 */
		void TryReplacePrefabs()
		{
			// Roads
			GameObject road = GameObject.Find("Road");
			if (road == null)
				return;
			NetCollection collection = road.GetComponent<NetCollection>();
			if (collection == null)
				return;
			GameObject garbage = GameObject.FindObjectsOfType<GameObject>().Where(go => go.name == "Garbage" && go.GetComponent<VehicleCollection>() != null).FirstOrDefault();
			if (garbage == null)
				return;
			VehicleCollection vCollection = garbage.GetComponent<VehicleCollection>();
			if (vCollection == null)
				return;

			NetInfo pavement = InstantiatePrefab("Pedestrian Pavement");
			pavement.m_prefabInitialized = false;
			pavement.m_class.m_service = ItemClass.Service.Road; // FIXME: this also changes the class of the non-zonable paths.
			typeof(NetInfo).GetFieldByName("m_UICategory").SetValue(pavement, "RoadsSmall");

			NetInfo.Lane[] lanes = new NetInfo.Lane[3];
			lanes[0] = pavement.m_lanes[0];

			NetInfo.Lane prefabLane = collection.m_prefabs[0].m_lanes[5];
			lanes[1] = new NetInfo.Lane();
			lanes[1].m_position = -1.75f / 2f;
			lanes[1].m_width = 1.75f;
			lanes[1].m_verticalOffset = 0f;
			lanes[1].m_stopOffset = 0.5f;
			lanes[1].m_speedLimit = 0.25f;
			lanes[1].m_direction = NetInfo.Direction.Backward;
			lanes[1].m_laneType = (NetInfo.LaneType)((byte)32);
			lanes[1].m_vehicleType = VehicleInfo.VehicleType.Car;
			lanes[1].m_laneProps = ScriptableObject.CreateInstance<NetLaneProps>();
			lanes[1].m_allowStop = true;
			lanes[1].m_useTerrainHeight = false;

			lanes[2] = new NetInfo.Lane();
			lanes[2].m_position = 3.75f / 2f;
			lanes[2].m_width = 1.75f;
			lanes[2].m_verticalOffset = 0f;
			lanes[2].m_stopOffset = 0.5f;
			lanes[2].m_speedLimit = 0.25f;
			lanes[2].m_direction = NetInfo.Direction.Forward;
			lanes[2].m_laneType = (NetInfo.LaneType)((byte)32);
			lanes[2].m_vehicleType = VehicleInfo.VehicleType.Car;
			lanes[2].m_laneProps = ScriptableObject.CreateInstance<NetLaneProps>();
			lanes[2].m_allowStop = true;
			lanes[2].m_useTerrainHeight = false;

			pavement.m_lanes = lanes;

			MethodInfo initMethod = typeof(NetCollection).GetMethod("InitializePrefabs", BindingFlags.Static | BindingFlags.NonPublic);
			Singleton<LoadingManager>.instance.QueueLoadingAction((IEnumerator)initMethod.Invoke(null, new object[] { collection.name, new[] { pavement }, new string[] { } }));

			VehicleInfo gTruck = ReplacePrefab("Garbage Truck");
			gTruck.m_prefabInitialized = false;

			initMethod = typeof(VehicleCollection).GetMethod("InitializePrefabs", BindingFlags.Static | BindingFlags.NonPublic);
			Singleton<LoadingManager>.instance.QueueLoadingAction((IEnumerator)initMethod.Invoke(null, new object[] { vCollection.name, new[] { gTruck }, new string[] { "Garbage Truck" } }));

			m_initialized = true;
		}

		// instantiates the pedestrian path prefab and changes the AI class for mine
		NetInfo InstantiatePrefab(string name)
		{
			GameObject prefab = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.name == name && g.GetComponent<PedestrianPathAI>() != null).FirstOrDefault();
			if (prefab == null)
				return null;

			GameObject instance = GameObject.Instantiate<GameObject>(prefab);
			instance.name = name + " With Zoning";
			GameObject.Destroy(instance.GetComponent<PedestrianPathAI>());
			instance.AddComponent<PedestrianZoningPathAI>();

			return instance.GetComponent<NetInfo>();
		}

		// instantiates the garbage truck prefab and changes the AI class for mine
		VehicleInfo ReplacePrefab(string name)
		{
			GameObject prefab = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.name == name).FirstOrDefault();
			if (prefab == null)
				return null;

			GameObject instance = GameObject.Instantiate<GameObject>(prefab);
			instance.name = name;
			GameObject.Destroy(instance.GetComponent<GarbageTruckAI>());
			instance.AddComponent<CustomGarbageTruckAI>();

			return instance.GetComponent<VehicleInfo>();
		}

		// Replace the pathfinding system for mine
		void ReplacePathManager()
		{
			// Change PathManager to CustomPathManager
			FieldInfo sInstance = typeof(Singleton<PathManager>).GetFieldByName("sInstance");
			PathManager originalPathManager = Singleton<PathManager>.instance;
			CustomPathManager customPathManager = originalPathManager.gameObject.AddComponent<CustomPathManager>();
			customPathManager.SetOriginalValues(originalPathManager);
			
			// change the new instance in the singleton
			sInstance.SetValue(null, customPathManager);

			// change the manager in the SimulationManager
			FastList<ISimulationManager> managers = (FastList<ISimulationManager>)typeof(SimulationManager).GetFieldByName("m_managers").GetValue(null);
			managers.Remove(originalPathManager);
			managers.Add(customPathManager);

			// Destroy in 10 seconds to give time to all references to update to the new manager without crashing
			GameObject.Destroy(originalPathManager, 10f);
		}
	}
}
