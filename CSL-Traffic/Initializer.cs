using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using CSL_Traffic.Extensions;

namespace CSL_Traffic
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
		 */
		void TryReplacePrefabs()
		{
			try {
				NetCollection beautificationNetCollection = GameObject.Find("Beautification").GetComponent<NetCollection>();
				VehicleCollection garbageVehicleCollection = GameObject.Find("Garbage").GetComponent<VehicleCollection>();
				VehicleCollection policeVehicleCollection = GameObject.Find("Police Department").GetComponent<VehicleCollection>();
				//VehicleCollection publicTansportVehicleCollection = GameObject.Find("Public Transport").GetComponent<VehicleCollection>();
				VehicleCollection healthCareVehicleCollection = GameObject.Find("Health Care").GetComponent<VehicleCollection>();
				VehicleCollection fireDepartmentVehicleCollection = GameObject.Find("Fire Department").GetComponent<VehicleCollection>();

				// roads
				PedestrianZoningPathAI.Initialize(beautificationNetCollection, transform);
				PedestrianZoningBridgeAI.Initialize(beautificationNetCollection, transform);
				
				// vehicles
				CustomGarbageTruckAI.Initialize(garbageVehicleCollection, transform);
				CustomAmbulanceAI.Initialize(healthCareVehicleCollection, transform);
				//CustomBusAI.Initialize(publicTansportVehicleCollection, transform);
				CustomFireTruckAI.Initialize(fireDepartmentVehicleCollection, transform);
				CustomHearseAI.Initialize(healthCareVehicleCollection, transform);
				CustomPoliceCarAI.Initialize(policeVehicleCollection, transform);

				m_initialized = true;
			} catch(KeyNotFoundException knf) {
#if DEBUG
				File.AppendAllText("Debug.txt", "Error trying to initialize custom prefabs: " + knf.Message + "\n");
				m_initialized = true;
#endif
			} catch(Exception) {}
		}

		// Replace the pathfinding system for mine
		void ReplacePathManager()
		{
			// Change PathManager to CustomPathManager
			FieldInfo sInstance = typeof(ColossalFramework.Singleton<PathManager>).GetFieldByName("sInstance");
			PathManager originalPathManager = ColossalFramework.Singleton<PathManager>.instance;
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
