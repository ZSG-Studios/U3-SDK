using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SDG.Unturned
{
    /// <summary>Persistent, structured diagnostics for editor and player qualification.</summary>
    public static class PortDiagnostics
    {
        private static readonly object gate = new object();
        private static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        private static StreamWriter writer;
        private static string directory, session;
        private static int errors, warnings;
        private static readonly Regex sensitive = new Regex(@"(?i)(password|token|authorization|secret|licenseKey|auth\s*ticket)[""']?\s*[:=]", RegexOptions.Compiled);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            StartSession(Application.isEditor ? "editor-play" : "player");
            var host = new GameObject("Port diagnostics");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<PortDiagnosticsHeartbeat>();
        }

        public static void StartSession(string kind)
        {
            lock (gate)
            {
                Application.logMessageReceivedThreaded -= Receive;
                writer?.Dispose();
                counts.Clear(); errors = warnings = 0;
                directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "Diagnostics");
                Directory.CreateDirectory(directory);
                session = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + kind;
                writer = new StreamWriter(Path.Combine(directory, session + ".jsonl"), false) { AutoFlush = true };
                Write(new { utc = DateTime.UtcNow, type = "session", session, unity = Application.unityVersion, graphicsApi = SystemInfo.graphicsDeviceType.ToString() });
                Application.logMessageReceivedThreaded += Receive;
                Application.quitting -= Stop;
                Application.quitting += Stop;
            }
        }

        private static string Sanitize(string text) => text != null && sensitive.IsMatch(text) ? "[Sensitive diagnostic redacted]" : text;
        private static void Write(object entry) { writer?.WriteLine(JsonConvert.SerializeObject(entry)); }
        private static void Receive(string message, string stackTrace, LogType type)
        {
            lock (gate)
            {
                if (writer == null) return;
                message = Sanitize(message); stackTrace = Sanitize(stackTrace);
                bool isError = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
                if (isError) errors++;
                if (type == LogType.Warning) warnings++;
                string key = type + ": " + message;
                counts.TryGetValue(key, out int count); counts[key] = ++count;
                // Keep the first stack and sampled recurrences, with exact totals in the summary.
                // Unity's console remains unchanged; disk use stays bounded during an error flood.
                if (count == 1 || count % 100 == 0)
                    Write(new { utc = DateTime.UtcNow, type = type.ToString(), message, stackTrace, occurrences = count });
            }
        }

        public static void Heartbeat()
        {
            lock (gate)
            {
                if (writer == null) return;
                Write(new { utc = DateTime.UtcNow, type = "heartbeat", scene = SceneManager.GetActiveScene().name,
                    frame = Time.frameCount, assetsLoading = Assets.isLoading, loadingBlocked = LoadingUI.isBlocked,
                    levelLoaded = Level.isLoaded, playerPresent = (UnityEngine.Object)Player.LocalPlayer != null, errors, warnings });
                File.WriteAllText(Path.Combine(directory, session + "-summary.json"), JsonConvert.SerializeObject(new { session, errors, warnings, occurrences = counts }, Formatting.Indented));
            }
        }

        internal static void Stop()
        {
            Heartbeat();
            lock (gate) { Application.logMessageReceivedThreaded -= Receive; writer?.Dispose(); writer = null; }
        }
    }

    public sealed class PortDiagnosticsHeartbeat : MonoBehaviour
    {
        private float next;
        private void Update() { if (Time.unscaledTime < next) return; next = Time.unscaledTime + 10; PortDiagnostics.Heartbeat(); }
        private void OnDestroy() { PortDiagnostics.Stop(); }
    }
}
