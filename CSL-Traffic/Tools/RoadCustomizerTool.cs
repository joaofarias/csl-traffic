using System.Linq;
using ColossalFramework.Math;
using System.Collections;
using UnityEngine;
using ColossalFramework.UI;
using System.Collections.Generic;

namespace CSL_Traffic
{
    class RoadCustomizerTool : ToolBase
    {
        const NetNode.Flags CUSTOMIZED_NODE_FLAG = (NetNode.Flags)(1 << 28);

        class NodeLaneMarker
        {
            public ushort m_node;
            public Vector3 m_position;
            public bool m_isSource;
            public uint m_lane;
            public float m_size = 1f;
            public Color m_color;
            public FastList<NodeLaneMarker> m_connections = new FastList<NodeLaneMarker>();
        }

        class SegmentLaneMarker
        {
            public uint m_lane;
            public int m_laneIndex;
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

        struct Segment
        {
            public ushort m_segmentId;
            public ushort m_targetNode;
        }

        ushort m_hoveredSegment;
        ushort m_hoveredNode;
        ushort m_selectedNode;        
        NodeLaneMarker m_selectedMarker;
        Dictionary<ushort, FastList<NodeLaneMarker>> m_nodeMarkers = new Dictionary<ushort, FastList<NodeLaneMarker>>();
        Dictionary<ushort, Segment> m_segments = new Dictionary<ushort, Segment>();
        Dictionary<int, FastList<SegmentLaneMarker>> m_hoveredLaneMarkers = new Dictionary<int, FastList<SegmentLaneMarker>>();
        List<SegmentLaneMarker> m_selectedLaneMarkers = new List<SegmentLaneMarker>();
        int m_hoveredLanes;
        UIButton m_toolButton;

        protected override void OnToolUpdate()
        {
            base.OnToolUpdate();

            if (m_toolController.IsInsideUI)
                return;

            if (m_selectedNode != 0)
            {
                HandleIntersectionRouting();
                return;
            }

            if (m_hoveredSegment != 0)
            {
                HandleLaneCustomization();
            }

            if (!RayCastSegmentAndNode(out m_hoveredSegment, out m_hoveredNode))
            {
                // clear lanes
                if (Input.GetMouseButtonUp(1))
                    m_selectedLaneMarkers.Clear();
                m_segments.Clear();
                m_hoveredLaneMarkers.Clear();
                return;
            }
                

            if (m_hoveredSegment != 0)
            {
                NetSegment segment = NetManager.instance.m_segments.m_buffer[m_hoveredSegment];
                NetNode startNode = NetManager.instance.m_nodes.m_buffer[segment.m_startNode];
                NetNode endNode = NetManager.instance.m_nodes.m_buffer[segment.m_endNode];
                Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
                    
                if (startNode.CountSegments() > 1)
                {
                    Bounds bounds = startNode.m_bounds;
                    if (m_hoveredNode != 0)
                        bounds.extents /= 2f;
                    if (bounds.IntersectRay(mouseRay))
                    {
                        m_hoveredSegment = 0;
                        m_hoveredNode = segment.m_startNode;
                    }
                }

                if (m_hoveredSegment != 0 && endNode.CountSegments() > 1)
                {
                    Bounds bounds = endNode.m_bounds;
                    if (m_hoveredNode != 0)
                        bounds.extents /= 2f;
                    if (bounds.IntersectRay(mouseRay))
                    {
                        m_hoveredSegment = 0;
                        m_hoveredNode = segment.m_endNode;
                    }
                }

                if (m_hoveredSegment != 0)
                {
                    m_hoveredNode = 0;
                    if (!m_segments.ContainsKey(m_hoveredSegment))
                    {
                        m_segments.Clear();
                        SetSegments(m_hoveredSegment);
                        SetLaneMarkers();
                    }
                }
                else if (Input.GetMouseButtonUp(1))
                {
                    // clear lane selection
                    m_selectedLaneMarkers.Clear();
                }
                        
            }
            else if (m_hoveredNode != 0 && NetManager.instance.m_nodes.m_buffer[m_hoveredNode].CountSegments() < 2)
            {
                m_hoveredNode = 0;
            }

            if (m_hoveredSegment == 0)
            {
                m_segments.Clear();
                m_hoveredLaneMarkers.Clear();
            }

            if (Input.GetMouseButtonUp(0))
            {
                m_selectedNode = m_hoveredNode;
                m_hoveredNode = 0;

                if (m_selectedNode != 0)
                    SetNodeMarkers(m_selectedNode, true);
            }
        }

