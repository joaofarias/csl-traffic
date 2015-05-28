using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.UI;
using CSL_Traffic.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml.Serialization;
using UnityEngine;

namespace CSL_Traffic
{
    public class Initializer : MonoBehaviour
    {
        [Flags]
        enum RoadType
        {
            Normal		= 0,
            
            Grass       = 1,
            Trees       = 2,
            
            Elevated    = 4,
            Bridge      = 8,
            Slope		= 16,
            Tunnel		= 32,
            
            Pavement    = 64,
            Gravel      = 128,
            
            OneWay      = 256
        }

        static Queue<IEnumerator> sm_actionQueue = new Queue<IEnumerator>();
        static System.Object sm_queueLock = new System.Object();
        static bool sm_localizationInitialized;
        static readonly string[] sm_collectionPrefixes = new string[] { "", "Europe " };
        static readonly string[] sm_thumbnailStates = new string[] { "", "Disabled", "Focused", "Hovered", "Pressed" };
        static readonly Dictionary<string, UI.UIUtils.SpriteTextureInfo> sm_thumbnailCoords = new Dictionary<string, UI.UIUtils.SpriteTextureInfo>()
        {
            {"Small Busway", new UI.UIUtils.SpriteTextureInfo() {width = 109, height = 75}},
            {"Small Busway Decoration Grass", new UI.UIUtils.SpriteTextureInfo() {startY = 75, width = 109, height = 75}},
            {"Small Busway Decoration Trees", new UI.UIUtils.SpriteTextureInfo() {startY = 150, width = 109, height = 75}},
            {"Small Busway OneWay", new UI.UIUtils.SpriteTextureInfo() {startY = 225, width = 109, height = 75}},
            {"Small Busway OneWay Decoration Grass", new UI.UIUtils.SpriteTextureInfo() {startY = 300, width = 109, height = 75}},
            {"Small Busway OneWay Decoration Trees", new UI.UIUtils.SpriteTextureInfo() {startY = 375, width = 109, height = 75}},
            {"Large Road With Bus Lanes", new UI.UIUtils.SpriteTextureInfo() {startY = 450, width = 109, height = 75}},
            {"Large Road Decoration Grass With Bus Lanes", new UI.UIUtils.SpriteTextureInfo() {startY = 525, width = 109, height = 75}},
            {"Large Road Decoration Trees With Bus Lanes", new UI.UIUtils.SpriteTextureInfo() {startY = 600, width = 109, height = 75}},
            {"Zonable Pedestrian Pavement", new UI.UIUtils.SpriteTextureInfo() {startY = 675, width = 109, height = 75}},
            {"Zonable Pedestrian Gravel", new UI.UIUtils.SpriteTextureInfo() {startY = 750, width = 109, height = 75}},
        };
        public static Dictionary<string, TextureInfo> sm_fileIndex = new Dictionary<string, TextureInfo>();
        //{
        //	{"RoadLargeBusLanesTrees", new TextureInfo() {name = "RoadLargeBusLanesTrees", mainTex = "RoadLargeBusLanesGrass"}},
        //	{"RoadSmallBusway", new TextureInfo() {name = "RoadSmallBusway", mainTex = "RoadLargeBusLanesGrass"}},
        //	{"xsdaf", new TextureInfo() {name = "fdsfs", mainTex = "RoadLargeBusLanesGrass"}},
        //};
        //static readonly Dictionary<string, string> sm_fileIndex = new Dictionary<string, string>()
        //{
        //	{"RoadLargeBusLanesTrees",				"RoadLargeBusLanesGrass"},
        //	{"RoadLargeBusLanesTrees-bus",			"RoadLargeBusLanesGrass-bus"},
        //	{"RoadLargeBusLanesTrees-busBoth",		"RoadLargeBusLanesGrass-busBoth"},
        //	{"RoadLargeBusLanesElevated",			"RoadLargeBusLanesBridge"},
            
        //	{"RoadSmallBuswayElevated",				"RoadSmallBuswayBridge"},
        //	{"RoadSmallBuswayOneWayBridge",			"RoadSmallBuswayBridge"},
        //	{"RoadSmallBuswayOneWayElevated",		"RoadSmallBuswayBridge"},
        //	{"RoadSmallBusway-bus",					"RoadSmallBusway"},
        //	{"RoadSmallBusway-busBoth",				"RoadSmallBusway"},
        //	{"RoadSmallBuswayOneWay",				"RoadSmallBusway"},
        //	{"RoadSmallBuswayOneWay-bus",			"RoadSmallBusway"},
        //	{"RoadSmallBuswayOneWay-busBoth",		"RoadSmallBusway"},
        //	{"RoadSmallBuswayGrass-bus",			"RoadSmallBuswayGrass"},
        //	{"RoadSmallBuswayGrass-busBoth",		"RoadSmallBuswayGrass"},
        //	{"RoadSmallBuswayTrees",				"RoadSmallBuswayGrass"},
        //	{"RoadSmallBuswayTrees-bus",			"RoadSmallBuswayGrass"},
        //	{"RoadSmallBuswayTrees-busBoth",		"RoadSmallBuswayGrass"},
        //	{"RoadSmallBuswayOneWayGrass",			"RoadSmallBuswayGrass"},
        //	{"RoadSmallBuswayOneWayGrass-bus",      "RoadSmallBuswayGrass"},
        //	{"RoadSmallBuswayOneWayGrass-busBoth",  "RoadSmallBuswayGrass"},
        //	{"RoadSmallBuswayOneWayTrees",          "RoadSmallBuswayGrass"},
        //	{"RoadSmallBuswayOneWayTrees-bus",      "RoadSmallBuswayGrass"},
        //	{"RoadSmallBuswayOneWayTrees-busBoth",  "RoadSmallBuswayGrass"},
        //};
        //static readonly Dictionary<string, string> sm_lodFileIndex = new Dictionary<string, string>()
        //{
        //	{"RoadLargeBusLanesElevated",       "RoadLargeBusLanesBridge"},
        //	{"RoadSmallBuswayElevated",         "RoadSmallBuswayBridge"},
        //	{"RoadSmallBuswayOneWayBridge",     "RoadSmallBuswayBridge"},
        //	{"RoadSmallBuswayOneWayElevated",   "RoadSmallBuswayBridge"},
        //	{"RoadSmallBuswayOneWay",           "RoadSmallBusway"},
        //	{"RoadSmallBuswayOneWay-bus",       "RoadSmallBusway-bus"},
        //	{"RoadSmallBuswayOneWay-busBoth",   "RoadSmallBusway-busBoth"},
        //};

