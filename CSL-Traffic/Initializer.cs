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
		bool m_localizationInitialized;
        bool m_initialized;

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

		void OnLevelWasLoaded(int level) {
			if (level == 6)
			{
                m_initialized = false;

				// roads
				ZonablePedestrianPathAI.sm_initialized = false;
				ZonablePedestrianBridgeAI.sm_initialized = false;
                LargeRoadWithBusLanesAI.sm_initialized = false;
                LargeRoadWithBusLanesBridgeAI.sm_initialized = false;
				
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

        void Update()
        {
            if (!m_initialized)
            {
                if ((CSLTraffic.Options & OptionsManager.ModOptions.FixMissingArrows) == OptionsManager.ModOptions.FixMissingArrows)
                    StartCoroutine(ReplacePrefabs());
                else
                    TryReplacePrefabs();
            }
                
        }

		/*
		 * In here I'm changing the prefabs to have my classes. This way, every time the game instantiates
		 * a prefab that I've changed, that object will run my code.
		 * The prefabs aren't available at the moment of creation of this class, that's why I keep trying to
		 * run it on update. I want to make sure I make the switch as soon as they exist to prevent the game
		 * from instantianting objects without my code.
		 */
		IEnumerator ReplacePrefabs()
		{
            if (m_initialized)
                yield break;

            m_initialized = true;
#if DEBUG
            // TODO: create a class to handle logging
            System.IO.File.Delete("TrafficPP_Debug.txt");
            System.IO.File.AppendAllText("TrafficPP_Debug.txt", "Initializing Traffic++.\n\n");
#endif

            NetCollection beautificationNetCollection = null;
            NetCollection roadsNetCollection = null;
            NetCollection publicTansportNetCollection = null;
            VehicleCollection garbageVehicleCollection = null;
            VehicleCollection policeVehicleCollection = null;
            VehicleCollection publicTansportVehicleCollection = null;
            VehicleCollection healthCareVehicleCollection = null;
            VehicleCollection fireDepartmentVehicleCollection = null;
            VehicleCollection industrialVehicleCollection = null;
            TransportCollection publicTransportTransportCollection = null;
            ToolController toolController = null;

            bool ready = false;
            while (!ready)
            {
                yield return new WaitForEndOfFrame();

                try {
                    // NetCollections
                    beautificationNetCollection = TryGetComponent<NetCollection>("Beautification");
                    if (beautificationNetCollection == null)
                        continue;

                    roadsNetCollection = TryGetComponent<NetCollection>("Road");
                    if (roadsNetCollection == null)
                        continue;

                    publicTansportNetCollection = TryGetComponent<NetCollection>("Public Transport");
                    if (publicTansportNetCollection == null)
                        continue;

                    // VehicleCollections
                    garbageVehicleCollection = TryGetComponent<VehicleCollection>("Garbage");
                    if (garbageVehicleCollection == null)
                        continue;

                    policeVehicleCollection = TryGetComponent<VehicleCollection>("Police Department");
                    if (policeVehicleCollection == null)
                        continue;

                    publicTansportVehicleCollection = TryGetComponent<VehicleCollection>("Public Transport");
                    if (publicTansportVehicleCollection == null)
                        continue;

                    healthCareVehicleCollection = TryGetComponent<VehicleCollection>("Health Care");
                    if (healthCareVehicleCollection == null)
                        continue;

                    fireDepartmentVehicleCollection = TryGetComponent<VehicleCollection>("Fire Department");
                    if (fireDepartmentVehicleCollection == null)
                        continue;

                    industrialVehicleCollection = TryGetComponent<VehicleCollection>("Industrial");
                    if (industrialVehicleCollection == null)
                        continue;

                    // Transports
                    publicTransportTransportCollection = TryGetComponent<TransportCollection>("Public Transport");
                    if (publicTransportTransportCollection == null)
                        continue;

                    // Tools
                    toolController = TryGetComponent<ToolController>("Tool Controller");
                    if (toolController == null)
                        continue;

                    ready = true;
                }
                catch (Exception e)
                {
#if DEBUG
                    System.IO.File.AppendAllText("TrafficPP_Debug.txt", "Unexpected " + e.GetType().Name + " getting required components: " + e.Message + "\n" + e.StackTrace + "\n");
#endif
                }
            }

            // allow 10 frames for objects to initialize (after some testing, I found that 5 is the minimum required. 10 should be safe for everyone)
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForEndOfFrame();
            }            

            try {
                // Localization
                UpdateLocalization();

                // roads
                ZonablePedestrianPathAI.Initialize(beautificationNetCollection, transform);
                ZonablePedestrianBridgeAI.Initialize(beautificationNetCollection, transform);
                LargeRoadWithBusLanesAI.Initialize(roadsNetCollection, transform);
                LargeRoadWithBusLanesBridgeAI.Initialize(roadsNetCollection, transform);

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

#if DEBUG
                System.IO.File.AppendAllText("TrafficPP_Debug.txt", "\nTraffic++ Initialized.\n\n");
#endif

			} catch(KeyNotFoundException knf) {
#if DEBUG
                System.IO.File.AppendAllText("TrafficPP_Debug.txt", "Error trying to initialize custom prefabs: " + knf.Message + "\n");
#endif
            }
            catch (Exception e )
            {
#if DEBUG
                System.IO.File.AppendAllText("TrafficPP_Debug.txt", "Unexpected " + e.GetType().Name + " trying to initialize custom prefabs: " + e.Message + "\n" + e.StackTrace + "\n");
#endif
            }
		}

        void TryReplacePrefabs()
        {
            NetCollection beautificationNetCollection = null;
            NetCollection roadsNetCollection = null;
            NetCollection publicTansportNetCollection = null;
            VehicleCollection garbageVehicleCollection = null;
            VehicleCollection policeVehicleCollection = null;
            VehicleCollection publicTansportVehicleCollection = null;
            VehicleCollection healthCareVehicleCollection = null;
            VehicleCollection fireDepartmentVehicleCollection = null;
            VehicleCollection industrialVehicleCollection = null;
            TransportCollection publicTransportTransportCollection = null;
            ToolController toolController = null;

            try
            {
                // NetCollections
                beautificationNetCollection = TryGetComponent<NetCollection>("Beautification");
                if (beautificationNetCollection == null)
                    return;

                roadsNetCollection = TryGetComponent<NetCollection>("Road");
                if (roadsNetCollection == null)
                    return;

                publicTansportNetCollection = TryGetComponent<NetCollection>("Public Transport");
                if (publicTansportNetCollection == null)
                    return;

                // VehicleCollections
                garbageVehicleCollection = TryGetComponent<VehicleCollection>("Garbage");
                if (garbageVehicleCollection == null)
                    return;

                policeVehicleCollection = TryGetComponent<VehicleCollection>("Police Department");
                if (policeVehicleCollection == null)
                    return;

                publicTansportVehicleCollection = TryGetComponent<VehicleCollection>("Public Transport");
                if (publicTansportVehicleCollection == null)
                    return;

                healthCareVehicleCollection = TryGetComponent<VehicleCollection>("Health Care");
                if (healthCareVehicleCollection == null)
                    return;

                fireDepartmentVehicleCollection = TryGetComponent<VehicleCollection>("Fire Department");
                if (fireDepartmentVehicleCollection == null)
                    return;

                industrialVehicleCollection = TryGetComponent<VehicleCollection>("Industrial");
                if (industrialVehicleCollection == null)
                    return;

                // Transports
                publicTransportTransportCollection = TryGetComponent<TransportCollection>("Public Transport");
                if (publicTransportTransportCollection == null)
                    return;

                // Tools
                toolController = TryGetComponent<ToolController>("Tool Controller");
                if (toolController == null)
                    return;

            }
            catch (Exception e)
            {
#if DEBUG
                System.IO.File.AppendAllText("TrafficPP_Debug.txt", "Unexpected " + e.GetType().Name + " getting required components: " + e.Message + "\n" + e.StackTrace + "\n");
#endif
            }

#if DEBUG
            // TODO: create a class to handle logging
            System.IO.File.Delete("TrafficPP_Debug.txt");
            System.IO.File.AppendAllText("TrafficPP_Debug.txt", "Initializing Traffic++.\n\n");
#endif

            try
            {
                // Localization
                UpdateLocalization();

                // roads
                ZonablePedestrianPathAI.Initialize(beautificationNetCollection, transform);
                ZonablePedestrianBridgeAI.Initialize(beautificationNetCollection, transform);
                LargeRoadWithBusLanesAI.Initialize(roadsNetCollection, transform);
                LargeRoadWithBusLanesBridgeAI.Initialize(roadsNetCollection, transform);

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
#if DEBUG
                System.IO.File.AppendAllText("TrafficPP_Debug.txt", "\nTraffic++ Initialized.\n\n");
#endif

            }
            catch (KeyNotFoundException knf)
            {
#if DEBUG
                System.IO.File.AppendAllText("TrafficPP_Debug.txt", "Error trying to initialize custom prefabs: " + knf.Message + "\n");
#endif
            }
            catch (Exception e)
            {
#if DEBUG
                System.IO.File.AppendAllText("TrafficPP_Debug.txt", "Unexpected " + e.GetType().Name + " trying to initialize custom prefabs: " + e.Message + "\n" + e.StackTrace + "\n");
#endif
            }
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
            IRenderableManager[] renderables;
            int count;
            RenderManager.GetManagers(out renderables, out count);
            if (renderables != null && count != 0)
            {
                for (int i = 0; i < count; i++)
                {
                    TransportManager temp = renderables[i] as TransportManager;
                    if (temp != null && temp == originalTransportManager)
                    {
                        renderables[i] = customTransportManager;
                        break;
                    }
                }
            }
            else
            {
                RenderManager.RegisterRenderableManager(customTransportManager);
            }
                
            // Destroy in 10 seconds to give time to all references to update to the new manager without crashing
            GameObject.Destroy(originalTransportManager, 10f);
        }

        T TryGetComponent<T>(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go == null)
                return default(T);

            return go.GetComponent<T>();
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

                // Pedestrian Pavement
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

                // Pedestrian Gravel
                k = new Locale.Key()
                {
                    m_Identifier = "NET_TITLE",
                    m_Key = "Zonable Pedestrian Gravel"
                };
                locale.AddLocalizedString(k, "Pedestrian Gravel Road");

                k = new Locale.Key()
                {
                    m_Identifier = "NET_DESC",
                    m_Key = "Zonable Pedestrian Gravel"
                };
                locale.AddLocalizedString(k, "Gravel roads allow pedestrians to walk fast and easy. They can also be used by public service vehicles.");

                // Large road with bus
                k = new Locale.Key()
                {
                    m_Identifier = "NET_TITLE",
                    m_Key = "Large Road With Bus Lanes"
                };
                locale.AddLocalizedString(k, "Six-Lane Road With Bus Lanes");

                k = new Locale.Key()
                {
                    m_Identifier = "NET_DESC",
                    m_Key = "Large Road With Bus Lanes"
                };
                locale.AddLocalizedString(k, "A six-lane road with parking spaces and dedicated bus lanes. The bus lanes can be used by vehicles in emergency. Supports high-traffic.");

				m_localizationInitialized = true;
			}
			catch (ArgumentException) {}
		}
	}
}
