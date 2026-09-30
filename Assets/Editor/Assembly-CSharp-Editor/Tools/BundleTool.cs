////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SDG.Unturned.Tools
{
	/// <summary>
	/// Selects a folder of assets and builds a bundle with the current build pipeline.
	/// </summary>
	public class BundleTool : EditorWindow
	{
		[MenuItem("Window/Unturned/Bundle Tool")]
		public static void ShowWindow()
		{
			GetWindow(typeof(BundleTool));
		}

		/// <summary>
		/// Selected folder.
		/// </summary>
		private static Object focus;

		/// <summary>
		/// Assets in the selected folder. The build pipeline includes their dependencies.
		/// </summary>
		private static Object[] selection;

		/// <summary>
		/// State of the scroll view showing the asset names.
		/// </summary>
		private static Vector2 scroll;

		/// <summary>
		/// Path to the last saved file.
		/// </summary>
		private static string path;

		/// <summary>
		/// Finds the selected folder and the assets inside.
		/// </summary>
		private void grabAssets()
		{
			if (Selection.activeObject == null)
			{
				clearAssets();

				Debug.LogError("Failed to find a selected file.");
				return;
			}

			focus = Selection.activeObject;
			selection = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);
		}

		/// <summary>
		/// Resets our selection.
		/// </summary>
		private void clearAssets()
		{
			focus = null;
			selection = null;
			scroll = Vector2.zero;
		}

		/// <summary>
		/// Creates an assetbundle at the provided path.
		/// </summary>
		/// <param name="path">Path to assetbundle.</param>
		private void bundleAssets()
		{
			if (path.Length > 0 && selection.Length > 0)
			{
				string[] assetPaths = selection
					.Where(asset => asset != null && !(asset is MonoScript))
					.Select(AssetDatabase.GetAssetPath)
					.Where(assetPath => (assetPath.StartsWith("Assets/") || assetPath.StartsWith("Packages/"))
						&& !AssetDatabase.IsValidFolder(assetPath))
					.Distinct()
					.ToArray();
				if (assetPaths.Length == 0)
				{
					Debug.LogError("No buildable assets in the selection.");
					return;
				}

				// Build in a unique staging directory so Unity's manifests and lowercase
				// bundle names do not change the user's chosen destination filename.
				string outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "BundleTool", System.Guid.NewGuid().ToString("N")));
				Directory.CreateDirectory(outputDirectory);
				try
				{
					AssetBundleBuild build = new AssetBundleBuild
					{
						assetBundleName = "selection.unity3d",
						assetNames = assetPaths
					};
					AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(outputDirectory, new[] { build },
						BuildAssetBundleOptions.UncompressedAssetBundle, BuildTarget.StandaloneWindows64);
					if (manifest == null)
					{
						Debug.LogError("Failed to build bundle for \"" + focus.name + "\"!");
						return;
					}
					File.Copy(Path.Join(outputDirectory, build.assetBundleName), path, overwrite: true);
				}
				finally
				{
					Directory.Delete(outputDirectory, recursive: true);
				}

				Debug.Log("Successfully built bundle for \"" + focus.name + "\"!");

				clearAssets();
			}
		}

		private void OnGUI()
		{
			if (GUILayout.Button("Grab"))
			{
				grabAssets();
			}

			if (focus != null)
			{
				GUILayout.Space(20);
				GUILayout.Label("Assets:");

				GUILayout.BeginVertical();
				scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(200));
				for (int index = 0; index < selection.Length; index++)
				{
					GUILayout.BeginHorizontal();

					Texture2D thumb = AssetPreview.GetMiniTypeThumbnail(selection[index].GetType());
					if (thumb != null)
					{
						GUILayout.Label(thumb, GUILayout.Width(20), GUILayout.Height(20));
					}

					GUILayout.Label(selection[index].name);

					GUILayout.EndHorizontal();
				}
				GUILayout.EndScrollView();
				GUILayout.EndVertical();

				GUILayout.Space(20);

				if (GUILayout.Button("Bundle " + focus.name))
				{
					path = EditorUtility.SaveFilePanel("Save Bundle", path, focus.name, "unity3d");

					bundleAssets();
				}

				if (GUILayout.Button("Clear"))
				{
					clearAssets();
				}
			}
		}

		private void OnEnable()
		{
			titleContent = new GUIContent("Bundle Tool");

			if (path == null || path.Length == 0)
			{
				path = new DirectoryInfo(Application.dataPath).Parent.ToString();
			}
		}
	}
}
