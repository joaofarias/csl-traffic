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
using ColossalFramework.Plugins;
using ColossalFramework.UI;

namespace CSL_Traffic
{
	class Initializer : MonoBehaviour
	{
        [Flags]
        enum RoadType
        {
            Normal      = 0,
            
            Grass       = 1,
            Trees       = 2,
            
            Elevated    = 4,
            Bridge      = 8,
            
            Pavement    = 16,
            Gravel      = 32,
            
            OneWay      = 64
        }

        [Flags]
        enum TextureType
        {
            Main = 1,
            XYS = 2,
            ACI = 4,
            APR = 8,
            
            All = 15
        }

        static Queue<IEnumerator> sm_actionQueue = new Queue<IEnumerator>();
        static System.Object sm_queueLock = new System.Object();
		static bool sm_localizationInitialized;
        static readonly string[] sm_thumbnailStates = new string[] { "", "Disabled", "Focused", "Hovered", "Pressed" };
        static readonly Dictionary<string, Vector2> sm_thumbnailCoords = new Dictionary<string, Vector2>()
        {
            {"RoadLargeBusLanes", new Vector2(0, 0)},
            {"RoadSmallBusway", new Vector2(109, 0)},
            {"RoadSmallBuswayOneWay", new Vector2(218, 0)},
        };
        static readonly Dictionary<string, string> sm_fileIndex = new Dictionary<string, string>()
        {
            {"RoadLargeBusLanesElevated",       "RoadLargeBusLanesBridge"},
            {"RoadSmallBuswayElevated",         "RoadSmallBuswayBridge"},
            {"RoadSmallBuswayOneWayBridge",     "RoadSmallBuswayBridge"},
            {"RoadSmallBuswayOneWayElevated",   "RoadSmallBuswayBridge"},
            {"RoadSmallBusway-bus",             "RoadSmallBusway"},
            {"RoadSmallBusway-busBoth",         "RoadSmallBusway"},
            {"RoadSmallBuswayOneWay",           "RoadSmallBusway"},
            {"RoadSmallBuswayOneWay-bus",       "RoadSmallBusway"},
            {"RoadSmallBuswayOneWay-busBoth",   "RoadSmallBusway"},
            {"PedestrianRoad-node",             "PedestrianRoad"},
        };
        static readonly Dictionary<string, string> sm_lodFileIndex = new Dictionary<string, string>()
        {
            {"RoadLargeBusLanes",               "RoadsWithBusLanes"},
            {"RoadLargeBusLanesBridge",         "RoadsWithBusLanes"},
            {"RoadLargeBusLanesElevated",       "RoadsWithBusLanes"},
            {"RoadSmallBusway",                 "RoadsWithBusLanes"},
            {"RoadSmallBuswayBridge",           "RoadsWithBusLanes"},
            {"RoadSmallBuswayElevated",         "RoadsWithBusLanes"},
            {"RoadSmallBuswayOneWay",           "RoadsWithBusLanes"},
            {"RoadSmallBuswayOneWayBridge",     "RoadsWithBusLanes"},
            {"RoadSmallBuswayOneWayElevated",   "RoadsWithBusLanes"},
        };

        Dictionary<string, NetLaneProps> m_customNetLaneProps;
        Dictionary<string, PrefabInfo> m_customPrefabs;
        Queue<Action> m_postLoadingActions;
        UITextureAtlas m_thumbnailsTextureAtlas;
        bool m_initialized;
        bool m_incompatibilityWarning;
        float m_gameStartedTime;

		void Awake()
		{
			DontDestroyOnLoad(this);

            m_customNetLaneProps = new Dictionary<string, NetLaneProps>();
            m_customPrefabs = new Dictionary<string, PrefabInfo>();
            m_postLoadingActions = new Queue<Action>();
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
                Debug.Log("Traffic++: Game level was loaded. Options enabled: \n\t" + CSLTraffic.Options);

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

                m_customNetLaneProps.Clear();
                m_customPrefabs.Clear();
                m_postLoadingActions.Clear();
			}
		}

        public void OnLevelUnloading()
        {
            if ((CSLTraffic.Options & OptionsManager.ModOptions.UseRealisticSpeeds) == OptionsManager.ModOptions.UseRealisticSpeeds)
            {
                for (uint i = 0; i < PrefabCollection<CitizenInfo>.LoadedCount(); i++)
                {
                    CitizenInfo cit = PrefabCollection<CitizenInfo>.GetLoaded(i);
                    cit.m_walkSpeed /= 0.25f;
                }
            }
        }

        void Update()
        {
            if (!m_initialized)
            {
                TryReplacePrefabs();
                return;
            }

            if (!Singleton<LoadingManager>.instance.m_loadingComplete)
                return;
            else if (m_gameStartedTime == 0f)
                m_gameStartedTime = Time.realtimeSinceStartup;

            while (m_postLoadingActions.Count > 0)
                m_postLoadingActions.Dequeue().Invoke();

            // contributed by Japa
            TransportTool transportTool = ToolsModifierControl.GetCurrentTool<TransportTool>();
            if (transportTool != null)
            {
                CustomTransportTool customTransportTool = ToolsModifierControl.SetTool<CustomTransportTool>();
                if (customTransportTool != null)
                {
                    customTransportTool.m_prefab = transportTool.m_prefab;
                }
            }

            // Checks if CustomPathManager have been replaced by another mod and prints a warning in the log
            // This check is only run in the first two minutes since game is loaded
            if (!m_incompatibilityWarning && (CSLTraffic.Options & OptionsManager.ModOptions.GhostMode) == OptionsManager.ModOptions.None)
            {
                if ((Time.realtimeSinceStartup - m_gameStartedTime) < 120f)
                {
                    CustomPathManager customPathManager = Singleton<PathManager>.instance as CustomPathManager;
                    if (customPathManager == null)
                    {
                        Debug.Log("Traffic++: CustomPathManager not found! There's an incompatibility with another mod.");
                        UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Incompatibility Issue", "Traffic++ detected an incompatibility with another mod! You can continue playing but it's NOT recommended.", false);
                        m_incompatibilityWarning = true;
                    }
                }
                else
                    m_incompatibilityWarning = true;
            }
        }

#if DEBUG
        void OnGUI()
        {
            if (Singleton<LoadingManager>.instance.m_loadingComplete)
            {
                if (GUI.Button(new Rect(10, 900, 150, 30), "Update Textures"))
                {
                    foreach (var item in m_customPrefabs.Values)
                    {
                        NetInfo netInfo = item as NetInfo;
                        if (netInfo.m_segments.Length == 0)
                            continue;

                        string fileName = netInfo.m_segments[0].m_material.mainTexture.name;
                        string lodFileName = netInfo.m_segments[0].m_combinedMaterial.mainTexture.name;
                        for (int i = 0; i < netInfo.m_segments.Length; i++)
                        {
                            if (i == 1) fileName += "-bus";
                            if (i == 2) fileName += "Both";
                            if (fileName.StartsWith("RoadLarge"))
                            {
                                ReplaceTextures(fileName, FileManager.Folder.LargeRoad, netInfo.m_segments[i].m_segmentMaterial);
                                ReplaceLODTextures(lodFileName, FileManager.Folder.Roads, netInfo.m_segments[i].m_combinedMaterial);
                            }
                            else if (fileName.StartsWith("RoadSmall"))
                            {
                                ReplaceTextures(fileName, FileManager.Folder.SmallRoad, netInfo.m_segments[i].m_segmentMaterial);
                                ReplaceLODTextures(lodFileName, FileManager.Folder.Roads, netInfo.m_segments[i].m_combinedMaterial);
                            }
                            else
                            {
                                ReplaceTextures(fileName, FileManager.Folder.PedestrianRoad, netInfo.m_segments[i].m_segmentMaterial);
                                ReplaceLODTextures(lodFileName, FileManager.Folder.Roads, netInfo.m_segments[i].m_combinedMaterial);
                            }
                        }
                    }

                    FileManager.ClearCache();
                }

                //if (GUI.Button(new Rect(10, 850, 150, 30), "Road Customizer"))
                //{
                //    //ToolsModifierControl.SetTool<RoadCustomizerTool>();
                //    //RoadCustomizerTool.InitializeUI();
                //}
                //if (GUI.Button(new Rect(10, 800, 150, 30), "Add Button"))
                //{
                //    RoadCustomizerTool.SetUIButton();
                //}
            }
        }
#endif

