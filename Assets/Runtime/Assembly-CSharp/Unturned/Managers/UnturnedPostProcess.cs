////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SDG.Unturned
{
	/// <summary>
	/// Manages global post-process volumes.
	/// </summary>
	public class UnturnedPostProcess : MonoBehaviour
	{
		public const int BASE_LAYER = LayerMasks.LOGIC;
		public const int VIEWMODEL_LAYER = LayerMasks.VIEWMODEL;
		public const int SCOPE_LAYER = LayerMasks.GROUND2;

		private bool _disableAntiAliasingForScreenshot;
		public bool DisableAntiAliasingForScreenshot
		{
			get => _disableAntiAliasingForScreenshot;
			set
			{
				if (_disableAntiAliasingForScreenshot != value)
				{
					_disableAntiAliasingForScreenshot = value;
					if (baseCameraData != null)
					{
						applyAntiAliasing(baseCameraData);
					}
					if (scopeCameraData != null)
					{
						applyAntiAliasing(scopeCameraData);
					}
					if (viewmodelCameraData != null)
					{
						applyAntiAliasing(viewmodelCameraData);
					}
				}
			}
		}

		public static UnturnedPostProcess instance
		{
			get;
			private set;
		}

		public void setBaseCamera(Camera baseCamera)
		{
			if (baseCameraData != null && viewmodelCameraData != null)
				baseCameraData.cameraStack.Remove(viewmodelCameraData.GetComponent<Camera>());
			baseCameraData = baseCamera.GetUniversalAdditionalCameraData();
			baseCameraData.renderType = CameraRenderType.Base;
			baseCameraData.renderPostProcessing = true;
			baseCameraData.volumeLayerMask = 1 << BASE_LAYER;
			baseCameraData.requiresDepthTexture = true;
			baseCameraData.requiresColorTexture = true;
			UniversalCameraSettings.Apply(baseCamera);
			if (viewmodelCameraData != null)
				baseCameraData.cameraStack.Add(viewmodelCameraData.GetComponent<Camera>());
			applyAntiAliasing(baseCameraData);
		}

		public void setOverlayCamera(Camera overlayCamera)
		{
			if (baseCameraData != null && viewmodelCameraData != null)
				baseCameraData.cameraStack.Remove(viewmodelCameraData.GetComponent<Camera>());
			viewmodelCameraData = overlayCamera.GetUniversalAdditionalCameraData();
			viewmodelCameraData.renderType = CameraRenderType.Overlay;
			// Overlay cameras clear depth by default in URP.
			viewmodelCameraData.renderPostProcessing = true;
			viewmodelCameraData.volumeLayerMask = 1 << VIEWMODEL_LAYER;
			UniversalCameraSettings.Apply(overlayCamera, true);
			if (baseCameraData != null && !baseCameraData.cameraStack.Contains(overlayCamera))
				baseCameraData.cameraStack.Add(overlayCamera);
			applyAntiAliasing(baseCameraData);
		}

		public void setScopeCamera(Camera scopeCamera)
		{
			scopeCameraData = scopeCamera.GetUniversalAdditionalCameraData();
			scopeCameraData.renderType = CameraRenderType.Base;
			scopeCameraData.renderPostProcessing = true;
			scopeCameraData.volumeLayerMask = 1 << SCOPE_LAYER;
			scopeCameraData.requiresDepthTexture = true;
			UniversalCameraSettings.Apply(scopeCamera);
			applyAntiAliasing(scopeCameraData);
		}

		public bool IsSingleRenderScopeActive()
		{
			return baseProfile.singleRenderScope.active;
		}

		public void SetSingleRenderScopeIsActive(bool isActive)
		{
			baseProfile.singleRenderScope.active = isActive;
		}

		public void SetSingleRenderScopeZoomFactor(float zoomFactor, float alpha)
		{
			if (zoomFactor > 1.0001f)
			{
				float blur = ((zoomFactor - 1.0f) * 0.5f);
				baseProfile.singleRenderScope.standardDeviation.Override(Mathf.Min(blur, 8f));
			}
			else
			{
				baseProfile.singleRenderScope.standardDeviation.Override(-1.0f);
			}
			baseProfile.singleRenderScope.scopeAlpha.Override(alpha);
		}

		public void SetSingleRenderScopeTarget(RenderTexture target)
		{
			baseProfile.singleRenderScope.renderTarget.Override(target);
		}

		public void setIsHallucinating(bool isHallucinating)
		{
			baseProfile.colorGrading.active = isHallucinating;
			baseProfile.colorGrading.hueShift.Override(Random.Range(-180.0f, 180.0f));
			viewmodelProfile.colorGrading.active = isHallucinating;
			viewmodelProfile.colorGrading.hueShift.Override(Random.Range(-180.0f, 180.0f));
			scopeProfile.colorGrading.active = isHallucinating;
			scopeProfile.colorGrading.hueShift.Override(Random.Range(-180.0f, 180.0f));

			baseProfile.vignette.active = isHallucinating;
		}

		private void tickHallucinationColorGrading(VolumeProfileWrapper profile, float deltaTime)
		{
			float cgSpeed = 2.5f; // How much to increase hue shift per second. Hue shift ranges from -180 to 180
			float hueShift = profile.colorGrading.hueShift.value;
			hueShift += deltaTime * cgSpeed;
			if (hueShift > 180.0f)
			{
				hueShift -= 360.0f;
			}
			profile.colorGrading.hueShift.Override(hueShift);
		}

		public void tickIsHallucinating(float deltaTime, float hallucinationTimer)
		{
			tickHallucinationColorGrading(baseProfile, deltaTime);
			tickHallucinationColorGrading(viewmodelProfile, deltaTime);
			tickHallucinationColorGrading(scopeProfile, deltaTime);

			float vignetteMaxIntensity = 0.333f;
			float vignettePeriod = 4.0f;
			baseProfile.vignette.intensity.Override(Mathf.Abs(Mathf.Sin(hallucinationTimer / vignettePeriod)) * vignetteMaxIntensity);
		}

		public void SetIsMainBlurEnabled(bool enabled)
		{
			baseProfile.dof.active = enabled;
		}

		/// <summary>
		/// Callback when in-game graphic settings change.
		/// </summary>
		public void applyUserSettings()
		{
			if (baseCameraData != null)
			{
				applyAntiAliasing(baseCameraData);
			}
			if (scopeCameraData != null)
			{
				applyAntiAliasing(scopeCameraData);
			}
			if (viewmodelCameraData != null)
			{
				applyAntiAliasing(viewmodelCameraData);
			}

			syncAmbientOcclusion();
			syncBloom();
			syncChromaticAberration();
			syncFilmGrain();
			syncScreenSpaceReflections();
		}

		/// <summary>
		/// Callback when player changes perspective.
		/// </summary>
		public void notifyPerspectiveChanged()
		{
			syncBloom();
			syncChromaticAberration();
			syncFilmGrain();
		}

		private void syncAmbientOcclusion()
		{
			// Inactive components fall back to URP's default stack values. Override zero explicitly.
			baseProfile.ambientOcclusion.active = true;
			viewmodelProfile.ambientOcclusion.active = true;
			scopeProfile.ambientOcclusion.active = true;
			baseProfile.ambientOcclusion.intensity = GraphicsSettings.isAmbientOcclusionEnabled ? .25f : 0f;
			viewmodelProfile.ambientOcclusion.intensity = GraphicsSettings.isAmbientOcclusionEnabled ? 1f : 0f;
			scopeProfile.ambientOcclusion.intensity = GraphicsSettings.isAmbientOcclusionEnabled ? .25f : 0f;
		}

		private void syncBloom()
		{
			// Bloom effects apply to all pixels even with forward rendering, so only active one at a time.
			if (hasActiveOverlay)
			{
				baseProfile.bloom.active = false;
				viewmodelProfile.bloom.active = GraphicsSettings.bloom;
			}
			else
			{
				baseProfile.bloom.active = GraphicsSettings.bloom;
				viewmodelProfile.bloom.active = false;
			}
			scopeProfile.bloom.active = false;
		}

		private void syncChromaticAberration()
		{
			if (hasActiveOverlay)
			{
				baseProfile.chromaticAberration.active = false;
				viewmodelProfile.chromaticAberration.active = GraphicsSettings.chromaticAberration;
			}
			else
			{
				baseProfile.chromaticAberration.active = GraphicsSettings.chromaticAberration;
				viewmodelProfile.chromaticAberration.active = false;
			}
			scopeProfile.chromaticAberration.active = false;
		}

		private void syncFilmGrain()
		{
			if (hasActiveOverlay)
			{
				baseProfile.filmGrain.active = false;
				viewmodelProfile.filmGrain.active = GraphicsSettings.filmGrain;
			}
			else
			{
				baseProfile.filmGrain.active = GraphicsSettings.filmGrain;
				viewmodelProfile.filmGrain.active = false;
			}
			scopeProfile.filmGrain.active = false;
		}

		private void syncScreenSpaceReflections()
		{
			// Current URP SSR supplies a depth/normal prepass for Forward+ as well as Deferred+.
			bool active = GraphicsSettings.reflectionQuality != EGraphicQuality.OFF;
			baseProfile.screenSpaceReflections.active = true;
			baseProfile.screenSpaceReflections.mode.Override(active ? ScreenSpaceReflectionVolumeSettings.ReflectionMode.OpaquesOnly
				: ScreenSpaceReflectionVolumeSettings.ReflectionMode.Disabled);
			viewmodelProfile.screenSpaceReflections.active = true;
			viewmodelProfile.screenSpaceReflections.mode.Override(ScreenSpaceReflectionVolumeSettings.ReflectionMode.Disabled);
			scopeProfile.screenSpaceReflections.active = true;
			scopeProfile.screenSpaceReflections.mode.Override(ScreenSpaceReflectionVolumeSettings.ReflectionMode.Disabled);

			if (!active)
				return;

			// Native URP SSR quality: preserve user quality intent through its current parameters.
			int steps = GraphicsSettings.reflectionQuality == EGraphicQuality.LOW ? 16
				: GraphicsSettings.reflectionQuality == EGraphicQuality.MEDIUM ? 32 : 64;
			baseProfile.screenSpaceReflections.maxRaySteps.Override(steps);
			baseProfile.screenSpaceReflections.resolution.Override(GraphicsSettings.reflectionQuality >= EGraphicQuality.HIGH
				? ScreenSpaceReflectionVolumeSettings.Resolution.Full : ScreenSpaceReflectionVolumeSettings.Resolution.Half);
		}

		private void applyAntiAliasing(UniversalAdditionalCameraData layer)
		{
			if (layer == null) return;
			if (_disableAntiAliasingForScreenshot)
			{
				layer.antialiasing = AntialiasingMode.None;
				return;
			}

			switch (GraphicsSettings.antiAliasingType)
			{
				default:
				case EAntiAliasingType.OFF:
					layer.antialiasing = AntialiasingMode.None;
					break;

				case EAntiAliasingType.FXAA:
					layer.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
					break;

				case EAntiAliasingType.TAA:
					// URP does not support temporal AA on a camera stack. Use its native SMAA for that case.
					layer.antialiasing = layer.renderType == CameraRenderType.Base && layer.cameraStack.Count == 0
						? AntialiasingMode.TemporalAntiAliasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
					break;

				case EAntiAliasingType.SMAA:
					layer.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
					break;
			}
		}

		private VolumeProfileWrapper createGlobalProfile(string name, int physicsLayer, EVolumeLayer layer)
		{
			GameObject volumeGameObject = new GameObject(name);
			volumeGameObject.transform.parent = transform;
			volumeGameObject.layer = physicsLayer;

			Volume volume = volumeGameObject.AddComponent<Volume>();
			volume.isGlobal = true;
			volume.priority = 1.0f;

			volume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
			return new VolumeProfileWrapper(volume.sharedProfile, layer); // Instantiates an empty profile.
		}

		public void initialize()
		{
			if (Dedicator.IsDedicatedServer)
			{
				Destroy(gameObject);
				return;
			}

			instance = this;
			DontDestroyOnLoad(this);

			baseProfile = createGlobalProfile("Base", BASE_LAYER, EVolumeLayer.Base);
			viewmodelProfile = createGlobalProfile("Viewmodel", VIEWMODEL_LAYER, EVolumeLayer.Viewmodel);
			scopeProfile = createGlobalProfile("Scope", SCOPE_LAYER, EVolumeLayer.Scope);

			// Base AO is weak due to artifacts, but strong AO on the gun looks nice.
			viewmodelProfile.ambientOcclusion.intensity = 1.0f;

			if (Provider.preferenceData.Graphics.Use_Lens_Dirt)
			{
				baseProfile.bloom.dirtTexture.Override(dirtTexture);
				baseProfile.bloom.dirtIntensity.Override(1.0f);
				viewmodelProfile.bloom.dirtTexture.Override(dirtTexture);
				viewmodelProfile.bloom.dirtIntensity.Override(1.0f);
			}

			baseProfile.chromaticAberration.intensity.Override(Provider.preferenceData.Graphics.Chromatic_Aberration_Intensity);
			viewmodelProfile.chromaticAberration.intensity.Override(Provider.preferenceData.Graphics.Chromatic_Aberration_Intensity);
			scopeProfile.chromaticAberration.intensity.Override(Provider.preferenceData.Graphics.Chromatic_Aberration_Intensity);
			applyUserSettings();
		}

		private void OnDestroy()
		{
			foreach (var wrapper in new[] { baseProfile, viewmodelProfile, scopeProfile })
			{
				if (wrapper?.profile == null) continue;
				foreach (var component in wrapper.profile.components) Destroy(component);
				Destroy(wrapper.profile);
			}
			if (instance == this) instance = null;
		}

		public Texture dirtTexture;

		private VolumeProfileWrapper baseProfile;
		private VolumeProfileWrapper viewmodelProfile;
		private VolumeProfileWrapper scopeProfile;

		private UniversalAdditionalCameraData baseCameraData;
		private UniversalAdditionalCameraData viewmodelCameraData;
		private UniversalAdditionalCameraData scopeCameraData;

		private bool hasActiveOverlay => viewmodelCameraData != null
			&& viewmodelCameraData.gameObject.activeInHierarchy
			&& viewmodelCameraData.GetComponent<Camera>().enabled;

		private enum EVolumeLayer
		{
			Base,
			Viewmodel,
			Scope,
		}

		private class VolumeProfileWrapper
		{
			public VolumeProfile profile;
			public ScreenSpaceAmbientOcclusionVolumeOverride ambientOcclusion;
			public Bloom bloom;
			public ChromaticAberration chromaticAberration;
			public ColorAdjustments colorGrading;
			public FilmGrain filmGrain;
			public ScreenSpaceReflectionVolumeSettings screenSpaceReflections;
			public Vignette vignette;
			public DepthOfField dof;
			public SrScope singleRenderScope;

			public VolumeProfileWrapper(VolumeProfile profile, EVolumeLayer layer)
			{
				this.profile = profile;

				ambientOcclusion = profile.Add<ScreenSpaceAmbientOcclusionVolumeOverride>(true);
				ambientOcclusion.active = false;
				ambientOcclusion.mode = ScreenSpaceAmbientOcclusionMode.GTAO;
				ambientOcclusion.intensity = 0.25f;

				bloom = profile.Add<Bloom>(true);
				bloom.active = false;
				bloom.intensity.Override(1f);
				bloom.scatter.Override(0.5f);

				colorGrading = profile.Add<ColorAdjustments>(true);
				colorGrading.active = false;

				chromaticAberration = profile.Add<ChromaticAberration>(true);
				chromaticAberration.active = false;

				filmGrain = profile.Add<FilmGrain>(true);
				filmGrain.active = false;
				filmGrain.intensity.Override(0.25f);

				screenSpaceReflections = profile.Add<ScreenSpaceReflectionVolumeSettings>(true);
				screenSpaceReflections.active = false;

				vignette = profile.Add<Vignette>(true);
				vignette.active = false;
				vignette.rounded.Override(true);

				if (layer == EVolumeLayer.Base)
				{
					// We currently use depth of field to as a background blur for the in-game dashboard. ;)
					dof = profile.Add<DepthOfField>(true);
					dof.active = false;
					dof.mode.Override(DepthOfFieldMode.Bokeh);
					dof.focusDistance.Override(1.0f);
				}

				if (layer != EVolumeLayer.Viewmodel)
				{
					profile.Add<SkyFog>(true).effectEnabled.Override(true);
				}

				if (layer == EVolumeLayer.Base)
				{
					singleRenderScope = profile.Add<SrScope>(true);
					singleRenderScope.active = false;
				}
			}
		}
	}
}