        void HandleIntersectionRouting()
        {
            FastList<NodeLaneMarker> nodeMarkers;
            if (m_nodeMarkers.TryGetValue(m_selectedNode, out nodeMarkers))
            {
                Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
                NodeLaneMarker hoveredMarker = null;
                Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
                for (int i = 0; i < nodeMarkers.m_size; i++)
                {
                    NodeLaneMarker marker = nodeMarkers.m_buffer[i];

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
            }

            if (Input.GetMouseButtonUp(1))
            {
                if (m_selectedMarker != null)
                    m_selectedMarker = null;
                else
                    m_selectedNode = 0;
            }
        }

        void HandleLaneCustomization()
        {
            // Handle lane settings
            Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            m_hoveredLanes = ushort.MaxValue;
            foreach (FastList<SegmentLaneMarker> laneMarkers in m_hoveredLaneMarkers.Values)
            {
                if (laneMarkers.m_size == 0)
                    continue;

                for (int i = 0; i < laneMarkers.m_size; i++)
                {
                    SegmentLaneMarker marker = laneMarkers.m_buffer[i];
                    if (NetManager.instance.m_lanes.m_buffer[marker.m_lane].m_segment != m_hoveredSegment)
                        continue;

                    if (marker.IntersectRay(mouseRay))
                    {
                        m_hoveredLanes = marker.m_laneIndex;
                        break;
                    }
                }

                if (m_hoveredLanes != ushort.MaxValue)
                    break;
            }

            if (m_hoveredLanes != ushort.MaxValue && Input.GetMouseButtonUp(0))
            {
                SegmentLaneMarker[] hoveredMarkers = m_hoveredLaneMarkers[m_hoveredLanes].ToArray();
                HashSet<uint> hoveredLanes = new HashSet<uint>(hoveredMarkers.Select(m => m.m_lane));
                if (m_selectedLaneMarkers.RemoveAll(m => hoveredLanes.Contains(m.m_lane)) == 0)
                    m_selectedLaneMarkers.AddRange(hoveredMarkers);
            }
            else if (Input.GetMouseButtonUp(1))
            {
                m_selectedLaneMarkers.Clear();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            m_hoveredNode = m_hoveredSegment = 0;
            m_selectedNode = 0;
            m_selectedMarker = null;
            m_selectedLaneMarkers.Clear();
            m_segments.Clear();
            m_hoveredLaneMarkers.Clear();
        }

        bool IsActive(NodeLaneMarker marker)
        {
            if (m_selectedMarker != null && (marker.m_isSource || NetManager.instance.m_lanes.m_buffer[m_selectedMarker.m_lane].m_segment == NetManager.instance.m_lanes.m_buffer[marker.m_lane].m_segment))
                return false;
            else if (m_selectedMarker == null && !marker.m_isSource)
                return false;

            return true;
        }

        public void SetNodeMarkers(ushort nodeId, bool overwrite = false)
        {
            if (nodeId == 0)
                return;

            if (!m_nodeMarkers.ContainsKey(nodeId) || (NetManager.instance.m_nodes.m_buffer[nodeId].m_flags & CUSTOMIZED_NODE_FLAG) != CUSTOMIZED_NODE_FLAG || overwrite)
            {
                FastList<NodeLaneMarker> nodeMarkers = new FastList<NodeLaneMarker>();
                SetNodeMarkers(nodeId, nodeMarkers);
                m_nodeMarkers[nodeId] = nodeMarkers;

                NetManager.instance.m_nodes.m_buffer[nodeId].m_flags |= CUSTOMIZED_NODE_FLAG;
            }
        }

        void SetNodeMarkers(ushort nodeId, FastList<NodeLaneMarker> nodeMarkers)
        {
            NetNode node = NetManager.instance.m_nodes.m_buffer[nodeId];
            int offsetMultiplier = node.CountSegments() <= 2 ? 3 : 1;
            ushort segmentId = node.m_segment0;
            for (int i = 0; i < 8 && segmentId != 0; i++)
            {
                NetSegment segment = NetManager.instance.m_segments.m_buffer[segmentId];
                bool isEndNode = segment.m_endNode == nodeId;
                Vector3 offset = segment.FindDirection(segmentId, nodeId) * offsetMultiplier;
                NetInfo.Lane[] lanes = segment.Info.m_lanes;
                uint laneId = segment.m_lanes;
                for (int j = 0; j < lanes.Length && laneId != 0; j++)
                {
                    if ((lanes[j].m_laneType & NetInfo.LaneType.Vehicle) == NetInfo.LaneType.Vehicle)
                    {
                        Vector3 pos = Vector3.zero;
                        NetInfo.Direction laneDir = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? lanes[j].m_finalDirection : NetInfo.InvertDirection(lanes[j].m_finalDirection);

                        bool isSource = false;
                        if (isEndNode)
                        {
                            if ((laneDir & (NetInfo.Direction.Forward | NetInfo.Direction.Avoid)) == NetInfo.Direction.Forward)
                                isSource = true;
                            pos = NetManager.instance.m_lanes.m_buffer[laneId].m_bezier.d;
                        }
                        else
                        {
                            if ((laneDir & (NetInfo.Direction.Backward | NetInfo.Direction.Avoid)) == NetInfo.Direction.Backward)
                                isSource = true;
                            pos = NetManager.instance.m_lanes.m_buffer[laneId].m_bezier.a;
                        }

                        nodeMarkers.Add(new NodeLaneMarker()
                        {
                            m_lane = laneId,
                            m_node = nodeId,
                            m_position = pos + offset,
                            m_color = colors[nodeMarkers.m_size],
                            m_isSource = isSource,
                        });
                    }

                    laneId = NetManager.instance.m_lanes.m_buffer[laneId].m_nextLane;   
                }

                segmentId = segment.GetRightSegment(nodeId);
                if (segmentId == node.m_segment0)
                    segmentId = 0;
            }

            for (int i = 0; i < nodeMarkers.m_size; i++)
            {
                if (!nodeMarkers.m_buffer[i].m_isSource)
                    continue;

                uint[] connections = RoadManager.GetLaneConnections(nodeMarkers.m_buffer[i].m_lane);
                if (connections == null || connections.Length == 0)
                    continue;

                for (int j = 0; j < nodeMarkers.m_size; j++)
                {
                    if (nodeMarkers.m_buffer[j].m_isSource)
                        continue;

                    if (connections.Contains(nodeMarkers.m_buffer[j].m_lane))
                        nodeMarkers.m_buffer[i].m_connections.Add(nodeMarkers.m_buffer[j]);
                }
            }
        }

        void SetLaneMarkers()
        {
            m_hoveredLaneMarkers.Clear();
            if (m_segments.Count == 0)
                return;

            NetSegment segment = NetManager.instance.m_segments.m_buffer[m_segments.Values.First().m_segmentId];
            NetInfo info = segment.Info;
            int laneCount = info.m_lanes.Length;
            bool bothWays = info.m_hasBackwardVehicleLanes && info.m_hasForwardVehicleLanes;
            bool isInverted = false;

            for (ushort i = 0; i < laneCount; i++)
                m_hoveredLaneMarkers[i] = new FastList<SegmentLaneMarker>();

            foreach (Segment seg in m_segments.Values)
            {
                segment = NetManager.instance.m_segments.m_buffer[seg.m_segmentId];
                uint laneId = segment.m_lanes;

                if (bothWays)
                {
                    isInverted = seg.m_targetNode == segment.m_startNode;
                    if ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.Invert)
                        isInverted = !isInverted;
                }

                for (int j = 0; j < laneCount && laneId != 0; j++)
                {
                    NetLane lane = NetManager.instance.m_lanes.m_buffer[laneId];
                    
                    if ((info.m_lanes[j].m_laneType & NetInfo.LaneType.Vehicle) == NetInfo.LaneType.Vehicle)
                    {
                        Bezier3 bezier = lane.m_bezier;
                        bezier.GetBounds().Expand(1f);

                        int index = j;
                        if (bothWays && isInverted)
                            index += (j % 2 == 0) ? 1 : -1;

                        m_hoveredLaneMarkers[index].Add(new SegmentLaneMarker()
                        {
                            m_bezier = bezier,
                            m_lane = laneId,
                            m_laneIndex = index
                        });
                    }

                    laneId = lane.m_nextLane;
                }
            }
        }

        void SetSegments(ushort segmentId)
        {
            NetSegment segment = NetManager.instance.m_segments.m_buffer[segmentId];
            Segment seg = new Segment()
            {
                m_segmentId = segmentId,
                m_targetNode = segment.m_endNode
            };

            m_segments[segmentId] = seg;

            ushort infoIndex = segment.m_infoIndex;
            NetNode node = NetManager.instance.m_nodes.m_buffer[segment.m_startNode];
            if (node.CountSegments() == 2)
                SetSegments(node.m_segment0 == segmentId ? node.m_segment1 : node.m_segment0, infoIndex, ref seg);
            
            node = NetManager.instance.m_nodes.m_buffer[segment.m_endNode];
            if (node.CountSegments() == 2)
                SetSegments(node.m_segment0 == segmentId ? node.m_segment1 : node.m_segment0, infoIndex, ref seg);
        }

        void SetSegments(ushort segmentId, ushort infoIndex, ref Segment previousSeg)
        {
            NetSegment segment = NetManager.instance.m_segments.m_buffer[segmentId];

            if (segment.m_infoIndex != infoIndex || m_segments.ContainsKey(segmentId))
                return;

            Segment seg = default(Segment);
            seg.m_segmentId = segmentId;

            NetSegment previousSegment = NetManager.instance.m_segments.m_buffer[previousSeg.m_segmentId];
            ushort nextNode;
            if ((segment.m_startNode == previousSegment.m_endNode) || (segment.m_startNode == previousSegment.m_startNode))
            {
                nextNode = segment.m_endNode;
                seg.m_targetNode = segment.m_startNode == previousSeg.m_targetNode ? segment.m_endNode : segment.m_startNode;
            }            
            else
            {
                nextNode = segment.m_startNode;
                seg.m_targetNode = segment.m_endNode == previousSeg.m_targetNode ? segment.m_startNode : segment.m_endNode;
            }    

            m_segments[segmentId] = seg;

            NetNode node = NetManager.instance.m_nodes.m_buffer[nextNode];
            if (node.CountSegments() == 2)
                SetSegments(node.m_segment0 == segmentId ? node.m_segment1 : node.m_segment0, infoIndex, ref seg);
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            base.RenderOverlay(cameraInfo);

            if (m_selectedNode != 0)
            {
                FastList<NodeLaneMarker> nodeMarkers;
                if (m_nodeMarkers.TryGetValue(m_selectedNode, out nodeMarkers))
                {
                    Vector3 nodePos = NetManager.instance.m_nodes.m_buffer[m_selectedNode].m_position;
                    for (int i = 0; i < nodeMarkers.m_size; i++)
                    {
                        NodeLaneMarker laneMarker = nodeMarkers.m_buffer[i];

                        for (int j = 0; j < laneMarker.m_connections.m_size; j++)
                            RenderLane(cameraInfo, laneMarker.m_position, laneMarker.m_connections.m_buffer[j].m_position, nodePos, laneMarker.m_connections.m_buffer[j].m_color);

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
            }
            else
            {
                foreach (KeyValuePair<int, FastList<SegmentLaneMarker>> keyValuePair in m_hoveredLaneMarkers)
                {
                    bool renderBig = false;
                    if (m_hoveredLanes == keyValuePair.Key)
                        renderBig = true;

                    FastList<SegmentLaneMarker> laneMarkers = keyValuePair.Value;
                    for (int i = 0; i < laneMarkers.m_size; i++)
                        RenderManager.instance.OverlayEffect.DrawBezier(cameraInfo, new Color(0f, 0f, 1f, 0.75f), laneMarkers.m_buffer[i].m_bezier, renderBig ? 2f : laneMarkers.m_buffer[i].m_size, 0, 0, 0, 500, false, false);
                }

                foreach (SegmentLaneMarker marker in m_selectedLaneMarkers)
                {
                    RenderManager.instance.OverlayEffect.DrawBezier(cameraInfo, new Color(0f, 1f, 0f, 0.75f), marker.m_bezier, 2f, 0, 0, 0, 500, false, false);
                }
            }

            foreach (ushort node in m_nodeMarkers.Keys)
            {
                if (node == m_selectedNode || (NetManager.instance.m_nodes.m_buffer[node].m_flags & CUSTOMIZED_NODE_FLAG) != CUSTOMIZED_NODE_FLAG)
                    continue;

                FastList<NodeLaneMarker> list = m_nodeMarkers[node];
                Vector3 nodePos = NetManager.instance.m_nodes.m_buffer[node].m_position;
                for (int i = 0; i < list.m_size; i++)
                {
                    NodeLaneMarker laneMarker = list.m_buffer[i];

                    for (int j = 0; j < laneMarker.m_connections.m_size; j++)
                    {
                        if (((NetLane.Flags)NetManager.instance.m_lanes.m_buffer[laneMarker.m_connections.m_buffer[j].m_lane].m_flags & NetLane.Flags.Created) == NetLane.Flags.Created)
                        {
                            Color color = laneMarker.m_connections.m_buffer[j].m_color;
                            color.a = 0.75f;
                            RenderLane(cameraInfo, laneMarker.m_position, laneMarker.m_connections.m_buffer[j].m_position, nodePos, color);
                        }
                            
                    }
                        
                }
            }

            if (m_hoveredNode != 0)
            {
                NetNode node = NetManager.instance.m_nodes.m_buffer[m_hoveredNode];
                RenderManager.instance.OverlayEffect.DrawCircle(cameraInfo, new Color(0f, 0f, 0.5f, 0.75f), node.m_position, 15f, 0, 500, false, true);
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


        int laneButtonsStart, laneButtonsWidth, laneButtonsHeight, laneButtonsSpacing;
        int screenWidth, screenHeight;
        protected override void OnToolGUI()
        {
            base.OnToolGUI();

            if (m_toolButton == null)
                m_toolButton = TryCreateToolButton();

            if (m_selectedLaneMarkers.Count == 0)
                return;

            if (screenWidth != Screen.width || screenHeight != Screen.height)
            {
                screenWidth = Screen.width;
                screenHeight = Screen.height;
                laneButtonsStart = (700 * screenHeight) / 1080;
                laneButtonsWidth = 150; // font doesn't scale, so width must remain the same for every resolution
                laneButtonsHeight = (20 * screenHeight) / 1080; // only causes problems in very low resolutions
                laneButtonsSpacing = (5 * screenHeight) / 1080;
            }

            RoadManager.VehicleType vehicleRestrictions = RoadManager.GetVehicleRestrictions(m_selectedLaneMarkers[0].m_lane);
            bool apply = false;
            int i = 1;
            if (GUI.Button(new Rect(10, laneButtonsStart, laneButtonsWidth, laneButtonsHeight), "Ambulances: " + ((vehicleRestrictions & RoadManager.VehicleType.Ambulance) == RoadManager.VehicleType.Ambulance ? "On" : "Off")))
            {
                vehicleRestrictions ^= RoadManager.VehicleType.Ambulance;
                apply = true;
            }
            if (GUI.Button(new Rect(10, laneButtonsStart + (laneButtonsHeight + laneButtonsSpacing) * i++, laneButtonsWidth, laneButtonsHeight), "Bus: " + ((vehicleRestrictions & RoadManager.VehicleType.Bus) == RoadManager.VehicleType.Bus ? "On" : "Off")))
            {
                vehicleRestrictions ^= RoadManager.VehicleType.Bus;
                apply = true;
            }
            if (GUI.Button(new Rect(10, laneButtonsStart + (laneButtonsHeight + laneButtonsSpacing) * i++, laneButtonsWidth, laneButtonsHeight), "Cargo Trucks: " + ((vehicleRestrictions & RoadManager.VehicleType.CargoTruck) == RoadManager.VehicleType.CargoTruck ? "On" : "Off")))
            {
                vehicleRestrictions ^= RoadManager.VehicleType.CargoTruck;
                apply = true;
            }
            if (GUI.Button(new Rect(10, laneButtonsStart + (laneButtonsHeight + laneButtonsSpacing) * i++, laneButtonsWidth, laneButtonsHeight), "Fire Trucks: " + ((vehicleRestrictions & RoadManager.VehicleType.FireTruck) == RoadManager.VehicleType.FireTruck ? "On" : "Off")))
            {
                vehicleRestrictions ^= RoadManager.VehicleType.FireTruck;
                apply = true;
            }
            if (GUI.Button(new Rect(10, laneButtonsStart + (laneButtonsHeight + laneButtonsSpacing) * i++, laneButtonsWidth, laneButtonsHeight), "Garbage Trucks: " + ((vehicleRestrictions & RoadManager.VehicleType.GarbageTruck) == RoadManager.VehicleType.GarbageTruck ? "On" : "Off")))
            {
                vehicleRestrictions ^= RoadManager.VehicleType.GarbageTruck;
                apply = true;
            }
            if (GUI.Button(new Rect(10, laneButtonsStart + (laneButtonsHeight + laneButtonsSpacing) * i++, laneButtonsWidth, laneButtonsHeight), "Hearses: " + ((vehicleRestrictions & RoadManager.VehicleType.Hearse) == RoadManager.VehicleType.Hearse ? "On" : "Off")))
            {
                vehicleRestrictions ^= RoadManager.VehicleType.Hearse;
                apply = true;
            }
            if (GUI.Button(new Rect(10, laneButtonsStart + (laneButtonsHeight + laneButtonsSpacing) * i++, laneButtonsWidth, laneButtonsHeight), "Citizens: " + ((vehicleRestrictions & RoadManager.VehicleType.PassengerCar) == RoadManager.VehicleType.PassengerCar ? "On" : "Off")))
            {
                vehicleRestrictions ^= RoadManager.VehicleType.PassengerCar;
                apply = true;
            }
            if (GUI.Button(new Rect(10, laneButtonsStart + (laneButtonsHeight + laneButtonsSpacing) * i++, laneButtonsWidth, laneButtonsHeight), "Police: " + ((vehicleRestrictions & RoadManager.VehicleType.PoliceCar) == RoadManager.VehicleType.PoliceCar ? "On" : "Off")))
            {
                vehicleRestrictions ^= RoadManager.VehicleType.PoliceCar;
                apply = true;
            }
            if (GUI.Button(new Rect(10, laneButtonsStart + (laneButtonsHeight + laneButtonsSpacing) * i++, laneButtonsWidth, laneButtonsHeight), "Emergency: " + ((vehicleRestrictions & RoadManager.VehicleType.Emergency) == RoadManager.VehicleType.Emergency ? "On" : "Off")))
            {
                vehicleRestrictions ^= RoadManager.VehicleType.Emergency;
                apply = true;
            }

            if (apply)
            {
                foreach (SegmentLaneMarker lane in m_selectedLaneMarkers)
                {
                    RoadManager.SetVehicleRestrictions(lane.m_lane, vehicleRestrictions);
                }
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
            btn.relativePosition = new Vector3(57, 36);
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
