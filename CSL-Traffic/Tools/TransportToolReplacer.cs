using UnityEngine;

namespace CSL_Traffic
{
    class TransportToolReplacer : MonoBehaviour
    {
        void Update()
        {
            //FineRoadHeightsLoadingExtension.ReplacePanels();
            TransportTool transportTool = ToolsModifierControl.GetCurrentTool<TransportTool>();
            if (transportTool == null)
                return;
            CustomTransportTool customTransportTool = ToolsModifierControl.SetTool<CustomTransportTool>();
            if (customTransportTool == null)
                return;
            customTransportTool.m_prefab = transportTool.m_prefab;
        }
    }
}
