using ColossalFramework.Globalization;
using ColossalFramework.UI;
using System.Collections.Generic;
using UnityEngine;

namespace CSL_Traffic.UI
{
    class RoadCustomizerGroupPanel : MonoBehaviour
    {
        private static readonly string kSubbarButtonTemplate = "SubbarButtonTemplate";
        private static readonly string kSubbarPanelTemplate = "SubbarPanelTemplate";
        static readonly string[] sm_thumbnailStates = new string[] { "Disabled", "", "Hovered", "Focused" };
        static readonly Dictionary<string, UIUtils.SpriteTextureInfo> sm_thumbnailCoords = new Dictionary<string, UIUtils.SpriteTextureInfo>()
        {        
            {"TabBackgrounds", new UIUtils.SpriteTextureInfo() {startX = 763, startY = 50, width = 60, height = 25}},
            {"Vehicle Restrictions", new UIUtils.SpriteTextureInfo() {startX = 763, startY = 0, width = 32, height = 22}},
            {"Speed Restrictions", new UIUtils.SpriteTextureInfo() {startX = 763, startY = 22, width = 32, height = 22}},
        };


        protected UITabstrip m_strip;
        protected UITextureAtlas m_atlas;
        private int m_objectIndex;

        void Awake()
        {
            this.m_strip = GetComponentInChildren<UITabstrip>();
            this.m_strip.relativePosition = new Vector3(13, -25);
            this.m_strip.startSelectedIndex = 0;
            this.m_atlas = UIUtils.LoadThumbnailsTextureAtlas("UIThumbnails");
            UIUtils.SetThumbnails("TabBg", sm_thumbnailCoords["TabBackgrounds"], this.m_atlas, sm_thumbnailStates);
            this.m_objectIndex = 0;
        }

        private void OnEnable()
        {
            this.RefreshPanel();
        }

        public void RefreshPanel()
        {
            this.PopulateGroups();
        }

        public void PopulateGroups()
        {
            this.m_objectIndex = 0;

            this.SpawnEntry("Vehicle Restrictions", null, null, "", true).stringUserData = "VehicleRestrictions";
            this.SpawnEntry("Speed Restrictions", null, null, "", true).stringUserData = "SpeedRestrictions";
        }

        protected UIButton SpawnEntry(string name, string localeID, string unlockText, string spriteBase, bool enabled)
        {
            UIButton btn;
            if (m_strip.childCount > this.m_objectIndex)
            {
                btn = (m_strip.components[this.m_objectIndex] as UIButton);
            }
            else
            {
                GameObject asGameObject = UITemplateManager.GetAsGameObject(kSubbarButtonTemplate);
                GameObject asGameObject2 = UITemplateManager.GetAsGameObject(kSubbarPanelTemplate);
                btn = m_strip.AttachUIComponent(asGameObject) as UIButton;
                //btn = m_strip.AddTab(name, asGameObject, asGameObject2, typeof(RoadCustomizerPanel)) as UIButton;
                //btn.eventClick += OnClick;
            }
            btn.isEnabled = enabled;

            btn.atlas = this.m_atlas;
            //btn.gameObject.GetComponent<TutorialUITag>().tutorialTag = name;
            string text = spriteBase + name;
            UIUtils.SetThumbnails(text, sm_thumbnailCoords[text], this.m_atlas);
            btn.normalFgSprite = text;
            btn.focusedFgSprite = text;// +"Focused";
            btn.hoveredFgSprite = text;// +"Hovered";
            btn.pressedFgSprite = text;// +"Pressed";
            btn.disabledFgSprite = text;// +"Disabled";

            btn.normalBgSprite = "TabBg";
            btn.focusedBgSprite = "TabBg" + "Focused";
            btn.hoveredBgSprite = btn.pressedBgSprite = "TabBg" + "Hovered";
            btn.disabledBgSprite = "TabBg" + "Disabled";
            
            if (!string.IsNullOrEmpty(localeID) && !string.IsNullOrEmpty(unlockText))
            {
                btn.tooltip = Locale.Get(localeID, name) + " - " + unlockText;
            }
            else if (!string.IsNullOrEmpty(localeID))
            {
                btn.tooltip = Locale.Get(localeID, name);
            }
            this.m_objectIndex++;
            return btn;
        }

        protected void OnClick(UIComponent comp, UIMouseEventParameter p)
        {
            p.Use();
            UIButton uIButton = comp as UIButton;
            if (uIButton != null && uIButton.parent == this.m_strip)
            {
                
            }
        }
    }
}
