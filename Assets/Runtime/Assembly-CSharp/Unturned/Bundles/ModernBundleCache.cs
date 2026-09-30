using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace SDG.Unturned
{
    /// <summary>Content-addressed, editor-rebuilt legacy bundles. Originals stay intact.</summary>
    public static class ModernBundleCache
    {
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
            string directory = Path.GetDirectoryName(Application.dataPath);
#if UNITY_EDITOR
            string cache = Path.Combine(directory, "Builds", "ConvertedBundles", Application.unityVersion);
#else
            string cache = Path.Combine(directory, "ConvertedBundles", Application.unityVersion);
#endif
            if (!Directory.Exists(cache)) return source;
            string candidate = Path.Combine(cache, Fingerprint(source) + ".unity3d");
            return IsVerified(candidate) ? candidate : source;
        }
    }
}
