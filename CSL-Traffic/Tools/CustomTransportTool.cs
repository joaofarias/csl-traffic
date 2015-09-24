using ColossalFramework;
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
        private CustomTransportTool.Mode m_mode
        {
            get
            {
                return (CustomTransportTool.Mode)((int)this.fi_mode.GetValue(this));
            }
            set
            {
                this.fi_mode.SetValue(this, (int)value);
            }
        }
        private ushort m_line
        {
            get
            {
                return (ushort)this.fi_line.GetValue(this);
            }
            set
            {
                this.fi_line.SetValue(this, value);
            }
        }
        private Ray m_mouseRay
        {
            get
            {
                return (Ray)this.fi_mouseRay.GetValue(this);
            }
            set
            {
                this.fi_mouseRay.SetValue(this, value);
            }
        }
        private float m_mouseRayLength
        {
            get
            {
                return (float)this.fi_mouseRayLength.GetValue(this);
            }
            set
            {
                this.fi_mouseRayLength.SetValue(this, value);
            }
        }
        private Vector3 m_hitPosition
        {
            get
            {
                return (Vector3)this.fi_hitPosition.GetValue(this);
            }
            set
            {
                this.fi_hitPosition.SetValue(this, value);
            }
        }
        private bool m_fixedPlatform
        {
            get
            {
                return (bool)this.fi_fixedPlatform.GetValue(this);
            }
            set
            {
                this.fi_fixedPlatform.SetValue(this, value);
            }
        }
        private ushort m_tempLine
        {
            get
            {
                return (ushort)this.fi_tempLine.GetValue(this);
            }
            set
            {
                this.fi_tempLine.SetValue(this, value);
            }
        }
        private ushort m_lastEditLine
        {
            get
            {
                return (ushort)this.fi_lastEditLine.GetValue(this);
            }
            set
            {
                this.fi_lastEditLine.SetValue(this, value);
            }
        }
        private int m_lastMoveIndex
        {
            get
            {
                return (int)this.fi_lastMoveIndex.GetValue(this);
            }
            set
            {
                this.fi_lastMoveIndex.SetValue(this, value);
            }
        }
        private int m_lastAddIndex
        {
            get
            {
                return (int)this.fi_lastAddIndex.GetValue(this);
            }
            set
            {
                this.fi_lastAddIndex.SetValue(this, value);
            }
        }
        private Vector3 m_lastMovePos
        {
            get
            {
                return (Vector3)this.fi_lastMovePos.GetValue(this);
            }
            set
            {
                this.fi_lastMovePos.SetValue(this, value);
            }
        }
        private Vector3 m_lastAddPos
        {
            get
            {
                return (Vector3)this.fi_lastAddPos.GetValue(this);
            }
            set
            {
                this.fi_lastAddPos.SetValue(this, value);
            }
        }
        private int m_hoverStopIndex
        {
            get
            {
                return (int)this.fi_hoverStopIndex.GetValue(this);
            }
            set
            {
                this.fi_hoverStopIndex.SetValue(this, value);
            }
        }
        private int m_hoverSegmentIndex
        {
            get
            {
                return (int)this.fi_hoverSegmentIndex.GetValue(this);
            }
            set
            {
                this.fi_hoverSegmentIndex.SetValue(this, value);
            }
        }
        private ToolBase.ToolErrors m_errors
        {
            get
            {
                return (ToolBase.ToolErrors)this.fi_errors.GetValue(this);
            }
            set
            {
                this.fi_errors.SetValue(this, value);
            }
        }

        protected override void Awake()
        {
            base.Awake();

            Type transportToolType = typeof(TransportTool);
            this.fi_mode = transportToolType.GetFieldByName("m_mode");
            this.fi_line = transportToolType.GetFieldByName("m_line");
            this.fi_mouseRay = transportToolType.GetFieldByName("m_mouseRay");
            this.fi_mouseRayLength = transportToolType.GetFieldByName("m_mouseRayLength");
            this.fi_hitPosition = transportToolType.GetFieldByName("m_hitPosition");
            this.fi_fixedPlatform = transportToolType.GetFieldByName("m_fixedPlatform");
            this.fi_tempLine = transportToolType.GetFieldByName("m_tempLine");
            this.fi_lastEditLine = transportToolType.GetFieldByName("m_lastEditLine");
            this.fi_lastMoveIndex = transportToolType.GetFieldByName("m_lastMoveIndex");
            this.fi_lastAddIndex = transportToolType.GetFieldByName("m_lastAddIndex");
            this.fi_lastMovePos = transportToolType.GetFieldByName("m_lastMovePos");
            this.fi_lastAddPos = transportToolType.GetFieldByName("m_lastAddPos");
            this.fi_hoverStopIndex = transportToolType.GetFieldByName("m_hoverStopIndex");
            this.fi_hoverSegmentIndex = transportToolType.GetFieldByName("m_hoverSegmentIndex");
            this.fi_errors = transportToolType.GetFieldByName("m_errors");
        }

        public override void SimulationStep()
        {
            TransportInfo prefab = this.m_prefab;
            if (prefab == null)
            {
                return;
            }
            ToolBase.ToolErrors toolErrors = ToolBase.ToolErrors.None;
            switch (this.m_mode)
            {
                case CustomTransportTool.Mode.NewLine:
                    {
                        Vector3 vector;
                        ushort num;
                        int hoverStopIndex;
                        int hoverSegmentIndex;
                        if ((Singleton<TransportManager>.instance as CustomTransportManager).RayCast(this.m_mouseRay, this.m_mouseRayLength, out vector, out num, out hoverStopIndex, out hoverSegmentIndex))
                        {
                            TransportInfo info = Singleton<TransportManager>.instance.m_lines.m_buffer[(int)num].Info;
                            bool flag = info == prefab;
                            if (flag)
                            {
                                flag = this.EnsureTempLine(prefab, num, -2, -2, vector, false);
                            }
                            if (flag)
                            {
                                this.m_hitPosition = vector;
                                this.m_fixedPlatform = false;
                                this.m_hoverStopIndex = hoverStopIndex;
                                this.m_hoverSegmentIndex = hoverSegmentIndex;
                                if (this.m_hoverSegmentIndex != -1 && !Singleton<NetManager>.instance.CheckLimits())
                                {
                                    toolErrors |= ToolBase.ToolErrors.TooManyObjects;
                                }
                            }
                            else
                            {
                                this.EnsureTempLine(prefab, 0, -2, -2, Vector3.zero, false);
                                this.m_hoverStopIndex = -1;
                                this.m_hoverSegmentIndex = -1;
                                toolErrors |= ToolBase.ToolErrors.RaycastFailed;
                            }
                        }
                        else
                        {
                            ToolBase.RaycastOutput raycastOutput;
                            bool flag2 = ToolBase.RayCast(new ToolBase.RaycastInput(this.m_mouseRay, this.m_mouseRayLength)
                            {
                                m_buildingService = new ToolBase.RaycastService(prefab.m_stationService, prefab.m_stationSubService, prefab.m_stationLayer),
                                m_netService = new ToolBase.RaycastService(prefab.m_netService, prefab.m_netSubService, prefab.m_netLayer),
                                m_ignoreTerrain = true,
                                m_ignoreSegmentFlags = (prefab.m_netService == ItemClass.Service.None) ? NetSegment.Flags.All : NetSegment.Flags.None,
                                m_ignoreBuildingFlags = (prefab.m_stationService == ItemClass.Service.None) ? Building.Flags.All : Building.Flags.None
                            }, out raycastOutput);
                            bool fixedPlatform = false;
                            if (flag2)
                            {
                                flag2 = this.GetStopPosition(prefab, raycastOutput.m_netSegment, raycastOutput.m_building, 0, ref raycastOutput.m_hitPos, out fixedPlatform);
                            }
                            if (flag2)
                            {
                                flag2 = this.CanAddStop(prefab, 0, -1, raycastOutput.m_hitPos);
                            }
                            if (flag2)
                            {
                                flag2 = this.EnsureTempLine(prefab, 0, -2, -1, raycastOutput.m_hitPos, fixedPlatform);
                            }
                            if (flag2)
                            {
                                this.m_hitPosition = raycastOutput.m_hitPos;
                                this.m_fixedPlatform = fixedPlatform;
                                this.m_hoverStopIndex = -1;
                                this.m_hoverSegmentIndex = -1;
                                if (!Singleton<NetManager>.instance.CheckLimits())
                                {
                                    toolErrors |= ToolBase.ToolErrors.TooManyObjects;
                                }
                                if (!Singleton<TransportManager>.instance.CheckLimits())
                                {
                                    toolErrors |= ToolBase.ToolErrors.TooManyObjects;
                                }
                            }
                            else
                            {
                                this.EnsureTempLine(prefab, 0, -2, -2, Vector3.zero, fixedPlatform);
                                this.m_hoverStopIndex = -1;
                                this.m_hoverSegmentIndex = -1;
                                toolErrors |= ToolBase.ToolErrors.RaycastFailed;
                            }
                        }
                        break;
                    }
                case CustomTransportTool.Mode.AddStops:
                    if (this.m_line == 0)
                    {
                        this.m_mode = CustomTransportTool.Mode.NewLine;
                        toolErrors |= ToolBase.ToolErrors.RaycastFailed;
                    }
                    else
                    {
                        ToolBase.RaycastOutput raycastOutput2;
                        bool flag3 = ToolBase.RayCast(new ToolBase.RaycastInput(this.m_mouseRay, this.m_mouseRayLength)
                        {
                            m_buildingService = new ToolBase.RaycastService(prefab.m_stationService, prefab.m_stationSubService, prefab.m_stationLayer),
                            m_netService = new ToolBase.RaycastService(prefab.m_netService, prefab.m_netSubService, prefab.m_netLayer),
                            m_ignoreTerrain = true,
                            m_ignoreSegmentFlags = (prefab.m_netService == ItemClass.Service.None) ? NetSegment.Flags.All : NetSegment.Flags.None,
                            m_ignoreBuildingFlags = (prefab.m_stationService == ItemClass.Service.None) ? Building.Flags.All : Building.Flags.None
                        }, out raycastOutput2);
                        bool fixedPlatform2 = false;
                        if (flag3)
                        {
                            ushort firstStop = 0;
                            if (this.m_line != 0)
                            {
                                TransportManager instance = Singleton<TransportManager>.instance;
                                if (!instance.m_lines.m_buffer[(int)this.m_line].Complete)
                                {
                                    firstStop = instance.m_lines.m_buffer[(int)this.m_line].m_stops;
                                }
                            }
                            flag3 = this.GetStopPosition(prefab, raycastOutput2.m_netSegment, raycastOutput2.m_building, firstStop, ref raycastOutput2.m_hitPos, out fixedPlatform2);
                        }
                        if (flag3)
                        {
                            flag3 = this.CanAddStop(prefab, this.m_line, -1, raycastOutput2.m_hitPos);
                        }
                        if (flag3)
                        {
                            flag3 = this.EnsureTempLine(prefab, this.m_line, -2, -1, raycastOutput2.m_hitPos, fixedPlatform2);
                        }
                        if (flag3)
                        {
                            this.m_hitPosition = raycastOutput2.m_hitPos;
                            this.m_fixedPlatform = fixedPlatform2;
                            if (!Singleton<NetManager>.instance.CheckLimits())
                            {
                                toolErrors |= ToolBase.ToolErrors.TooManyObjects;
                            }
                        }
                        else
                        {
                            this.EnsureTempLine(prefab, this.m_line, -2, -2, Vector3.zero, fixedPlatform2);
                            toolErrors |= ToolBase.ToolErrors.RaycastFailed;
                        }
                    }
                    break;
                case CustomTransportTool.Mode.MoveStops:
                    if (this.m_line == 0)
                    {
                        this.m_mode = CustomTransportTool.Mode.NewLine;
                        toolErrors |= ToolBase.ToolErrors.RaycastFailed;
                    }
                    else
                    {
                        ToolBase.RaycastOutput raycastOutput3;
                        bool flag4 = ToolBase.RayCast(new ToolBase.RaycastInput(this.m_mouseRay, this.m_mouseRayLength)
                        {
                            m_buildingService = new ToolBase.RaycastService(prefab.m_stationService, prefab.m_stationSubService, prefab.m_stationLayer),
                            m_netService = new ToolBase.RaycastService(prefab.m_netService, prefab.m_netSubService, prefab.m_netLayer),
                            m_ignoreTerrain = true,
                            m_ignoreSegmentFlags = (prefab.m_netService == ItemClass.Service.None) ? NetSegment.Flags.All : NetSegment.Flags.None,
                            m_ignoreBuildingFlags = (prefab.m_stationService == ItemClass.Service.None) ? Building.Flags.All : Building.Flags.None
                        }, out raycastOutput3);
                        bool fixedPlatform3 = false;
                        if (flag4)
                        {
                            flag4 = this.GetStopPosition(prefab, raycastOutput3.m_netSegment, raycastOutput3.m_building, 0, ref raycastOutput3.m_hitPos, out fixedPlatform3);
                        }
                        if (this.m_hoverStopIndex != -1)
                        {
                            if (flag4)
                            {
                                flag4 = this.CanMoveStop(prefab, this.m_line, this.m_hoverStopIndex, raycastOutput3.m_hitPos);
                            }
                            if (flag4)
                            {
                                flag4 = this.EnsureTempLine(prefab, this.m_line, this.m_hoverStopIndex, -2, raycastOutput3.m_hitPos, fixedPlatform3);
                            }
                        }
                        else if (this.m_hoverSegmentIndex != -1)
                        {
                            if (flag4)
                            {
                                flag4 = this.CanAddStop(prefab, this.m_line, this.m_hoverSegmentIndex + 1, raycastOutput3.m_hitPos);
                            }
                            if (flag4)
                            {
                                flag4 = this.EnsureTempLine(prefab, this.m_line, -2, this.m_hoverSegmentIndex + 1, raycastOutput3.m_hitPos, fixedPlatform3);
                            }
                        }
                        if (flag4)
                        {
                            this.m_hitPosition = raycastOutput3.m_hitPos;
                            this.m_fixedPlatform = fixedPlatform3;
                            if (this.m_hoverSegmentIndex != -1 && !Singleton<NetManager>.instance.CheckLimits())
                            {
                                toolErrors |= ToolBase.ToolErrors.TooManyObjects;
                            }
                        }
                        else
                        {
                            this.EnsureTempLine(prefab, this.m_line, -2, -2, Vector3.zero, fixedPlatform3);
                            toolErrors |= ToolBase.ToolErrors.RaycastFailed;
                        }
                    }
                    break;
                default:
                    toolErrors |= ToolBase.ToolErrors.RaycastFailed;
                    break;
            }
            this.m_errors = toolErrors;
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
            NetManager instance = Singleton<NetManager>.instance;
            bool toggleSnapTarget = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            fixedPlatform = false;
            if (segment != 0)
            {
                if (!toggleSnapTarget && (instance.m_segments.m_buffer[(int)segment].m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None)
                {
                    building = NetSegment.FindOwnerBuilding(segment, 363f);
                    if (building != 0)
                    {
                        BuildingManager instance2 = Singleton<BuildingManager>.instance;
					    BuildingInfo info2 = instance2.m_buildings.m_buffer[(int)building].Info;
					    TransportInfo transportLineInfo = info2.m_buildingAI.GetTransportLineInfo();
                        if (transportLineInfo != null && transportLineInfo.m_transportType == info.m_transportType)
                        {
                            segment = 0;
                        }
                        else
                        {
                            building = 0;
                        }
                    }
                }
                Vector3 point;
                int num;
                float num2;
                Vector3 vector;
                int num3;
                float num4;
                if (segment != 0 && instance.m_segments.m_buffer[(int)segment].GetClosestLanePosition(hitPos, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, out point, out num, out num2) && instance.m_segments.m_buffer[(int)segment].GetClosestLanePosition(point, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, out vector, out num3, out num4))
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
                    BuildingManager instance3 = Singleton<BuildingManager>.instance;
                    BuildingInfo info3 = instance3.m_buildings.m_buffer[(int)building].Info;
                    if (info3.m_buildingAI.GetTransportLineInfo() != null)
                    {
                        Vector3 vector3 = Vector3.zero;
                        int num6 = 1000000;
                        for (int i = 0; i < 12; i++)
                        {
                            Randomizer randomizer = new Randomizer(i);
                            Vector3 vector4;
                            Vector3 a;
                            info3.m_buildingAI.CalculateSpawnPosition(building, ref instance3.m_buildings.m_buffer[(int)building], ref randomizer, randomVehicleInfo, out vector4, out a);
                            int lineCount = this.GetLineCount(vector4, a - vector4, info.m_transportType);
                            if (lineCount < num6)
                            {
                                vector3 = vector4;
                                num6 = lineCount;
                            }
                            else if (lineCount == num6 && Vector3.SqrMagnitude(vector4 - hitPos) < Vector3.SqrMagnitude(vector3 - hitPos))
                            {
                                vector3 = vector4;
                            }
                        }
                        if (firstStop != 0)
                        {
                            Vector3 position = instance.m_nodes.m_buffer[(int)firstStop].m_position;
                            if (Vector3.SqrMagnitude(position - vector3) < 16384f)
                            {
                                uint lane2 = instance.m_nodes.m_buffer[(int)firstStop].m_lane;
                                if (lane2 != 0u)
                                {
                                    ushort segment2 = instance.m_lanes.m_buffer[(int)((UIntPtr)lane2)].m_segment;
                                    if (segment2 != 0 && (instance.m_segments.m_buffer[(int)segment2].m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None)
                                    {
                                        ushort num7 = NetSegment.FindOwnerBuilding(segment2, 363f);
                                        if (building == num7)
                                        {
                                            hitPos = position;
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                        hitPos = vector3;
                        return true;
                    }
                }
            }
            return false;
        }

        private int GetLineCount(Vector3 stopPosition, Vector3 stopDirection, TransportInfo.TransportType transportType)
        {
            NetManager instance = Singleton<NetManager>.instance;
            TransportManager instance2 = Singleton<TransportManager>.instance;
            stopDirection.Normalize();
            Segment3 segment = new Segment3(stopPosition - stopDirection * 16f, stopPosition + stopDirection * 16f);
            Vector3 vector = segment.Min();
            Vector3 vector2 = segment.Max();
            int num = Mathf.Max((int)((vector.x - 4f) / 64f + 135f), 0);
            int num2 = Mathf.Max((int)((vector.z - 4f) / 64f + 135f), 0);
            int num3 = Mathf.Min((int)((vector2.x + 4f) / 64f + 135f), 269);
            int num4 = Mathf.Min((int)((vector2.z + 4f) / 64f + 135f), 269);
            int num5 = 0;
            for (int i = num2; i <= num4; i++)
            {
                for (int j = num; j <= num3; j++)
                {
                    ushort num6 = instance.m_nodeGrid[i * 270 + j];
                    int num7 = 0;
                    while (num6 != 0)
                    {
                        ushort transportLine = instance.m_nodes.m_buffer[(int)num6].m_transportLine;
                        if (transportLine != 0)
                        {
                            TransportInfo info = instance2.m_lines.m_buffer[(int)transportLine].Info;
                            if (info.m_transportType == transportType && (instance2.m_lines.m_buffer[(int)transportLine].m_flags & TransportLine.Flags.Temporary) == TransportLine.Flags.None && segment.DistanceSqr(instance.m_nodes.m_buffer[(int)num6].m_position) < 16f)
                            {
                                num5++;
                            }
                        }
                        num6 = instance.m_nodes.m_buffer[(int)num6].m_nextGridNode;
                        if (++num7 >= 32768)
                        {
                            CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                            break;
                        }
                    }
                }
            }
            return num5;
        }

        private bool EnsureTempLine(TransportInfo info, ushort sourceLine, int moveIndex, int addIndex, Vector3 addPos, bool fixedPlatform)
        {
            TransportManager instance = Singleton<TransportManager>.instance;
            if (this.m_tempLine != 0)
            {
                if ((instance.m_lines.m_buffer[(int)this.m_tempLine].m_flags & TransportLine.Flags.Temporary) == TransportLine.Flags.None)
                {
                    this.m_tempLine = 0;
                    this.SetEditLine(0, true);
                }
                else if (instance.m_lines.m_buffer[(int)this.m_tempLine].Info != info)
                {
                    instance.ReleaseLine(this.m_tempLine);
                    this.m_tempLine = 0;
                    this.SetEditLine(0, true);
                }
            }
            if (this.m_tempLine == 0)
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
                            this.m_tempLine = (ushort)i;
                            this.SetEditLine(sourceLine, true);
                        }
                        break;
                    }
                }
            }
            ushort tempLine = this.m_tempLine;
            bool flag = this.m_tempLine == 0 && Singleton<TransportManager>.instance.CreateLine(out tempLine, ref Singleton<SimulationManager>.instance.m_randomizer, info, false);
            this.m_tempLine = tempLine;
            if (flag)
            {
                TransportLine[] expr_141_cp_0 = instance.m_lines.m_buffer;
                ushort expr_141_cp_1 = this.m_tempLine;
                expr_141_cp_0[(int)expr_141_cp_1].m_flags = (expr_141_cp_0[(int)expr_141_cp_1].m_flags | TransportLine.Flags.Temporary);
                this.SetEditLine(sourceLine, true);
            }
            if (this.m_tempLine != 0)
            {
                this.SetEditLine(sourceLine, false);
                if (this.m_lastMoveIndex != moveIndex || this.m_lastAddIndex != addIndex || this.m_lastAddPos != addPos)
                {
                    if (this.m_lastAddIndex != -2 && instance.m_lines.m_buffer[(int)this.m_tempLine].RemoveStop(this.m_tempLine, this.m_lastAddIndex))
                    {
                        this.m_lastAddIndex = -2;
                        this.m_lastAddPos = Vector3.zero;
                    }
                    if (this.m_lastMoveIndex != -2 && instance.m_lines.m_buffer[(int)this.m_tempLine].MoveStop(this.m_tempLine, this.m_lastMoveIndex, this.m_lastMovePos, fixedPlatform))
                    {
                        this.m_lastMoveIndex = -2;
                        this.m_lastMovePos = Vector3.zero;
                    }
                    instance.m_lines.m_buffer[(int)this.m_tempLine].CopyMissingPaths(sourceLine);
                    Vector3 lastMovePos;
                    if (moveIndex != -2 && instance.m_lines.m_buffer[(int)this.m_tempLine].MoveStop(this.m_tempLine, moveIndex, addPos, fixedPlatform, out lastMovePos))
                    {
                        this.m_lastMoveIndex = moveIndex;
                        this.m_lastMovePos = lastMovePos;
                        this.m_lastAddPos = addPos;
                    }
                    if (addIndex != -2 && instance.m_lines.m_buffer[(int)this.m_tempLine].AddStop(this.m_tempLine, addIndex, addPos, fixedPlatform))
                    {
                        this.m_lastAddIndex = addIndex;
                        this.m_lastAddPos = addPos;
                    }
                }
                instance.m_lines.m_buffer[(int)this.m_tempLine].m_color = instance.m_lines.m_buffer[(int)sourceLine].m_color;
                TransportLine[] expr_327_cp_0 = instance.m_lines.m_buffer;
                ushort expr_327_cp_1 = this.m_tempLine;
                expr_327_cp_0[(int)expr_327_cp_1].m_flags = (expr_327_cp_0[(int)expr_327_cp_1].m_flags & ~TransportLine.Flags.Hidden);
                if ((instance.m_lines.m_buffer[(int)sourceLine].m_flags & TransportLine.Flags.CustomColor) != TransportLine.Flags.None)
                {
                    TransportLine[] expr_36C_cp_0 = instance.m_lines.m_buffer;
                    ushort expr_36C_cp_1 = this.m_tempLine;
                    expr_36C_cp_0[(int)expr_36C_cp_1].m_flags = (expr_36C_cp_0[(int)expr_36C_cp_1].m_flags | TransportLine.Flags.CustomColor);
                }
                else
                {
                    TransportLine[] expr_398_cp_0 = instance.m_lines.m_buffer;
                    ushort expr_398_cp_1 = this.m_tempLine;
                    expr_398_cp_0[(int)expr_398_cp_1].m_flags = (expr_398_cp_0[(int)expr_398_cp_1].m_flags & ~TransportLine.Flags.CustomColor);
                }
                return true;
            }
            this.SetEditLine(0, false);
            return false;
        }

        private void SetEditLine(ushort line, bool forceRefresh)
        {
            if (line != this.m_lastEditLine || forceRefresh)
            {
                TransportManager instance = Singleton<TransportManager>.instance;
                if (this.m_lastEditLine != 0)
                {
                    TransportLine[] expr_39_cp_0 = instance.m_lines.m_buffer;
                    ushort expr_39_cp_1 = this.m_lastEditLine;
                    expr_39_cp_0[(int)expr_39_cp_1].m_flags = (expr_39_cp_0[(int)expr_39_cp_1].m_flags & ~(TransportLine.Flags.Hidden | TransportLine.Flags.Selected));
                }
                this.m_lastEditLine = line;
                this.m_lastMoveIndex = -2;
                this.m_lastAddIndex = -2;
                this.m_lastMovePos = Vector3.zero;
                this.m_lastAddPos = Vector3.zero;
                if (this.m_lastEditLine != 0)
                {
                    TransportLine[] expr_95_cp_0 = instance.m_lines.m_buffer;
                    ushort expr_95_cp_1 = this.m_lastEditLine;
                    expr_95_cp_0[(int)expr_95_cp_1].m_flags = (expr_95_cp_0[(int)expr_95_cp_1].m_flags | (TransportLine.Flags.Hidden | TransportLine.Flags.Selected));
                }
                if (this.m_tempLine != 0)
                {
                    instance.m_lines.m_buffer[(int)this.m_tempLine].CloneLine(this.m_tempLine, this.m_lastEditLine);
                    BusTransportLineAI.UpdateMeshData(ref instance.m_lines.m_buffer[(int)this.m_tempLine], this.m_tempLine);
                }
            }
        }

        protected override void OnToolUpdate()
        {
            if (!this.m_toolController.IsInsideUI && Cursor.visible)
            {
                CustomTransportTool.Mode mode = this.m_mode;
                ushort lastEditLine = this.m_lastEditLine;
                int hoverStopIndex = this.m_hoverStopIndex;
                int hoverSegmentIndex = this.m_hoverSegmentIndex;
                Vector3 hitPosition = this.m_hitPosition;
                string text = null;
                if (this.m_errors != ToolBase.ToolErrors.Pending && this.m_errors != ToolBase.ToolErrors.RaycastFailed)
                {
                    if (mode == CustomTransportTool.Mode.NewLine)
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
                    else if (mode == CustomTransportTool.Mode.AddStops)
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
                    else if (mode == CustomTransportTool.Mode.MoveStops)
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
                base.ShowToolInfo(true, text, this.m_hitPosition);
            }
            else
            {
                base.ShowToolInfo(false, null, this.m_hitPosition);
            }
        }
    }
}