        Dictionary<string, NetLaneProps> m_customNetLaneProps;
        Dictionary<string, PrefabInfo> m_customPrefabs;
        Dictionary<string, Texture2D> m_customTextures;
        //Queue<Action> m_postLoadingActions;
        UITextureAtlas m_thumbnailsTextureAtlas;
        bool m_initialized;
        bool m_incompatibilityWarning;
        float m_gameStartedTime;
        int m_level;

        void Awake()
        {
            DontDestroyOnLoad(this);

            m_customNetLaneProps = new Dictionary<string, NetLaneProps>();
            m_customPrefabs = new Dictionary<string, PrefabInfo>();
            m_customTextures = new Dictionary<string, Texture2D>();
            //m_postLoadingActions = new Queue<Action>();

            LoadTextureIndex();
        }

        void Start()
        {
            if ((CSLTraffic.Options & OptionsManager.ModOptions.GhostMode) != OptionsManager.ModOptions.GhostMode)
            {
                ReplacePathManager();
                ReplaceTransportManager();
            }
#if DEBUG
            //StartCoroutine(Print());
#endif
        }

        void OnLevelWasLoaded(int level) {
            this.m_level = level;

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
                //m_postLoadingActions.Clear();
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

                for (uint i = 0; i < PrefabCollection<VehicleInfo>.LoadedCount(); i++)
                {
                    SetRealisitcSpeeds(PrefabCollection<VehicleInfo>.GetLoaded(i), false);	
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

            if ((CSLTraffic.Options & OptionsManager.ModOptions.GhostMode) == OptionsManager.ModOptions.GhostMode)
                return;

            if (!Singleton<LoadingManager>.instance.m_loadingComplete)
                return;
            else if (m_gameStartedTime == 0f)
                m_gameStartedTime = Time.realtimeSinceStartup;

            //while (m_postLoadingActions.Count > 0)
            //	m_postLoadingActions.Dequeue().Invoke();

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

#if DEBUG
            if (Input.GetKeyUp(KeyCode.KeypadPlus))
            {
                VehicleInfo vehicleInfo = null;
                Color color = default(Color);
                switch (count)
                {
                    case 0:
                        vehicleInfo = PrefabCollection<VehicleInfo>.FindLoaded("Lorry");
                        color = vehicleInfo.m_material.color;
                        break;
                    case 1:
                        vehicleInfo = PrefabCollection<VehicleInfo>.FindLoaded("Bus");
                        color = vehicleInfo.m_material.color;
                        break;
                    case 2:
                        vehicleInfo = PrefabCollection<VehicleInfo>.FindLoaded("Ambulance");
                        color = vehicleInfo.m_material.color;
                        break;
                    case 3:
                        vehicleInfo = PrefabCollection<VehicleInfo>.FindLoaded("Police Car");
                        color = vehicleInfo.m_material.color;
                        break;
                    case 4:
                        vehicleInfo = PrefabCollection<VehicleInfo>.FindLoaded("Fire Truck");
                        color = vehicleInfo.m_material.color;
                        break;
                    case 5:
                        vehicleInfo = PrefabCollection<VehicleInfo>.FindLoaded("Hearse");
                        color = vehicleInfo.m_material.color;
                        break;
                    case 6:
                        vehicleInfo = PrefabCollection<VehicleInfo>.FindLoaded("Garbage Truck");
                        color = vehicleInfo.m_material.color;
                        break;
                    case 7:
                        vehicleInfo = PrefabCollection<VehicleInfo>.FindLoaded("Sports-car");
                        color = Color.yellow;
                        break;
                    default:
                        break;
                }
                count = (count + 1) % 8;
                
                if (vehicleInfo == null)
                    Debug.Log("Damn it!");
                else
                {
                    CreateVehicle(vehicleInfo.m_mesh, vehicleInfo.m_material, color);
                }
            }
#endif
        }

#if DEBUG
        int count = 0;
        GameObject vehicle;
        GameObject quad;
        void OnGUI()
        {
            if (Singleton<LoadingManager>.instance.m_loadingComplete)
            {
                if (GUI.Button(new Rect(10, 900, 150, 30), "Update Textures"))
                {
                    m_customTextures.Clear();
                    LoadTextureIndex();
                    foreach (var item in m_customPrefabs.Values)
                    {
                        NetInfo netInfo = item as NetInfo;
                        if (netInfo.m_segments.Length == 0)
                            continue;

                        TextureInfo textureInfo;
                        if (!sm_fileIndex.TryGetValue(netInfo.name, out textureInfo))
                            continue;

                        FileManager.Folder folder;
                        if (netInfo.name.Contains("Large"))
                            folder = FileManager.Folder.LargeRoad;
                        else if (netInfo.name.Contains("Small"))
                            folder = FileManager.Folder.SmallRoad;
                        else
                            folder = FileManager.Folder.PedestrianRoad;

                        for (int i = 0; i < netInfo.m_segments.Length; i++)
                        {
                            TextureType textureType = TextureType.Normal;
                            if (!netInfo.name.Contains("Bridge") && !netInfo.name.Contains("Elevated") && !netInfo.name.Contains("Slope") && !netInfo.name.Contains("Tunnel"))
                            {
                                if (i == 1) textureType = TextureType.Bus;
                                if (i == 2) textureType = TextureType.BusBoth;
                            }

                            ReplaceTextures(textureInfo, textureType,  folder, netInfo.m_segments[i].m_segmentMaterial);
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

        void CreateVehicle(Mesh mesh, Material material, Color color)
        {
            if (vehicle != null)
                Destroy(vehicle);

            vehicle = new GameObject("Vehicle");
            vehicle.transform.position = new Vector3(0f, 131f, -10f);
            vehicle.transform.rotation = Quaternion.Euler(0f, 210f, 0f);
            MeshFilter mf = vehicle.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = vehicle.AddComponent<MeshRenderer>();
            material.color = color;
            mr.sharedMaterial = material;

            if (quad == null)
            {
                quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.transform.position = new Vector3(0f, 130f, -10f);
                quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                quad.transform.localScale = new Vector3(100, 100);
                quad.GetComponent<Renderer>().sharedMaterial.color = new Color(255f, 203f, 219f);
            }

            CameraController cameraController = Camera.main.GetComponent<CameraController>();
            cameraController.m_targetPosition = new Vector3(0f, 139.775f, 0f);
            cameraController.m_targetSize = 40;
            cameraController.m_targetAngle = new Vector2(0f, 0f);
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
                    this.m_thumbnailsTextureAtlas = UI.UIUtils.LoadThumbnailsTextureAtlas("RoadThumbnails");

                    CreatePedestrianRoad(roadsNetCollection, beautificationNetCollection);
                    CreateSmallBusway(roadsNetCollection);
                    CreateLargeRoadWithBusLanes(roadsNetCollection);

                    if ((CSLTraffic.Options & OptionsManager.ModOptions.GhostMode) != OptionsManager.ModOptions.GhostMode && this.m_level == 6)
                    {
                        ReplaceVehicleAI(healthCareVehicleCollection);
                        ReplaceVehicleAI(publicTansportVehicleCollection);
                        ReplaceVehicleAI(industrialVehicleCollection);
                        ReplaceVehicleAI(industrialFarmingVehicleCollection);
                        ReplaceVehicleAI(industrialForestryVehicleCollection);
                        ReplaceVehicleAI(industrialOilVehicleCollection);
                        ReplaceVehicleAI(industrialOreVehicleCollection);
                        ReplaceVehicleAI(fireDepartmentVehicleCollection);
                        ReplaceVehicleAI(garbageVehicleCollection);
                        ReplaceVehicleAI(residentialVehicleCollection);
                        ReplaceVehicleAI(policeVehicleCollection);

                        StartCoroutine(HandleCustomVehicles());

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
            foreach (string prefix in sm_collectionPrefixes)
            {
                GameObject go = GameObject.Find(prefix + name);
                if (go != null)
                    return go.GetComponent<T>();
            }
            
            return default(T);
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
            instance.transform.localPosition = new Vector3(-7500, -7500, -7500);

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

        NetInfo CloneRoad(string prefabName, string newName, RoadType roadType, NetCollection collection, FileManager.Folder folder = FileManager.Folder.Roads)
        {
            bool ghostMode = (CSLTraffic.Options & OptionsManager.ModOptions.GhostMode) == OptionsManager.ModOptions.GhostMode;
            NetInfo road = ClonePrefab<NetInfo>(prefabName + GetDecoratedName(roadType & ~RoadType.OneWay), collection.m_prefabs, newName, transform, false, ghostMode);
            if (road == null)
                return null;

            // Replace textures

            if (m_thumbnailsTextureAtlas != null && SetThumbnails(newName))
            {
                road.m_Atlas = m_thumbnailsTextureAtlas;
                road.m_Thumbnail = newName;
            }

            TextureInfo textureInfo;
            if (!sm_fileIndex.TryGetValue(newName, out textureInfo))
                return road;

            for (int i = 0; i < road.m_segments.Length; i++)
            {
                // FIXME: handle different kind of segments that shouldn't be touched.
                if (roadType.HasFlag(RoadType.Bridge) && i != 0)
                    break;

                TextureType textureType = TextureType.Normal;
                if ((roadType & (RoadType.Bridge | RoadType.Elevated | RoadType.Tunnel | RoadType.Slope)) == RoadType.Normal)
                {
                    if (i == 1) textureType = TextureType.Bus;
                    if (i == 2) textureType = TextureType.BusBoth;
                }

                road.m_segments[i].m_material = new Material(road.m_segments[i].m_material);
                ReplaceTextures(textureInfo, textureType, folder, road.m_segments[i].m_material);

                road.m_segments[i].m_lodMaterial = new Material(road.m_segments[i].m_lodMaterial);
                ReplaceTextures(textureInfo, textureType | TextureType.LOD, folder, road.m_segments[i].m_lodMaterial);
            }

            for (int i = 0; i < road.m_nodes.Length; i++)
            {
                // FIXME: handle different kind of nodes that shouldn't be touched.
                if (newName.Contains("Pedestrian") && i != 0)
                    break;

                road.m_nodes[i].m_material = new Material(road.m_nodes[i].m_material);
                ReplaceTextures(textureInfo, TextureType.Node, folder, road.m_nodes[i].m_material);

                road.m_nodes[i].m_lodMaterial = new Material(road.m_nodes[i].m_lodMaterial);
                ReplaceTextures(textureInfo, TextureType.NodeLOD, folder, road.m_nodes[i].m_lodMaterial);
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
            else if (roadType.HasFlag(RoadType.Slope))
                sb.Append(whiteSpaces ? " Slope" : "Slope");
            else if (roadType.HasFlag(RoadType.Tunnel))
                sb.Append(whiteSpaces ? " Tunnel" : "Tunnel");
            
            if (roadType.HasFlag(RoadType.Grass))
                sb.Append(whiteSpaces ? " Decoration Grass" : "Grass");
            else if (roadType.HasFlag(RoadType.Trees))
                sb.Append(whiteSpaces ? " Decoration Trees" : "Trees");

            return sb.ToString();
        }

        #endregion

        #region Small Roads

        void CreateSmallBusway(NetCollection collection)
        {
            CreateSmallBusway(RoadType.Normal, collection);
            CreateSmallBusway(RoadType.OneWay, collection);
            CreateSmallBusway(RoadType.Grass, collection);
            CreateSmallBusway(RoadType.OneWay | RoadType.Grass, collection);
            CreateSmallBusway(RoadType.Trees, collection);
            CreateSmallBusway(RoadType.OneWay | RoadType.Trees, collection);
        }

        void CreateSmallBusway(RoadType roadType, NetCollection collection)
        {
            string prefabName = (roadType & RoadType.OneWay) == RoadType.OneWay ? "Oneway Road" : "Basic Road";
            string newName = "Small Busway" + GetDecoratedName(roadType);
            if (m_customPrefabs.ContainsKey(newName))
                return;

            NetInfo smallRoad = CloneRoad(prefabName, newName, roadType, collection, FileManager.Folder.SmallRoad);
            bool abort = smallRoad == null;

            RoadType otherPrefabsType = roadType & ~(RoadType.Grass | RoadType.Trees);
            PrefabInfo bridge;
            string otherPrefabName = "Small Busway" + GetDecoratedName(otherPrefabsType | RoadType.Bridge);
            if (!m_customPrefabs.TryGetValue(otherPrefabName, out bridge))
                bridge = CreateSmallBuswayBridge(otherPrefabsType | RoadType.Bridge, collection);

            PrefabInfo elevated;
            otherPrefabName = "Small Busway" + GetDecoratedName(otherPrefabsType | RoadType.Elevated);
            if (!m_customPrefabs.TryGetValue(otherPrefabName, out elevated))
                elevated = CreateSmallBuswayBridge(otherPrefabsType | RoadType.Elevated, collection);

            PrefabInfo tunnel;
            otherPrefabName = "Small Busway" + GetDecoratedName(otherPrefabsType | RoadType.Tunnel);
            if (!m_customPrefabs.TryGetValue(otherPrefabName, out tunnel))
                tunnel = CreateSmallBuswayBridge(otherPrefabsType | RoadType.Tunnel, collection);

            PrefabInfo slope;
            otherPrefabName = "Small Busway" + GetDecoratedName(otherPrefabsType | RoadType.Slope);
            if (!m_customPrefabs.TryGetValue(otherPrefabName, out slope))
                slope = CreateSmallBuswayBridge(otherPrefabsType | RoadType.Slope, collection);

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
            roadAI.m_tunnelInfo = tunnel as NetInfo;
            roadAI.m_slopeInfo = slope as NetInfo;

            NetLaneProps laneProps = null;
            m_customNetLaneProps.TryGetValue("BusLane", out laneProps);

            NetInfo.Lane[] lanes = new NetInfo.Lane[4];
            Array.Copy(smallRoad.m_lanes, lanes, 2);
            Array.Copy(smallRoad.m_lanes, smallRoad.m_lanes.Length-2, lanes, 2, 2);
            smallRoad.m_lanes = lanes;

            smallRoad.m_lanes[2] = new NetInfoLane(smallRoad.m_lanes[2], RoadManager.VehicleType.Bus | RoadManager.VehicleType.Emergency, NetInfoLane.SpecialLaneType.BusLane);
            smallRoad.m_lanes[2].m_laneProps = laneProps;
            smallRoad.m_lanes[2].m_speedLimit = 1.6f;
            if ((roadType & (RoadType.Grass | RoadType.Trees)) == RoadType.Normal)
            {
                smallRoad.m_lanes[2].m_position -= 1f;
                smallRoad.m_lanes[2].m_stopOffset += 1f;
            }

            smallRoad.m_lanes[3] = new NetInfoLane(smallRoad.m_lanes[3], RoadManager.VehicleType.Bus | RoadManager.VehicleType.Emergency, NetInfoLane.SpecialLaneType.BusLane);
            smallRoad.m_lanes[3].m_laneProps = laneProps;
            smallRoad.m_lanes[3].m_speedLimit = 1.6f;
            if ((roadType & (RoadType.Grass | RoadType.Trees)) == RoadType.Normal)
            {
                smallRoad.m_lanes[3].m_position += 1f;
                smallRoad.m_lanes[3].m_stopOffset -= 1f;
            }

            m_customPrefabs.Add(newName, smallRoad);
        }

        NetInfo CreateSmallBuswayBridge(RoadType roadType, NetCollection collection)
        {
            string prefabName = (roadType & RoadType.OneWay) == RoadType.OneWay ? "Oneway Road" : "Basic Road";
            string newName = "Small Busway" + GetDecoratedName(roadType);
            NetInfo smallRoad = CloneRoad(prefabName, newName, roadType, collection, FileManager.Folder.SmallRoad);
            if (smallRoad == null)
                return null;

            RoadBaseAI roadAI = smallRoad.GetComponent<RoadBaseAI>();
            roadAI.m_maintenanceCost = CalculateMaintenanceCost(roadAI.m_maintenanceCost / 625 + 0.06f);
            // TODO: check maintenace costs for tunnels

            NetLaneProps laneProps = null;
            m_customNetLaneProps.TryGetValue("BusLane", out laneProps);

            smallRoad.m_lanes[2] = new NetInfoLane(smallRoad.m_lanes[2], RoadManager.VehicleType.Bus | RoadManager.VehicleType.Emergency, NetInfoLane.SpecialLaneType.BusLane);
            smallRoad.m_lanes[2].m_laneProps = laneProps;
            smallRoad.m_lanes[2].m_speedLimit = 1.6f;

            smallRoad.m_lanes[3] = new NetInfoLane(smallRoad.m_lanes[3], RoadManager.VehicleType.Bus | RoadManager.VehicleType.Emergency, NetInfoLane.SpecialLaneType.BusLane);
            smallRoad.m_lanes[3].m_laneProps = laneProps;
            smallRoad.m_lanes[3].m_speedLimit = 1.6f;

            m_customPrefabs.Add(newName, smallRoad);

            return smallRoad;
        }

        #endregion

        #region Large Roads

        void CreateLargeRoadWithBusLanes(NetCollection collection)
        {
            CreateLargeRoadWithBusLanes(RoadType.Normal, collection);
            CreateLargeRoadWithBusLanes(RoadType.Grass, collection);
            CreateLargeRoadWithBusLanes(RoadType.Trees, collection);
        }

        void CreateLargeRoadWithBusLanes(RoadType roadType, NetCollection collection)
        {
            string prefabName = (roadType & RoadType.OneWay) == RoadType.OneWay ? "Large Oneway" : "Large Road";
            string newName = "Large Road" + GetDecoratedName(roadType) + " With Bus Lanes";
            if (m_customPrefabs.ContainsKey(newName))
                return;

            NetInfo largeRoad = CloneRoad(prefabName, newName, roadType, collection, FileManager.Folder.LargeRoad);
            bool abort = largeRoad == null;

            RoadType otherPrefabsType = roadType & ~(RoadType.Grass | RoadType.Trees);
            PrefabInfo bridge;
            string otherPrefabName = "Large Road" + GetDecoratedName(otherPrefabsType | RoadType.Bridge) + " With Bus Lanes";
            if (!m_customPrefabs.TryGetValue(otherPrefabName, out bridge))
                bridge = CreateLargeRoadBridgeWithBusLanes(otherPrefabsType | RoadType.Bridge, collection);
            
            PrefabInfo elevated;
            otherPrefabName = "Large Road" + GetDecoratedName(otherPrefabsType | RoadType.Elevated) + " With Bus Lanes";
            if (!m_customPrefabs.TryGetValue(otherPrefabName, out elevated))
                elevated = CreateLargeRoadBridgeWithBusLanes(otherPrefabsType | RoadType.Elevated, collection);

            PrefabInfo tunnel;
            otherPrefabName = "Large Road" + GetDecoratedName(otherPrefabsType | RoadType.Tunnel) + " With Bus Lanes";
            if (!m_customPrefabs.TryGetValue(otherPrefabName, out tunnel))
                tunnel = CreateLargeRoadBridgeWithBusLanes(otherPrefabsType | RoadType.Tunnel, collection);

            PrefabInfo slope;
            otherPrefabName = "Large Road" + GetDecoratedName(otherPrefabsType | RoadType.Slope) + " With Bus Lanes";
            if (!m_customPrefabs.TryGetValue(otherPrefabName, out slope))
                slope = CreateLargeRoadBridgeWithBusLanes(otherPrefabsType | RoadType.Slope, collection);

            if (abort)
                return;

            largeRoad.m_UIPriority = 20 + (int)roadType;

            RoadAI roadAI = largeRoad.GetComponent<RoadAI>();
            roadAI.m_maintenanceCost = 662;
            roadAI.m_bridgeInfo = bridge as NetInfo;
            roadAI.m_elevatedInfo = elevated as NetInfo;
            roadAI.m_tunnelInfo = tunnel as NetInfo;
            roadAI.m_slopeInfo = slope as NetInfo;

            NetInfo highway = collection.m_prefabs.FirstOrDefault(p => p.name == "Highway");
            if (highway != null)
                largeRoad.m_UnlockMilestone = highway.m_UnlockMilestone;

            NetLaneProps laneProps = null;
            m_customNetLaneProps.TryGetValue("BusLane", out laneProps);

            largeRoad.m_lanes[4] = new NetInfoLane(largeRoad.m_lanes[4], RoadManager.VehicleType.Bus | RoadManager.VehicleType.Emergency, NetInfoLane.SpecialLaneType.BusLane);
            largeRoad.m_lanes[4].m_laneProps = laneProps;

            largeRoad.m_lanes[5] = new NetInfoLane(largeRoad.m_lanes[5], RoadManager.VehicleType.Bus | RoadManager.VehicleType.Emergency, NetInfoLane.SpecialLaneType.BusLane);
            largeRoad.m_lanes[5].m_laneProps = laneProps;

            m_customPrefabs.Add(newName, largeRoad);
        }

        NetInfo CreateLargeRoadBridgeWithBusLanes(RoadType roadType, NetCollection collection)
        {
            string prefabName = (roadType & RoadType.OneWay) == RoadType.OneWay ? "Large Oneway" : "Large Road";
            string newName = "Large Road" + GetDecoratedName(roadType) + " With Bus Lanes";
            NetInfo largeRoad = CloneRoad(prefabName, newName, roadType, collection, FileManager.Folder.LargeRoad);
            if (largeRoad == null)
                return null;

            RoadBaseAI roadAI = largeRoad.GetComponent<RoadBaseAI>();
            roadAI.m_maintenanceCost = 1980;
            // TODO: check maintenace costs for tunnels

            NetLaneProps laneProps = null;
            m_customNetLaneProps.TryGetValue("BusLane", out laneProps);

            largeRoad.m_lanes[2] = new NetInfoLane(largeRoad.m_lanes[2], RoadManager.VehicleType.Bus | RoadManager.VehicleType.Emergency, NetInfoLane.SpecialLaneType.BusLane);
            largeRoad.m_lanes[2].m_laneProps = laneProps;

            largeRoad.m_lanes[3] = new NetInfoLane(largeRoad.m_lanes[3], RoadManager.VehicleType.Bus | RoadManager.VehicleType.Emergency, NetInfoLane.SpecialLaneType.BusLane);
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
            if (m_customPrefabs.ContainsKey(newName))
                return;

            NetInfo pedestrianRoad = CloneRoad("Gravel Road", newName, roadType, roadsCollection, FileManager.Folder.PedestrianRoad);
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
                //pedestrianRoad.m_Thumbnail = "ThumbnailBuildingBeautificationPedestrianPavement";

                roadAI.m_constructionCost = 2000;
                roadAI.m_maintenanceCost = 250;
                roadAI.m_bridgeInfo = roadAI.m_elevatedInfo = bridge as NetInfo;
            }
            else
            {
                //pedestrianRoad.m_Thumbnail = "ThumbnailBuildingBeautificationPedestrianGravel";
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

            pedestrianRoad.m_lanes[2] = new NetInfoLane(pedestrianRoad.m_lanes[2], vehiclesAllowed, NetInfoLane.SpecialLaneType.PedestrianLane);
            pedestrianRoad.m_lanes[2].m_position = -1.25f;
            pedestrianRoad.m_lanes[2].m_speedLimit = 0.3f;
            pedestrianRoad.m_lanes[2].m_laneType = NetInfo.LaneType.Vehicle;
            //pedestrianRoad.m_lanes[2].m_laneProps = laneProps;

            pedestrianRoad.m_lanes[3] = new NetInfoLane(pedestrianRoad.m_lanes[3], vehiclesAllowed, NetInfoLane.SpecialLaneType.PedestrianLane);
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
            lanes[1] = new NetInfoLane(vehiclesAllowed, NetInfoLane.SpecialLaneType.PedestrianLane);
            lanes[1].m_position = -1.5f;
            lanes[1].m_width = 2f;
            lanes[1].m_speedLimit = 0.3f;
            lanes[1].m_direction = NetInfo.Direction.Backward;
            lanes[1].m_laneType = NetInfo.LaneType.Vehicle;
            lanes[1].m_vehicleType = VehicleInfo.VehicleType.Car;

            // Forward Lane
            lanes[2] = new NetInfoLane(vehiclesAllowed, NetInfoLane.SpecialLaneType.PedestrianLane);
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

        void ReplaceVehicleAI(VehicleCollection collection)
        {
            foreach (VehicleInfo vehicle in collection.m_prefabs)
                    ReplaceVehicleAI(vehicle);
        }

        void ReplaceVehicleAI(VehicleInfo info)
        {
            VehicleAI vAI = info.m_vehicleAI;
            if (vAI == null)
                return;

            Type type = vAI.GetType();

            if (type == typeof(AmbulanceAI))
                ReplaceVehicleAI<CustomAmbulanceAI>(info);
            else if (type == typeof(BusAI))
                ReplaceVehicleAI<CustomBusAI>(info);
            else if (type == typeof(CargoTruckAI))
                ReplaceVehicleAI<CustomCargoTruckAI>(info);
            else if (type == typeof(FireTruckAI))
                ReplaceVehicleAI<CustomFireTruckAI>(info);
            else if (type == typeof(GarbageTruckAI))
                ReplaceVehicleAI<CustomGarbageTruckAI>(info);
            else if (type == typeof(HearseAI))
                ReplaceVehicleAI<CustomHearseAI>(info);
            else if (type == typeof(PassengerCarAI))
                ReplaceVehicleAI<CustomPassengerCarAI>(info);
            else if (type == typeof(PoliceCarAI))
                ReplaceVehicleAI<CustomPoliceCarAI>(info);
        }

        void ReplaceVehicleAI<T>(VehicleInfo vehicle) where T : VehicleAI
        {
            VehicleAI originalAI = vehicle.GetComponent<VehicleAI>();
            T newAI = vehicle.gameObject.AddComponent<T>();
            CopyVehicleAIAttributes<T>(originalAI, newAI);
            Destroy(originalAI);

            vehicle.m_vehicleAI = newAI;
            newAI.m_info = vehicle;

            if ((CSLTraffic.Options & OptionsManager.ModOptions.UseRealisticSpeeds) == OptionsManager.ModOptions.UseRealisticSpeeds)
            {
                SetRealisitcSpeeds(vehicle, true);
            }
        }

        // TODO: set correct values on vehicles for realistic speeds
        void SetRealisitcSpeeds(VehicleInfo vehicle, bool activate)
        {
            float accelerationMultiplier;
            float maxSpeedMultiplier;
            switch (vehicle.name)
            {
                case "Ambulance":
                    accelerationMultiplier = 0.2f;
                    //vehicle.m_braking *= 0.3f;
                    //vehicle.m_turning *= 0.25f;
                    maxSpeedMultiplier = 0.5f;
                    break;
                case "Bus":
                case "Fire Truck":
                case "Garbage Truck":
                    accelerationMultiplier = 0.15f;
                    //vehicle.m_braking *= 0.25f;
                    //vehicle.m_turning *= 0.2f;
                    maxSpeedMultiplier = 0.5f;
                    break;
                case "Hearse":
                case "Police Car":
                    accelerationMultiplier = 0.25f;
                    //vehicle.m_braking *= 0.35f;
                    //vehicle.m_turning *= 0.3f;
                    maxSpeedMultiplier = 0.5f;
                    break;
                default:
                    accelerationMultiplier = 0.25f;
                    //vehicle.m_braking *= 0.35f;
                    //vehicle.m_turning *= 0.3f;
                    maxSpeedMultiplier = 0.5f;
                    break;
            }

            if (!activate)
            {
                accelerationMultiplier = 1f / accelerationMultiplier;
                maxSpeedMultiplier = 1f / maxSpeedMultiplier;
            }

            vehicle.m_acceleration *= accelerationMultiplier;
            vehicle.m_maxSpeed *= maxSpeedMultiplier;
        }

        void CopyVehicleAIAttributes<T>(VehicleAI from, T to)
        {
            foreach (FieldInfo fi in typeof(T).BaseType.GetFields())
            {
                    fi.SetValue(to, fi.GetValue(from));
            }
        }

        IEnumerator HandleCustomVehicles()
        {
            uint index = 0;
            List<string> replacedVehicles = new List<string>();
            while (!Singleton<LoadingManager>.instance.m_loadingComplete)
            {
                while (PrefabCollection<VehicleInfo>.LoadedCount() > index)
                {
                    VehicleInfo info = PrefabCollection<VehicleInfo>.GetLoaded(index);
                    if (info != null && info.name.EndsWith("_Data") && !replacedVehicles.Contains(info.name))
                    {
                        replacedVehicles.Add(info.name);
                        ReplaceVehicleAI(info);
                    }
                    
                    ++index;
                }					
                
                yield return new WaitForEndOfFrame();
            }
        }
        #endregion

        #region Props

        void CreateLaneProps()
        {
            if (m_customNetLaneProps.ContainsKey("BusLane"))
                return;

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

            TextureInfo textureInfo;
            if (sm_fileIndex.TryGetValue(newName, out textureInfo))
            {
                Material newMat = new Material(newProp.GetComponent<Renderer>().sharedMaterial);
                ReplaceTextures(textureInfo, TextureType.Normal, FileManager.Folder.Props, newMat, 4);
                newMat.color = new Color32(255, 255, 255, 100);
                newProp.GetComponent<Renderer>().sharedMaterial = newMat;
            }

            newProp.InitializePrefab();
            newProp.m_prefabInitialized = true;

            return newProp;
        }

        #endregion

        #region Transports

        void ReplaceTransportLineAI<T>(string prefabName, NetCollection collection, string transportName, TransportCollection transportCollection)
        {
            if (transform.FindChild(prefabName) != null)
                return;

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
            if (toolController.GetComponent<T>() != null)
                return;

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
        [Flags]
        enum TextureType
        {
            Normal = 0,
            Bus = 1,
            BusBoth = 2,
            Node = 4,
            LOD = 8,
            BusLOD = 9,
            BusBothLOD = 10,
            NodeLOD = 12
        }
        
        static string[] sm_mapNames = new string[] { "_MainTex", "_XYSMap", "_ACIMap", "_APRMap" };

        bool ReplaceTextures(TextureInfo textureInfo, TextureType textureType, FileManager.Folder textureFolder, Material mat, int anisoLevel = 8, FilterMode filterMode = FilterMode.Trilinear, bool skipCache = false)
        {
            bool success = false;
            byte[] textureBytes;
            Texture2D tex = null;

            for (int i = 0; i < sm_mapNames.Length; i++)
            {
                if (mat.HasProperty(sm_mapNames[i]) && mat.GetTexture(sm_mapNames[i]) != null)
                {
                    string fileName = GetTextureName(sm_mapNames[i], textureInfo, textureType);
                    if (!String.IsNullOrEmpty(fileName) && !m_customTextures.TryGetValue(fileName, out tex))
                    {
                        if (FileManager.GetTextureBytes(fileName + ".png", textureFolder, skipCache, out textureBytes))
                        {
                            tex = new Texture2D(1, 1);
                            tex.LoadImage(textureBytes);
                        }
                        else if (fileName.Contains("-LOD"))
                        {
                            Texture2D original = mat.GetTexture(sm_mapNames[i]) as Texture2D;
                            if (original != null)
                            {
                                tex = new Texture2D(original.width, original.height);
                                tex.SetPixels(original.GetPixels());
                                tex.Apply();
                            }
                        }
                    }

                    if (tex != null)
                    {
                        tex.name = fileName;
                        tex.anisoLevel = anisoLevel;
                        tex.filterMode = filterMode;
                        mat.SetTexture(sm_mapNames[i], tex);
                        m_customTextures[tex.name] = tex;
                        success = true;
                        tex = null;
                    }
                }
            }
            
            return success;
        }

        string GetTextureName(string map, TextureInfo info, TextureType type)
        {
            switch (type)
            {
                case TextureType.Normal:
                    switch (map)
                    {
                        case "_MainTex":	return info.mainTex;
                        case "_XYSMap":		return info.xysTex;
                        case "_ACIMap":		return info.aciTex;
                        case "_APRMap":		return info.aprTex;
                    }
                    break;
                case TextureType.Bus:
                    switch (map)
                    {
                        case "_MainTex":	return info.mainTexBus;
                        case "_XYSMap":		return info.xysTexBus;
                        case "_ACIMap":		return info.aciTexBus;
                        case "_APRMap":		return info.aprTexBus;
                    }
                    break;
                case TextureType.BusBoth:
                    switch (map)
                    {
                        case "_MainTex":	return info.mainTexBusBoth;
                        case "_XYSMap":		return info.xysTexBusBoth;
                        case "_ACIMap":		return info.aciTexBusBoth;
                        case "_APRMap":		return info.aprTexBusBoth;
                    }
                    break;
                case TextureType.Node:
                    switch (map)
                    {
                        case "_MainTex":	return info.mainTexNode;
                        case "_XYSMap":		return info.xysTexNode;
                        case "_ACIMap":		return info.aciTexNode;
                        case "_APRMap":		return info.aprTexNode;
                    }
                    break;
                case TextureType.LOD:
                    switch (map)
                    {
                        case "_MainTex":	return info.lodMainTex;
                        case "_XYSMap":		return info.lodXysTex;
                        case "_ACIMap":		return info.lodAciTex;
                        case "_APRMap":		return info.lodAprTex;
                    }
                    break;
                case TextureType.BusLOD:
                    switch (map)
                    {
                        case "_MainTex":	return info.lodMainTexBus;
                        case "_XYSMap":		return info.lodXysTexBus;
                        case "_ACIMap":		return info.lodAciTexBus;
                        case "_APRMap":		return info.lodAprTexBus;
                    }
                    break;
                case TextureType.BusBothLOD:
                    switch (map)
                    {
                        case "_MainTex":	return info.lodMainTexBusBoth;
                        case "_XYSMap":		return info.lodXysTexBusBoth;
                        case "_ACIMap":		return info.lodAciTexBusBoth;
                        case "_APRMap":		return info.lodAprTexBusBoth;
                    }
                    break;
                case TextureType.NodeLOD:
                    switch (map)
                    {
                        case "_MainTex":	return info.lodMainTexNode;
                        case "_XYSMap":		return info.lodXysTexNode;
                        case "_ACIMap":		return info.lodAciTexNode;
                        case "_APRMap":		return info.lodAprTexNode;
                    }
                    break;
                default:
                    break;
            }

            return null;
        }

        bool SetThumbnails(string name)
        {
            if (m_thumbnailsTextureAtlas == null || !sm_thumbnailCoords.ContainsKey(name))
                return false;

            return UI.UIUtils.SetThumbnails(name, sm_thumbnailCoords[name], m_thumbnailsTextureAtlas, sm_thumbnailStates);
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

                // Large road decoration grass with bus
                k = new Locale.Key()
                {
                    m_Identifier = "NET_TITLE",
                    m_Key = "Large Road Decoration Grass With Bus Lanes"
                };
                locale.AddLocalizedString(k, "Six-Lane Road With Bus Lanes And Decorative Grass");

                k = new Locale.Key()
                {
                    m_Identifier = "NET_DESC",
                    m_Key = "Large Road Decoration Grass With Bus Lanes"
                };
                locale.AddLocalizedString(k, "A six-lane road with decorative grass and dedicated bus lanes. The bus lanes can be used by vehicles in emergency. Decorations lower noise pollution. Supports high-traffic.");

                // Large road decoration trees with bus
                k = new Locale.Key()
                {
                    m_Identifier = "NET_TITLE",
                    m_Key = "Large Road Decoration Trees With Bus Lanes"
                };
                locale.AddLocalizedString(k, "Six-Lane Road With Bus Lanes And Decorative Trees");

                k = new Locale.Key()
                {
                    m_Identifier = "NET_DESC",
                    m_Key = "Large Road Decoration Trees With Bus Lanes"
                };
                locale.AddLocalizedString(k, "A six-lane road with decorative trees and dedicated bus lanes. The bus lanes can be used by vehicles in emergency. Decorations lower noise pollution. Supports high-traffic.");

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

                // Small road decoration grass with bus
                k = new Locale.Key()
                {
                    m_Identifier = "NET_TITLE",
                    m_Key = "Small Busway Decoration Grass"
                };
                locale.AddLocalizedString(k, "Small Busway With Decorative Grass");

                k = new Locale.Key()
                {
                    m_Identifier = "NET_DESC",
                    m_Key = "Small Busway Decoration Grass"
                };
                locale.AddLocalizedString(k, "A two-lane busway with decorative grass to remove buses from common traffic and improve public transport coverage. It can also be used by vehicles in emergency.");

                // Small road decoration trees with bus
                k = new Locale.Key()
                {
                    m_Identifier = "NET_TITLE",
                    m_Key = "Small Busway Decoration Trees"
                };
                locale.AddLocalizedString(k, "Small Busway With Decorative Trees");

                k = new Locale.Key()
                {
                    m_Identifier = "NET_DESC",
                    m_Key = "Small Busway Decoration Trees"
                };
                locale.AddLocalizedString(k, "A two-lane busway with decorative trees to remove buses from common traffic and improve public transport coverage. It can also be used by vehicles in emergency.");

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

                // Small road OneWay decoration grass with bus
                k = new Locale.Key()
                {
                    m_Identifier = "NET_TITLE",
                    m_Key = "Small Busway OneWay Decoration Grass"
                };
                locale.AddLocalizedString(k, "One-Way Small Busway With Decorative Grass");

                k = new Locale.Key()
                {
                    m_Identifier = "NET_DESC",
                    m_Key = "Small Busway OneWay Decoration Grass"
                };
                locale.AddLocalizedString(k, "A two-lane, one-way busway with decorative grass to remove buses from common traffic and improve public transport coverage. It can also be used by vehicles in emergency.");

                // Small road OneWay decoration trees with bus
                k = new Locale.Key()
                {
                    m_Identifier = "NET_TITLE",
                    m_Key = "Small Busway OneWay Decoration Trees"
                };
                locale.AddLocalizedString(k, "One-Way Small Busway With Decorative Trees");

                k = new Locale.Key()
                {
                    m_Identifier = "NET_DESC",
                    m_Key = "Small Busway OneWay Decoration Trees"
                };
                locale.AddLocalizedString(k, "A two-lane, one-way busway with decorative trees to remove buses from common traffic and improve public transport coverage. It can also be used by vehicles in emergency.");

                // Road Customizer Tool Advisor
                k = new Locale.Key()
                {
                    m_Identifier = "TUTORIAL_ADVISER_TITLE",
                    m_Key = "RoadCustomizer"
                };
                locale.AddLocalizedString(k, "Road Customizer Tool");

                k = new Locale.Key()
                {
                    m_Identifier = "TUTORIAL_ADVISER",
                    m_Key = "RoadCustomizer"
                };
                locale.AddLocalizedString(k,	"Vehicle and Speed Restrictions:\n\n" +
                                                "1. Hover over roads to display their lanes\n" +
                                                "2. Left-click to toggle selection of lane(s), right-click clears current selection(s)\n" +
                                                "3. With lanes selected, set vehicle and speed restrictions using the menu icons\n\n\n" +
                                                "Lane Changer:\n\n" +
                                                "1. Hover over roads and find an intersection (circle appears), then click to edit it\n" +
                                                "2. Entry points will be shown, click one to select it (right-click goes back to step 1)\n" +
                                                "3. Click the exit routes you wish to allow (right-click goes back to step 2)" +
                                                "\n\nUse PageUp/PageDown to toggle Underground View.");

                sm_localizationInitialized = true;
            }
            catch (ArgumentException e)
            {
                Debug.Log("Traffic++: Unexpected " + e.GetType().Name + " updating localization: " + e.Message + "\n" + e.StackTrace + "\n");
            }

            Debug.Log("Traffic++: Localization successfully updated.");
        }

#if DEBUG
        #region SceneInspectionTools

        IEnumerator Print()
        {
            while (!LoadingManager.instance.m_loadingComplete)
                yield return new WaitForEndOfFrame();

            List<GameObject> sceneObjects = GameObject.FindObjectsOfType<GameObject>().ToList();
            foreach (var item in sceneObjects)
            {
                if (item.transform.parent == null)
                    PrintGameObjects(item, "MapScene_110b.txt");
            }

            List<GameObject> prefabs = Resources.FindObjectsOfTypeAll<GameObject>().Except(sceneObjects).ToList();
            foreach (var item in prefabs)
            {
                if (item.transform.parent == null)
                    PrintGameObjects(item, "MapScenePrefabs_110b.txt");
            }
        }

        public static void PrintGameObjects(GameObject go, string fileName, int depth = 0)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < depth; i++)
            {
                sb.Append("\t");
            }

            sb.Append(go.name);
            sb.Append(" {\n");

            System.IO.File.AppendAllText(fileName, sb.ToString());

            PrintComponents(go, fileName, depth);

            foreach (Transform t in go.transform)
            {
                PrintGameObjects(t.gameObject, fileName, depth + 1);
            }

            sb = new StringBuilder();
            for (int i = 0; i < depth; i++)
            {
                sb.Append("\t");
            }
            sb.Append("}\n\n");
            System.IO.File.AppendAllText(fileName, sb.ToString());
        }

        public static void PrintComponents(GameObject go, string fileName, int depth)
        {
            foreach (var item in go.GetComponents<Component>())
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < depth; i++)
                {
                    sb.Append("\t");
                }
                sb.Append("\t-- ");
                sb.Append(item.GetType().Name);
                sb.Append("\n");

                System.IO.File.AppendAllText(fileName, sb.ToString());
            }
        }

        #endregion
#endif

        void LoadTextureIndex()
        {
            TextureInfo[] textureIndex = FileManager.GetTextureIndex();
            if (textureIndex == null)
                return;

            sm_fileIndex.Clear();
            foreach (TextureInfo item in textureIndex)
                sm_fileIndex.Add(item.name, item);
        }

        public class TextureInfo
        {
            [XmlAttribute]
            public string name;
            
            // normal
            public string mainTex = "";
            public string aprTex = "";
            public string xysTex = "";
            public string aciTex = "";
            public string lodMainTex = "";
            public string lodAprTex = "";
            public string lodXysTex = "";
            public string lodAciTex = "";
            
            // bus
            public string mainTexBus = "";
            public string aprTexBus = "";
            public string xysTexBus = "";
            public string aciTexBus = "";
            public string lodMainTexBus = "";
            public string lodAprTexBus = "";
            public string lodXysTexBus = "";
            public string lodAciTexBus = "";

            // busBoth
            public string mainTexBusBoth = "";
            public string aprTexBusBoth = "";
            public string xysTexBusBoth = "";
            public string aciTexBusBoth = "";
            public string lodMainTexBusBoth = "";
            public string lodAprTexBusBoth = "";
            public string lodXysTexBusBoth = "";
            public string lodAciTexBusBoth = "";

            // node
            public string mainTexNode = "";
            public string aprTexNode = "";
            public string xysTexNode = "";
            public string aciTexNode = "";
            public string lodMainTexNode = "";
            public string lodAprTexNode = "";
            public string lodXysTexNode = "";
            public string lodAciTexNode = "";
        }
    }
}
