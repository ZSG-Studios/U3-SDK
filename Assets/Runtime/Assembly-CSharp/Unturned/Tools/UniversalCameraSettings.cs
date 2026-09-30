using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SDG.Unturned
{
    internal static class UniversalCameraSettings
    {
        public static void Apply(Camera camera, bool overlay = false)
        {
            if (camera == null) return;
            var data = camera.GetUniversalAdditionalCameraData();
            overlay |= data.renderType == CameraRenderType.Overlay;
            // Deferred renderers support Base cameras. Viewmodels use Forward+ overlays.
            data.SetRenderer(!overlay && GraphicsSettings.renderMode == ERenderMode.DEFERRED ? 1 : 0);
            data.requiresDepthTexture = !overlay;
            data.requiresColorTexture = !overlay;
            camera.allowHDR = true;
            camera.allowMSAA = false;
        }
    }
}
