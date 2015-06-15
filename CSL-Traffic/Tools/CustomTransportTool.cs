﻿using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.Math;
using CSL_Traffic.Extensions;
using System;
using System.Reflection;
using UnityEngine;

namespace CSL_Traffic
{
    class CustomTransportTool : TransportTool
    {
        private enum Mode
        {
            NewLine,
            AddStops,
            MoveStops
        }

        private FieldInfo fi_mode;
        private FieldInfo fi_line;
        private FieldInfo fi_mouseRay;
        private FieldInfo fi_mouseRayLength;
        private FieldInfo fi_hitPosition;
        private FieldInfo fi_fixedPlatform;
        private FieldInfo fi_tempLine;
        private FieldInfo fi_lastEditLine;
        private FieldInfo fi_lastMoveIndex;
        private FieldInfo fi_lastAddIndex;
        private FieldInfo fi_lastMovePos;
        private FieldInfo fi_lastAddPos;
        private FieldInfo fi_hoverStopIndex;
        private FieldInfo fi_hoverSegmentIndex;
        private FieldInfo fi_errors;
        private Mode m_mode
        {
            get
            {
                return (Mode)((int)fi_mode.GetValue(this));
            }
            set
            {
                fi_mode.SetValue(this, (int)value);
            }
        }
        private ushort m_line
        {
            get
            {
                return (ushort)fi_line.GetValue(this);
            }
            set
            {
                fi_line.SetValue(this, value);
            }
        }
        private Ray m_mouseRay
        {
            get
            {
                return (Ray)fi_mouseRay.GetValue(this);
            }
            set
            {
                fi_mouseRay.SetValue(this, value);
            }
        }
        private float m_mouseRayLength
        {
            get
            {
                return (float)fi_mouseRayLength.GetValue(this);
            }
            set
            {
                fi_mouseRayLength.SetValue(this, value);
            }
        }
        private Vector3 m_hitPosition
        {
            get
            {
                return (Vector3)fi_hitPosition.GetValue(this);
            }
            set
            {
                fi_hitPosition.SetValue(this, value);
            }
        }
        private bool m_fixedPlatform
        {
            get
            {
                return (bool)fi_fixedPlatform.GetValue(this);
            }
            set
            {
                fi_fixedPlatform.SetValue(this, value);
            }
        }
        private ushort m_tempLine
        {
            get
            {
                return (ushort)fi_tempLine.GetValue(this);
            }
            set
            {
                fi_tempLine.SetValue(this, value);
            }
        }
        private ushort m_lastEditLine
        {
            get
            {
                return (ushort)fi_lastEditLine.GetValue(this);
            }
            set
            {
                fi_lastEditLine.SetValue(this, value);
            }
        }
        private int m_lastMoveIndex
        {
            get
            {
                return (int)fi_lastMoveIndex.GetValue(this);
            }
            set
            {
                fi_lastMoveIndex.SetValue(this, value);
            }
        }
        private int m_lastAddIndex
        {
            get
            {
                return (int)fi_lastAddIndex.GetValue(this);
            }
            set
            {
                fi_lastAddIndex.SetValue(this, value);
            }
        }
        private Vector3 m_lastMovePos
        {
            get
            {
                return (Vector3)fi_lastMovePos.GetValue(this);
            }
            set
            {
                fi_lastMovePos.SetValue(this, value);
            }
        }
        private Vector3 m_lastAddPos
        {
            get
            {
                return (Vector3)fi_lastAddPos.GetValue(this);
            }
            set
            {
                fi_lastAddPos.SetValue(this, value);
            }
        }
        private int m_hoverStopIndex
        {
            get
            {
                return (int)fi_hoverStopIndex.GetValue(this);
            }
            set
            {
                fi_hoverStopIndex.SetValue(this, value);
            }
        }
        private int m_hoverSegmentIndex
        {
            get
            {
                return (int)fi_hoverSegmentIndex.GetValue(this);
            }
            set
            {
                fi_hoverSegmentIndex.SetValue(this, value);
            }
        }
        private ToolErrors m_errors
        {
            get
            {
                return (ToolErrors)fi_errors.GetValue(this);
            }
            set
            {
                fi_errors.SetValue(this, value);
            }
        }

