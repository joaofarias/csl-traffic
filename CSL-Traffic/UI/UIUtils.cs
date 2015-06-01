using ColossalFramework.UI;
using System.Collections.Generic;
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
                Logger.LogInfo("Cannot find UI Shader. Using default thumbnails.");
                return null;
            }

            byte[] bytes;
            if (!FileManager.GetTextureBytes(name + ".png", FileManager.Folder.UI, out bytes))
            {
                Logger.LogInfo("Cannot find UI Atlas file. Using default thumbnails.");
                return null;
            }

            Texture2D atlasTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            atlasTexture.LoadImage(bytes);
            FixTransparency(atlasTexture);

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


        //=========================================================================
        // Methods created by petrucio -> http://answers.unity3d.com/questions/238922/png-transparency-has-white-borderhalo.html
        //
        // Copy the values of adjacent pixels to transparent pixels color info, to
        // remove the white border artifact when importing transparent .PNGs.
        public static void FixTransparency(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            int w = texture.width;
            int h = texture.height;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    Color32 pixel = pixels[idx];
                    if (pixel.a == 0)
                    {
                        bool done = false;
                        if (!done && x > 0) done = TryAdjacent(ref pixel, pixels[idx - 1]);        // Left   pixel
                        if (!done && x < w - 1) done = TryAdjacent(ref pixel, pixels[idx + 1]);        // Right  pixel
                        if (!done && y > 0) done = TryAdjacent(ref pixel, pixels[idx - w]);        // Top    pixel
                        if (!done && y < h - 1) done = TryAdjacent(ref pixel, pixels[idx + w]);        // Bottom pixel
                        pixels[idx] = pixel;
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
        }

        private static bool TryAdjacent(ref Color32 pixel, Color32 adjacent)
        {
            if (adjacent.a == 0) return false;

            pixel.r = adjacent.r;
            pixel.g = adjacent.g;
            pixel.b = adjacent.b;
            return true;
        }
        //=========================================================================
    }
}
