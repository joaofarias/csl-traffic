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
			// bits 55 to 62 reserved for beta tests that won't have their own option
			BetaTestRoadCustomizerTool = 1L << 55,
			FixCargoTrucksNotSpawning = 1L << 61,
			GhostMode = 1L << 62
		}

		UICheckBox m_allowTrucksCheckBox = null;
		UICheckBox m_allowResidentsCheckBox = null;
		UICheckBox m_disableCentralLaneCheckBox = null;
		UICheckBox m_realisticSpeedsCheckBox = null;
		UICheckBox m_noDespawnCheckBox = null;
		UICheckBox m_improvedAICheckBox = null;
		UICheckBox m_betaTestRoadCustomizerCheckBox = null;
		UICheckBox m_ghostModeCheckBox = null;

		void Awake()
		{
			DontDestroyOnLoad(this);
		}

        public void CreateSettings(UIHelperBase helper)
        {
            UIHelperBase group = helper.AddGroup("Traffic++ Options");
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

		private void OnSave()
		{
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
				this.m_ghostModeCheckBox.isChecked = options.ghostMode;
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

			if (options.betaTestRoadCustomizer)
				CSLTraffic.Options |= ModOptions.BetaTestRoadCustomizerTool;

			if (options.ghostMode)
				CSLTraffic.Options |= ModOptions.GhostMode;

			if (options.fixCargoTrucksNotSpawning)
				CSLTraffic.Options |= ModOptions.FixCargoTrucksNotSpawning;
		}

		public struct Options
		{
			public bool allowTrucks;
			public bool allowResidents;
			public bool disableCentralLane;
			public bool realisticSpeeds;
			public bool noDespawn;
			public bool improvedAI;

			public bool noStopForCrossing;

			public bool betaTestRoadCustomizer;

			public bool fixCargoTrucksNotSpawning;

			public bool ghostMode;
		}
	}
}
