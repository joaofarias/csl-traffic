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
            m_strip = GetComponentInChildren<UITabstrip>();
            m_strip.relativePosition = new Vector3(13, -25);
            m_strip.startSelectedIndex = 0;
            m_atlas = UIUtils.LoadThumbnailsTextureAtlas("UIThumbnails");
            UIUtils.SetThumbnails("TabBg", sm_thumbnailCoords["TabBackgrounds"], m_atlas, sm_thumbnailStates);
            m_objectIndex = 0;
        }

        private void OnEnable()
        {
            RefreshPanel();
        }

        public void RefreshPanel()
        {
            PopulateGroups();
        }

        public void PopulateGroups()
        {
            m_objectIndex = 0;

            SpawnEntry("Vehicle Restrictions", null, null, "", true).stringUserData = "VehicleRestrictions";
            SpawnEntry("Speed Restrictions", null, null, "", true).stringUserData = "SpeedRestrictions";
        }

        protected UIButton SpawnEntry(string name, string localeID, string unlockText, string spriteBase, bool enabled)
        {
            UIButton btn;
            if (m_strip.childCount > m_objectIndex)
            {
                btn = (m_strip.components[m_objectIndex] as UIButton);
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

            btn.atlas = m_atlas;
            //btn.gameObject.GetComponent<TutorialUITag>().tutorialTag = name;
            string text = spriteBase + name;
            UIUtils.SetThumbnails(text, sm_thumbnailCoords[text], m_atlas);
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
            m_objectIndex++;
            return btn;
        }

        protected void OnClick(UIComponent comp, UIMouseEventParameter p)
        {
            p.Use();
            UIButton uIButton = comp as UIButton;
            if (uIButton != null && uIButton.parent == m_strip)
            {
                
            }
        }
    }
}
