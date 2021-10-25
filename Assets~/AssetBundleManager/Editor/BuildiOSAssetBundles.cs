#if UNITY_IOS

using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

public class BuildiOSAssetBundles
{
#if UNITY_CLOUD_BUILD
	[InitializeOnLoadMethod]
	static void SetupResourcesBuild()
	{
		UnityEditor.iOS.BuildPipeline.collectResources += CollectResources;
	}
#endif

	static UnityEditor.iOS.Resource[] CollectResources()
	{
		Debug.Log("[BuildiOSAssetBundles] CollectResources - start");

		var resources = new List<UnityEditor.iOS.Resource>();

		string platform = "iOS";
		string direcotry_path = Path.Combine(AssetBundles.Utility.AssetBundlesOutputPath, platform);
		if (Directory.Exists(direcotry_path))
		{
			var files = Directory.GetFiles(direcotry_path);
			foreach (string file_path in files)
			{
				if (file_path.EndsWith(".manifest"))
					continue;

				string file_name = Path.GetFileNameWithoutExtension(file_path);
				if (file_name == platform)
					continue;

				Debug.Log($"[BuildiOSAssetBundles] Add: {file_name} - {file_path}");
				resources.Add(new UnityEditor.iOS.Resource(file_name, file_path).AddOnDemandResourceTags(file_name));
			}
		}

		Debug.Log("[BuildiOSAssetBundles] CollectResources - done");

		return resources.ToArray();
	}

	[MenuItem("Bundle/Build iOS AssetBundle")]
	static void BuildAssetBundles()
	{
		var options = BuildAssetBundleOptions.None;
		var buildTarget = BuildTarget.iOS; // EditorUserBuildSettings.activeBuildTarget

		string path = Path.Combine("AssetBundles", $"{buildTarget}");
		string fullPath = Path.Combine(Application.dataPath, "..", path);
		if (Directory.Exists(fullPath))
			Directory.Delete(fullPath, true);
		Directory.CreateDirectory(fullPath);
		
		UnityEditor.iOS.BuildPipeline.collectResources += CollectResources;
		
		BuildPipeline.BuildAssetBundles(path, options, buildTarget);
		
		UnityEditor.iOS.BuildPipeline.collectResources -= CollectResources;
	}

}

#endif