        protected override void Awake()
        {
            base.Awake();

            Type transportToolType = typeof(TransportTool);
            fi_mode = transportToolType.GetFieldByName("m_mode");
            fi_line = transportToolType.GetFieldByName("m_line");
            fi_mouseRay = transportToolType.GetFieldByName("m_mouseRay");
            fi_mouseRayLength = transportToolType.GetFieldByName("m_mouseRayLength");
            fi_hitPosition = transportToolType.GetFieldByName("m_hitPosition");
            fi_fixedPlatform = transportToolType.GetFieldByName("m_fixedPlatform");
            fi_tempLine = transportToolType.GetFieldByName("m_tempLine");
            fi_lastEditLine = transportToolType.GetFieldByName("m_lastEditLine");
            fi_lastMoveIndex = transportToolType.GetFieldByName("m_lastMoveIndex");
            fi_lastAddIndex = transportToolType.GetFieldByName("m_lastAddIndex");
            fi_lastMovePos = transportToolType.GetFieldByName("m_lastMovePos");
            fi_lastAddPos = transportToolType.GetFieldByName("m_lastAddPos");
            fi_hoverStopIndex = transportToolType.GetFieldByName("m_hoverStopIndex");
            fi_hoverSegmentIndex = transportToolType.GetFieldByName("m_hoverSegmentIndex");
            fi_errors = transportToolType.GetFieldByName("m_errors");
        }

