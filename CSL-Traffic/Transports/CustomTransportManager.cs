using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using CSL_Traffic.Extensions;
using ColossalFramework;

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
                return (ushort[])this.fi_lineNumber.GetValue(this);
            }
            set
            {
                this.fi_lineNumber.SetValue(this, value);
            }
        }
        private bool m_linesVisible
        {
            get
            {
                return (bool)this.fi_linesVisible.GetValue(this);
            }
            set
            {
                this.fi_linesVisible.SetValue(this, value);
            }
        }
        private Camera m_undergroundCamera
        {
            get
            {
                return (Camera)this.fi_undergroundCamera.GetValue(this);
            }
            set
            {
                this.fi_undergroundCamera.SetValue(this, value);
            }
        }
        private TransportPatch[] m_patches
        {
            get
            {
                return (TransportPatch[])this.fi_patches.GetValue(this);
            }
            set
            {
                this.fi_patches.SetValue(this, value);
            }
        }
        private bool m_patchesDirty
        {
            get
            {
                return (bool)this.fi_patchesDirty.GetValue(this);
            }
            set
            {
                this.fi_patchesDirty.SetValue(this, value);
            }
        }

        protected override void Awake()
        {
            base.Awake();

            Type transportManagerType = typeof(TransportManager);
            this.fi_lineNumber = transportManagerType.GetFieldByName("m_lineNumber");
            this.fi_linesVisible = transportManagerType.GetFieldByName("m_linesVisible");
            this.fi_undergroundCamera = transportManagerType.GetFieldByName("m_undergroundCamera");
            this.fi_patches = transportManagerType.GetFieldByName("m_patches");
            this.fi_patchesDirty = transportManagerType.GetFieldByName("m_patchesDirty");
        }

        public void SetOriginalValues(TransportManager originalTransportManager)
        {
            this.m_simulationProfiler = originalTransportManager.m_simulationProfiler;
            this.m_drawCallData = originalTransportManager.m_drawCallData;
            this.m_properties = originalTransportManager.m_properties;
            this.m_lineCount = originalTransportManager.m_lineCount;
            this.m_infoCount = originalTransportManager.m_infoCount;
            this.m_passengers = originalTransportManager.m_passengers;
            this.m_lineNumber = (ushort[])this.fi_lineNumber.GetValue(originalTransportManager);
            this.m_linesVisible = (bool)this.fi_linesVisible.GetValue(originalTransportManager);
            this.m_undergroundCamera = (Camera)this.fi_undergroundCamera.GetValue(originalTransportManager);
            this.m_patches = (TransportPatch[])this.fi_patches.GetValue(originalTransportManager);
            this.m_patchesDirty = (bool)this.fi_patchesDirty.GetValue(originalTransportManager);
        }

        protected override void SimulationStepImpl(int subStep)
        {
            if (this.m_linesUpdated)
            {
                this.m_linesUpdated = false;
                int num = this.m_updatedLines.Length;
                for (int i = 0; i < num; i++)
                {
                    ulong num2 = this.m_updatedLines[i];
                    if (num2 != 0uL)
                    {
                        for (int j = 0; j < 64; j++)
                        {
                            if ((num2 & 1uL << j) != 0uL)
                            {
                                ushort num3 = (ushort)(i << 6 | j);
                                if (this.m_lines.m_buffer[(int)num3].m_flags != TransportLine.Flags.None)
                                {
                                    if (BusTransportLineAI.UpdatePaths(this.m_lines.m_buffer[(int)num3], num3) && BusTransportLineAI.UpdateMeshData(this.m_lines.m_buffer[(int)num3], num3))
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
                        this.m_updatedLines[i] = num2;
                        if (num2 != 0uL)
                        {
                            this.m_linesUpdated = true;
                        }
                    }
                }
            }
            if (this.m_patchesDirty)
            {
                this.m_patchesDirty = false;
                int num4 = this.m_patches.Length;
                for (int k = 0; k < num4; k++)
                {
                    TransportPatch transportPatch = this.m_patches[k];
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
                    TransportLine.Flags flags = this.m_lines.m_buffer[l].m_flags;
                    if ((flags & (TransportLine.Flags.Created | TransportLine.Flags.Temporary)) == TransportLine.Flags.Created)
                    {
                        this.m_lines.m_buffer[l].SimulationStep((ushort)l);
                    }
                }
                if ((Singleton<SimulationManager>.instance.m_currentFrameIndex & 4095u) == 0u)
                {
                    StatisticsManager instance = Singleton<StatisticsManager>.instance;
                    StatisticBase statisticBase = instance.Acquire<StatisticArray>(StatisticType.AveragePassengers);
                    for (int m = 0; m < 5; m++)
                    {
                        this.m_passengers[m].Update();
                        this.m_passengers[m].Reset();
                        statisticBase.Acquire<StatisticInt32>(m, 5).Set((int)(this.m_passengers[m].m_residentPassengers.m_averageCount + this.m_passengers[m].m_touristPassengers.m_averageCount));
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

    }
}
