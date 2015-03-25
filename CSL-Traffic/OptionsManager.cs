using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ColossalFramework.UI;
using ColossalFramework.DataBinding;
using System.Xml.Serialization;
using System.IO;

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

            All = 7,

            GhostMode = long.MaxValue
        }

        GameObject m_optionsButtonGo;
        GameObject m_optionsPanel;

        UICheckBox m_allowTrucksCheckBox;
        //UICheckBox m_allowTrucksCheckBox;
        UICheckBox m_disableCentralLaneCheckBox;
        UICheckBox m_ghostModeCheckBox;
        
        bool m_initialized;

        void Awake()
        {
            DontDestroyOnLoad(this);
        }

        void Start()
        {
            GameObject modsList = GameObject.Find("ModsList");
            GameObject mod = null;
            foreach (var item in modsList.GetComponentsInChildren<UILabel>().Where(l => l.name == "Name"))
            {
                if (item.text.Contains("jfarias") || item.text.Contains("Traffic++"))
                {
                    mod = item.transform.parent.gameObject;
                    break;
                }
            }

            if (mod == null)
            {
#if DEBUG
                System.IO.File.AppendAllText("Debug.txt", "Can't find mod.\n");
#endif
                return;
            }
            
            // Options Button

            UIButton shareButton = mod.transform.FindChild("Share").GetComponent<UIButton>();
            this.m_optionsButtonGo = GameObject.Instantiate<GameObject>(shareButton.gameObject);
            this.m_optionsButtonGo.transform.SetParent(shareButton.transform.parent);
            this.m_optionsButtonGo.transform.localPosition = shareButton.transform.localPosition;

            UIButton optionsButton = this.m_optionsButtonGo.GetComponent<UIButton>();
            optionsButton.isVisible = true;
            optionsButton.text = "Options";
            optionsButton.eventClick += OpenOptionsPanel;
            optionsButton.position += Vector3.left * shareButton.width * 2.1f; 

            // Options Panel

            GameObject optionsPanel = GameObject.Find("(Library) OptionsPanel");
            this.m_optionsPanel = GameObject.Instantiate<GameObject>(optionsPanel);
            this.m_optionsPanel.transform.SetParent(modsList.transform.parent.transform);
            GameObject.Destroy(this.m_optionsPanel.GetComponent<OptionsPanel>());

            GameObject checkboxTemplate = this.m_optionsPanel.GetComponentsInChildren<UICheckBox>().Where(c => c.name == "EdgeScrolling").FirstOrDefault().gameObject;
            Vector3 localPosition = checkboxTemplate.transform.localPosition;
            
            GameObject caption = null;
            foreach (Transform transform in m_optionsPanel.transform)
            {
                if (transform.name == "Caption")
                {
                    caption = transform.gameObject;
                }
                else
                {
                    GameObject.Destroy(transform.gameObject);
                }
            }

            this.m_optionsPanel.GetComponent<UIPanel>().autoFitChildrenVertically = true;
            this.m_optionsPanel.GetComponent<UIPanel>().autoFitChildrenHorizontally = true;

            // set caption
            UIButton closeButton = caption.transform.FindChild("Close").GetComponent<UIButton>();
            GameObject.Destroy(closeButton.GetComponent<BindEvent>());
            closeButton.eventClick += CloseOptionsPanel;
            caption.transform.FindChild("Label").GetComponent<UILabel>().text = "Zonable Pedestrian Paths - Options";

            // set options list
            GameObject optionsList = GameObject.Instantiate<GameObject>(modsList);
            for (int i = optionsList.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(optionsList.transform.GetChild(i).gameObject);
            }
            optionsList.transform.SetParent(this.m_optionsPanel.transform);
            optionsList.GetComponent<UIScrollablePanel>().AlignTo(this.m_optionsPanel.GetComponent<UIPanel>(), UIAlignAnchor.TopLeft);
            optionsList.GetComponent<UIScrollablePanel>().position += new Vector3(caption.transform.FindChild("Label").GetComponent<UILabel>().height, -caption.transform.FindChild("Label").GetComponent<UILabel>().height * 2f);
            

            GameObject allowTrucks = GameObject.Instantiate<GameObject>(checkboxTemplate);
            GameObject.Destroy(allowTrucks.GetComponent<BindProperty>());
            allowTrucks.transform.SetParent(optionsList.transform);
            //allowTrucks.transform.localPosition = localPosition + new Vector3(0.05f, -0.1f, 0f);
            this.m_allowTrucksCheckBox = allowTrucks.GetComponent<UICheckBox>();
            this.m_allowTrucksCheckBox.isChecked = false;
            this.m_allowTrucksCheckBox.text = "Allow Trucks in Pedestrian Roads";
            this.m_allowTrucksCheckBox.isVisible = true;

            // allow residents
            //GameObject allowTrucks = GameObject.Instantiate<GameObject>(checkboxTemplate);
            //GameObject.Destroy(allowTrucks.GetComponent<BindProperty>());
            //allowTrucks.transform.SetParent(optionsList.transform);
            ////allowTrucks.transform.localPosition = localPosition + new Vector3(0.05f, -0.1f, 0f);
            //this.m_allowTrucksCheckBox = allowTrucks.GetComponent<UICheckBox>();
            //this.m_allowTrucksCheckBox.isChecked = false;
            //this.m_allowTrucksCheckBox.text = "Allow Trucks in Pedestrian Roads";
            //this.m_allowTrucksCheckBox.isVisible = true;

            // disable central lane
            GameObject disableCentralLane = GameObject.Instantiate<GameObject>(allowTrucks);
            disableCentralLane.transform.SetParent(allowTrucks.transform.parent);
            this.m_disableCentralLaneCheckBox = disableCentralLane.GetComponent<UICheckBox>();
            this.m_disableCentralLaneCheckBox.isChecked = false;
            this.m_disableCentralLaneCheckBox.text = "Disable Central Lane on Pedestrian Roads";
            this.m_disableCentralLaneCheckBox.isVisible = true;





            GameObject ghostMode = GameObject.Instantiate<GameObject>(allowTrucks);
            ghostMode.transform.SetParent(this.m_optionsPanel.transform);
            this.m_ghostModeCheckBox = ghostMode.GetComponent<UICheckBox>();
            this.m_ghostModeCheckBox.isChecked = false;
            this.m_ghostModeCheckBox.text = "Enable Ghost Mode (disables all mod functionality leaving only enough logic to load the map)";
            this.m_ghostModeCheckBox.isVisible = true;
            this.m_ghostModeCheckBox.AlignTo(m_optionsPanel.GetComponent<UIPanel>(), UIAlignAnchor.BottomLeft);

            GameObject save = GameObject.Instantiate<GameObject>(this.m_optionsButtonGo);
            save.transform.SetParent(this.m_optionsPanel.transform);
            
            UIButton saveButton = save.GetComponent<UIButton>();
            saveButton.isVisible = true;
            saveButton.eventClick += OnSave;
            saveButton.color = Color.green;
            saveButton.text = "Save";
            saveButton.AlignTo(m_optionsPanel.GetComponent<UIPanel>(), UIAlignAnchor.BottomRight);            

            this.Load();

            this.m_initialized = true;
        }

        private void OpenOptionsPanel(UIComponent component, UIMouseEventParameter eventParam)
        {
            this.m_optionsPanel.GetComponent<UIPanel>().isVisible = true;
            this.m_optionsPanel.GetComponent<UIPanel>().BringToFront();
        }
        private void CloseOptionsPanel(UIComponent component, UIMouseEventParameter eventParam)
        {
            this.m_optionsPanel.GetComponent<UIPanel>().isVisible = false;
        }
        private void OnSave(UIComponent component, UIMouseEventParameter eventParam)
        {
            this.m_optionsPanel.GetComponent<UIPanel>().isVisible = false;

            Options options = new Options();
            CSLTraffic.Options = ModOptions.None;
            if (this.m_allowTrucksCheckBox.isChecked)
            {
                options.allowTrucks = true;
                CSLTraffic.Options |= ModOptions.AllowTrucksInPedestrianRoads;
            }
            if (this.m_disableCentralLaneCheckBox.isChecked)
            {
                options.disableCentralLane = true;
                CSLTraffic.Options |= ModOptions.DisableCentralLaneOnPedestrianRoads;
            }
            if (this.m_ghostModeCheckBox.isChecked)
            {
                options.ghostMode = true;
                CSLTraffic.Options |= ModOptions.GhostMode;
            }

            XmlSerializer xmlSerializer = new XmlSerializer(typeof(Options));
            using (StreamWriter streamWriter = new StreamWriter("CSL-TrafficOptions.xml"))
            {
                xmlSerializer.Serialize(streamWriter, options);
            }
        }

        public void Load()
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(Options));
            Options options = new Options();
            try
            {
                using (StreamReader streamReader = new StreamReader("CSL-TrafficOptions.xml"))
                {
                    options = (Options)xmlSerializer.Deserialize(streamReader);
                }
            }
            catch (Exception)
            {
            }

            CSLTraffic.Options = ModOptions.None;
            if (options.allowTrucks)
            {
                this.m_allowTrucksCheckBox.isChecked = true;
                CSLTraffic.Options |= ModOptions.AllowTrucksInPedestrianRoads;
            }
            if (options.disableCentralLane)
            {
                this.m_disableCentralLaneCheckBox.isChecked = true;
                CSLTraffic.Options |= ModOptions.DisableCentralLaneOnPedestrianRoads;
            }
            if (options.ghostMode)
            {
                this.m_ghostModeCheckBox.isChecked = true;
                CSLTraffic.Options |= ModOptions.GhostMode;
            }
        }

        void OnLevelWasLoaded(int level)
        {
            if (level == 5)
                m_initialized = false;
        }

        void Update()
        {
            if (!m_initialized || (Application.loadedLevel == 5 && m_optionsButtonGo == null))
            {
                try
                {
                    Start();
                }
                catch { }
            }
        }

        public struct Options
        {
            public bool allowTrucks;
            //public bool allowResidents;
            public bool disableCentralLane;
            
            public bool ghostMode;
        }

    }
}
