using ColossalFramework.DataBinding;
using ColossalFramework.UI;
using ICities;
using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using UnityEngine;

namespace CSL_Traffic
{
    public class OptionsManager : MonoBehaviour
    {
        [Flags]
        public enum ModOptions : long
        {
            None = 0,
            AllowTrucksInPedestrianRoads = 1,
            AllowResidentsInPedestrianRoads = 2,
            DisableCentralLaneOnPedestrianRoads = 4,
            UseRealisticSpeeds = 8,
            NoDespawn = 16,
            ImprovedAI = 32,
            DisableCustomRoads = 64,
            //noStopForCrossing = 64,

            // bits 55 to 62 reserved for beta tests that won't have their own option
            //BetaTestNewRoads = 1L << 54,
            BetaTestRoadCustomizerTool = 1L << 55,
            //BetaTest3 = 1L << 56,
            //BetaTest4 = 1L << 57,
            //BetaTest5 = 1L << 58,
            //BetaTest6 = 1L << 59,
            //BetaTest7 = 1L << 60,
            //BetaTest8 = 1L << 61,

            FixCargoTrucksNotSpawning = 1L << 61,

            GhostMode = 1L << 62
        }

        //GameObject m_optionsButtonGo;
        //GameObject m_optionsPanel;
        //GameObject m_optionsList;
        //GameObject m_checkboxTemplate;


        UICheckBox m_allowTrucksCheckBox = null;
        UICheckBox m_allowResidentsCheckBox = null;
        UICheckBox m_disableCentralLaneCheckBox = null;
        UICheckBox m_realisticSpeedsCheckBox = null;
        UICheckBox m_noDespawnCheckBox = null;
        UICheckBox m_improvedAICheckBox = null;
        UICheckBox m_disableCustomRoadsCheckBox = null;
        UICheckBox m_betaTestRoadCustomizerCheckBox = null;
        UICheckBox m_ghostModeCheckBox = null;
        //UICheckBox m_noStopForCrossing = null;

        //bool m_initialized;

        void Awake()
        {
            DontDestroyOnLoad(this);
        }

        public void CreateSettings(UIHelperBase helper)
        {
            UIHelperBase group = helper.AddGroup("Traffic++ Options");
            m_disableCustomRoadsCheckBox = group.AddCheckbox("Disable Custom Roads", false, IgnoreMe) as UICheckBox;
            m_allowTrucksCheckBox = group.AddCheckbox("Allow Trucks in Pedestrian Roads", false, IgnoreMe) as UICheckBox;
            m_allowResidentsCheckBox = group.AddCheckbox("Allow Residents in Pedestrian Roads", false, IgnoreMe) as UICheckBox;
            m_disableCentralLaneCheckBox = group.AddCheckbox("Disable Central Lane on Pedestrian Roads", false, IgnoreMe) as UICheckBox;
            m_noDespawnCheckBox = group.AddCheckbox("No Despawn by CBeTHaX", false, IgnoreMe) as UICheckBox;
            m_realisticSpeedsCheckBox = group.AddCheckbox("Beta Test: Realistic Speeds", false, IgnoreMe) as UICheckBox;
            m_betaTestRoadCustomizerCheckBox = group.AddCheckbox("Beta Test: Road Customizer Tool", false, IgnoreMe) as UICheckBox;
            m_improvedAICheckBox = group.AddCheckbox("Beta Test: Improved AI", false, IgnoreMe) as UICheckBox;
            m_ghostModeCheckBox = group.AddCheckbox("Ghost Mode (disables all mod functionality leaving only enough logic to load the map)", false, IgnoreMe) as UICheckBox;

            group.AddButton("Save", OnSave);

            LoadOptions();
        }

        private void IgnoreMe(bool c)
        {
            // The addCheckbox methods above require an event
            // This is temporary as the options panel will be reworked in future release
        }


        //        void Start()
        //        {
        //#if DEBUG
        //            //foreach (var item in GameObject.FindObjectsOfType<GameObject>())
        //            //{
        //            //	if (item.transform.parent == null)
        //            //		Initializer.PrintGameObjects(item, "MainMenuScene_110b.txt");
        //            //}
        //#endif

        //            GameObject contentManager = GameObject.Find("(Library) ContentManagerPanel");
        //            if (contentManager == null)
        //                return;

        //            Transform mods = contentManager.transform.GetChild(0).FindChild("Mods");
        //            if (mods == null)
        //            {
        //                //Logger.LogInfo("Can't find mods");
        //                return;
        //            }

