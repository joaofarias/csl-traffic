using ColossalFramework;
using ColossalFramework.Math;
using CSL_Traffic.Extensions;
using System;
using System.Reflection;
using UnityEngine;

namespace CSL_Traffic
{
    class CustomTransportManager : TransportManager
    {
        private FieldInfo fi_lineNumber;
        private FieldInfo fi_linesVisible;
        private FieldInfo fi_undergroundCamera;
        private FieldInfo fi_patches;
        private FieldInfo fi_patchesDirty;
        private ushort[] m_lineNumber
        {
            get
            {
                return (ushort[])fi_lineNumber.GetValue(this);
            }
            set
            {
                fi_lineNumber.SetValue(this, value);
            }
        }
        private bool m_linesVisible
        {
            get
            {
                return (bool)fi_linesVisible.GetValue(this);
            }
            set
            {
                fi_linesVisible.SetValue(this, value);
            }
        }
        private Camera m_undergroundCamera
        {
            get
            {
                return (Camera)fi_undergroundCamera.GetValue(this);
            }
            set
            {
                fi_undergroundCamera.SetValue(this, value);
            }
        }
        private TransportPatch[] m_patches
        {
            get
            {
                return (TransportPatch[])fi_patches.GetValue(this);
            }
            set
            {
                fi_patches.SetValue(this, value);
            }
        }
        private bool m_patchesDirty
        {
            get
            {
                return (bool)fi_patchesDirty.GetValue(this);
            }
            set
            {
                fi_patchesDirty.SetValue(this, value);
            }
        }

        protected override void Awake()
        {
            base.Awake();

            Type transportManagerType = typeof(TransportManager);
            fi_lineNumber = transportManagerType.GetFieldByName("m_lineNumber");
            fi_linesVisible = transportManagerType.GetFieldByName("m_linesVisible");
            fi_undergroundCamera = transportManagerType.GetFieldByName("m_undergroundCamera");
            fi_patches = transportManagerType.GetFieldByName("m_patches");
            fi_patchesDirty = transportManagerType.GetFieldByName("m_patchesDirty");
        }

        public void SetOriginalValues(TransportManager originalTransportManager)
        {
            m_simulationProfiler = originalTransportManager.m_simulationProfiler;
            m_drawCallData = originalTransportManager.m_drawCallData;
            m_properties = originalTransportManager.m_properties;
            m_lineCount = originalTransportManager.m_lineCount;
            m_infoCount = originalTransportManager.m_infoCount;
            m_passengers = originalTransportManager.m_passengers;
            m_lineNumber = (ushort[])fi_lineNumber.GetValue(originalTransportManager);
            m_linesVisible = (bool)fi_linesVisible.GetValue(originalTransportManager);
            m_undergroundCamera = (Camera)fi_undergroundCamera.GetValue(originalTransportManager);
            m_patches = (TransportPatch[])fi_patches.GetValue(originalTransportManager);
            m_patchesDirty = (bool)fi_patchesDirty.GetValue(originalTransportManager);
        }

        protected override void SimulationStepImpl(int subStep)
        {
            if (m_linesUpdated)
            {
                m_linesUpdated = false;
                int num = m_updatedLines.Length;
                for (int i = 0; i < num; i++)
                {
                    ulong num2 = m_updatedLines[i];
                    if (num2 != 0uL)
                    {
                        for (int j = 0; j < 64; j++)
                        {
                            if ((num2 & 1uL << j) != 0uL)
                            {
                                ushort num3 = (ushort)(i << 6 | j);
                                if (m_lines.m_buffer[num3].m_flags != TransportLine.Flags.None)
                                {
                                    if (BusTransportLineAI.UpdatePaths(ref m_lines.m_buffer[num3], num3) && BusTransportLineAI.UpdateMeshData(ref m_lines.m_buffer[num3], num3))
                                    //if (this.m_lines.m_buffer[(int)num3].UpdatePaths(num3) && this.m_lines.m_buffer[(int)num3].UpdateMeshData(num3))
                                    {
                                        num2 &= ~(1uL << j);
                                    }
                                }
                                else
                                {
                                    num2 &= ~(1uL << j);
                                }
                            }
                        }
                        m_updatedLines[i] = num2;
                        if (num2 != 0uL)
                        {
                            m_linesUpdated = true;
                        }
                    }
                }
            }
            if (m_patchesDirty)
            {
                m_patchesDirty = false;
                int num4 = m_patches.Length;
                for (int k = 0; k < num4; k++)
                {
                    TransportPatch transportPatch = m_patches[k];
                    int num5 = 0;
                    while (transportPatch != null)
                    {
                        if (transportPatch.m_isDirty)
                        {
                            transportPatch.UpdateMeshData();
                        }
                        transportPatch = transportPatch.m_nextPatch;
                        if (++num5 >= 100)
                        {
                            CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                            break;
                        }
                    }
                }
            }
            if (subStep != 0)
            {
                int num6 = (int)(Singleton<SimulationManager>.instance.m_currentFrameIndex & 255u);
                int num7 = num6 * 1;
                int num8 = (num6 + 1) * 1 - 1;
                for (int l = num7; l <= num8; l++)
                {
                    TransportLine.Flags flags = m_lines.m_buffer[l].m_flags;
                    if ((flags & (TransportLine.Flags.Created | TransportLine.Flags.Temporary)) == TransportLine.Flags.Created)
                    {
                        m_lines.m_buffer[l].SimulationStep((ushort)l);
                    }
                }
                if ((Singleton<SimulationManager>.instance.m_currentFrameIndex & 4095u) == 0u)
                {
                    StatisticsManager instance = Singleton<StatisticsManager>.instance;
                    StatisticBase statisticBase = instance.Acquire<StatisticArray>(StatisticType.AveragePassengers);
                    for (int m = 0; m < 5; m++)
                    {
                        m_passengers[m].Update();
                        m_passengers[m].Reset();
                        statisticBase.Acquire<StatisticInt32>(m, 5).Set((int)(m_passengers[m].m_residentPassengers.m_averageCount + m_passengers[m].m_touristPassengers.m_averageCount));
                    }
                }
            }
            if (subStep <= 1)
            {
                int num9 = (int)(Singleton<SimulationManager>.instance.m_currentTickIndex & 1023u);
                int num10 = num9 * PrefabCollection<TransportInfo>.PrefabCount() >> 10;
                int num11 = ((num9 + 1) * PrefabCollection<TransportInfo>.PrefabCount() >> 10) - 1;
                for (int n = num10; n <= num11; n++)
                {
                    TransportInfo prefab = PrefabCollection<TransportInfo>.GetPrefab((uint)n);
                    if (prefab != null)
                    {
                        MilestoneInfo unlockMilestone = prefab.m_UnlockMilestone;
                        if (unlockMilestone != null)
                        {
                            Singleton<UnlockManager>.instance.CheckMilestone(unlockMilestone, false, false);
                        }
                    }
                }
            }
        }

