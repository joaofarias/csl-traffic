using ICities;
using UnityEngine;

namespace PedestrianZoning
{
	public class PedestrianZoning : LoadingExtensionBase, IUserMod
	{
		GameObject m_initializer;

		public string Name
		{
			get { return "Pedestrian Zoning"; }
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
				m_initializer = new GameObject("Initializer");
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