        //            UILabel modLabel = mods.GetComponentsInChildren<UILabel>().FirstOrDefault(l => l.text.Contains("jfarias") || l.text.Contains("Traffic++"));
        //            if (modLabel == null)
        //            {
        //                //Logger.LogInfo("Can't find label");
        //                return;
        //            }

        //            GameObject mod = modLabel.transform.parent.gameObject;
        //            UIButton shareButton = mod.GetComponentsInChildren<UIButton>(true).FirstOrDefault(b => b.name == "Share");
        //            if (shareButton == null)
        //                return;

        //            //// Options Button
        //            //Transform shareButtonTransform = mod.transform.FindChild("Share");
        //            //if (shareButtonTransform == null)
        //            //{
        //            //	//Logger.LogInfo("Can't find share");
        //            //	return;
        //            //}

        //            //UIButton shareButton = shareButtonTransform.gameObject.GetComponent<UIButton>();
        //            this.m_optionsButtonGo = Instantiate<GameObject>(shareButton.gameObject);
        //            this.m_optionsButtonGo.name = "Options";
        //            UIButton optionsButton = mod.GetComponent<UIPanel>().AttachUIComponent(this.m_optionsButtonGo) as UIButton;
        //            this.m_optionsButtonGo.transform.localPosition = shareButton.transform.localPosition;

        //            optionsButton.isVisible = true;
        //            optionsButton.text = "Options";
        //            optionsButton.eventClick += OpenOptionsPanel;
        //            optionsButton.position += Vector3.right * (optionsButton.width * 1.1f);

        //            // Options Panel
        //            GameObject optionsPanel = GameObject.Find("(Library) OptionsPanel");
        //            this.m_optionsPanel = Instantiate<GameObject>(optionsPanel);
        //            this.m_optionsPanel.transform.SetParent(GameObject.Find("(Library) ContentManagerPanel").transform);
        //            GameObject.Destroy(this.m_optionsPanel.GetComponent<OptionsPanel>());

        //            m_checkboxTemplate = this.m_optionsPanel.GetComponentsInChildren<UICheckBox>().FirstOrDefault(c => c.name == "EdgeScrolling").gameObject;
        //            GameObject.Destroy(m_checkboxTemplate.GetComponent<BindProperty>());

        //            // clear panel but keep title
        //            GameObject caption = null;
        //            foreach (Transform transform in m_optionsPanel.transform)
        //            {
        //                if (transform.name == "Caption")
        //                    caption = transform.gameObject;
        //                else
        //                    GameObject.Destroy(transform.gameObject);
        //            }

        //            this.m_optionsPanel.GetComponent<UIPanel>().autoFitChildrenVertically = true;
        //            this.m_optionsPanel.GetComponent<UIPanel>().autoFitChildrenHorizontally = true;

        //            // set caption
        //            caption.transform.FindChild("Label").GetComponent<UILabel>().text = "Traffic++ Options";

        //            // clear close event
        //            UIButton closeButton = caption.transform.FindChild("Close").GetComponent<UIButton>();
        //            GameObject.Destroy(closeButton.GetComponent<BindEvent>());
        //            closeButton.eventClick += CloseOptionsPanel;

        //            // set options list
        //            m_optionsList = Instantiate<GameObject>(mod.transform.parent.gameObject);
        //            for (int i = m_optionsList.transform.childCount - 1; i >= 0; i--)
        //            {
        //                Destroy(m_optionsList.transform.GetChild(i).gameObject);
        //            }
        //            m_optionsList.transform.SetParent(this.m_optionsPanel.transform);
        //            m_optionsList.GetComponent<UIScrollablePanel>().AlignTo(this.m_optionsPanel.GetComponent<UIPanel>(), UIAlignAnchor.TopLeft);
        //            m_optionsList.GetComponent<UIScrollablePanel>().position += new Vector3(caption.transform.FindChild("Label").GetComponent<UILabel>().height, -caption.transform.FindChild("Label").GetComponent<UILabel>().height * 2f);

        //            // save button
        //            GameObject save = Instantiate<GameObject>(this.m_optionsButtonGo);
        //            save.transform.SetParent(this.m_optionsPanel.transform);

        //            UIButton saveButton = save.GetComponent<UIButton>();
        //            saveButton.isVisible = true;
        //            saveButton.eventClick += OnSave;
        //            saveButton.color = Color.green;
        //            saveButton.text = "Save";
        //            saveButton.AlignTo(m_optionsPanel.GetComponent<UIPanel>(), UIAlignAnchor.BottomRight);
        //            Vector3 cornerOffset = new Vector3(saveButton.width * -0.1f, saveButton.height * 0.3f);
        //            saveButton.position += cornerOffset;