        #region Initialization

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
            VehicleCollection industrialFarmingVehicleCollection = null;
            VehicleCollection industrialForestryVehicleCollection = null;
            VehicleCollection industrialOilVehicleCollection = null;
            VehicleCollection industrialOreVehicleCollection = null;
            VehicleCollection residentialVehicleCollection = null;
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

                industrialFarmingVehicleCollection = TryGetComponent<VehicleCollection>("Industrial Farming");
                if (industrialFarmingVehicleCollection == null)
                    return;

                industrialForestryVehicleCollection = TryGetComponent<VehicleCollection>("Industrial Forestry");
                if (industrialForestryVehicleCollection == null)
                    return;

                industrialOilVehicleCollection = TryGetComponent<VehicleCollection>("Industrial Oil");
                if (industrialOilVehicleCollection == null)
                    return;

                industrialOreVehicleCollection = TryGetComponent<VehicleCollection>("Industrial Ore");
                if (industrialOreVehicleCollection == null)
                    return;

                residentialVehicleCollection = TryGetComponent<VehicleCollection>("Residential Low");
                if (residentialVehicleCollection == null)
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

                    CreateLaneProps();
                    LoadThumbnailsTextureAtlas();

                    CreatePedestrianRoad(roadsNetCollection, beautificationNetCollection);
                    CreateSmallBusway(roadsNetCollection);
                    CreateLargeRoadWithBusLanes(roadsNetCollection);

                    if ((CSLTraffic.Options & OptionsManager.ModOptions.GhostMode) != OptionsManager.ModOptions.GhostMode)
                    {
                        ReplaceVehicleAI<CustomAmbulanceAI>("Ambulance", healthCareVehicleCollection);
                        ReplaceVehicleAI<CustomBusAI>("Bus", publicTansportVehicleCollection);
                        ReplaceVehicleAI<CustomCargoTruckAI>(industrialVehicleCollection);
                        ReplaceVehicleAI<CustomCargoTruckAI>(industrialFarmingVehicleCollection);
                        ReplaceVehicleAI<CustomCargoTruckAI>(industrialForestryVehicleCollection);
                        ReplaceVehicleAI<CustomCargoTruckAI>(industrialOilVehicleCollection);
                        ReplaceVehicleAI<CustomCargoTruckAI>(industrialOreVehicleCollection);
                        ReplaceVehicleAI<CustomFireTruckAI>("Fire Truck", fireDepartmentVehicleCollection);
                        ReplaceVehicleAI<CustomGarbageTruckAI>("Garbage Truck", garbageVehicleCollection);
                        ReplaceVehicleAI<CustomHearseAI>("Hearse", healthCareVehicleCollection);
                        ReplaceVehicleAI<CustomPassengerCarAI>(residentialVehicleCollection);
                        ReplaceVehicleAI<CustomPoliceCarAI>("Police Car", policeVehicleCollection);

                        ReplaceTransportLineAI<BusTransportLineAI>("Bus Line", publicTansportNetCollection, "Bus", publicTransportTransportCollection);

                        AddTool<CustomTransportTool>(toolController);

                        if ((CSLTraffic.Options & OptionsManager.ModOptions.BetaTestRoadCustomizerTool) == OptionsManager.ModOptions.BetaTestRoadCustomizerTool)
                            AddTool<RoadCustomizerTool>(toolController);

                        if ((CSLTraffic.Options & OptionsManager.ModOptions.UseRealisticSpeeds) == OptionsManager.ModOptions.UseRealisticSpeeds)
                        {
                            for (uint i = 0; i < PrefabCollection<CitizenInfo>.LoadedCount(); i++)
                            {
                                CitizenInfo cit = PrefabCollection<CitizenInfo>.GetLoaded(i);
                                cit.m_walkSpeed *= 0.25f;
                            }
                        }
                    }

                    // Localization
                    UpdateLocalization();

                    AddQueuedActionsToLoadingQueue();

                    FileManager.ClearCache();

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

        public static void QueuePrioritizedLoadingAction(Action action)
        {
            QueuePrioritizedLoadingAction(ActionWrapper(action));
        }

        public static void QueuePrioritizedLoadingAction(IEnumerator action)
        {
            while (!Monitor.TryEnter(sm_queueLock, SimulationManager.SYNCHRONIZE_TIMEOUT)) { }
            try
            {
                sm_actionQueue.Enqueue(action);
            }
            finally { Monitor.Exit(sm_queueLock); }
        }

