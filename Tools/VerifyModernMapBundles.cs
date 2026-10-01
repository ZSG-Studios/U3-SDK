using System;
using System.IO;
using SDG.Unturned;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System.Linq;

public static class VerifyModernMapBundles
{
    public static object Main()
    {
        var inputs = JArray.Parse(File.ReadAllText("Logs/legacy-map-bundle-conversion.json"));
        var results = new List<object>();
        var seen = new HashSet<string>();
        int terrainManifests = 0, terrainNames = 0;
        foreach (var record in inputs)
        {
            string source = (string)record["source"], target = (string)record["target"];
            if (!seen.Add(target)) continue;
            var original = AssetBundle.LoadFromFile(source);
            if (original == null) throw new InvalidDataException("Original bundle load failed");
            if (ModernBundleCache.IsTerrainNamesSource(source))
            {
                try
                {
                    var names = original.LoadAllAssets<Texture2D>().Select(texture => texture.name).ToArray();
                    var prepared = Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>(File.ReadAllText(target));
                    if (!ModernBundleCache.IsVerified(target) || !names.SequenceEqual(prepared))
                        throw new InvalidDataException("Terrain texture order changed: " + source);
                    results.Add(new { bundle = Path.GetFileName(target), kind = "terrainNames", names });
                    terrainManifests++; terrainNames += names.Length;
                }
                finally { original.Unload(true); }
                continue;
            }
            var rebuilt = AssetBundle.LoadFromFile(target);
            if (original == null || rebuilt == null) throw new InvalidDataException("Bundle load failed");
            try
            {
                var before = original.LoadAllAssets();
                var after = rebuilt.LoadAllAssets();
                if (before.Length != after.Length) throw new InvalidDataException("Asset count changed: " + source);
                foreach (var asset in before)
                {
                    var copy = after.Single(x => x.name == asset.name && x.GetType() == asset.GetType());
                    if (asset is Texture2D texture)
                    {
                        var other = (Texture2D)copy;
                        if (texture.width != other.width || texture.height != other.height || texture.mipmapCount != other.mipmapCount
                            || texture.wrapMode != other.wrapMode || texture.filterMode != other.filterMode)
                            throw new InvalidDataException("Texture metadata changed: " + texture.name);
                        for (int mip = 0; mip < texture.mipmapCount; mip++)
                        {
                            var a = AsyncGPUReadback.Request(texture, mip, TextureFormat.RGBA32);
                            var b = AsyncGPUReadback.Request(other, mip, TextureFormat.RGBA32);
                            a.WaitForCompletion(); b.WaitForCompletion();
                            if (a.hasError || b.hasError) throw new InvalidOperationException("Texture readback failed");
                            var ad = a.GetData<byte>(); var bd = b.GetData<byte>();
                            if (ad.Length != bd.Length) throw new InvalidDataException("Texture payload dimensions changed");
                            for (int i = 0; i < ad.Length; i++) if (ad[i] != bd[i]) throw new InvalidDataException("Texture pixel changed: " + texture.name + " mip " + mip);
                        }
                        results.Add(new { bundle = Path.GetFileName(target), asset = texture.name, kind = "texture", exactMips = texture.mipmapCount });
                    }
                    else if (asset is AudioClip clip)
                    {
                        var other = (AudioClip)copy;
                        clip.UnloadAudioData();
                        var so = new SerializedObject(clip); so.FindProperty("m_LoadType").intValue = 0; so.ApplyModifiedPropertiesWithoutUndo();
                        if (!clip.LoadAudioData() || !other.LoadAudioData()) throw new InvalidOperationException("Audio decode failed");
                        if (clip.channels != other.channels || clip.frequency != other.frequency || Math.Abs(clip.samples - other.samples) > 1)
                            throw new InvalidDataException("Audio dimensions changed");
                        int count = Math.Min(clip.samples, other.samples) * clip.channels;
                        var a = new float[count]; var b = new float[count];
                        if (!clip.GetData(a, 0) || !other.GetData(b, 0)) throw new InvalidOperationException("Audio sample read failed");
                        float maxError = 0; int nonzero = 0;
                        for (int i = 0; i < count; i++) { maxError = Mathf.Max(maxError, Mathf.Abs(a[i] - b[i])); if (a[i] != 0) nonzero++; }
                        if (maxError > .00005f) throw new InvalidDataException("Audio samples changed: " + clip.name + " error " + maxError);
                        results.Add(new { bundle = Path.GetFileName(target), asset = clip.name, kind = "audio", samples = count, nonzero, maxError });
                    }
                }
            }
            finally { if (original != null) original.Unload(true); if (rebuilt != null) rebuilt.Unload(true); }
        }
        File.WriteAllText("Logs/modern-map-bundle-verification.json", Newtonsoft.Json.JsonConvert.SerializeObject(results, Newtonsoft.Json.Formatting.Indented));
        return new { passed = true, bundles = seen.Count - terrainManifests, assets = results.Count - terrainManifests, terrainManifests, terrainNames };
    }
}
