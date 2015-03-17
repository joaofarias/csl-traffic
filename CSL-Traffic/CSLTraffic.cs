using ICities;
using UnityEngine;

namespace CSL_Traffic
{
	public class CSLTraffic : LoadingExtensionBase, IUserMod
	{
		GameObject m_initializer;

		public string Name
		{
			get { return "Zonable Pedestrian Paths"; }
		}

		public string Description
		{
			get { return "Enables zoning on pedestrian paths."; }
		}

		public override void OnCreated(ILoading loading)
		{
			base.OnCreated(loading);

			if (GameObject.Find("Initializer") == null)
			{
				m_initializer = new GameObject("Custom Prefabs");
				m_initializer.AddComponent<Initializer>();
			}
		}

		public override void OnLevelUnloading()
		{
			base.OnLevelUnloading();

			GameObject.Destroy(m_initializer);
		}
	}
}
