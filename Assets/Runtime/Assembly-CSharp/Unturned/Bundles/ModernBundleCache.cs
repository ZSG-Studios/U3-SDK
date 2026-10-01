using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using Newtonsoft.Json;

namespace SDG.Unturned
{
    /// <summary>Content-addressed, editor-rebuilt legacy bundles. Originals stay intact.</summary>
    public static class ModernBundleCache
    {
        public static string CacheDirectory
        {
            get
            {
                string directory = Path.GetDirectoryName(Application.dataPath);
#if UNITY_EDITOR
                return Path.Combine(directory, "Builds", "ConvertedBundles", Application.unityVersion);
#else
                return Path.Combine(directory, "ConvertedBundles", Application.unityVersion);
#endif
            }
        }
        public static bool IsTerrainNamesSource(string source) => Path.GetFileName(source) == "Materials.unity3d";
        public static string PreparedFileName(string source) => Fingerprint(source) +
            (IsTerrainNamesSource(source) ? ".terrain-names-v1.json" : ".unity3d");
        public static string[] ReadTerrainTextureNames(string source)
        {
            string file = Path.Combine(CacheDirectory, Fingerprint(source) + ".terrain-names-v1.json");
            if (!IsVerified(file)) return null;
            return JsonConvert.DeserializeObject<string[]>(File.ReadAllText(file));
        }
        public static string VerificationText(string bundle) => Application.unityVersion + "\nWindows64:map-conversion-v3\n" + Fingerprint(bundle);
        public static bool IsVerified(string bundle)
        {
            if (!File.Exists(bundle) || !File.Exists(bundle + ".verified")) return false;
            try { return File.ReadAllText(bundle + ".verified") == VerificationText(bundle); }
            catch (IOException) { return false; }
        }
        public static string Fingerprint(string source)
        {
            using (var hash = SHA256.Create())
            using (var stream = File.OpenRead(source))
                return System.BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        public static string Resolve(string source)
        {
            if (!File.Exists(source)) return source;
            string cache = CacheDirectory;
            if (!Directory.Exists(cache)) return source;
            string candidate = Path.Combine(cache, Fingerprint(source) + ".unity3d");
            return IsVerified(candidate) ? candidate : source;
        }
    }
}
