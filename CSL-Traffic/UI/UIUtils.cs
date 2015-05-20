using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CSL_Traffic.UI
{
	class UIUtils
	{
		public struct SpriteTextureInfo
		{
			public int startX;
			public int startY;
			public int width;
			public int height;
		}

		static Dictionary<string, UITextureAtlas> sm_atlases = new Dictionary<string,UITextureAtlas>();

		public static UITextureAtlas LoadThumbnailsTextureAtlas(string name)
        {
			UITextureAtlas atlas;
            if (sm_atlases.TryGetValue(name, out atlas))
                return atlas;

            Shader shader = Shader.Find("UI/Default UI Shader");
            if (shader == null)
            {
                Debug.Log("Traffic++: Cannot find UI Shader. Using default thumbnails.");
                return null;
            }

            byte[] bytes;
            if (!FileManager.GetTextureBytes(name + ".png", FileManager.Folder.UI, out bytes))
            {
                Debug.Log("Traffic++: Cannot find UI Atlas file. Using default thumbnails.");
                return null;
            }

            Texture2D atlasTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            atlasTexture.LoadImage(bytes);

            Material atlasMaterial = new Material(shader);
            atlasMaterial.mainTexture = atlasTexture;

            atlas = ScriptableObject.CreateInstance<UITextureAtlas>();
            atlas.name = "Traffic++ " + name;
            atlas.material = atlasMaterial;

			sm_atlases[name] = atlas;

			return atlas;
        }

		public static bool SetThumbnails(string name, SpriteTextureInfo info, UITextureAtlas atlas, string[] states = null)
		{
			if (atlas == null || atlas.texture == null)
				return false;

			Texture2D atlasTex = atlas.texture;
			float atlasWidth = atlasTex.width;
			float atlasHeight = atlasTex.height;
			float rectWidth = info.width / atlasWidth;
			float rectHeight = info.height / atlasHeight;
			int x = info.startX;
			int y = info.startY;

			if (states == null)
				states = new string[] { "" };

			for (int i = 0; i < states.Length; i++, x += info.width)
			{
				if (x < 0 || x + info.width > atlasWidth || y < 0 || y > atlasHeight)
					continue;

				Texture2D spriteTex = new Texture2D(info.width, info.height);
				spriteTex.SetPixels(atlasTex.GetPixels(x, y, info.width, info.height));

				UITextureAtlas.SpriteInfo sprite = new UITextureAtlas.SpriteInfo()
				{
					name = name + states[i],
					region = new Rect(x / atlasWidth, y / atlasHeight, rectWidth, rectHeight),
					texture = spriteTex
				};
				atlas.AddSprite(sprite);
			}

			return true;
		}
	}
}