        public new bool RayCast(Ray ray, float rayLength, out Vector3 hit, out ushort lineIndex, out int stopIndex, out int segmentIndex)
        {
            int num = 0;
            int num2 = 0;
            int num3 = 0;
            int num4 = 0;
            float num5 = 16f;
            float num6 = 9f;
            Vector3 vector = Vector3.zero;
            Vector3 vector2 = Vector3.zero;
            Vector3 origin = ray.origin;
            Vector3 normalized = ray.direction.normalized;
            Vector3 b = ray.origin + normalized * rayLength;
            Segment3 segment = new Segment3(origin, b);
            NetManager instance = Singleton<NetManager>.instance;
            for (int i = 1; i < 256; i++)
            {
                if ((m_lines.m_buffer[i].m_flags & (TransportLine.Flags.Created | TransportLine.Flags.Temporary)) == TransportLine.Flags.Created && m_lines.m_buffer[i].m_bounds.IntersectRay(ray))
                {
                    LineSegment[] array = m_lineSegments[i];
                    Bezier3[] array2 = m_lineCurves[i];
                    ushort stops = m_lines.m_buffer[i].m_stops;
                    ushort num7 = stops;
                    int num8 = 0;
                    while (num7 != 0)
                    {
                        Vector3 position = instance.m_nodes.m_buffer[num7].m_position;
                        float num9 = Line3.DistanceSqr(ray.direction, ray.origin - position);
                        if (num9 < num5)
                        {
                            num = i;
                            num3 = num8;
                            num5 = num9;
                            vector = position;
                        }
                        if (array.Length > num8 && array[num8].m_bounds.IntersectRay(ray))
                        {
                            int curveStart = array[num8].m_curveStart;
                            int curveEnd = array[num8].m_curveEnd;
                            for (int j = curveStart; j < curveEnd; j++)
                            {
                                Vector3 min = array2[j].Min() - new Vector3(3f, 3f, 3f);
                                Vector3 max = array2[j].Max() + new Vector3(3f, 3f, 3f);
                                Bounds bounds = default(Bounds);
                                bounds.SetMinMax(min, max);
                                if (bounds.IntersectRay(ray))
                                {
                                    float t;
                                    float num10;
                                    num9 = array2[j].DistanceSqr(segment, out t, out num10);
                                    if (num9 < num6)
                                    {
                                        num2 = i;
                                        num4 = num8;
                                        num6 = num9;
                                        vector2 = array2[j].Position(t);
                                    }
                                }
                            }
                        }
                        num7 = TransportLine.GetNextStop(num7);
                        if (num7 == stops)
                        {
                            break;
                        }
                        if (++num8 >= 32768)
                        {
                            CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                            break;
                        }
                    }
                }
            }
            if (num != 0)
            {
                hit = vector;
                lineIndex = (ushort)num;
                stopIndex = num3;
                segmentIndex = -1;
                return true;
            }
            if (num2 != 0)
            {
                hit = vector2;
                lineIndex = (ushort)num2;
                stopIndex = -1;
                segmentIndex = num4;
                return true;
            }
            hit = Vector3.zero;
            lineIndex = 0;
            stopIndex = -1;
            segmentIndex = -1;
            return false;
        }
    }
}
