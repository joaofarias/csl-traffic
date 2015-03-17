using ColossalFramework.Globalization;
using ICities;
using System.Collections.Generic;
using UnityEngine;
using CSL_Traffic.Extensions;
using ColossalFramework;

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

			if (m_initializer == null)
			{
				m_initializer = new GameObject("CSL-Traffic Custom Prefabs");
				m_initializer.AddComponent<Initializer>();
			}
		}

		public override void OnReleased()
		{
			base.OnReleased();

			GameObject.Destroy(m_initializer);
		}
	}
}