        //            // add options
        //            m_allowTrucksCheckBox = AddOptionCheckbox("Allow Trucks in Pedestrian Roads", 0);
        //            m_allowResidentsCheckBox = AddOptionCheckbox("Allow Residents in Pedestrian Roads", 1);
        //            m_disableCentralLaneCheckBox = AddOptionCheckbox("Disable Central Lane on Pedestrian Roads", 2);
        //            m_noDespawnCheckBox = AddOptionCheckbox("No Despawn by CBeTHaX", 3);
        //            m_realisticSpeedsCheckBox = AddOptionCheckbox("Beta Test: Realistic Speeds", 4);
        //            m_betaTestRoadCustomizerCheckBox = AddOptionCheckbox("Beta Test: Road Customizer Tool", 5);
        //            m_improvedAICheckBox = AddOptionCheckbox("Beta Test: Improved AI", 6);
        //            //m_noStopForCrossing = AddOptionCheckbox("Beta Test: Cars drive trough pedestrian crossings", 7);

        //            m_ghostModeCheckBox = AddOptionCheckbox("Ghost Mode (disables all mod functionality leaving only enough logic to load the map)");
        //            m_ghostModeCheckBox.gameObject.transform.SetParent(m_optionsPanel.transform);
        //            m_ghostModeCheckBox.AlignTo(m_optionsPanel.GetComponent<UIPanel>(), UIAlignAnchor.BottomLeft);
        //            m_ghostModeCheckBox.position += new Vector3(-cornerOffset.x, cornerOffset.y);
        //            m_ghostModeCheckBox.width -= saveButton.width;

        //            LoadOptions();

        //            m_initialized = true;
        //        }

        //UICheckBox AddOptionCheckbox(string text, int zOrder = -1)
        //{
        //    GameObject newCheckbox = Instantiate<GameObject>(m_checkboxTemplate);
        //    newCheckbox.transform.SetParent(m_optionsList.transform);

        //    UICheckBox checkBox = newCheckbox.GetComponent<UICheckBox>();
        //    checkBox.isChecked = false;
        //    checkBox.text = text;
        //    checkBox.isVisible = true;
        //    if (zOrder != -1)
        //        checkBox.zOrder = zOrder;

        //    return checkBox;
        //}

        //private void OpenOptionsPanel(UIComponent component, UIMouseEventParameter eventParam)
        //{
        //    LoadOptions();
        //    this.m_optionsPanel.GetComponent<UIPanel>().isVisible = true;
        //    this.m_optionsPanel.GetComponent<UIPanel>().BringToFront();
        //}

        //private void CloseOptionsPanel(UIComponent component, UIMouseEventParameter eventParam)
        //{
        //    this.m_optionsPanel.GetComponent<UIPanel>().isVisible = false;
        //}

        private void OnSave()
        {
            //this.m_optionsPanel.GetComponent<UIPanel>().isVisible = false;

            Options options = new Options();
            CSLTraffic.Options = ModOptions.None;
            if (this.m_allowTrucksCheckBox.isChecked)
            {
                options.allowTrucks = true;
                CSLTraffic.Options |= ModOptions.AllowTrucksInPedestrianRoads;
            }
            if (this.m_allowResidentsCheckBox.isChecked)
            {
                options.allowResidents = true;
                CSLTraffic.Options |= ModOptions.AllowResidentsInPedestrianRoads;
            }
            if (this.m_disableCentralLaneCheckBox.isChecked)
            {
                options.disableCentralLane = true;
                CSLTraffic.Options |= ModOptions.DisableCentralLaneOnPedestrianRoads;
            }
            if (this.m_realisticSpeedsCheckBox.isChecked)
            {
                options.realisticSpeeds = true;
                CSLTraffic.Options |= ModOptions.UseRealisticSpeeds;
            }
            if (this.m_noDespawnCheckBox.isChecked)
            {
                options.noDespawn = true;
                CSLTraffic.Options |= ModOptions.NoDespawn;
            }
            if (this.m_improvedAICheckBox.isChecked)
            {
                options.improvedAI = true;
                CSLTraffic.Options |= ModOptions.ImprovedAI;
            }
            if (this.m_betaTestRoadCustomizerCheckBox.isChecked)
            {
                options.betaTestRoadCustomizer = true;
                CSLTraffic.Options |= ModOptions.BetaTestRoadCustomizerTool;
            }
            if (this.m_disableCustomRoadsCheckBox.isChecked)
            {
                options.disableCustomRoads = true;
                CSLTraffic.Options |= ModOptions.DisableCustomRoads;
            }
            //if (this.m_noStopForCrossing.isChecked)
            //{
            //    options.noStopForCrossing = true;
            //    CSLTraffic.Options |= ModOptions.noStopForCrossing;
            //}
            if (this.m_ghostModeCheckBox.isChecked)
            {
                options.ghostMode = true;
                CSLTraffic.Options |= ModOptions.GhostMode;
            }

            try
            {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(Options));
                using (StreamWriter streamWriter = new StreamWriter("CSL-TrafficOptions.xml"))
                {
                    xmlSerializer.Serialize(streamWriter, options);
                }
            }
            catch (Exception e)
            {
                Logger.LogInfo("Unexpected " + e.GetType().Name + " saving options: " + e.Message + "\n" + e.StackTrace);
            }
        }

