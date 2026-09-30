using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unturned.UnityEx
{
    /// <summary>Render thumbnails, item icons, maps, and scopes through the active pipeline.</summary>
    public static class CameraRenderEx
    {
        private static readonly Vector3[] CubeDirections = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        private static readonly Vector3[] CubeUp = { Vector3.up, Vector3.up, Vector3.back, Vector3.forward, Vector3.up, Vector3.up };

        public static void RenderCubemapFace(Camera camera, RenderTexture destination, int face)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if ((uint)face >= 6) throw new ArgumentOutOfRangeException(nameof(face));
            var previousRotation = camera.transform.rotation;
            var previousActive = RenderTexture.active;
            float previousFieldOfView = camera.fieldOfView, previousAspect = camera.aspect;
            // URP 17.7 creates cube request intermediates without depth. Render a depth-backed
            // 2D face explicitly so native decal passes have a valid depth attachment.
            // The default descriptor also omits AllowVerticalFlip, as URP's cube requests do.
            // A descriptor copied from a script-created texture flips every cubemap face.
            var descriptor = new RenderTextureDescriptor
            {
                width = destination.width,
                height = destination.height,
                graphicsFormat = destination.graphicsFormat,
                dimension = TextureDimension.Tex2D,
                volumeDepth = 1,
                msaaSamples = 1,
                mipCount = 1,
                shadowSamplingMode = ShadowSamplingMode.None,
                depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormatUtility.GetDepthStencilFormat(24, 8)
            };
            var faceTarget = RenderTexture.GetTemporary(descriptor);
            var faceHandle = RTHandles.Alloc(faceTarget);
            var copy = CommandBufferPool.Get("Copy reflection cubemap face");
            try
            {
                camera.transform.rotation = Quaternion.LookRotation(CubeDirections[face], CubeUp[face]);
                camera.fieldOfView = 90f;
                camera.aspect = 1f;
                var request = new RenderPipeline.StandardRequest
                    { destination = faceTarget };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new NotSupportedException("The active pipeline does not support cubemap face requests.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                CoreUtils.SetRenderTarget(copy, destination, cubemapFace: (CubemapFace)face);
                copy.SetViewport(new Rect(0, 0, destination.width, destination.height));
                // Native cubemap capture uses the opposite vertical orientation to 2D camera requests.
                Blitter.BlitTexture(copy, faceHandle, new Vector4(1, -1, 0, 1), 0, false);
                Graphics.ExecuteCommandBuffer(copy);
            }
            finally
            {
                camera.transform.rotation = previousRotation;
                camera.fieldOfView = previousFieldOfView;
                camera.aspect = previousAspect;
                RenderTexture.active = previousActive;
                CommandBufferPool.Release(copy);
                faceHandle.Release();
                RenderTexture.ReleaseTemporary(faceTarget);
            }
        }

        public static void Render(Camera camera)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (GraphicsSettings.currentRenderPipeline == null)
            {
                camera.Render();
                return;
            }

            if (camera.targetTexture == null)
                throw new InvalidOperationException("An off-screen render request requires a target texture.");

            var request = new RenderPipeline.StandardRequest { destination = camera.targetTexture };
            if (!RenderPipeline.SupportsRenderRequest(camera, request))
                throw new NotSupportedException("The active pipeline does not support standard camera render requests.");
            var previousActive = RenderTexture.active;
            try { RenderPipeline.SubmitRenderRequest(camera, request); }
            finally { RenderTexture.active = previousActive; }
        }
    }
}
