using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using CSL_Traffic.Extensions;
using ColossalFramework.Globalization;
using ColossalFramework;
using System.IO;
using System.Text;
using System.Collections;
using System.Linq;

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
            if ((CSLTraffic.Options & OptionsManager.ModOptions.GhostMode) != OptionsManager.ModOptions.GhostMode)
            {
                ReplacePathManager();
                ReplaceTransportManager();
            }
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
				ZonablePedestrianPathAI.sm_initialized = false;
				ZonablePedestrianBridgeAI.sm_initialized = false;
				
				// vehicles
				CustomAmbulanceAI.sm_initialized = false;
                CustomBusAI.sm_initialized = false;
                CustomCargoTruckAI.sm_initialized = false;
				CustomFireTruckAI.sm_initialized = false;
				CustomGarbageTruckAI.sm_initialized = false;
				CustomHearseAI.sm_initialized = false;
				CustomPoliceCarAI.sm_initialized = false;

                // Tools
                CustomTransportTool.sm_initialized = false;

                // Transports
                BusTransportLineAI.sm_initialized = false;
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
                // NetCollections
				NetCollection beautificationNetCollection = GameObject.Find("Beautification").GetComponent<NetCollection>();
                //NetCollection roadsNetCollection = GameObject.Find("Road").GetComponent<NetCollection>();
                NetCollection publicTansportNetCollection = GameObject.Find("Public Transport").GetComponent<NetCollection>();

                // VehicleCollections
				VehicleCollection garbageVehicleCollection = GameObject.Find("Garbage").GetComponent<VehicleCollection>();
				VehicleCollection policeVehicleCollection = GameObject.Find("Police Department").GetComponent<VehicleCollection>();
				VehicleCollection publicTansportVehicleCollection = GameObject.Find("Public Transport").GetComponent<VehicleCollection>();
				VehicleCollection healthCareVehicleCollection = GameObject.Find("Health Care").GetComponent<VehicleCollection>();
				VehicleCollection fireDepartmentVehicleCollection = GameObject.Find("Fire Department").GetComponent<VehicleCollection>();
                VehicleCollection industrialVehicleCollection = GameObject.Find("Industrial").GetComponent<VehicleCollection>();

                TransportCollection publicTransportTransportCollection = GameObject.Find("Public Transport").GetComponent<TransportCollection>();

                // Tools
                ToolController toolController = GameObject.Find("Tool Controller").GetComponent<ToolController>();

				// Localization
				UpdateLocalization();

				// roads
				ZonablePedestrianPathAI.Initialize(beautificationNetCollection, transform);
				ZonablePedestrianBridgeAI.Initialize(beautificationNetCollection, transform);
				
                if ((CSLTraffic.Options & OptionsManager.ModOptions.GhostMode) != OptionsManager.ModOptions.GhostMode)
                {
                    // Transports
                    BusTransportLineAI.Initialize(publicTansportNetCollection, publicTansportVehicleCollection, publicTransportTransportCollection, transform);

                    // vehicles
                    CustomAmbulanceAI.Initialize(healthCareVehicleCollection, transform);
                    CustomBusAI.Initialize(publicTansportVehicleCollection, transform);
                    CustomCargoTruckAI.Initialize(industrialVehicleCollection, transform);
                    CustomFireTruckAI.Initialize(fireDepartmentVehicleCollection, transform);
                    CustomGarbageTruckAI.Initialize(garbageVehicleCollection, transform);
                    CustomHearseAI.Initialize(healthCareVehicleCollection, transform);
                    CustomPoliceCarAI.Initialize(policeVehicleCollection, transform);

                    //Tools
                    CustomTransportTool.Initialize(toolController);
                }
				
				m_initialized = true;
			} catch(KeyNotFoundException knf) {
#if DEBUG
				System.IO.File.AppendAllText("Debug.txt", "Error trying to initialize custom prefabs: " + knf.Message + "\n");
				m_initialized = true;
#endif
			} catch(Exception) {}
		}

        //IEnumerator Print()
        //{
        //    yield return new WaitForSeconds(30f);

        //    foreach (var item in Resources.FindObjectsOfTypeAll<GameObject>().Except(GameObject.FindObjectsOfType<GameObject>()))
        //    {
        //        if (item.transform.parent == null)
        //            printGameObjects(item);
        //    }
        //}

        //void printGameObjects(GameObject go, int depth = 0)
        //{
        //    StringBuilder sb = new StringBuilder();
        //    for (int i = 0; i < depth; i++)
        //    {
        //        sb.Append(">");
        //    }
        //    sb.Append("> ");
        //    sb.Append(go.name);
        //    sb.Append("\n");

        //    System.IO.File.AppendAllText("MapScenePrefabs.txt", sb.ToString());

        //    printComponents(go, depth);

        //    foreach (Transform t in go.transform)
        //    {
        //        printGameObjects(t.gameObject, depth + 1);
        //    }
        //}

        //void printComponents(GameObject go, int depth)
        //{
        //    foreach (var item in go.GetComponents<Component>())
        //    {
        //        StringBuilder sb = new StringBuilder();
        //        for (int i = 0; i < depth; i++)
        //        {
        //            sb.Append(" ");
        //        }
        //        sb.Append("  -- ");
        //        sb.Append(item.GetType().Name);
        //        sb.Append("\n");

        //        System.IO.File.AppendAllText("MapScenePrefabs.txt", sb.ToString());
        //    }
        //}


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

        void ReplaceTransportManager()
        {
            // Change TransportManager to CustomTransportManager
            FieldInfo sInstance = typeof(ColossalFramework.Singleton<TransportManager>).GetFieldByName("sInstance");
            TransportManager originalTransportManager = ColossalFramework.Singleton<TransportManager>.instance;
            CustomTransportManager customTransportManager = originalTransportManager.gameObject.AddComponent<CustomTransportManager>();
            customTransportManager.SetOriginalValues(originalTransportManager);

            // change the new instance in the singleton
            sInstance.SetValue(null, customTransportManager);

            // change the manager in the SimulationManager
            FastList<ISimulationManager> managers = (FastList<ISimulationManager>)typeof(SimulationManager).GetFieldByName("m_managers").GetValue(null);
            managers.Remove(originalTransportManager);
            managers.Add(customTransportManager);

            // add to renderable managers
            RenderManager.RegisterRenderableManager(customTransportManager);

            // Destroy in 10 seconds to give time to all references to update to the new manager without crashing
            GameObject.Destroy(originalTransportManager, 10f);
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