        public override void SimulationStep()
        {
            TransportInfo prefab = m_prefab;
            if (prefab == null)
            {
                return;
            }
            ToolErrors toolErrors = ToolErrors.None;
            switch (m_mode)
            {
                case Mode.NewLine:
                    {
                        Vector3 vector;
                        ushort num;
                        int hoverStopIndex;
                        int hoverSegmentIndex;
                        if ((Singleton<TransportManager>.instance as CustomTransportManager).RayCast(m_mouseRay, m_mouseRayLength, out vector, out num, out hoverStopIndex, out hoverSegmentIndex))
                        {
                            TransportInfo info = Singleton<TransportManager>.instance.m_lines.m_buffer[(int)num].Info;
                            bool flag = info == prefab;
                            if (flag)
                            {
                                flag = EnsureTempLine(prefab, num, -2, -2, vector, false);
                            }
                            if (flag)
                            {
                                m_hitPosition = vector;
                                m_fixedPlatform = false;
                                m_hoverStopIndex = hoverStopIndex;
                                m_hoverSegmentIndex = hoverSegmentIndex;
                                if (m_hoverSegmentIndex != -1 && !Singleton<NetManager>.instance.CheckLimits())
                                {
                                    toolErrors |= ToolErrors.TooManyObjects;
                                }
                            }
                            else
                            {
                                EnsureTempLine(prefab, 0, -2, -2, Vector3.zero, false);
                                m_hoverStopIndex = -1;
                                m_hoverSegmentIndex = -1;
                                toolErrors |= ToolErrors.RaycastFailed;
                            }
                        }
                        else
                        {
                            RaycastOutput raycastOutput;
                            bool flag2 = RayCast(new RaycastInput(m_mouseRay, m_mouseRayLength)
                            {
                                m_buildingService = new RaycastService(prefab.m_stationService, prefab.m_stationSubService, prefab.m_stationLayer),
                                m_netService = new RaycastService(prefab.m_netService, prefab.m_netSubService, prefab.m_netLayer),
                                m_ignoreTerrain = true,
                                m_ignoreSegmentFlags = (prefab.m_netService == ItemClass.Service.None) ? NetSegment.Flags.All : NetSegment.Flags.None,
                                m_ignoreBuildingFlags = (prefab.m_stationService == ItemClass.Service.None) ? Building.Flags.All : Building.Flags.None
                            }, out raycastOutput);
                            bool fixedPlatform = false;
                            if (flag2)
                            {
                                flag2 = GetStopPosition(prefab, raycastOutput.m_netSegment, raycastOutput.m_building, 0, ref raycastOutput.m_hitPos, out fixedPlatform);
                            }
                            if (flag2)
                            {
                                flag2 = CanAddStop(prefab, 0, -1, raycastOutput.m_hitPos);
                            }
                            if (flag2)
                            {
                                flag2 = EnsureTempLine(prefab, 0, -2, -1, raycastOutput.m_hitPos, fixedPlatform);
                            }
                            if (flag2)
                            {
                                m_hitPosition = raycastOutput.m_hitPos;
                                m_fixedPlatform = fixedPlatform;
                                m_hoverStopIndex = -1;
                                m_hoverSegmentIndex = -1;
                                if (!Singleton<NetManager>.instance.CheckLimits())
                                {
                                    toolErrors |= ToolErrors.TooManyObjects;
                                }
                                if (!Singleton<TransportManager>.instance.CheckLimits())
                                {
                                    toolErrors |= ToolErrors.TooManyObjects;
                                }
                            }
                            else
                            {
                                EnsureTempLine(prefab, 0, -2, -2, Vector3.zero, fixedPlatform);
                                m_hoverStopIndex = -1;
                                m_hoverSegmentIndex = -1;
                                toolErrors |= ToolErrors.RaycastFailed;
                            }
                        }
                        break;
                    }
                case Mode.AddStops:
                    if (m_line == 0)
                    {
                        m_mode = Mode.NewLine;
                        toolErrors |= ToolErrors.RaycastFailed;
                    }
                    else
                    {
                        RaycastOutput raycastOutput2;
                        bool flag3 = RayCast(new RaycastInput(m_mouseRay, m_mouseRayLength)
                        {
                            m_buildingService = new RaycastService(prefab.m_stationService, prefab.m_stationSubService, prefab.m_stationLayer),
                            m_netService = new RaycastService(prefab.m_netService, prefab.m_netSubService, prefab.m_netLayer),
                            m_ignoreTerrain = true,
                            m_ignoreSegmentFlags = (prefab.m_netService == ItemClass.Service.None) ? NetSegment.Flags.All : NetSegment.Flags.None,
                            m_ignoreBuildingFlags = (prefab.m_stationService == ItemClass.Service.None) ? Building.Flags.All : Building.Flags.None
                        }, out raycastOutput2);
                        bool fixedPlatform2 = false;
                        if (flag3)
                        {
                            ushort firstStop = 0;
                            if (m_line != 0)
                            {
                                TransportManager instance = Singleton<TransportManager>.instance;
                                if (!instance.m_lines.m_buffer[(int)m_line].Complete)
                                {
                                    firstStop = instance.m_lines.m_buffer[(int)m_line].m_stops;
                                }
                            }
                            flag3 = GetStopPosition(prefab, raycastOutput2.m_netSegment, raycastOutput2.m_building, firstStop, ref raycastOutput2.m_hitPos, out fixedPlatform2);
                        }
                        if (flag3)
                        {
                            flag3 = CanAddStop(prefab, m_line, -1, raycastOutput2.m_hitPos);
                        }
                        if (flag3)
                        {
                            flag3 = EnsureTempLine(prefab, m_line, -2, -1, raycastOutput2.m_hitPos, fixedPlatform2);
                        }
                        if (flag3)
                        {
                            m_hitPosition = raycastOutput2.m_hitPos;
                            m_fixedPlatform = fixedPlatform2;
                            if (!Singleton<NetManager>.instance.CheckLimits())
                            {
                                toolErrors |= ToolErrors.TooManyObjects;
                            }
                        }
                        else
                        {
                            EnsureTempLine(prefab, m_line, -2, -2, Vector3.zero, fixedPlatform2);
                            toolErrors |= ToolErrors.RaycastFailed;
                        }
                    }
                    break;
                case Mode.MoveStops:
                    if (m_line == 0)
                    {
                        m_mode = Mode.NewLine;
                        toolErrors |= ToolErrors.RaycastFailed;
                    }
                    else
                    {
                        RaycastOutput raycastOutput3;
                        bool flag4 = RayCast(new RaycastInput(m_mouseRay, m_mouseRayLength)
                        {
                            m_buildingService = new RaycastService(prefab.m_stationService, prefab.m_stationSubService, prefab.m_stationLayer),
                            m_netService = new RaycastService(prefab.m_netService, prefab.m_netSubService, prefab.m_netLayer),
                            m_ignoreTerrain = true,
                            m_ignoreSegmentFlags = (prefab.m_netService == ItemClass.Service.None) ? NetSegment.Flags.All : NetSegment.Flags.None,
                            m_ignoreBuildingFlags = (prefab.m_stationService == ItemClass.Service.None) ? Building.Flags.All : Building.Flags.None
                        }, out raycastOutput3);
                        bool fixedPlatform3 = false;
                        if (flag4)
                        {
                            flag4 = GetStopPosition(prefab, raycastOutput3.m_netSegment, raycastOutput3.m_building, 0, ref raycastOutput3.m_hitPos, out fixedPlatform3);
                        }
                        if (m_hoverStopIndex != -1)
                        {
                            if (flag4)
                            {
                                flag4 = CanMoveStop(prefab, m_line, m_hoverStopIndex, raycastOutput3.m_hitPos);
                            }
                            if (flag4)
                            {
                                flag4 = EnsureTempLine(prefab, m_line, m_hoverStopIndex, -2, raycastOutput3.m_hitPos, fixedPlatform3);
                            }
                        }
                        else if (m_hoverSegmentIndex != -1)
                        {
                            if (flag4)
                            {
                                flag4 = CanAddStop(prefab, m_line, m_hoverSegmentIndex + 1, raycastOutput3.m_hitPos);
                            }
                            if (flag4)
                            {
                                flag4 = EnsureTempLine(prefab, m_line, -2, m_hoverSegmentIndex + 1, raycastOutput3.m_hitPos, fixedPlatform3);
                            }
                        }
                        if (flag4)
                        {
                            m_hitPosition = raycastOutput3.m_hitPos;
                            m_fixedPlatform = fixedPlatform3;
                            if (m_hoverSegmentIndex != -1 && !Singleton<NetManager>.instance.CheckLimits())
                            {
                                toolErrors |= ToolErrors.TooManyObjects;
                            }
                        }
                        else
                        {
                            EnsureTempLine(prefab, m_line, -2, -2, Vector3.zero, fixedPlatform3);
                            toolErrors |= ToolErrors.RaycastFailed;
                        }
                    }
                    break;
                default:
                    toolErrors |= ToolErrors.RaycastFailed;
                    break;
            }
            m_errors = toolErrors;
        }

