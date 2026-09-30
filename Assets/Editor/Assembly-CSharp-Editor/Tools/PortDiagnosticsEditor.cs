using SDG.Unturned;
using UnityEditor;

[InitializeOnLoad]
public static class PortDiagnosticsEditor
{
    private static double next;
    static PortDiagnosticsEditor()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode) PortDiagnostics.StartSession("editor");
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) PortDiagnostics.StartSession("editor");
        };
    }
    private static void Update()
    {
        if (EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + 10;
        PortDiagnostics.Heartbeat();
    }
}
