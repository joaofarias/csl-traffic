using ICities;
using UnityEngine;

namespace CSL_Traffic
{
    public class CSLTraffic : LoadingExtensionBase, IUserMod
    {
        public const ulong WORKSHOP_ID = 409184143ul;

        public static OptionsManager.ModOptions Options = OptionsManager.ModOptions.None;
        static GameObject sm_optionsManager;
        
        GameObject m_initializer;

        public string Name
        {
            get
            {
                if (sm_optionsManager == null)
                {
                    sm_optionsManager = new GameObject("OptionsManager");
                    sm_optionsManager.AddComponent<OptionsManager>();
                }

                return "Traffic++";
            }
        }

        public string Description
        {
            get { return "Adds zonable pedestrian paths and other traffic improvements."; }
        }

        public override void OnCreated(ILoading loading)
        {
            base.OnCreated(loading);

            if (sm_optionsManager != null)
            {
                sm_optionsManager.GetComponent<OptionsManager>().LoadOptions();
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

            Object.Destroy(m_initializer);
        }
    }
}
