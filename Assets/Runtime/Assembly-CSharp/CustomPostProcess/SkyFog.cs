////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using SDG.Framework.Devkit;
using SDG.Framework.Water;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SDG.Unturned
{
	[Serializable]
	[VolumeComponentMenu("Unturned/Sky and underwater fog")]
	[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
	public sealed class SkyFog : VolumeComponent, IPostProcessComponent
	{
		public BoolParameter effectEnabled = new BoolParameter(false);
		public bool IsActive() => active && effectEnabled.value && RenderSettings.skybox != null;
	}

	public sealed class SkyFogRenderer
	{
		public SkyFogRenderer()
		{

			fogColorId = Shader.PropertyToID("_FogColor");
			skyColorId = Shader.PropertyToID("_SkyColor");
			equatorColorId = Shader.PropertyToID("_EquatorColor");
			groundColorId = Shader.PropertyToID("_GroundColor");
			inverseProjectionMatrixId = Shader.PropertyToID("_InverseProjectionMatrix");
			cameraToWorldMatrixId = Shader.PropertyToID("_CameraToWorld");

			waterColorId = Shader.PropertyToID("_WaterColor");
			isCameraUnderwaterId = Shader.PropertyToID("_IsCameraUnderwater");
			waterCountId = Shader.PropertyToID("_WaterCount");
			waterMatricesId = Shader.PropertyToID("_WaterMatrices");
		}

		public MaterialPropertyBlock Prepare(Camera camera)
		{
			var properties = new MaterialPropertyBlock();

			// _FogColor uniform is declared in Fog.hlsl
			properties.SetColor(fogColorId, RenderSettings.fogColor);

			properties.SetColor(skyColorId, RenderSettings.skybox.GetColor(skyColorId));
			properties.SetColor(equatorColorId, RenderSettings.skybox.GetColor(equatorColorId));
			properties.SetColor(groundColorId, RenderSettings.skybox.GetColor(groundColorId));

			properties.SetMatrix(inverseProjectionMatrixId, camera.projectionMatrix.inverse);
			properties.SetMatrix(cameraToWorldMatrixId, camera.cameraToWorldMatrix);

			FindRelevantWaterVolumes(camera.transform.position);
			int waterCount = LevelLighting.enableUnderwaterEffects ? Mathf.Min(relevantWaterVolumes.Count, MAX_WATER_COUNT) : 0;
			// Disable underwater effects if we do not have any water, otherwise values from level may affect the menu.
			bool isCameraUnderwater = LevelLighting.isSea && waterCount > 0;
			properties.SetColor(waterColorId, LevelLighting.getSeaColor("_BaseColor"));
			properties.SetFloat(isCameraUnderwaterId, isCameraUnderwater ? 1.0f : 0.0f);
			properties.SetInt(waterCountId, waterCount);
			for (int waterIndex = 0; waterIndex < waterCount; ++waterIndex)
			{
				waterMatrices[waterIndex] = relevantWaterVolumes[waterIndex].volume.transform.worldToLocalMatrix;
			}
			properties.SetMatrixArray(waterMatricesId, waterMatrices);

			return properties;
		}

		private void FindRelevantWaterVolumes(Vector3 viewPosition)
		{
			UnityEngine.Profiling.Profiler.BeginSample("FindRelevantWaterVolumes");
			relevantWaterVolumes.Clear();
			List<WaterVolume> allVolumes = WaterVolumeManager.Get().InternalGetAllVolumes();
			if (allVolumes.Count > MAX_WATER_COUNT)
			{
				foreach (WaterVolume volume in allVolumes)
				{
					Vector3 closestPoint = volume.GetClosestWorldPosition(viewPosition);
					float sqrDistance = (viewPosition - closestPoint).sqrMagnitude;
					//RuntimeGizmos.Get().Cube(closestPoint, 0.1f, Color.red);
					if (sqrDistance < 4.0f)
					{
						relevantWaterVolumes.Add(new VolumeAlphaPair<WaterVolume>(volume, sqrDistance));
					}
				}
				relevantWaterVolumes.Sort(volumeComparison);
			}
			else
			{
				foreach (WaterVolume volume in allVolumes)
				{
					relevantWaterVolumes.Add(new VolumeAlphaPair<WaterVolume>(volume, 0f));
				}
			}
			UnityEngine.Profiling.Profiler.EndSample();
		}

		private int fogColorId;
		private int skyColorId;
		private int equatorColorId;
		private int groundColorId;
		private int inverseProjectionMatrixId;
		private int cameraToWorldMatrixId;

		private int waterColorId;
		private int isCameraUnderwaterId;
		private int waterCountId;
		private int waterMatricesId;

		private const int MAX_WATER_COUNT = 3;
		private static Matrix4x4[] waterMatrices = new Matrix4x4[MAX_WATER_COUNT];
		private static List<VolumeAlphaPair<WaterVolume>> relevantWaterVolumes = new List<VolumeAlphaPair<WaterVolume>>();
		private static System.Comparison<VolumeAlphaPair<WaterVolume>> volumeComparison = CompareVolumes;
		private static int CompareVolumes(VolumeAlphaPair<WaterVolume> lhs, VolumeAlphaPair<WaterVolume> rhs)
		{
			return lhs.alpha.CompareTo(rhs.alpha);
		}
	}
}
