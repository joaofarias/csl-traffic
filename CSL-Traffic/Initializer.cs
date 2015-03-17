using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using CSL_Traffic.Extensions;
using ColossalFramework.Globalization;
using ColossalFramework;
using System.IO;

namespace CSL_Traffic
{
	class Initializer : MonoBehaviour
	{
		bool m_initialized;
		bool m_localizationInitialized;

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

		void OnLevelWasLoaded(int level) {
			if (level == 6)
			{
				m_initialized = false;

				// roads
				PedestrianZoningPathAI.sm_initialized = false;
				PedestrianZoningBridgeAI.sm_initialized = false;
				
				// vehicles
				CustomAmbulanceAI.sm_initialized = false;
				CustomFireTruckAI.sm_initialized = false;
				CustomGarbageTruckAI.sm_initialized = false;
				CustomHearseAI.sm_initialized = false;
				CustomPoliceCarAI.sm_initialized = false;
			}
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

				// Localization
				UpdateLocalization();

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
				System.IO.File.AppendAllText("Debug.txt", "Error trying to initialize custom prefabs: " + knf.Message + "\n");
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

		void UpdateLocalization()
		{
			if (m_localizationInitialized)
				return;

			try
			{
				// Localization
				Locale locale = (Locale)typeof(LocaleManager).GetFieldByName("m_Locale").GetValue(SingletonLite<LocaleManager>.instance);
				if (locale == null)
					throw new KeyNotFoundException("Locale is null");

				Locale.Key k = new Locale.Key()
				{
					m_Identifier = "NET_TITLE",
					m_Key = "Zonable Pedestrian Pavement"
				};
				locale.AddLocalizedString(k, "Pedestrian Road");

				k = new Locale.Key()
				{
					m_Identifier = "NET_DESC",
					m_Key = "Zonable Pedestrian Pavement"
				};
				locale.AddLocalizedString(k, "Paved roads are nicer to walk on than gravel. They offer access to pedestrians and can be used by public service vehicles.");

				m_localizationInitialized = true;
			}
			catch (ArgumentException) {}
		}
	}
}
