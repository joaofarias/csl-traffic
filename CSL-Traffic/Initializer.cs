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
using System.Threading;

namespace CSL_Traffic
{
	class Initializer : MonoBehaviour
	{
        static Queue<IEnumerator> sm_actionQueue = new Queue<IEnumerator>();
        static System.Object sm_queueLock = new System.Object();
		static bool sm_localizationInitialized;

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
                Debug.Log("Traffic++: Game level was loaded. Preparing initialization.");

                m_initialized = false;

                while (!Monitor.TryEnter(sm_queueLock, SimulationManager.SYNCHRONIZE_TIMEOUT)) { }
                try
                {
                    sm_actionQueue.Clear();
                }
                finally
                {
                    Monitor.Exit(sm_queueLock);
                }

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
                TryReplacePrefabs();
                return;
            }

            // contributed by Japa
            if (CustomTransportTool.sm_initialized && Singleton<LoadingManager>.instance.m_loadingComplete)
            {
                TransportTool transportTool = ToolsModifierControl.GetCurrentTool<TransportTool>();
                if (transportTool != null)
                {
                    CustomTransportTool customTransportTool = ToolsModifierControl.SetTool<CustomTransportTool>();
                    if (customTransportTool != null)
                    {
                        customTransportTool.m_prefab = transportTool.m_prefab;
                    }
                }
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
                Debug.Log("Traffic++: Unexpected " + e.GetType().Name + " getting required components: " + e.Message + "\n" + e.StackTrace + "\n");
                return;
            }

            Debug.Log("Traffic++: Queueing prefabs for loading...");

            Singleton<LoadingManager>.instance.QueueLoadingAction(ActionWrapper(() =>
            {
                try
                {
                    // roads
                    LargeRoadWithBusLanesBridgeAI.Initialize(roadsNetCollection, transform);
                    LargeRoadWithBusLanesAI.Initialize(roadsNetCollection, transform);
                    ZonablePedestrianBridgeAI.Initialize(beautificationNetCollection, transform);
                    ZonablePedestrianPathAI.Initialize(beautificationNetCollection, transform);

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

                    // Localization
                    UpdateLocalization();

                    ExecuteQueuedActions();

                }
                catch (KeyNotFoundException knf)
                {
                    Debug.Log("Traffic++: Error initializing a prefab: " + knf.Message + "\n" + knf.StackTrace + "\n");
                }
                catch (Exception e)
                {
                    Debug.Log("Traffic++: Unexpected " + e.GetType().Name + " initializing prefabs: " + e.Message + "\n" + e.StackTrace + "\n");
                }
            }));

            m_initialized = true;

            Debug.Log("Traffic++: Prefabs queued for loading.");
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
            if (Singleton<PathManager>.instance as CustomPathManager != null)
                return;

            Debug.Log("Traffic++: Replacing Path Manager");

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

            Debug.Log("Traffic++: Path Manager successfully replaced.");
		}

        void ReplaceTransportManager()
        {
            if (Singleton<TransportManager>.instance as CustomTransportManager != null)
                return;

            Debug.Log("Traffic++: Replacing Transport Manager");

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

            Debug.Log("Traffic++: Transport Manager successfully replaced.");
        }

        T TryGetComponent<T>(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go == null)
                return default(T);

            return go.GetComponent<T>();
        }

        public static void QueueLoadingAction(Action action)
        {
            QueueLoadingAction(ActionWrapper(action));
        }

        public static void QueueLoadingAction(IEnumerator action)
        {
            while (!Monitor.TryEnter(sm_queueLock, SimulationManager.SYNCHRONIZE_TIMEOUT)) { }
            try
            {
                sm_actionQueue.Enqueue(action);
            }
            finally { Monitor.Exit(sm_queueLock); }
        }

        static void ExecuteQueuedActions()
        {
            IEnumerator action;
            do
            {
                while (!Monitor.TryEnter(sm_queueLock, SimulationManager.SYNCHRONIZE_TIMEOUT)) { }
                try
                {
                    action = sm_actionQueue.Count > 0 ? sm_actionQueue.Dequeue() : null;
                }
                finally
                {
                    Monitor.Exit(sm_queueLock);
                }

                if (action != null)
                    Singleton<LoadingManager>.instance.QueueLoadingAction(action);

            } while (action != null);
        }

        static IEnumerator ActionWrapper(Action a)
        {
            a.Invoke();
            yield break;
        }

        public static void QueuePrioritizedLoadingAction(Action action)
        {
            QueuePrioritizedLoadingAction(ActionWrapper(action));
        }

        public static void QueuePrioritizedLoadingAction(IEnumerator action)
        {
            LoadingManager loadingManager = Singleton<LoadingManager>.instance;
            object loadingLock = typeof(LoadingManager).GetFieldByName("m_loadingLock").GetValue(loadingManager);

            while (!Monitor.TryEnter(loadingLock, SimulationManager.SYNCHRONIZE_TIMEOUT)) { }
            try
            {
                FieldInfo mainThreadQueueField = typeof(LoadingManager).GetFieldByName("m_mainThreadQueue");
                Queue<IEnumerator> mainThreadQueue = (Queue<IEnumerator>) mainThreadQueueField.GetValue(loadingManager);
                if (mainThreadQueue != null)
                {
                    Queue<IEnumerator> newQueue = new Queue<IEnumerator>(mainThreadQueue.Count + 1);
                    newQueue.Enqueue(mainThreadQueue.Dequeue()); // currently running action must continue to be the first in the queue
                    newQueue.Enqueue(action);
                    while (mainThreadQueue.Count > 0)
                        newQueue.Enqueue(mainThreadQueue.Dequeue());

                    mainThreadQueueField.SetValue(loadingManager, newQueue);
                }
            }
            finally
            {
                Monitor.Exit(loadingLock);
            }
        }

        // TODO: Put this in its own class
		void UpdateLocalization()
		{
			if (sm_localizationInitialized)
				return;

            Debug.Log("Traffic++: Updating Localization.");

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

				sm_localizationInitialized = true;
			}
			catch (ArgumentException e)
            {
                Debug.Log("Traffic++: Unexpected " + e.GetType().Name + " updating localization: " + e.Message + "\n" + e.StackTrace + "\n");
            }

            Debug.Log("Traffic++: Localization successfully updated.");
		}
	}
}
