using System.Linq;
using ColossalFramework.Math;
using System.Collections;
using UnityEngine;
using ColossalFramework.UI;

namespace CSL_Traffic
{
    class RoadCustomizerTool : ToolBase
    {
        class NodeLaneMarker
        {
            public Vector3 m_position;
            public bool m_isStart;
            public uint m_lane;
            public float m_size = 1f;
            public Color m_color;
            public FastList<NodeLaneMarker> m_connections = new FastList<NodeLaneMarker>();
        }

        class SegmentLaneMarker
        {
            public uint m_lane;
            public float m_size = 1f;
            public Bezier3 m_bezier;
            public Bounds[] m_bounds;

            public bool IntersectRay(Ray ray)
            {
                if (m_bounds == null)
                    CalculateBounds();

                foreach (Bounds bounds in m_bounds)
                {
                    if (bounds.IntersectRay(ray))
                        return true;
                }

                return false;
            }

            void CalculateBounds()
            {
                float angle = Vector3.Angle(m_bezier.a, m_bezier.b);
                if (Mathf.Approximately(angle, 0f) || Mathf.Approximately(angle, 180f))
                {
                    angle = Vector3.Angle(m_bezier.b, m_bezier.c);
                    if (Mathf.Approximately(angle, 0f) || Mathf.Approximately(angle, 180f))
                    {
                        angle = Vector3.Angle(m_bezier.c, m_bezier.d);
                        if (Mathf.Approximately(angle, 0f) || Mathf.Approximately(angle, 180f))
                        {
                            // linear bezier
                            Bounds bounds = m_bezier.GetBounds();
                            bounds.Expand(1f);
                            m_bounds = new Bounds[] { bounds };
                            return;
                        }
                    }
                }                
                
                // split bezier in 10 parts to correctly raycast curves
                Bezier3 bezier;
                int amount = 10;
                m_bounds = new Bounds[amount];
                float size = 1f / amount;
                for (int i = 0; i < amount; i++)
                {
                    bezier = m_bezier.Cut(i * size, (i+1) * size);
                    
                    Bounds bounds = bezier.GetBounds();
                    bounds.Expand(1f);
                    m_bounds[i] = bounds;
                }

                
                
            }
        }

        ushort m_currentSegment;
        ushort m_currentNode;
        ushort m_selectedSegment;
        ushort m_selectedNode;
        NodeLaneMarker m_selectedMarker;
        SegmentLaneMarker m_selectedLane;
        FastList<NodeLaneMarker> m_nodeLaneMarkers = new FastList<NodeLaneMarker>();
        FastList<SegmentLaneMarker> m_segmentLaneMarkers = new FastList<SegmentLaneMarker>();
        UIButton m_toolButton;