        static void AddQueuedActionsToLoadingQueue()
        {
            LoadingManager loadingManager = Singleton<LoadingManager>.instance;
            object loadingLock = typeof(LoadingManager).GetFieldByName("m_loadingLock").GetValue(loadingManager);

            while (!Monitor.TryEnter(loadingLock, SimulationManager.SYNCHRONIZE_TIMEOUT)) { }
            try
            {
                FieldInfo mainThreadQueueField = typeof(LoadingManager).GetFieldByName("m_mainThreadQueue");
                Queue<IEnumerator> mainThreadQueue = (Queue<IEnumerator>)mainThreadQueueField.GetValue(loadingManager);
                if (mainThreadQueue != null)
                {
                    Queue<IEnumerator> newQueue = new Queue<IEnumerator>(mainThreadQueue.Count + 1);
                    newQueue.Enqueue(mainThreadQueue.Dequeue()); // currently running action must continue to be the first in the queue

                    while (!Monitor.TryEnter(sm_queueLock, SimulationManager.SYNCHRONIZE_TIMEOUT)) { }
                    try
                    {
                        while (sm_actionQueue.Count > 0)
                            newQueue.Enqueue(sm_actionQueue.Dequeue());
                    }
                    finally
                    {
                        Monitor.Exit(sm_queueLock);
                    }


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

        static IEnumerator ActionWrapper(Action a)
        {
            a.Invoke();
            yield break;
        }

        public static void QueueLoadingAction(Action action)
        {
            Singleton<LoadingManager>.instance.QueueLoadingAction(ActionWrapper(action));
        }

        public static void QueueLoadingAction(IEnumerator action)
        {
            Singleton<LoadingManager>.instance.QueueLoadingAction(action);
        }

        #endregion

        #region Clone Methods

        static T ClonePrefab<T>(string prefabName, string newName, Transform customPrefabsHolder, bool replace = false, bool ghostMode = false) where T : PrefabInfo
        {
            T[] prefabs = Resources.FindObjectsOfTypeAll<T>();
            return ClonePrefab<T>(prefabName, prefabs, newName, customPrefabsHolder, replace, ghostMode);
        }

        static T ClonePrefab<T>(string prefabName, T[] prefabs, string newName, Transform customPrefabsHolder, bool replace = false, bool ghostMode = false) where T : PrefabInfo 
        {
            T originalPrefab = prefabs.FirstOrDefault(p => p.name == prefabName);
            if (originalPrefab == null)
                return null;

            GameObject instance = GameObject.Instantiate<GameObject>(originalPrefab.gameObject);
            instance.name = newName;
            instance.transform.SetParent(customPrefabsHolder);

            MethodInfo initMethod = GetCollectionType(typeof(T).Name).GetMethod("InitializePrefabs", BindingFlags.Static | BindingFlags.NonPublic);
            if (ghostMode)
            {
                Initializer.QueuePrioritizedLoadingAction((IEnumerator)initMethod.Invoke(null, new object[] { newName, new[] { instance.GetComponent<T>() }, new string[] { replace ? prefabName : null } }));
                return null;
            }

            T newPrefab = instance.GetComponent<T>();
            newPrefab.m_prefabInitialized = false;

            Initializer.QueuePrioritizedLoadingAction((IEnumerator)initMethod.Invoke(null, new object[] { newName, new[] { newPrefab }, new string[] { replace ? prefabName : null } }));

            return newPrefab;
        }

        NetInfo CloneRoad(string prefabName, string newName, RoadType roadType, NetCollection collection, string fileName = null, FileManager.Folder folder = FileManager.Folder.Roads)
        {
            bool ghostMode = (CSLTraffic.Options & OptionsManager.ModOptions.GhostMode) == OptionsManager.ModOptions.GhostMode;
            NetInfo road = ClonePrefab<NetInfo>(prefabName + GetDecoratedName(roadType & ~RoadType.OneWay), collection.m_prefabs, newName, transform, false, ghostMode);
            if (road == null)
                return null;

            // Replace textures

            if (String.IsNullOrEmpty(fileName))
                return road;

            fileName += GetDecoratedName(roadType, false);
            
            if (m_thumbnailsTextureAtlas != null && SetThumbnails(fileName))
            {
                road.m_Atlas = m_thumbnailsTextureAtlas;
                road.m_Thumbnail = fileName;
            }

            string str;
            if (sm_fileIndex.TryGetValue(fileName, out str))
                fileName = str;

            string lodFileName;
            if (!sm_lodFileIndex.TryGetValue(fileName, out lodFileName))
                lodFileName = fileName;

            
            for (int i = 0; i < road.m_segments.Length; i++)
            {
                string segFileName = fileName;
                if (roadType != RoadType.Bridge && roadType != RoadType.Elevated)
                {
                    if (i == 1) segFileName += "-bus";
                    if (i == 2) segFileName += "-busBoth";
                }

                if (sm_fileIndex.TryGetValue(segFileName, out str))
                    segFileName = str;

                road.m_segments[i].m_material = new Material(road.m_segments[i].m_material);
                ReplaceTextures(segFileName, folder, road.m_segments[i].m_material);
                int index = i;
                m_postLoadingActions.Enqueue(() =>
                {
                    ReplaceLODTextures(lodFileName, FileManager.Folder.Roads, road.m_segments[index].m_combinedMaterial);
                    road.m_segments[index].m_lodRenderDistance *= 2;
                });
            }

            for (int i = 0; i < road.m_nodes.Length; i++)
            {
                string nodeFileName = fileName + "-node";
                if (i != 0) nodeFileName += "" + i;

                if (sm_fileIndex.TryGetValue(nodeFileName, out str))
                    nodeFileName = str;

                road.m_nodes[i].m_material = new Material(road.m_nodes[i].m_material);
                ReplaceTextures(nodeFileName, folder, road.m_nodes[i].m_material);
                int index = i;
                m_postLoadingActions.Enqueue(() =>
                {
                    ReplaceLODTextures(lodFileName, FileManager.Folder.Roads, road.m_nodes[index].m_combinedMaterial);
                    road.m_nodes[index].m_lodRenderDistance *= 2;
                });
            }

            return road;
        }

        static NetLaneProps CloneNetLaneProps(string prefabName, int deltaSpace = 0)
        {
            NetLaneProps prefab = Resources.FindObjectsOfTypeAll<NetLaneProps>().FirstOrDefault(p => p.name == prefabName);
            if (prefab == null)
                return null;

            NetLaneProps newLaneProps = ScriptableObject.CreateInstance<NetLaneProps>();
            newLaneProps.m_props = new NetLaneProps.Prop[Mathf.Max(0, prefab.m_props.Length + deltaSpace)];
            Array.Copy(prefab.m_props, newLaneProps.m_props, Mathf.Min(newLaneProps.m_props.Length, prefab.m_props.Length));

            return newLaneProps;
        }

        static Type GetCollectionType(string prefabType)
        {
            switch (prefabType)
            {
                case "NetInfo":
                    return typeof(NetCollection);
                case "VehicleInfo":
                    return typeof(VehicleCollection);
                case "PropInfo":
                    return typeof(PropCollection);
                case "CitizenInfo":
                    return typeof(CitizenCollection);
                default:
                    return null;
            }
        }

        // whitespaces is for prefab names, no whitespaces is for texture file names
        static string GetDecoratedName(RoadType roadType, bool whiteSpaces = true)
        {
            StringBuilder sb = new StringBuilder();

            if (roadType.HasFlag(RoadType.OneWay))
                sb.Append(whiteSpaces ? " OneWay" : "OneWay");
            
            if (roadType.HasFlag(RoadType.Elevated))
                sb.Append(whiteSpaces ? " Elevated" : "Elevated");
            else if (roadType.HasFlag(RoadType.Bridge))
                sb.Append(whiteSpaces ? " Bridge" : "Bridge");
            
            if (roadType.HasFlag(RoadType.Grass))
                sb.Append(whiteSpaces ? " Decoration Grass" : "DecorationGrass");
            else if (roadType.HasFlag(RoadType.Trees))
                sb.Append(whiteSpaces ? " Decoration Trees" : "DecorationTrees");

            return sb.ToString();
        }

        #endregion

        #region Small Roads

        void CreateSmallBusway(NetCollection collection)
        {
            CreateSmallBusway(RoadType.Normal, collection);
            if ((CSLTraffic.Options & (OptionsManager.ModOptions.BetaTestNewRoads | OptionsManager.ModOptions.GhostMode)) != OptionsManager.ModOptions.None)
                CreateSmallBusway(RoadType.OneWay, collection);
        }

        void CreateSmallBusway(RoadType roadType, NetCollection collection)
        {
            string prefabName = (roadType & RoadType.OneWay) == RoadType.OneWay ? "Oneway Road" : "Basic Road";
            string newName = "Small Busway" + GetDecoratedName(roadType);
            NetInfo smallRoad = CloneRoad(prefabName, newName, roadType, collection, "RoadSmallBusway", FileManager.Folder.SmallRoad);
            bool abort = smallRoad == null;

            PrefabInfo bridge;
            string bridgeName = "Small Busway" + GetDecoratedName(roadType | RoadType.Bridge);
            if (!m_customPrefabs.TryGetValue(bridgeName, out bridge))
                bridge = CreateSmallBuswayBridge(roadType | RoadType.Bridge, collection);

            PrefabInfo elevated;
            bridgeName = "Small Busway" + GetDecoratedName(roadType | RoadType.Elevated);
            if (!m_customPrefabs.TryGetValue(bridgeName, out elevated))
                elevated = CreateSmallBuswayBridge(roadType | RoadType.Elevated, collection);

            if (abort)
                return;

            // TODO: review this system (roadtype can be big)
            smallRoad.m_UIPriority = 20 + (int)roadType;
            NetInfo highway = collection.m_prefabs.FirstOrDefault(p => p.name == "Highway");
            if (highway != null)
                smallRoad.m_UnlockMilestone = highway.m_UnlockMilestone;


            RoadAI roadAI = smallRoad.GetComponent<RoadAI>();
            roadAI.m_maintenanceCost = CalculateMaintenanceCost(0.36f);
            roadAI.m_enableZoning = false;
            roadAI.m_bridgeInfo = bridge as NetInfo;
            roadAI.m_elevatedInfo = elevated as NetInfo;

            NetLaneProps laneProps = null;
            m_customNetLaneProps.TryGetValue("BusLane", out laneProps);

            NetInfo.Lane[] lanes = new NetInfo.Lane[4];
            Array.Copy(smallRoad.m_lanes, lanes, 2);
            Array.Copy(smallRoad.m_lanes, 4, lanes, 2, 2);
            smallRoad.m_lanes = lanes;

            smallRoad.m_lanes[2] = new NetInfoLane(smallRoad.m_lanes[2], RoadManager.VehicleType.Bus | RoadManager.VehicleType.Emergency);
            smallRoad.m_lanes[2].m_position -= 1f;
            smallRoad.m_lanes[2].m_stopOffset += 1f;
            smallRoad.m_lanes[2].m_laneProps = laneProps;

            smallRoad.m_lanes[3] = new NetInfoLane(smallRoad.m_lanes[3], RoadManager.VehicleType.Bus | RoadManager.VehicleType.Emergency);
            smallRoad.m_lanes[3].m_position += 1f;
            smallRoad.m_lanes[3].m_stopOffset -= 1f;
            smallRoad.m_lanes[3].m_laneProps = laneProps;

            m_customPrefabs.Add(newName, smallRoad);
        }

        NetInfo CreateSmallBuswayBridge(RoadType roadType, NetCollection collection)
        {
            string prefabName = (roadType & RoadType.OneWay) == RoadType.OneWay ? "Oneway Road" : "Basic Road";
            string newName = "Small Busway" + GetDecoratedName(roadType);
            NetInfo smallRoad = CloneRoad(prefabName, newName, roadType, collection, "RoadSmallBusway", FileManager.Folder.SmallRoad);
            if (smallRoad == null)
                return null;

            RoadBridgeAI roadAI = smallRoad.GetComponent<RoadBridgeAI>();
            roadAI.m_maintenanceCost = CalculateMaintenanceCost(roadAI.m_maintenanceCost / 625 + 0.06f);

            NetLaneProps laneProps = null;
            m_customNetLaneProps.TryGetValue("BusLane", out laneProps);

            smallRoad.m_lanes[2] = new NetInfoLane(smallRoad.m_lanes[2], RoadManager.VehicleType.Bus | RoadManager.VehicleType.Emergency);
            smallRoad.m_lanes[2].m_laneProps = laneProps;

            smallRoad.m_lanes[3] = new NetInfoLane(smallRoad.m_lanes[3], RoadManager.VehicleType.Bus | RoadManager.VehicleType.Emergency);
            smallRoad.m_lanes[3].m_laneProps = laneProps;

            m_customPrefabs.Add(newName, smallRoad);

            return smallRoad;
        }

        #endregion

        #region Large Roads

        void CreateLargeRoadWithBusLanes(NetCollection collection)
        {
            CreateLargeRoadWithBusLanes(RoadType.Normal, collection);
        }

        void CreateLargeRoadWithBusLanes(RoadType roadType, NetCollection collection)
        {
            string prefabName = (roadType & RoadType.OneWay) == RoadType.OneWay ? "Large Oneway" : "Large Road";
            string newName = "Large Road" + GetDecoratedName(roadType) + " With Bus Lanes";
            NetInfo largeRoad = CloneRoad(prefabName, newName, roadType, collection, "RoadLargeBusLanes", FileManager.Folder.LargeRoad);
            bool abort = largeRoad == null;

            PrefabInfo bridge;
            string bridgeName = "Large Road" + GetDecoratedName(roadType | RoadType.Bridge) + " With Bus Lanes";
            if (!m_customPrefabs.TryGetValue(bridgeName, out bridge))
                bridge = CreateLargeRoadBridgeWithBusLanes(roadType | RoadType.Bridge, collection);
            
            PrefabInfo elevated;
            bridgeName = "Large Road" + GetDecoratedName(roadType | RoadType.Elevated) + " With Bus Lanes";
            if (!m_customPrefabs.TryGetValue(bridgeName, out elevated))
                elevated = CreateLargeRoadBridgeWithBusLanes(roadType | RoadType.Elevated, collection);

            if (abort)
                return;

            largeRoad.m_UIPriority = 20 + (int)roadType;

            RoadAI roadAI = largeRoad.GetComponent<RoadAI>();
            roadAI.m_maintenanceCost = 662;
            roadAI.m_bridgeInfo = bridge as NetInfo;
            roadAI.m_elevatedInfo = elevated as NetInfo;

            NetInfo highway = collection.m_prefabs.FirstOrDefault(p => p.name == "Highway");
            if (highway != null)
                largeRoad.m_UnlockMilestone = highway.m_UnlockMilestone;

            NetLaneProps laneProps = null;
            m_customNetLaneProps.TryGetValue("BusLane", out laneProps);

            largeRoad.m_lanes[4] = new NetInfoLane(largeRoad.m_lanes[4], RoadManager.VehicleType.Bus | RoadManager.VehicleType.Emergency);
            largeRoad.m_lanes[4].m_laneProps = laneProps;

            largeRoad.m_lanes[5] = new NetInfoLane(largeRoad.m_lanes[5], RoadManager.VehicleType.Bus | RoadManager.VehicleType.Emergency);
            largeRoad.m_lanes[5].m_laneProps = laneProps;

            m_customPrefabs.Add(newName, largeRoad);
        }

        NetInfo CreateLargeRoadBridgeWithBusLanes(RoadType roadType, NetCollection collection)
        {
            string prefabName = (roadType & RoadType.OneWay) == RoadType.OneWay ? "Large Oneway" : "Large Road";
            string newName = "Large Road" + GetDecoratedName(roadType) + " With Bus Lanes";
            NetInfo largeRoad = CloneRoad(prefabName, newName, roadType, collection, "RoadLargeBusLanes", FileManager.Folder.LargeRoad);
            if (largeRoad == null)
                return null;

            RoadBridgeAI roadAI = largeRoad.GetComponent<RoadBridgeAI>();
            roadAI.m_maintenanceCost = 1980;

            NetLaneProps laneProps = null;
            m_customNetLaneProps.TryGetValue("BusLane", out laneProps);

            largeRoad.m_lanes[2] = new NetInfoLane(largeRoad.m_lanes[2], RoadManager.VehicleType.Bus | RoadManager.VehicleType.Emergency);
            largeRoad.m_lanes[2].m_laneProps = laneProps;

            largeRoad.m_lanes[3] = new NetInfoLane(largeRoad.m_lanes[3], RoadManager.VehicleType.Bus | RoadManager.VehicleType.Emergency);
            largeRoad.m_lanes[3].m_laneProps = laneProps;

            m_customPrefabs.Add(newName, largeRoad);

            return largeRoad;
        }

        #endregion

        #region Pedestrian Roads

        void CreatePedestrianRoad(NetCollection roads, NetCollection beautification)
        {
            CreatePedestrianRoad(RoadType.Gravel, roads, beautification);
            CreatePedestrianRoad(RoadType.Pavement, roads, beautification);
        }

        void CreatePedestrianRoad(RoadType roadType, NetCollection roadsCollection, NetCollection beautificationCollection)
        {
            string newName = "Zonable Pedestrian" + (roadType.HasFlag(RoadType.Pavement) ? " Pavement" : " Gravel");
            NetInfo pedestrianRoad = CloneRoad("Gravel Road", newName, roadType, roadsCollection, "PedestrianRoad", FileManager.Folder.PedestrianRoad);
            bool abort = pedestrianRoad == null;

            PrefabInfo bridge = null;
            if ((roadType & RoadType.Pavement) == RoadType.Pavement)
            {
                string bridgeName = "Zonable Pedestrian" + GetDecoratedName(roadType | RoadType.Elevated);
                if (!m_customPrefabs.TryGetValue(bridgeName, out bridge))
                    bridge = CreatePedestrianRoadBridge(roadType | RoadType.Elevated, beautificationCollection);
            }

            if (abort)
                return;

            RoadAI roadAI = pedestrianRoad.GetComponent<RoadAI>();

            if ((roadType & RoadType.Pavement) == RoadType.Pavement)
            {
                pedestrianRoad.m_createGravel = false;
                pedestrianRoad.m_createPavement = true;
                pedestrianRoad.m_setVehicleFlags = Vehicle.Flags.None;
                pedestrianRoad.m_Thumbnail = "ThumbnailBuildingBeautificationPedestrianPavement";

                roadAI.m_constructionCost = 2000;
                roadAI.m_maintenanceCost = 250;
                roadAI.m_bridgeInfo = roadAI.m_elevatedInfo = bridge as NetInfo;
            }
            else
            {
                pedestrianRoad.m_Thumbnail = "ThumbnailBuildingBeautificationPedestrianGravel";
                roadAI.m_constructionCost = 1000;
                roadAI.m_maintenanceCost = 150;
            }

            NetInfo onewayRoad = roadsCollection.m_prefabs.FirstOrDefault(p => p.name == "Oneway Road");
            if (onewayRoad != null)
                pedestrianRoad.m_UnlockMilestone = onewayRoad.m_UnlockMilestone;

            int nLanes = 4;
            if ((CSLTraffic.Options & OptionsManager.ModOptions.DisableCentralLaneOnPedestrianRoads) == OptionsManager.ModOptions.None)
                nLanes++;

            NetInfo.Lane[] lanes = new NetInfo.Lane[nLanes];
            Array.Copy(pedestrianRoad.m_lanes, lanes, 2);
            Array.Copy(pedestrianRoad.m_lanes, 4, lanes, 2, 2);
            pedestrianRoad.m_lanes = lanes;

            pedestrianRoad.m_lanes[0].m_position = -4f;
            pedestrianRoad.m_lanes[0].m_width = 2f;
            
            pedestrianRoad.m_lanes[1].m_position = 4f;
            pedestrianRoad.m_lanes[1].m_width = 2f;

            RoadManager.VehicleType vehiclesAllowed = RoadManager.VehicleType.ServiceVehicles;
            if ((CSLTraffic.Options & OptionsManager.ModOptions.AllowTrucksInPedestrianRoads) == OptionsManager.ModOptions.AllowTrucksInPedestrianRoads)
                vehiclesAllowed |= RoadManager.VehicleType.CargoTruck;
            if ((CSLTraffic.Options & OptionsManager.ModOptions.AllowResidentsInPedestrianRoads) == OptionsManager.ModOptions.AllowResidentsInPedestrianRoads)
                vehiclesAllowed |= RoadManager.VehicleType.PassengerCar;

            pedestrianRoad.m_lanes[2] = new NetInfoLane(pedestrianRoad.m_lanes[2], vehiclesAllowed);
            pedestrianRoad.m_lanes[2].m_position = -1.25f;
            pedestrianRoad.m_lanes[2].m_speedLimit = 0.3f;
            pedestrianRoad.m_lanes[2].m_laneType = NetInfo.LaneType.Vehicle;
            //pedestrianRoad.m_lanes[2].m_laneProps = laneProps;

            pedestrianRoad.m_lanes[3] = new NetInfoLane(pedestrianRoad.m_lanes[3], vehiclesAllowed);
            pedestrianRoad.m_lanes[3].m_position = 1.25f;
            pedestrianRoad.m_lanes[3].m_speedLimit = 0.3f;
            pedestrianRoad.m_lanes[3].m_laneType = NetInfo.LaneType.Vehicle;
            //pedestrianRoad.m_lanes[5].m_laneProps = laneProps;

            if (nLanes == 5)
            {
                pedestrianRoad.m_lanes[4] = CloneLane(pedestrianRoad.m_lanes[0]);
                pedestrianRoad.m_lanes[4].m_position = 0f;
                pedestrianRoad.m_lanes[4].m_width = 5f;
                pedestrianRoad.m_lanes[4].m_laneProps = null;
            }

            m_customPrefabs.Add(newName, pedestrianRoad);
        }

        NetInfo CreatePedestrianRoadBridge(RoadType roadType, NetCollection collection)
        {
            string newName = "Zonable Pedestrian" + GetDecoratedName(roadType);
            NetInfo pedestrianBridge = CloneRoad("Pedestrian", newName, RoadType.Elevated, collection);
            if (pedestrianBridge == null)
                return null;
                
            pedestrianBridge.m_class = ScriptableObject.CreateInstance<ItemClass>();
            pedestrianBridge.m_class.m_service = ItemClass.Service.Road;
            pedestrianBridge.m_class.m_level = ItemClass.Level.Level1;

            PedestrianBridgeAI roadAI = pedestrianBridge.GetComponent<PedestrianBridgeAI>();
            roadAI.m_maintenanceCost = 250;
            roadAI.m_constructionCost = 2000;

            NetInfo.Lane[] lanes = new NetInfo.Lane[3];
            lanes[0] = pedestrianBridge.m_lanes[0];
            lanes[0].m_laneProps = null;

            RoadManager.VehicleType vehiclesAllowed = RoadManager.VehicleType.ServiceVehicles;
            if ((CSLTraffic.Options & OptionsManager.ModOptions.AllowTrucksInPedestrianRoads) == OptionsManager.ModOptions.AllowTrucksInPedestrianRoads)
                vehiclesAllowed |= RoadManager.VehicleType.CargoTruck;
            if ((CSLTraffic.Options & OptionsManager.ModOptions.AllowResidentsInPedestrianRoads) == OptionsManager.ModOptions.AllowResidentsInPedestrianRoads)
                vehiclesAllowed |= RoadManager.VehicleType.PassengerCar;

            // Backward Lane
            lanes[1] = new NetInfoLane(vehiclesAllowed);
            lanes[1].m_position = -1.5f;
            lanes[1].m_width = 2f;
            lanes[1].m_speedLimit = 0.3f;
            lanes[1].m_direction = NetInfo.Direction.Backward;
            lanes[1].m_laneType = NetInfo.LaneType.Vehicle;
            lanes[1].m_vehicleType = VehicleInfo.VehicleType.Car;

            // Forward Lane
            lanes[2] = new NetInfoLane(vehiclesAllowed);
            lanes[2].m_position = 1.5f;
            lanes[2].m_width = 2f;
            lanes[2].m_speedLimit = 0.3f;
            lanes[2].m_laneType = NetInfo.LaneType.Vehicle;
            lanes[2].m_vehicleType = VehicleInfo.VehicleType.Car;

            pedestrianBridge.m_lanes = lanes;

            m_customPrefabs.Add(newName, pedestrianBridge);

            return pedestrianBridge;
        }

        #endregion

        #region Road Utils

        static int CalculateMaintenanceCost(float target)
        {
            return Mathf.RoundToInt(target * 625);
        }

        static NetInfo.Lane CloneLane(NetInfo.Lane lane)
        {
            return new NetInfo.Lane()
            {
                m_position = lane.m_position,
		        m_width = lane.m_width,
		        m_verticalOffset = lane.m_verticalOffset,
		        m_stopOffset = lane.m_stopOffset,
		        m_speedLimit = lane.m_speedLimit,
		        m_direction = lane.m_direction,
		        m_laneType = lane.m_laneType,
		        m_vehicleType = lane.m_vehicleType,
		        m_laneProps = lane.m_laneProps,
		        m_allowStop = lane.m_allowStop,
		        m_useTerrainHeight = lane.m_useTerrainHeight
            };
        }

        #endregion

        #region Vehicles

        void ReplaceVehicleAI<T>(VehicleCollection collection) where T : VehicleAI
        {
            foreach (VehicleInfo vehicle in collection.m_prefabs)
            {
                if (vehicle.GetComponent<VehicleAI>().GetType() == typeof(T).BaseType)
                    ReplaceVehicleAI<T>(vehicle.name, collection);
            }
        }

        void ReplaceVehicleAI<T>(string prefabName, VehicleCollection collection) where T : VehicleAI
        {
            VehicleInfo vehicle = ClonePrefab<VehicleInfo>(prefabName, collection.m_prefabs, prefabName, transform, true);
            if (vehicle == null)
                return;

            VehicleAI originalAI = vehicle.GetComponent<VehicleAI>();
            T newAI = vehicle.gameObject.AddComponent<T>();
            CopyVehicleAIAttributes<T>(originalAI, newAI);
            Destroy(originalAI);

            if (vehicle.m_generatedInfo.m_vehicleInfo != null)// && vehicle.m_generatedInfo.m_vehicleInfo != vehicle)
                vehicle.m_generatedInfo.m_vehicleInfo = null;

            if ((CSLTraffic.Options & OptionsManager.ModOptions.UseRealisticSpeeds) == OptionsManager.ModOptions.UseRealisticSpeeds)
            {
                // TODO: set correct values on vehicles for realistic speeds
                switch (prefabName)
                {
                    case "Ambulance":
                        vehicle.m_acceleration *= 0.2f;
                        //vehicle.m_braking *= 0.3f;
                        //vehicle.m_turning *= 0.25f;
                        vehicle.m_maxSpeed *= 0.5f;
                        break;
                    case "Bus":
                    case "Fire Truck":
                    case "Garbage Truck":
                        vehicle.m_acceleration *= 0.15f;
                        //vehicle.m_braking *= 0.25f;
                        //vehicle.m_turning *= 0.2f;
                        vehicle.m_maxSpeed *= 0.5f;
                        break;
                    case "Hearse":
                    case "Police Car":
                        vehicle.m_acceleration *= 0.25f;
                        //vehicle.m_braking *= 0.35f;
                        //vehicle.m_turning *= 0.3f;
                        vehicle.m_maxSpeed *= 0.5f;
                        break;
                    default:
                        vehicle.m_acceleration *= 0.25f;
                        //vehicle.m_braking *= 0.35f;
                        //vehicle.m_turning *= 0.3f;
                        vehicle.m_maxSpeed *= 0.5f;
                        break;
                }
            }
        }

        void CopyVehicleAIAttributes<T>(VehicleAI from, T to)
        {
            foreach (FieldInfo fi in typeof(T).BaseType.GetFields())
            {
                    fi.SetValue(to, fi.GetValue(from));
            }
        }
        #endregion

        #region Props

        void CreateLaneProps()
        {
            // bus lane
            PropInfo busLaneText = CreateBusLaneTextProp("Road Arrow F", "BusLaneText");
            if (busLaneText == null)
                return;

            NetLaneProps busLaneProps = CloneNetLaneProps("Props - Carlane", 1);
            if (busLaneProps == null)
                return;

            busLaneProps.m_props[busLaneProps.m_props.Length - 1] = new NetLaneProps.Prop()
            {
                m_prop = busLaneText,
                m_position = new Vector3(0f, 0f, 7.5f),
                m_angle = 180f,
                m_segmentOffset = -1f,
                m_minLength = 8f,
                m_startFlagsRequired = NetNode.Flags.Junction
            };

            m_customNetLaneProps.Add("BusLane", busLaneProps);
        }

        PropInfo CreateBusLaneTextProp(string prefabName, string newName)
        {
            PropInfo newProp = ClonePrefab<PropInfo>(prefabName, newName, transform);
            if (newProp == null)
                return null;

            newProp.m_useColorVariations = false;

            Material newMat = new Material(newProp.GetComponent<Renderer>().sharedMaterial);
            ReplaceTextures(newName, FileManager.Folder.Props, newMat, TextureType.Main | TextureType.ACI | TextureType.XYS, 4);
            newMat.color = new Color32(255, 255, 255, 100);
            newProp.GetComponent<Renderer>().sharedMaterial = newMat;

            newProp.InitializePrefab();
            newProp.m_prefabInitialized = true;

            return newProp;
        }

        #endregion

        #region Transports

        void ReplaceTransportLineAI<T>(string prefabName, NetCollection collection, string transportName, TransportCollection transportCollection)
        {
            NetInfo transportLine = ClonePrefab<NetInfo>(prefabName, collection.m_prefabs, prefabName, transform, true);
            if (transportLine == null)
                return;

            Destroy(transportLine.GetComponent<TransportLineAI>());
            transportLine.gameObject.AddComponent<BusTransportLineAI>();

            TransportInfo transportInfo = transportCollection.m_prefabs.FirstOrDefault(p => p.name == transportName);
            if (transportInfo == null)
                return;
                //throw new KeyNotFoundException(transportName + " Transport Info not found on " + transportCollection.name);

            transportInfo.m_netInfo = transportLine;
        }

        #endregion

        #region Tools

        void AddTool<T>(ToolController toolController) where T : ToolBase
        {
            toolController.gameObject.AddComponent<T>();

            // contributed by Japa
            FieldInfo toolControllerField = typeof(ToolController).GetField("m_tools", BindingFlags.Instance | BindingFlags.NonPublic);
            if (toolControllerField != null)
                toolControllerField.SetValue(toolController, toolController.GetComponents<ToolBase>());
            FieldInfo toolModifierDictionary = typeof(ToolsModifierControl).GetField("m_Tools", BindingFlags.Static | BindingFlags.NonPublic);
            if (toolModifierDictionary != null)
                toolModifierDictionary.SetValue(null, null); // to force a refresh
        }

        #endregion

        #region Textures

        static bool ReplaceLODTextures(string fileName, FileManager.Folder textureFolder, Material mat, TextureType textureTypes = TextureType.All, int anisoLevel = 8, FilterMode filterMode = FilterMode.Trilinear, bool skipCache = false)
        {
            return ReplaceTextures(fileName + "-lodAtlas", textureFolder, mat, textureTypes, anisoLevel, filterMode, skipCache);
        }

        static bool ReplaceTextures(string fileName, FileManager.Folder textureFolder, Material mat, TextureType textureTypes = TextureType.All, int anisoLevel = 8, FilterMode filterMode = FilterMode.Trilinear, bool skipCache = false)
        {
            bool success = false;
            byte[] textureBytes;
            Texture2D tex;

            if ((textureTypes & TextureType.Main) == TextureType.Main)
            {
                if (FileManager.GetTextureBytes(fileName + ".png", textureFolder, skipCache, out textureBytes))
                {
                    tex = new Texture2D(1, 1);
                    tex.name = fileName;
                    tex.LoadImage(textureBytes);
                    tex.anisoLevel = anisoLevel;
                    tex.filterMode = filterMode;

                    mat.mainTexture = tex;

                    success = true;
                }
            }

            if ((textureTypes & TextureType.XYS) == TextureType.XYS && mat.HasProperty("_XYSMap"))
            {
                if (FileManager.GetTextureBytes(fileName + "-xys.png", textureFolder, skipCache, out textureBytes))
                {
                    tex = new Texture2D(1, 1);
                    tex.name = fileName + "-xys";
                    tex.LoadImage(textureBytes);
                    tex.anisoLevel = anisoLevel;
                    tex.filterMode = filterMode;

                    mat.SetTexture("_XYSMap", tex);

                    success = true;
                }
            }

            if ((textureTypes & TextureType.ACI) == TextureType.ACI && mat.HasProperty("_ACIMap"))
            {
                if (FileManager.GetTextureBytes(fileName + "-aci.png", textureFolder, skipCache, out textureBytes))
                {
                    tex = new Texture2D(1, 1);
                    tex.name = fileName + "-aci";
                    tex.LoadImage(textureBytes);
                    tex.anisoLevel = anisoLevel;
                    tex.filterMode = filterMode;

                    mat.SetTexture("_ACIMap", tex);

                    success = true;
                }
            }

            if ((textureTypes & TextureType.APR) == TextureType.APR && mat.HasProperty("_APRMap"))
            {
                if (FileManager.GetTextureBytes(fileName + "-apr.png", textureFolder, skipCache, out textureBytes))
                {
                    tex = new Texture2D(1, 1);
                    tex.name = fileName + "-apr";
                    tex.LoadImage(textureBytes);
                    tex.anisoLevel = anisoLevel;
                    tex.filterMode = filterMode;

                    mat.SetTexture("_APRMap", tex);

                    success = true;
                }
            }

            return success;
        }

        // based on method from "Some Roads" mod: https://gist.github.com/thatfool/0545ff2641ef46c2cf52
        void LoadThumbnailsTextureAtlas()
        {
            if (m_thumbnailsTextureAtlas != null)
                return;

            Shader shader = Shader.Find("UI/Default UI Shader");
            if (shader == null)
            {
                Debug.Log("Traffic++: Cannot find UI Shader. Using default thumbnails.");
                return;
            }

            byte[] bytes;
            if (!FileManager.GetTextureBytes("Thumbnails.png", FileManager.Folder.Roads, out bytes))
            {
                Debug.Log("Traffic++: Cannot find UI Atlas file. Using default thumbnails.");
                return;
            }

            Texture2D atlas = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            atlas.LoadImage(bytes);
            
            Material atlasMaterial = new Material(shader);
            atlasMaterial.mainTexture = atlas;

            m_thumbnailsTextureAtlas = ScriptableObject.CreateInstance<UITextureAtlas>();
            m_thumbnailsTextureAtlas.name = "Traffic++ Thumbnails";
            m_thumbnailsTextureAtlas.material = atlasMaterial;
        }

        bool SetThumbnails(string name)
        {
            if (m_thumbnailsTextureAtlas == null || !sm_thumbnailCoords.ContainsKey(name))
                return false;
            
            Vector2 startPosition = sm_thumbnailCoords[name];
            if (startPosition == null) // sanity check
                return false;

            Texture2D atlasTex = m_thumbnailsTextureAtlas.texture;
            float atlasWidth = atlasTex.width;
            float atlasHeight = atlasTex.height;
            float rectWidth = 109 / atlasWidth;
            float rectHeight = 75 / atlasHeight;
            int maxWidth = (int)atlasWidth / 109 * 109; // integer division
            int maxHeight = (int)atlasHeight / 75 * 75; // integer division
            int x = (int)startPosition.x;
            int y = (int)startPosition.y;

            for (int i = 0; i < 5; i++)
            {
                Texture2D spriteTex = new Texture2D(109, 75);
                spriteTex.SetPixels(atlasTex.GetPixels(x, y, 109, 75));

                UITextureAtlas.SpriteInfo sprite = new UITextureAtlas.SpriteInfo()
                {
                    name = name + sm_thumbnailStates[i],
                    region = new Rect(x/atlasWidth, y/atlasHeight, rectWidth, rectHeight),
                    texture = spriteTex
                };
                m_thumbnailsTextureAtlas.AddSprite(sprite);

                y += 75;
                if (y >= maxHeight)
                {
                    y = 0;
                    x += 109;
                    if (x >= maxWidth)
                    {
                        Debug.Log("Traffic++: Error setting thumbnail for " + name + ". Using default thumbnails.");
                        return false;
                    }
                }
            }

            return true;
        }

#if DEBUG
        public static void DumpRenderTexture(RenderTexture rt, string pngOutPath)
        {
            var oldRT = RenderTexture.active;

            var tex = new Texture2D(rt.width, rt.height);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();

            File.WriteAllBytes(pngOutPath, tex.EncodeToPNG());
            RenderTexture.active = oldRT;
        }

        public static void DumpTextureToPNG(Texture previewTexture, string filename = null)
        {
            if (filename == null)
            {
                filename = "";
                var filenamePrefix = String.Format("rt_dump_{0}", previewTexture.name);
                if (!File.Exists(filenamePrefix + ".png"))
                {
                    filename = filenamePrefix + ".png";
                }
                else
                {
                    int i = 1;
                    while (File.Exists(String.Format("{0}_{1}.png", filenamePrefix, i)))
                    {
                        i++;
                    }

                    filename = String.Format("{0}_{1}.png", filenamePrefix, i);
                }
            }

            if (File.Exists(filename))
            {
                File.Delete(filename);
            }

            if (previewTexture is RenderTexture)
            {
                DumpRenderTexture((RenderTexture)previewTexture, filename);
                //Log.Warning(String.Format("Texture dumped to \"{0}\"", filename));
            }
            else if (previewTexture is Texture2D)
            {
                var texture = previewTexture as Texture2D;
                byte[] bytes = null;

                try
                {
                    bytes = texture.EncodeToPNG();
                }
                catch (UnityException)
                {
                    //Log.Warning(String.Format("Texture \"{0}\" is marked as read-only, running workaround..", texture.name));
                }

                if (bytes == null)
                {
                    try
                    {
                        var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0);
                        Graphics.Blit(texture, rt);
                        DumpRenderTexture(rt, filename);
                        RenderTexture.ReleaseTemporary(rt);
                        //Log.Warning(String.Format("Texture dumped to \"{0}\"", filename));
                    }
                    catch (Exception ex)
                    {
                        //Log.Error("There was an error while dumping the texture - " + ex.Message);
                    }

                    return;
                }

                File.WriteAllBytes(filename, bytes);
                //Log.Warning(String.Format("Texture dumped to \"{0}\"", filename));
            }
            else
            {
                //Log.Error(String.Format("Don't know how to dump type \"{0}\"", previewTexture.GetType()));
            }
        }
#endif
        #endregion

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

                // Small road with bus
                k = new Locale.Key()
                {
                    m_Identifier = "NET_TITLE",
                    m_Key = "Small Busway"
                };
                locale.AddLocalizedString(k, "Small Busway");

                k = new Locale.Key()
                {
                    m_Identifier = "NET_DESC",
                    m_Key = "Small Busway"
                };
                locale.AddLocalizedString(k, "A two-lane busway to remove buses from common traffic and improve public transport coverage. It can also be used by vehicles in emergency.");

                // Small road with bus
                k = new Locale.Key()
                {
                    m_Identifier = "NET_TITLE",
                    m_Key = "Small Busway OneWay"
                };
                locale.AddLocalizedString(k, "One-Way Small Busway");

                k = new Locale.Key()
                {
                    m_Identifier = "NET_DESC",
                    m_Key = "Small Busway OneWay"
                };
                locale.AddLocalizedString(k, "A two-lane, one-way busway to remove buses from common traffic and improve public transport coverage. It can also be used by vehicles in emergency.");

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