        public void LoadOptions()
        {
            CSLTraffic.Options = ModOptions.None;
            Options options = new Options();
            try
            {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(Options));
                using (StreamReader streamReader = new StreamReader("CSL-TrafficOptions.xml"))
                {
                    options = (Options)xmlSerializer.Deserialize(streamReader);
                }
            }
            catch (FileNotFoundException)
            {
                // No options file yet
                return;
            }
            catch (Exception e)
            {
                Logger.LogInfo("Unexpected " + e.GetType().Name + " loading options: " + e.Message + "\n" + e.StackTrace);
                return;
            }

            if (this.m_allowTrucksCheckBox != null)
            {
                this.m_allowTrucksCheckBox.isChecked = options.allowTrucks;
                this.m_allowResidentsCheckBox.isChecked = options.allowResidents;
                this.m_disableCentralLaneCheckBox.isChecked = options.disableCentralLane;
                this.m_realisticSpeedsCheckBox.isChecked = options.realisticSpeeds;
                this.m_noDespawnCheckBox.isChecked = options.noDespawn;
                this.m_improvedAICheckBox.isChecked = options.improvedAI;
                this.m_betaTestRoadCustomizerCheckBox.isChecked = options.betaTestRoadCustomizer;
                this.m_disableCustomRoadsCheckBox.isChecked = options.disableCustomRoads;
                this.m_ghostModeCheckBox.isChecked = options.ghostMode;
                //this.m_noStopForCrossing.isChecked = options.noStopForCrossing;
            }

            if (options.allowTrucks)
                CSLTraffic.Options |= ModOptions.AllowTrucksInPedestrianRoads;

            if (options.allowResidents)
                CSLTraffic.Options |= ModOptions.AllowResidentsInPedestrianRoads;

            if (options.disableCentralLane)
                CSLTraffic.Options |= ModOptions.DisableCentralLaneOnPedestrianRoads;

            if (options.realisticSpeeds)
                CSLTraffic.Options |= ModOptions.UseRealisticSpeeds;

            if (options.noDespawn)
                CSLTraffic.Options |= ModOptions.NoDespawn;

            if (options.improvedAI)
                CSLTraffic.Options |= ModOptions.ImprovedAI;

            if (options.disableCustomRoads)
                CSLTraffic.Options |= ModOptions.DisableCustomRoads;

            //if (options.noStopForCrossing)
            //    CSLTraffic.Options |= ModOptions.noStopForCrossing;

            if (options.betaTestRoadCustomizer)
                CSLTraffic.Options |= ModOptions.BetaTestRoadCustomizerTool;

            if (options.ghostMode)
                CSLTraffic.Options |= ModOptions.GhostMode;

            if (options.fixCargoTrucksNotSpawning)
                CSLTraffic.Options |= ModOptions.FixCargoTrucksNotSpawning;
        }

        //void OnLevelWasLoaded(int level)
        //{
        //    if (level == 5 || level == 3)
        //        m_initialized = false;
        //}

        //void Update()
        //{
        //    if (Application.loadedLevel == 3 || Application.loadedLevel == 5) // both are the main menu!?
        //    {
        //        if (!m_initialized || m_optionsButtonGo == null)
        //            Start();
        //        else if (m_optionsButtonGo.GetComponent<UIButton>().isVisible == false)
        //            m_optionsButtonGo.GetComponent<UIButton>().isVisible = true;
        //    }
        //}

        public struct Options
        {
            public bool allowTrucks;
            public bool allowResidents;
            public bool disableCentralLane;
            public bool realisticSpeeds;
            public bool noDespawn;
            public bool improvedAI;
            public bool disableCustomRoads;
            public bool noStopForCrossing;

            public bool betaTestRoadCustomizer;

            public bool fixCargoTrucksNotSpawning;

            public bool ghostMode;
        }
    }
}
