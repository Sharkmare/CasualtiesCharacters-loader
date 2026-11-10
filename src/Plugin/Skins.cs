using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasualtiesCharacters.Core;
using UnityEngine;

namespace CasualtiesCharacters
{
    public static class Skins
    {
        const float PixelsPerUnit = 8f;
        static readonly Dictionary<string, Dictionary<string, Sprite>> cache = new Dictionary<string, Dictionary<string, Sprite>>();

        public static Dictionary<string, Sprite> For(CharacterDefinition definition)
        {
            if (string.IsNullOrEmpty(definition.SkinFolder)) return null;
            Dictionary<string, Sprite> sprites;
            if (cache.TryGetValue(definition.FullKey, out sprites)) return sprites;
            sprites = Load(definition);
            cache[definition.FullKey] = sprites;
            return sprites;
        }

        static Dictionary<string, Sprite> Load(CharacterDefinition definition)
        {
            string skinFolder = definition.SkinPath;
            if (skinFolder != null && Directory.Exists(skinFolder))
            {
                string bodyFolder = FindBody(skinFolder);
                if (bodyFolder != null) return LoadFolder(definition, bodyFolder);
            }
            Plugin.Log.LogWarning("skin '" + definition.SkinFolder + "' for " + definition.FullKey + " not found in its pack, using the base character's look");
            return null;
        }

        static string FindBody(string skinFolder)
        {
            List<string> relative = Directory.GetDirectories(skinFolder, "*", SearchOption.AllDirectories)
                .Select(path => path.Substring(skinFolder.Length).TrimStart('\\', '/')).ToList();
            string found = SkinMatcher.FindBodyFolder(relative);
            return found == null ? null : Path.Combine(skinFolder, found);
        }

        static Dictionary<string, Sprite> LoadFolder(CharacterDefinition definition, string bodyFolder)
        {
            string[] files = Directory.GetFiles(bodyFolder, "*.png");
            SkinMatcher.Report report = SkinMatcher.Match(files.Select(Path.GetFileName));
            Plugin.Log.LogInfo("skin " + definition.FullKey + ": " + report.Summary());
            Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
            foreach (string file in files)
            {
                string part = SkinMatcher.CanonicalPart(SkinMatcher.StripPng(file));
                if (part == null) continue;
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.name = part;
                if (!texture.LoadImage(File.ReadAllBytes(file)))
                {
                    Plugin.Log.LogWarning("could not decode " + file);
                    continue;
                }
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.Tight);
                sprite.name = part;
                sprites[part] = sprite;
            }
            return sprites;
        }
    }

    public sealed class SkinApplier : MonoBehaviour
    {
        const float RefreshSeconds = 0.5f;

        Dictionary<string, Sprite> sprites;
        SpriteRenderer[] renderers = new SpriteRenderer[0];
        float refreshTimer;
        bool dumped;
        readonly Dictionary<SpriteRenderer, Sprite> originals = new Dictionary<SpriteRenderer, Sprite>();

        public void Configure(CharacterDefinition definition)
        {
            sprites = Skins.For(definition);
        }

        void LateUpdate()
        {
            if (sprites == null || sprites.Count == 0) return;
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = RefreshSeconds;
                renderers = GetComponentsInChildren<SpriteRenderer>(true);
                DumpNamesOnce();
            }
            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null || renderer.sprite == null) continue;
                Sprite replacement;
                if (sprites.TryGetValue(renderer.sprite.name, out replacement) && renderer.sprite != replacement)
                {
                    if (!originals.ContainsKey(renderer)) originals[renderer] = renderer.sprite;
                    renderer.sprite = replacement;
                }
            }
        }

        void OnDestroy()
        {
            foreach (KeyValuePair<SpriteRenderer, Sprite> pair in originals)
                if (pair.Key != null) pair.Key.sprite = pair.Value;
        }

        void DumpNamesOnce()
        {
            if (dumped || !Plugin.DebugLogging.Value) return;
            dumped = true;
            Plugin.Log.LogInfo("sprite names on " + name + ": " + string.Join(",", renderers
                .Where(r => r != null && r.sprite != null).Select(r => r.sprite.name).Distinct().ToArray()));
        }
    }
}