        private bool CanMoveStop(TransportInfo info, ushort sourceLine, int moveIndex, Vector3 movePos)
        {
            return sourceLine != 0 && Singleton<TransportManager>.instance.m_lines.m_buffer[(int)sourceLine].CanMoveStop(sourceLine, moveIndex, movePos);
        }

        private bool CanAddStop(TransportInfo info, ushort sourceLine, int addIndex, Vector3 addPos)
        {
            return sourceLine == 0 || Singleton<TransportManager>.instance.m_lines.m_buffer[(int)sourceLine].CanAddStop(sourceLine, addIndex, addPos);
        }

        private bool GetStopPosition(TransportInfo info, ushort segment, ushort building, ushort firstStop, ref Vector3 hitPos, out bool fixedPlatform)
        {
            bool toggleSnapTarget = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            fixedPlatform = false;
            if (segment != 0)
            {
                NetManager instance = Singleton<NetManager>.instance;
                if (!toggleSnapTarget && (instance.m_segments.m_buffer[(int)segment].m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None)
                {
                    building = NetSegment.FindOwnerBuilding(segment, 363f);
                    if (building != 0)
                    {
                        segment = 0;
                    }
                }
                Vector3 point;
                int num;
                float num2;
                Vector3 vector;
                int num3;
                float num4;
                if (segment != 0 && instance.m_segments.m_buffer[(int)segment].GetClosestLanePosition(hitPos, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, out point, out num, out num2) && instance.m_segments.m_buffer[(int)segment].GetClosestLanePosition(point, NetInfo.LaneType.Vehicle | (NetInfo.LaneType)((byte)32) | ((NetInfo.LaneType)((byte)64)), info.m_vehicleType, out vector, out num3, out num4))
                {
                    PathUnit.Position pathPos;
                    pathPos.m_segment = segment;
                    pathPos.m_lane = (byte)num3;
                    pathPos.m_offset = 128;
                    NetInfo.Lane lane = instance.m_segments.m_buffer[(int)segment].Info.m_lanes[num3];
                    if (!lane.m_allowStop)
                    {
                        return false;
                    }
                    float num5 = lane.m_stopOffset;
                    if ((instance.m_segments.m_buffer[(int)segment].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None)
                    {
                        num5 = -num5;
                    }
                    uint laneID = PathManager.GetLaneID(pathPos);
                    Vector3 vector2;
                    instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculateStopPositionAndDirection((float)pathPos.m_offset * 0.003921569f, num5, out hitPos, out vector2);
                    fixedPlatform = true;
                    return true;
                }
            }
            if (!toggleSnapTarget && building != 0)
            {
                VehicleInfo randomVehicleInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(ref Singleton<SimulationManager>.instance.m_randomizer, info.m_class.m_service, info.m_class.m_subService, info.m_class.m_level);
                if (randomVehicleInfo != null)
                {
                    BuildingManager instance2 = Singleton<BuildingManager>.instance;
                    BuildingInfo info2 = instance2.m_buildings.m_buffer[(int)building].Info;
                    if (info2.m_buildingAI.GetTransportLineInfo() != null)
                    {
                        Randomizer randomizer = new Randomizer((int)building);
                        Vector3 vector3;
                        Vector3 vector4;
                        info2.m_buildingAI.CalculateSpawnPosition(building, ref instance2.m_buildings.m_buffer[(int)building], ref randomizer, randomVehicleInfo, out vector3, out vector4);
                        if (firstStop != 0)
                        {
                            Vector3 position = Singleton<NetManager>.instance.m_nodes.m_buffer[(int)firstStop].m_position;
                            if (Vector3.SqrMagnitude(position - vector3) < 16384f && instance2.FindBuilding(vector3, 128f, info.m_class.m_service, info.m_class.m_subService, Building.Flags.None, Building.Flags.None) == building)
                            {
                                hitPos = position;
                                return true;
                            }
                        }
                        hitPos = vector3;
                        return true;
                    }
                }
            }
            return false;
        }


        private bool EnsureTempLine(TransportInfo info, ushort sourceLine, int moveIndex, int addIndex, Vector3 addPos, bool fixedPlatform)
        {
            TransportManager instance = Singleton<TransportManager>.instance;
            if (m_tempLine != 0)
            {
                if ((instance.m_lines.m_buffer[(int)m_tempLine].m_flags & TransportLine.Flags.Temporary) == TransportLine.Flags.None)
                {
                    m_tempLine = 0;
                    SetEditLine(0, true);
                }
                else if (instance.m_lines.m_buffer[(int)m_tempLine].Info != info)
                {
                    instance.ReleaseLine(m_tempLine);
                    m_tempLine = 0;
                    SetEditLine(0, true);
                }
            }
            if (m_tempLine == 0)
            {
                for (int i = 1; i < 256; i++)
                {
                    if ((instance.m_lines.m_buffer[i].m_flags & TransportLine.Flags.Temporary) != TransportLine.Flags.None)
                    {
                        if (instance.m_lines.m_buffer[i].Info != info)
                        {
                            instance.ReleaseLine((ushort)i);
                        }
                        else
                        {
                            m_tempLine = (ushort)i;
                            SetEditLine(sourceLine, true);
                        }
                        break;
                    }
                }
            }
            ushort tempLine = m_tempLine;
            bool flag = m_tempLine == 0 && Singleton<TransportManager>.instance.CreateLine(out tempLine, ref Singleton<SimulationManager>.instance.m_randomizer, info, false);
            m_tempLine = tempLine;
            if (flag)
            {
                TransportLine[] expr_141_cp_0 = instance.m_lines.m_buffer;
                ushort expr_141_cp_1 = m_tempLine;
                expr_141_cp_0[(int)expr_141_cp_1].m_flags = (expr_141_cp_0[(int)expr_141_cp_1].m_flags | TransportLine.Flags.Temporary);
                SetEditLine(sourceLine, true);
            }
            if (m_tempLine != 0)
            {
                SetEditLine(sourceLine, false);
                if (m_lastMoveIndex != moveIndex || m_lastAddIndex != addIndex || m_lastAddPos != addPos)
                {
                    if (m_lastAddIndex != -2 && instance.m_lines.m_buffer[(int)m_tempLine].RemoveStop(m_tempLine, m_lastAddIndex))
                    {
                        m_lastAddIndex = -2;
                        m_lastAddPos = Vector3.zero;
                    }
                    if (m_lastMoveIndex != -2 && instance.m_lines.m_buffer[(int)m_tempLine].MoveStop(m_tempLine, m_lastMoveIndex, m_lastMovePos, fixedPlatform))
                    {
                        m_lastMoveIndex = -2;
                        m_lastMovePos = Vector3.zero;
                    }
                    instance.m_lines.m_buffer[(int)m_tempLine].CopyMissingPaths(sourceLine);
                    Vector3 lastMovePos;
                    if (moveIndex != -2 && instance.m_lines.m_buffer[(int)m_tempLine].MoveStop(m_tempLine, moveIndex, addPos, fixedPlatform, out lastMovePos))
                    {
                        m_lastMoveIndex = moveIndex;
                        m_lastMovePos = lastMovePos;
                        m_lastAddPos = addPos;
                    }
                    if (addIndex != -2 && instance.m_lines.m_buffer[(int)m_tempLine].AddStop(m_tempLine, addIndex, addPos, fixedPlatform))
                    {
                        m_lastAddIndex = addIndex;
                        m_lastAddPos = addPos;
                    }
                }
                instance.m_lines.m_buffer[(int)m_tempLine].m_color = instance.m_lines.m_buffer[(int)sourceLine].m_color;
                TransportLine[] expr_327_cp_0 = instance.m_lines.m_buffer;
                ushort expr_327_cp_1 = m_tempLine;
                expr_327_cp_0[(int)expr_327_cp_1].m_flags = (expr_327_cp_0[(int)expr_327_cp_1].m_flags & ~TransportLine.Flags.Hidden);
                if ((instance.m_lines.m_buffer[(int)sourceLine].m_flags & TransportLine.Flags.CustomColor) != TransportLine.Flags.None)
                {
                    TransportLine[] expr_36C_cp_0 = instance.m_lines.m_buffer;
                    ushort expr_36C_cp_1 = m_tempLine;
                    expr_36C_cp_0[(int)expr_36C_cp_1].m_flags = (expr_36C_cp_0[(int)expr_36C_cp_1].m_flags | TransportLine.Flags.CustomColor);
                }
                else
                {
                    TransportLine[] expr_398_cp_0 = instance.m_lines.m_buffer;
                    ushort expr_398_cp_1 = m_tempLine;
                    expr_398_cp_0[(int)expr_398_cp_1].m_flags = (expr_398_cp_0[(int)expr_398_cp_1].m_flags & ~TransportLine.Flags.CustomColor);
                }
                return true;
            }
            SetEditLine(0, false);
            return false;
        }

        private void SetEditLine(ushort line, bool forceRefresh)
        {
            if (line != m_lastEditLine || forceRefresh)
            {
                TransportManager instance = Singleton<TransportManager>.instance;
                if (m_lastEditLine != 0)
                {
                    TransportLine[] expr_39_cp_0 = instance.m_lines.m_buffer;
                    ushort expr_39_cp_1 = m_lastEditLine;
                    expr_39_cp_0[(int)expr_39_cp_1].m_flags = (expr_39_cp_0[(int)expr_39_cp_1].m_flags & ~(TransportLine.Flags.Hidden | TransportLine.Flags.Selected));
                }
                m_lastEditLine = line;
                m_lastMoveIndex = -2;
                m_lastAddIndex = -2;
                m_lastMovePos = Vector3.zero;
                m_lastAddPos = Vector3.zero;
                if (m_lastEditLine != 0)
                {
                    TransportLine[] expr_95_cp_0 = instance.m_lines.m_buffer;
                    ushort expr_95_cp_1 = m_lastEditLine;
                    expr_95_cp_0[(int)expr_95_cp_1].m_flags = (expr_95_cp_0[(int)expr_95_cp_1].m_flags | (TransportLine.Flags.Hidden | TransportLine.Flags.Selected));
                }
                if (m_tempLine != 0)
                {
                    instance.m_lines.m_buffer[(int)m_tempLine].CloneLine(m_tempLine, m_lastEditLine);
                    BusTransportLineAI.UpdateMeshData(ref instance.m_lines.m_buffer[(int)m_tempLine], m_tempLine);
                }
            }
        }

        protected override void OnToolUpdate()
        {
            if (!m_toolController.IsInsideUI && Cursor.visible)
            {
                Mode mode = m_mode;
                ushort lastEditLine = m_lastEditLine;
                int hoverStopIndex = m_hoverStopIndex;
                int hoverSegmentIndex = m_hoverSegmentIndex;
                Vector3 hitPosition = m_hitPosition;
                string text = null;
                if (m_errors != ToolErrors.Pending && m_errors != ToolErrors.RaycastFailed)
                {
                    if (mode == Mode.NewLine)
                    {
                        if (hoverStopIndex != -1)
                        {
                            text = Locale.Get("TOOL_DRAG_STOP");
                        }
                        else if (hoverSegmentIndex != -1)
                        {
                            text = Locale.Get("TOOL_DRAG_LINE");
                        }
                        else
                        {
                            text = Locale.Get("TOOL_NEW_LINE");
                        }
                    }
                    else if (mode == Mode.AddStops)
                    {
                        if (lastEditLine != 0)
                        {
                            ushort stops = Singleton<TransportManager>.instance.m_lines.m_buffer[(int)lastEditLine].m_stops;
                            if (stops != 0)
                            {
                                Vector3 position = Singleton<NetManager>.instance.m_nodes.m_buffer[(int)stops].m_position;
                                if (Vector3.SqrMagnitude(hitPosition - position) < 6.25f)
                                {
                                    text = Locale.Get("TOOL_CLOSE_LINE");
                                }
                            }
                        }
                        if (text == null)
                        {
                            text = Locale.Get("TOOL_ADD_STOP");
                        }
                    }
                    else if (mode == Mode.MoveStops)
                    {
                        if (hoverStopIndex != -1)
                        {
                            text = Locale.Get("TOOL_MOVE_STOP");
                        }
                        else if (hoverSegmentIndex != -1)
                        {
                            text = Locale.Get("TOOL_ADD_STOP");
                        }
                    }
                }
                ShowToolInfo(true, text, m_hitPosition);
            }
            else
            {
                ShowToolInfo(false, null, m_hitPosition);
            }
        }
    }
}
