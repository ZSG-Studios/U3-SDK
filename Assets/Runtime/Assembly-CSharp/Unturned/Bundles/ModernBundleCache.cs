using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace SDG.Unturned
{
    /// <summary>Content-addressed, editor-rebuilt legacy bundles. Originals stay intact.</summary>
    public static class ModernBundleCache
    {
        public static string Fingerprint(string source)
        {
            using (var hash = SHA256.Create())
            using (var stream = File.OpenRead(source))
                return System.BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        public static string Resolve(string source)
        {
            if (!File.Exists(source)) return source;
            string directory = Path.GetDirectoryName(Application.dataPath);
#if UNITY_EDITOR
            string cache = Path.Combine(directory, "Builds", "ConvertedBundles", Application.unityVersion);
#else
            string cache = Path.Combine(directory, "ConvertedBundles", Application.unityVersion);
#endif
            if (!Directory.Exists(cache)) return source;
            string candidate = Path.Combine(cache, Fingerprint(source) + ".unity3d");
            return File.Exists(candidate) && File.Exists(candidate + ".verified") ? candidate : source;
        }
    }
}