        protected override void OnToolUpdate()
        {
            base.OnToolUpdate();

            if (m_toolController.IsInsideUI)
                return;

            if (m_selectedNode != 0)
            {
                // Handle lane connections

                Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
                NodeLaneMarker hoveredMarker = null;
                Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
                for (int i = 0; i < m_nodeLaneMarkers.m_size; i++)
                {
                    NodeLaneMarker marker = m_nodeLaneMarkers.m_buffer[i];

                    if (!IsActive(marker))
                        continue;

                    bounds.center = marker.m_position;
                    if (bounds.IntersectRay(mouseRay))
                    {
                        hoveredMarker = marker;
                        marker.m_size = 2f;
                    }
                    else
                        marker.m_size = 1f;
                }

                if (hoveredMarker != null && Input.GetMouseButtonUp(0))
                {
                    if (m_selectedMarker == null)
                    {
                        m_selectedMarker = hoveredMarker;
                    }
                    else if (RoadManager.RemoveLaneConnection(m_selectedMarker.m_lane, hoveredMarker.m_lane))
                    {
                        m_selectedMarker.m_connections.Remove(hoveredMarker);
                    }
                    else if (RoadManager.AddLaneConnection(m_selectedMarker.m_lane, hoveredMarker.m_lane))
                    {
                        m_selectedMarker.m_connections.Add(hoveredMarker);
                    }
                }

                if (Input.GetMouseButtonUp(1))
                {
                    if (m_selectedMarker != null)
                        m_selectedMarker = null;
                    else
                        m_selectedNode = 0;
                }
            }
            else if (m_selectedSegment != 0)
            {
                // Handle lane settings
                Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
                SegmentLaneMarker hoveredMarker = null;
                for (int i = 0; i < m_segmentLaneMarkers.m_size; i++)
                {
                    SegmentLaneMarker marker = m_segmentLaneMarkers.m_buffer[i];
                    
                    if (marker.IntersectRay(mouseRay))
                    {
                        hoveredMarker = marker;
                        marker.m_size = 2f;
                    }
                    else
                        marker.m_size = 1f;
                }

                if (hoveredMarker != null && Input.GetMouseButtonUp(0))
                {
                    m_selectedLane = hoveredMarker;
                }
                
                if (Input.GetMouseButtonUp(1))
                {
                    m_selectedSegment = 0;
                    m_selectedLane = null;
                }
            }
            else
            {
                if (!RayCastSegmentAndNode(out m_currentSegment, out m_currentNode))
                    return;

                if (m_currentNode != 0 && NetManager.instance.m_nodes.m_buffer[m_currentNode].CountSegments() <= 2)
                    m_currentNode = 0;

                if (Input.GetMouseButtonUp(0) && !m_toolController.IsInsideUI)
                {
                    m_selectedNode = m_currentNode;
                    m_currentNode = 0;
                    if (m_selectedNode == 0)
                        m_selectedSegment = m_currentSegment;
                    m_currentSegment = 0;

                    if (m_selectedNode != 0)
                        SetNodeLaneMarkers();
                    else if (m_selectedSegment != 0)
                        SetSegmentLaneMarkers();
                }
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            m_currentNode = m_currentSegment = 0;
            m_selectedNode = m_selectedSegment = 0;
            m_selectedMarker = null;
        }

        bool IsActive(NodeLaneMarker marker)
        {
            if (m_selectedMarker != null && (marker.m_isStart || NetManager.instance.m_lanes.m_buffer[m_selectedMarker.m_lane].m_segment == NetManager.instance.m_lanes.m_buffer[marker.m_lane].m_segment))
                return false;
            else if (m_selectedMarker == null && !marker.m_isStart)
                return false;

            return true;
        }

        void SetNodeLaneMarkers()
        {
            m_nodeLaneMarkers.Clear();
            NetNode node = NetManager.instance.m_nodes.m_buffer[m_selectedNode];
            int colorIndex = 0;
            ushort seg = node.m_segment0;
            for (int i = 0; i < 8; i++)
            {
                if (seg == 0)
                    break;

                NetSegment segment = NetManager.instance.m_segments.m_buffer[seg];
                Vector3 offset = segment.FindDirection(seg, m_selectedNode);
                uint laneId = segment.m_lanes;
                NetInfo info = segment.Info;
                int laneCount = info.m_lanes.Length;
                for (int j = 0; j < laneCount && laneId != 0; j++)
                {
                    NetLane lane = NetManager.instance.m_lanes.m_buffer[laneId];
                    if ((info.m_lanes[j].m_laneType & NetInfo.LaneType.Vehicle) == NetInfo.LaneType.Vehicle)
                    {
                        Vector3 pos = Vector3.zero;
                        NetInfo.Direction laneDir = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? info.m_lanes[j].m_finalDirection : NetInfo.InvertDirection(info.m_lanes[j].m_finalDirection);
                        
                        bool isStart = false;
                        if (segment.m_endNode == m_selectedNode)
                        {
                            if ((laneDir & (NetInfo.Direction.Forward | NetInfo.Direction.Avoid)) == NetInfo.Direction.Forward)
                                isStart = true;
                            pos = lane.m_bezier.d;
                        }
                        else if (segment.m_startNode == m_selectedNode)
                        {
                            if ((laneDir & (NetInfo.Direction.Backward | NetInfo.Direction.Avoid)) == NetInfo.Direction.Backward)
                                isStart = true;
                            pos = lane.m_bezier.a;
                        }

                        m_nodeLaneMarkers.Add(new NodeLaneMarker()
                        {
                            m_position = pos + offset,
                            m_lane = laneId,
                            m_color = colors[colorIndex++],
                            m_isStart = isStart
                        });
                    }
                    
                    laneId = lane.m_nextLane;
                }

                seg = segment.GetRightSegment(m_selectedNode);
                if (seg == node.m_segment0)
                    break;
            }

            for (int i = 0; i < m_nodeLaneMarkers.m_size; i++)
            {
                if (!m_nodeLaneMarkers.m_buffer[i].m_isStart)
                    continue;

                uint[] connections = RoadManager.GetLaneConnections(m_nodeLaneMarkers.m_buffer[i].m_lane);
                if (connections == null || connections.Length == 0)
                    continue;

                for (int j = 0; j < m_nodeLaneMarkers.m_size; j++)
                {
                    if (m_nodeLaneMarkers.m_buffer[j].m_isStart)
                        continue;

                    if (connections.Contains(m_nodeLaneMarkers.m_buffer[j].m_lane))
                        m_nodeLaneMarkers.m_buffer[i].m_connections.Add(m_nodeLaneMarkers.m_buffer[j]);
                }
            }
        }

        void SetSegmentLaneMarkers()
        {
            m_segmentLaneMarkers.Clear();
            NetSegment segment = NetManager.instance.m_segments.m_buffer[m_selectedSegment];
            uint laneId = segment.m_lanes;
            NetInfo info = segment.Info;
            int laneCount = info.m_lanes.Length;
            for (int j = 0; j < laneCount && laneId != 0; j++)
            {
                NetLane lane = NetManager.instance.m_lanes.m_buffer[laneId];
                if ((info.m_lanes[j].m_laneType & NetInfo.LaneType.Vehicle) == NetInfo.LaneType.Vehicle)
                {
                    Bezier3 bezier = lane.m_bezier;
                    bezier.GetBounds().Expand(1f);

                    m_segmentLaneMarkers.Add(new SegmentLaneMarker()
                    {
                        m_bezier = bezier,
                        m_lane = laneId,
                    });
                }

                laneId = lane.m_nextLane;
            }

        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            base.RenderOverlay(cameraInfo);

            if (m_currentNode != 0)
            {
                NetNode node = NetManager.instance.m_nodes.m_buffer[m_currentNode];
                RenderManager.instance.OverlayEffect.DrawCircle(cameraInfo, new Color(0f, 0f, 0.5f, 0.75f), node.m_position, 15f, 0, 500, false, true);
            }
            else if (m_currentSegment != 0)
            {
                NetTool.RenderOverlay(RenderManager.instance.CurrentCameraInfo, ref NetManager.instance.m_segments.m_buffer[m_currentSegment], new Color(0f, 0.5f, 0f, 0.75f), new Color(0f, 0.5f, 0f, 0.75f));
            }
            else if (m_selectedNode != 0)
            {
                Vector3 nodePos = NetManager.instance.m_nodes.m_buffer[m_selectedNode].m_position;
                for (int i = 0; i < m_nodeLaneMarkers.m_size; i++)
                {
                    NodeLaneMarker laneMarker = m_nodeLaneMarkers.m_buffer[i];               

                    for (int j = 0; j < laneMarker.m_connections.m_size; j++)
                        RenderLane(cameraInfo, laneMarker.m_position, laneMarker.m_connections.m_buffer[j].m_position, nodePos, laneMarker.m_color);

                    if (m_selectedMarker != laneMarker && !IsActive(laneMarker))
                        continue;

                    if (m_selectedMarker == laneMarker)
                    {
                        RaycastOutput output;
                        if (RayCastSegmentAndNode(out output))
                        {
                            RenderLane(cameraInfo, m_selectedMarker.m_position, output.m_hitPos, nodePos, m_selectedMarker.m_color);
                            m_selectedMarker.m_size = 2f;
                        }
                    }

                    RenderManager.instance.OverlayEffect.DrawCircle(cameraInfo, laneMarker.m_color, laneMarker.m_position, laneMarker.m_size, 0, 500, false, true);
                }
            }
            else if (m_selectedSegment != 0)
            {
                for (int i = 0; i < m_segmentLaneMarkers.m_size; i++)
                {
                    SegmentLaneMarker marker = m_segmentLaneMarkers.m_buffer[i];
                    
                    bool selected = false;
                    if (marker == m_selectedLane)
                        selected = true;

                    RenderManager.instance.OverlayEffect.DrawBezier(cameraInfo, selected ? new Color(0f, 1f, 0f, 0.75f) : new Color(0f, 0f, 1f, 0.75f), marker.m_bezier, selected ? 2f : marker.m_size, 0, 0, 0, 500, false, true);
                }
            }
        }

        void RenderLane(RenderManager.CameraInfo cameraInfo, Vector3 start, Vector3 end, Color color, float size = 0.1f)
        {
            Vector3 middlePoint = (start + end) / 2f;
            RenderLane(cameraInfo, start, end, middlePoint, color, size);
        }

        void RenderLane(RenderManager.CameraInfo cameraInfo, Vector3 start, Vector3 end, Vector3 middlePoint, Color color, float size = 0.1f)
        {
            Bezier3 bezier;
            bezier.a = start;
            bezier.d = end;
            NetSegment.CalculateMiddlePoints(bezier.a, (middlePoint - bezier.a).normalized, bezier.d, (middlePoint - bezier.d).normalized, false, false, out bezier.b, out bezier.c);
            
            RenderManager.instance.OverlayEffect.DrawBezier(cameraInfo, color, bezier, size, 0, 0, 0, 500, false, true);
        }

        bool RayCastSegmentAndNode(out RaycastOutput output)
        {
            RaycastInput input = new RaycastInput(Camera.main.ScreenPointToRay(Input.mousePosition), Camera.main.farClipPlane);
            input.m_netService.m_service = ItemClass.Service.Road;
            input.m_ignoreSegmentFlags = NetSegment.Flags.None;
            input.m_ignoreNodeFlags = NetNode.Flags.None;
            input.m_ignoreTerrain = true;

            return RayCast(input, out output);
        }

        bool RayCastSegmentAndNode(out ushort netSegment, out ushort netNode)
        {
            RaycastOutput output;
            if (RayCastSegmentAndNode(out output))
            {
                netSegment = output.m_netSegment;
                netNode = output.m_netNode;
                return true;
            }

            netSegment = 0;
            netNode = 0;
            return false;
        }

        #region UI

        //public static bool InitializeUI()
        //{
        //    GameObject container = GameObject.Find("TSContainer");
        //    if (container == null)
        //        return false;

        //    GameObject roadsPanel = container.transform.GetChild(0).gameObject;
        //    if (roadsPanel == null)
        //        return false;

        //    GameObject roadCustomizerPanel = Instantiate<GameObject>(roadsPanel);
        //    roadCustomizerPanel.transform.SetParent(container.transform);
            
        //    Destroy(roadCustomizerPanel.GetComponentInChildren<RoadsGroupPanel>());
        //    foreach (var item in roadCustomizerPanel.GetComponentsInChildren<RoadsPanel>())
        //        Destroy(item);

            

        //    UIButton toolButton = CreateToolButton();
        //    toolButton.eventClick += delegate(UIComponent component, UIMouseEventParameter eventParam)
        //    {
        //        ToolsModifierControl.SetTool<RoadCustomizerTool>();
        //        roadCustomizerPanel.SetActive(true);
        //        roadCustomizerPanel.GetComponent<UIPanel>().isVisible = true;
        //        roadCustomizerPanel.GetComponent<UIPanel>().BringToFront();
        //    };
            
        //    return true;
        //}

        //public static void AddClone<T>(GameObject go, T originalComp) where T : Component
        //{
        //    T newComp = go.AddComponent<T>();

        //    foreach (FieldInfo fi in typeof(T).GetAllFieldsFromType())
        //    {
        //        try { fi.SetValue(newComp, fi.GetValue(originalComp)); } catch {}
        //    }
        //}

        //public static bool InitializeUI()
        //{
        //    GameObject container = GameObject.Find("TSContainer");
        //    if (container == null)
        //        return false;

        //    GameObject roadsPanel = container.transform.FindChild("RoadsPanel").gameObject;

        //    GameObject roadCustomizerPanel = new GameObject("RoadCustomizerPanel");
        //    roadCustomizerPanel.transform.SetParent(container.transform);
        //    AddClone<UIPanel>(roadCustomizerPanel, roadsPanel.GetComponent<UIPanel>());

        //    UIButton toolButton = TryCreateToolButton();
        //    toolButton.eventClick += delegate(UIComponent component, UIMouseEventParameter eventParam)
        //    {
        //        ToolsModifierControl.SetTool<RoadCustomizerTool>();
        //        roadCustomizerPanel.SetActive(true);
        //        roadCustomizerPanel.GetComponent<UIPanel>().isVisible = true;
        //        roadCustomizerPanel.GetComponent<UIPanel>().BringToFront();
        //    };

        //    return true;
        //}

        //public static bool InitializeUI(bool s)
        //{
        //    GameObject container = GameObject.Find("TSContainer");
        //    if (container == null)
        //        return false;

        //    UIPanel originalPanel = GameObject.FindObjectsOfType<UIPanel>().FirstOrDefault(p => p.name == "ScrollableSubPanelTemplate");
        //    if (originalPanel == null)
        //    {
        //        Debug.Log("Fuck!");
        //        return false;
        //    }
        //    GameObject panel =  Instantiate<GameObject>(originalPanel.gameObject);
        //    if (panel == null)
        //        return false;

        //    UIPanel originalScrollablePanel = GameObject.FindObjectsOfType<UIPanel>().FirstOrDefault(p => p.name == "ScrollablePanelTemplate");
        //    if (originalScrollablePanel == null)
        //    {
        //        Debug.Log("Fuck Again!");
        //        return false;
        //    }
        //    GameObject scrollablePanel = Instantiate<GameObject>(originalScrollablePanel.gameObject);
        //    if (scrollablePanel == null)
        //    {
        //        //UITemplateManager.RemoveInstance("ScrollableSubPanelTemplate", panel.GetComponent<UIComponent>());
        //        return false;
        //    }

        //    // add RoadCustomizerGroupPanel to panel
        //    panel.transform.SetParent(container.transform);
        //    panel.AddComponent<RoadCustomizerGroupPanel>();

        //    // add RoadCustomizerPanel to scrollablePanel
        //    scrollablePanel.transform.SetParent(panel.transform.GetChild(0));
        //    RoadCustomizerPanel roadCustomizerPanel = scrollablePanel.AddComponent<RoadCustomizerPanel>();
            

        //    UIButton toolButton = CreateToolButton();
        //    toolButton.eventClick += delegate(UIComponent component, UIMouseEventParameter eventParam)
        //    {
        //        ToolsModifierControl.SetTool<RoadCustomizerTool>();
        //        panel.SetActive(true);
        //        panel.GetComponent<UIPanel>().isVisible = true;
        //        panel.GetComponent<UIPanel>().BringToFront();
        //    };

        //    return true;
        //}

        protected override void OnToolGUI()
        {
            base.OnToolGUI();

            if (m_toolButton == null)
                m_toolButton = TryCreateToolButton();

            if (m_selectedSegment == 0 || m_selectedLane == null)
                return;

            RoadManager.VehicleType vehicleRestrictions = RoadManager.GetVehicleRestrictions(m_selectedLane.m_lane);

            if (GUI.Button(new Rect(10, 700, 150, 20), "Ambulances: " + ((vehicleRestrictions & RoadManager.VehicleType.Ambulance) == RoadManager.VehicleType.Ambulance ? "On" : "Off")))
            {
                RoadManager.ToggleVehicleRestriction(m_selectedLane.m_lane, RoadManager.VehicleType.Ambulance);
            }
            if (GUI.Button(new Rect(10, 725, 150, 20), "Bus: " + ((vehicleRestrictions & RoadManager.VehicleType.Bus) == RoadManager.VehicleType.Bus ? "On" : "Off")))
            {
                RoadManager.ToggleVehicleRestriction(m_selectedLane.m_lane, RoadManager.VehicleType.Bus);
            }
            if (GUI.Button(new Rect(10, 750, 150, 20), "Cargo Trucks: " + ((vehicleRestrictions & RoadManager.VehicleType.CargoTruck) == RoadManager.VehicleType.CargoTruck ? "On" : "Off")))
            {
                RoadManager.ToggleVehicleRestriction(m_selectedLane.m_lane, RoadManager.VehicleType.CargoTruck);
            }
            if (GUI.Button(new Rect(10, 775, 150, 20), "Fire Trucks: " + ((vehicleRestrictions & RoadManager.VehicleType.FireTruck) == RoadManager.VehicleType.FireTruck ? "On" : "Off")))
            {
                RoadManager.ToggleVehicleRestriction(m_selectedLane.m_lane, RoadManager.VehicleType.FireTruck);
            }
            if (GUI.Button(new Rect(10, 800, 150, 20), "Garbage Trucks: " + ((vehicleRestrictions & RoadManager.VehicleType.GarbageTruck) == RoadManager.VehicleType.GarbageTruck ? "On" : "Off")))
            {
                RoadManager.ToggleVehicleRestriction(m_selectedLane.m_lane, RoadManager.VehicleType.GarbageTruck);
            }
            if (GUI.Button(new Rect(10, 825, 150, 20), "Hearses: " + ((vehicleRestrictions & RoadManager.VehicleType.Hearse) == RoadManager.VehicleType.Hearse ? "On" : "Off")))
            {
                RoadManager.ToggleVehicleRestriction(m_selectedLane.m_lane, RoadManager.VehicleType.Hearse);
            }
            if (GUI.Button(new Rect(10, 850, 150, 20), "Citizens: " + ((vehicleRestrictions & RoadManager.VehicleType.PassengerCar) == RoadManager.VehicleType.PassengerCar ? "On" : "Off")))
            {
                RoadManager.ToggleVehicleRestriction(m_selectedLane.m_lane, RoadManager.VehicleType.PassengerCar);
            }
            if (GUI.Button(new Rect(10, 875, 150, 20), "Police: " + ((vehicleRestrictions & RoadManager.VehicleType.PoliceCar) == RoadManager.VehicleType.PoliceCar ? "On" : "Off")))
            {
                RoadManager.ToggleVehicleRestriction(m_selectedLane.m_lane, RoadManager.VehicleType.PoliceCar);
            }
            if (GUI.Button(new Rect(10, 900, 150, 20), "Emergency: " + ((vehicleRestrictions & RoadManager.VehicleType.Emergency) == RoadManager.VehicleType.Emergency ? "On" : "Off")))
            {
                RoadManager.ToggleVehicleRestriction(m_selectedLane.m_lane, RoadManager.VehicleType.Emergency);
            }
        }

        protected override void Awake()
        {
            base.Awake();
            StartCoroutine(CreateToolButton());
        }

        IEnumerator CreateToolButton()
        {
            while (m_toolButton == null)
            {
                m_toolButton = TryCreateToolButton();
                yield return new WaitForEndOfFrame();
            }
        }

        public static UIButton TryCreateToolButton()
        {
            GameObject roadsOptionPanel = GameObject.Find("RoadsOptionPanel(RoadsPanel)");
            if (roadsOptionPanel == null)
                return null;

            UITabstrip tabstrip = roadsOptionPanel.GetComponentInChildren<UITabstrip>();
            if (tabstrip == null)
                return null;

            UIButton updateModeButton = (UIButton)tabstrip.tabs.Last();

            UIButton btn = roadsOptionPanel.GetComponent<UIComponent>().AddUIComponent<UIButton>();
            btn.name = "RoadCustomizer";
            btn.text = "RC";
            btn.tooltip = "Road Customizer Tool";
            btn.size = new Vector2(36, 36);
            btn.atlas = updateModeButton.atlas;
            btn.playAudioEvents = true;
            btn.relativePosition = new Vector3(93, 36);
            btn.disabledBgSprite = updateModeButton.disabledBgSprite;
            btn.focusedBgSprite = updateModeButton.focusedBgSprite;
            btn.hoveredBgSprite = updateModeButton.hoveredBgSprite;
            btn.normalBgSprite = updateModeButton.normalBgSprite;
            btn.pressedBgSprite = updateModeButton.pressedBgSprite;
            btn.disabledFgSprite = updateModeButton.disabledFgSprite;
            btn.focusedFgSprite = updateModeButton.focusedFgSprite;
            btn.hoveredFgSprite = updateModeButton.hoveredFgSprite;
            btn.normalFgSprite = updateModeButton.normalFgSprite;
            btn.pressedFgSprite = updateModeButton.pressedFgSprite;
            btn.group = updateModeButton.group;
            
            btn.eventClick += delegate(UIComponent component, UIMouseEventParameter eventParam)
            {
                ToolsModifierControl.SetTool<RoadCustomizerTool>();
            };

            btn.eventButtonStateChanged += delegate(UIComponent component, UIButton.ButtonState value)
            {
                if (value == UIButton.ButtonState.Focused)
                {
                    foreach (var item in btn.group.components)
                    {
                        UIButton b = item as UIButton;
                        if (b != null)
                            b.state = UIButton.ButtonState.Normal;
                    }                    
                }
                else if (value == UIButton.ButtonState.Normal)
                {
                    if (ToolsModifierControl.GetCurrentTool<RoadCustomizerTool>() != null)
                        ToolsModifierControl.SetTool<NetTool>();
                }
            };

            return btn;
        }

        #endregion

        static readonly Color32[] colors = new Color32[]
        {
            new Color32(161, 64, 206, 255), 
            new Color32(79, 251, 8, 255), 
            new Color32(243, 96, 44, 255), 
            new Color32(45, 106, 105, 255), 
            new Color32(253, 165, 187, 255), 
            new Color32(90, 131, 14, 255), 
            new Color32(58, 20, 70, 255), 
            new Color32(248, 246, 183, 255), 
            new Color32(255, 205, 29, 255), 
            new Color32(91, 50, 18, 255), 
            new Color32(76, 239, 155, 255), 
            new Color32(241, 25, 130, 255), 
            new Color32(125, 197, 240, 255), 
            new Color32(57, 102, 187, 255), 
            new Color32(160, 27, 61, 255), 
            new Color32(167, 251, 107, 255), 
            new Color32(165, 94, 3, 255), 
            new Color32(204, 18, 161, 255), 
            new Color32(208, 136, 237, 255), 
            new Color32(232, 211, 202, 255), 
            new Color32(45, 182, 15, 255), 
            new Color32(8, 40, 47, 255), 
            new Color32(249, 172, 142, 255), 
            new Color32(248, 99, 101, 255), 
            new Color32(180, 250, 208, 255), 
            new Color32(126, 25, 77, 255), 
            new Color32(243, 170, 55, 255), 
            new Color32(47, 69, 126, 255), 
            new Color32(50, 105, 70, 255), 
            new Color32(156, 49, 1, 255), 
            new Color32(233, 231, 255, 255), 
            new Color32(107, 146, 253, 255), 
            new Color32(127, 35, 26, 255), 
            new Color32(240, 94, 222, 255), 
            new Color32(58, 28, 24, 255), 
            new Color32(165, 179, 240, 255), 
            new Color32(239, 93, 145, 255), 
            new Color32(47, 110, 138, 255), 
            new Color32(57, 195, 101, 255), 
            new Color32(124, 88, 213, 255), 
            new Color32(252, 220, 144, 255), 
            new Color32(48, 106, 224, 255), 
            new Color32(90, 109, 28, 255), 
            new Color32(56, 179, 208, 255), 
            new Color32(239, 73, 177, 255), 
            new Color32(84, 60, 2, 255), 
            new Color32(169, 104, 238, 255), 
            new Color32(97, 201, 238, 255), 
        };
    }
}
