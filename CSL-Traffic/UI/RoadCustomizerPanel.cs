using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CSL_Traffic.UI
{
    class RoadCustomizerPanel : MonoBehaviour
    {
        private static readonly string kItemTemplate = "PlaceableItemTemplate";
        public UITextureAtlas m_atlas;
        private UIScrollablePanel m_scrollablePanel;
        private int m_objectIndex;

        private void Awake()
        {            
            this.m_scrollablePanel = base.GetComponent<UIScrollablePanel>();
            this.m_objectIndex = -1;
        }

        private void OnEnable()
        {
            this.RefreshPanel();
        }

        private void Update()
        {
            // update selected options
        }

        public void RefreshPanel()
        {
            this.PopulateAssets();
        }

        public void PopulateAssets()
        {
            this.m_objectIndex = 0;
            foreach (RoadManager.VehicleType vehicleType in Enum.GetValues(typeof(RoadManager.VehicleType)))
            {
                this.SpawnEntry(vehicleType.ToString(), "Default Tooltip", null, null, true, false).objectUserData = vehicleType;
            }
        }

        protected UIButton SpawnEntry(string name, string tooltip, string thumbnail, UITextureAtlas atlas, bool enabled, bool grouped)
        {
            if (atlas == null)
            {
                atlas = this.m_atlas;
            }
            if (string.IsNullOrEmpty(thumbnail) || atlas[thumbnail] == null)
            {
                thumbnail = "ThumbnailBuildingDefault";
            }
            return this.CreateButton(name, tooltip, name, -1, atlas, null, enabled, grouped);
        }

        protected UIButton CreateButton(string name, string tooltip, string baseIconName, int index, UITextureAtlas atlas, UIComponent tooltipBox, bool enabled, bool grouped)
        {
            UIButton uIButton;
            if (this.m_scrollablePanel.childCount > this.m_objectIndex)
            {
                uIButton = (this.m_scrollablePanel.components[this.m_objectIndex] as UIButton);
            }
            else
            {
                GameObject asGameObject = UITemplateManager.GetAsGameObject(RoadCustomizerPanel.kItemTemplate);
                uIButton = (this.m_scrollablePanel.AttachUIComponent(asGameObject) as UIButton);
            }
            uIButton.gameObject.GetComponent<TutorialUITag>().tutorialTag = name;
            uIButton.text = name;//string.Empty;
            uIButton.name = name;
            uIButton.tooltipAnchor = UITooltipAnchor.Anchored;
            uIButton.tabStrip = true;
            uIButton.horizontalAlignment = UIHorizontalAlignment.Center;
            uIButton.verticalAlignment = UIVerticalAlignment.Middle;
            uIButton.pivot = UIPivotPoint.TopCenter;
            if (atlas != null)
            {
                uIButton.atlas = atlas;
            }
            if (index != -1)
            {
                uIButton.zOrder = index;
            }
            uIButton.verticalAlignment = UIVerticalAlignment.Bottom;
            uIButton.foregroundSpriteMode = UIForegroundSpriteMode.Fill;
            uIButton.normalFgSprite = baseIconName;
            uIButton.focusedFgSprite = baseIconName + "Focused";
            uIButton.hoveredFgSprite = baseIconName + "Hovered";
            uIButton.pressedFgSprite = baseIconName + "Pressed";
            uIButton.disabledFgSprite = baseIconName + "Disabled";
            UIComponent uIComponent = (uIButton.childCount <= 0) ? null : uIButton.components[0];
            if (uIComponent != null)
            {
                uIComponent.isVisible = false;
            }
            uIButton.isEnabled = enabled;
            uIButton.tooltip = tooltip;
            uIButton.tooltipBox = tooltipBox;
            uIButton.group = grouped ? this.m_scrollablePanel : null;
            this.m_objectIndex++;
            return uIButton;
        }

        protected void OnButtonClicked(UIComponent comp)
        {
            //object objectUserData = comp.objectUserData;
            RoadManager.VehicleType vehicleType = (RoadManager.VehicleType)Enum.Parse(typeof(RoadManager.VehicleType), comp.stringUserData);

            if (vehicleType != RoadManager.VehicleType.None)
            {
                //RoadManager.ToggleVehicleRestriction(vehicleType);
            }
            
            
            
            //NetInfo netInfo = objectUserData as NetInfo;
            //BuildingInfo buildingInfo = objectUserData as BuildingInfo;
            //if (netInfo != null)
            //{
            //    if (base.roadsOptionPanel != null)
            //    {
            //        base.roadsOptionPanel.Show();
            //    }
            //    NetTool netTool = ToolsModifierControl.SetTool<NetTool>();
            //    if (netTool != null)
            //    {
            //        netTool.m_prefab = netInfo;
            //    }
            //}
            //if (buildingInfo != null)
            //{
            //    if (base.roadsOptionPanel != null)
            //    {
            //        base.roadsOptionPanel.Hide();
            //    }
            //    BuildingTool buildingTool = ToolsModifierControl.SetTool<BuildingTool>();
            //    if (buildingTool != null)
            //    {
            //        buildingTool.m_prefab = buildingInfo;
            //        buildingTool.m_relocate = 0;
            //    }
            //}
        }

        protected void OnClick(UIComponent comp, UIMouseEventParameter p)
        {
            UIButton uIButton = p.source as UIButton;
            if (uIButton != null && uIButton.parent == this.m_scrollablePanel)
            {
                //int zOrder = uIButton.zOrder;
                //this.m_LastSelectedIndex = zOrder;
                this.OnButtonClicked(uIButton);
                //this.SelectByIndex(zOrder);
            }
        }
    }
}
