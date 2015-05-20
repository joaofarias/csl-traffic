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
		public enum Panel
		{
			Unset,

			VehicleRestrictions,
			SpeedRestrictions
		}

		private static readonly string kItemTemplate = "PlaceableItemTemplate";
		static readonly string[] sm_speedThumbnailStates = new string[] { "Disabled", "", "Hovered", "Focused" };
		static readonly string[] sm_vehicleThumbnailStates = new string[] { "Disabled", "Deselected", "90%", "", "80%" };
		static readonly Dictionary<string, UIUtils.SpriteTextureInfo> sm_thumbnailCoords = new Dictionary<string, UIUtils.SpriteTextureInfo>()
        {        
            {"Emergency", new UIUtils.SpriteTextureInfo() {startX = 0, startY = 0, width = 109, height = 75}},
			{"Hearse", new UIUtils.SpriteTextureInfo() {startX = 0, startY = 75, width = 109, height = 75}},
			{"GarbageTruck", new UIUtils.SpriteTextureInfo() {startX = 0, startY = 150, width = 109, height = 75}},
			{"CargoTruck", new UIUtils.SpriteTextureInfo() {startX = 0, startY = 225, width = 109, height = 75}},
			{"Bus", new UIUtils.SpriteTextureInfo() {startX = 0, startY = 300, width = 109, height = 75}},
			{"PassengerCar", new UIUtils.SpriteTextureInfo() {startX = 0, startY = 375, width = 109, height = 75}},

			{"SpeedSignBackground", new UIUtils.SpriteTextureInfo() {startX = 545, startY = 375, width = 109, height = 100}},
			{"15", new UIUtils.SpriteTextureInfo() {startX = 654, startY = 75, width = 109, height = 75}},
			{"30", new UIUtils.SpriteTextureInfo() {startX = 872, startY = 75, width = 109, height = 75}},
			{"40", new UIUtils.SpriteTextureInfo() {startX = 545, startY = 150, width = 109, height = 75}},
			{"50", new UIUtils.SpriteTextureInfo() {startX = 654, startY = 150, width = 109, height = 75}},
			{"70", new UIUtils.SpriteTextureInfo() {startX = 872, startY = 150, width = 109, height = 75}},
			{"90", new UIUtils.SpriteTextureInfo() {startX = 654, startY = 225, width = 109, height = 75}},
			{"100", new UIUtils.SpriteTextureInfo() {startX = 763, startY = 225, width = 109, height = 75}},
			{"120", new UIUtils.SpriteTextureInfo() {startX = 545, startY = 300, width = 109, height = 75}},
			{"140", new UIUtils.SpriteTextureInfo() {startX = 763, startY = 300, width = 109, height = 75}},
			
        };

		public UITextureAtlas m_atlas;
		private UIScrollablePanel m_scrollablePanel;
		private int m_objectIndex, m_selectedIndex;
		private Panel m_panelType;


		private void Awake()
		{
			this.m_atlas = UIUtils.LoadThumbnailsTextureAtlas("UIThumbnails");
			this.m_scrollablePanel = GetComponentInChildren<UIScrollablePanel>();
			this.m_scrollablePanel.autoLayoutStart = LayoutStart.TopLeft;
			this.m_objectIndex = m_selectedIndex = 0;
			this.m_panelType = Panel.Unset;
		}

		private void OnEnable()
		{
			if (m_panelType == Panel.Unset)
				return;

			this.RefreshPanel();
			RoadCustomizerTool rct = ToolsModifierControl.GetTool<RoadCustomizerTool>();
			if (rct != null)
			{
				rct.OnStartLaneCustomization += EnableIcons;
				rct.OnEndLaneCustomization += DisableIcons;
			}
		}

		private void OnDisable()
		{
			RoadCustomizerTool rct = ToolsModifierControl.GetTool<RoadCustomizerTool>();
			if (rct != null)
			{
				if (m_panelType == Panel.VehicleRestrictions)
				{
					rct.OnStartLaneCustomization -= EnableIcons;
					rct.OnEndLaneCustomization -= DisableIcons;
				}
			}
		}

		void Update()
		{
			if (this.m_panelType == Panel.SpeedRestrictions && this.m_scrollablePanel.isVisible)
			{
				(this.m_scrollablePanel.components[m_selectedIndex] as UIButton).state = UIButton.ButtonState.Focused;
			}
		}

		public void SetPanel(Panel panel)
		{
			this.m_panelType = panel;
			OnEnable();
		}

		void EnableIcons()
		{
			RoadCustomizerTool rct = ToolsModifierControl.GetCurrentTool<RoadCustomizerTool>();
			if (rct != null)
			{
				RoadManager.VehicleType restrictions = rct.GetCurrentRestrictions();
				//if (restrictions == RoadManager.VehicleType.None)
				//{
				//	DisableIcons();
				//	return;
				//}

				for (int i = 0; i < this.m_scrollablePanel.components.Count; i++)
				{
					UIButton btn = this.m_scrollablePanel.components[i] as UIButton;

					if (this.m_panelType == Panel.VehicleRestrictions)
					{
						RoadManager.VehicleType vehicleType = (RoadManager.VehicleType)btn.objectUserData;

						if ((vehicleType & restrictions) == vehicleType)
						{
							btn.stringUserData = "Selected";
							btn.normalFgSprite = btn.name;
							btn.focusedFgSprite = btn.name;
							btn.hoveredFgSprite = btn.name + "90%";
							btn.pressedFgSprite = btn.name + "80%";
						}
						else
						{
							btn.stringUserData = null;
							btn.normalFgSprite = btn.name + "Deselected";
							btn.focusedFgSprite = btn.name + "Deselected";
							btn.hoveredFgSprite = btn.name + "80%";
							btn.pressedFgSprite = btn.name + "90%";
						}
						btn.state = UIButton.ButtonState.Normal;
					}

					btn.isEnabled = true;
				}
			}

		}

		void DisableIcons()
		{
			for (int i = 0; i < this.m_scrollablePanel.components.Count; i++)
			{
				UIButton btn = this.m_scrollablePanel.components[i] as UIButton;
				btn.state = UIButton.ButtonState.Disabled;
				btn.isEnabled = false;
			}
		}

		public void RefreshPanel()
		{
			this.PopulateAssets();
		}

		public void PopulateAssets()
		{
			this.m_objectIndex = 0;
			if (this.m_panelType == Panel.VehicleRestrictions)
			{
				this.SpawnEntry("PassengerCar", "PassengerCar", null, null, false, false).objectUserData = RoadManager.VehicleType.PassengerCar;
				this.SpawnEntry("Bus", "Bus", null, null, false, false).objectUserData = RoadManager.VehicleType.Bus;
				this.SpawnEntry("CargoTruck", "CargoTruck", null, null, false, false).objectUserData = RoadManager.VehicleType.CargoTruck;
				this.SpawnEntry("GarbageTruck", "GarbageTruck", null, null, false, false).objectUserData = RoadManager.VehicleType.GarbageTruck;
				this.SpawnEntry("Hearse", "Hearse", null, null, false, false).objectUserData = RoadManager.VehicleType.Hearse;
				this.SpawnEntry("Emergency", "Emergency", null, null, false, false).objectUserData = RoadManager.VehicleType.Emergency;
			}
			else if (this.m_panelType == Panel.SpeedRestrictions)
			{
				this.SpawnEntry("15", "15 Km/h", null, null, false, true).objectUserData = 15;
				this.SpawnEntry("30", "30 Km/h", null, null, false, true).objectUserData = 30;
				this.SpawnEntry("40", "40 Km/h", null, null, false, true).objectUserData = 40;
				this.SpawnEntry("50", "50 Km/h", null, null, false, true).objectUserData = 50;
				this.SpawnEntry("70", "70 Km/h", null, null, false, true).objectUserData = 70;
				this.SpawnEntry("90", "90 Km/h", null, null, false, true).objectUserData = 90;
				this.SpawnEntry("100", "100 Km/h", null, null, false, true).objectUserData = 100;
				this.SpawnEntry("120", "120 Km/h", null, null, false, true).objectUserData = 120;
				this.SpawnEntry("140", "140 Km/h", null, null, false, true).objectUserData = 140;
			}

			UIScrollbar scrollbar = this.m_scrollablePanel.GetComponentInChildren<UIScrollbar>();
			if (scrollbar != null)
				scrollbar.incrementAmount = 109;
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
			UIButton btn;
			if (this.m_scrollablePanel.childCount > this.m_objectIndex)
			{
				btn = (this.m_scrollablePanel.components[this.m_objectIndex] as UIButton);
			}
			else
			{
				GameObject asGameObject = UITemplateManager.GetAsGameObject(RoadCustomizerPanel.kItemTemplate);
				btn = (this.m_scrollablePanel.AttachUIComponent(asGameObject) as UIButton);
				btn.eventClick += OnClick;
			}
			btn.gameObject.GetComponent<TutorialUITag>().tutorialTag = name;
			btn.text = string.Empty;
			btn.name = name;
			btn.tooltipAnchor = UITooltipAnchor.Anchored;
			btn.tabStrip = true;
			btn.horizontalAlignment = UIHorizontalAlignment.Center;
			btn.verticalAlignment = UIVerticalAlignment.Middle;
			btn.pivot = UIPivotPoint.TopCenter;
			if (atlas != null)
			{
				btn.atlas = atlas;
				switch (m_panelType)
				{
					case Panel.VehicleRestrictions:
						SetVehicleButtonsThumbnails(btn);
						break;
					case Panel.SpeedRestrictions:
						UIUtils.SetThumbnails("SpeedSignBackground", sm_thumbnailCoords["SpeedSignBackground"], atlas, sm_speedThumbnailStates);
						SetSpeedButtonsThumbnails(btn);
						break;
					default:
						break;
				}

			}
			if (index != -1)
			{
				btn.zOrder = index;
			}
			btn.verticalAlignment = UIVerticalAlignment.Bottom;
			btn.foregroundSpriteMode = UIForegroundSpriteMode.Fill;

			UIComponent uIComponent = (btn.childCount <= 0) ? null : btn.components[0];
			if (uIComponent != null)
			{
				uIComponent.isVisible = false;
			}
			btn.isEnabled = enabled;
			btn.state = UIButton.ButtonState.Disabled;
			btn.tooltip = tooltip;
			btn.tooltipBox = tooltipBox;
			btn.group = grouped ? this.m_scrollablePanel : null;
			this.m_objectIndex++;
			return btn;
		}

		protected void SetVehicleButtonsThumbnails(UIButton btn)
		{
			string iconName = btn.name;
			UIUtils.SetThumbnails(iconName, sm_thumbnailCoords[iconName], btn.atlas, sm_vehicleThumbnailStates);

			btn.normalFgSprite = iconName;
			btn.focusedFgSprite = iconName;
			btn.hoveredFgSprite = iconName;
			btn.pressedFgSprite = iconName;
			btn.disabledFgSprite = iconName + "Disabled";

			btn.eventMouseEnter += (UIComponent comp, UIMouseEventParameter p) =>
			{
				if (btn.state == UIButton.ButtonState.Focused)
				{
					if (String.IsNullOrEmpty(btn.stringUserData))
						btn.focusedFgSprite = iconName + "80%";
					else
						btn.focusedFgSprite = iconName + "90%";
				}
			};

			btn.eventMouseLeave += (UIComponent comp, UIMouseEventParameter p) =>
			{
				if (btn.state == UIButton.ButtonState.Focused)
				{
					if (String.IsNullOrEmpty(btn.stringUserData))
						btn.focusedFgSprite = iconName + "Deselected";
					else
						btn.focusedFgSprite = iconName;
				}
			};

			btn.eventMouseDown += (UIComponent comp, UIMouseEventParameter p) =>
			{
				if (btn.state == UIButton.ButtonState.Focused)
				{
					if (String.IsNullOrEmpty(btn.stringUserData))
						btn.focusedFgSprite = iconName + "90%";
					else
						btn.focusedFgSprite = iconName + "80%";
				}
			};

		}

		protected void SetSpeedButtonsThumbnails(UIButton btn)
		{
			string iconName = btn.name;
			UIUtils.SetThumbnails(iconName, sm_thumbnailCoords[iconName], btn.atlas);

			btn.normalBgSprite = "SpeedSignBackground";
			btn.disabledBgSprite = "SpeedSignBackgroundDisabled";
			btn.focusedBgSprite = "SpeedSignBackgroundFocused";
			btn.hoveredBgSprite = btn.pressedBgSprite = "SpeedSignBackgroundHovered";

			btn.normalFgSprite = iconName;
			btn.focusedFgSprite = iconName;
			btn.hoveredFgSprite = iconName;
			btn.pressedFgSprite = iconName;
			btn.disabledFgSprite = iconName;
		}

		protected void OnButtonClicked(UIButton btn)
		{
			if (m_panelType == Panel.VehicleRestrictions)
			{
				RoadManager.VehicleType vehicleType = (RoadManager.VehicleType)btn.objectUserData;
				if (vehicleType != RoadManager.VehicleType.None)
				{
					RoadCustomizerTool rct = ToolsModifierControl.GetCurrentTool<RoadCustomizerTool>();
					if (rct != null)
						rct.ToggleRestriction(vehicleType);

					if (String.IsNullOrEmpty(btn.stringUserData))
					{
						btn.stringUserData = "Selected";
						btn.normalFgSprite = btn.name;
						btn.focusedFgSprite = btn.name;
						btn.hoveredFgSprite = btn.name + "90%";
						btn.pressedFgSprite = btn.name + "80%";
					}
					else
					{
						btn.stringUserData = null;
						btn.normalFgSprite = btn.name + "Deselected";
						btn.focusedFgSprite = btn.name + "Deselected";
						btn.hoveredFgSprite = btn.name + "80%";
						btn.pressedFgSprite = btn.name + "90%";
					}
				}
			}
		}

		protected void OnClick(UIComponent comp, UIMouseEventParameter p)
		{
			p.Use();
			UIButton uIButton = comp as UIButton;
			if (uIButton != null && uIButton.parent == this.m_scrollablePanel)
			{
				this.OnButtonClicked(uIButton);
				this.m_selectedIndex = this.m_scrollablePanel.components.IndexOf(uIButton);
			}
		}
	}
}
