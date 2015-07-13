using ICities;
using UnityEngine;

namespace CSL_Traffic
{
    public class CSLTraffic : LoadingExtensionBase, IUserMod
    {
        public const ulong WORKSHOP_ID = 409184143ul;

        public static OptionsManager.ModOptions Options = OptionsManager.ModOptions.None;
        static OptionsManager sm_optionsManager;
        
        GameObject m_initializer;

        public string Name
        {
            get { return "Traffic++"; }
        }

        public string Description
        {
            get { return "Adds zonable pedestrian paths and other traffic improvements."; }
        }

        public void OnSettingsUI(UIHelperBase helper)
        {
            if (sm_optionsManager == null)
                sm_optionsManager = new GameObject("OptionsManager").AddComponent<OptionsManager>();

            sm_optionsManager.CreateSettings(helper);
        }

        public override void OnCreated(ILoading loading)
        {
            base.OnCreated(loading);

            if (sm_optionsManager != null)
            {
                sm_optionsManager.LoadOptions();
            }

            if (m_initializer == null)
            {
                m_initializer = new GameObject("CSL-Traffic Custom Prefabs");
                m_initializer.AddComponent<Initializer>();
            }
        }

        public override void OnLevelUnloading()
        {
            base.OnLevelUnloading();

            if (m_initializer != null)
                m_initializer.GetComponent<Initializer>().OnLevelUnloading();
        }

        public override void OnReleased()
        {
            base.OnReleased();

            GameObject.Destroy(m_initializer);
        }
    }
}
