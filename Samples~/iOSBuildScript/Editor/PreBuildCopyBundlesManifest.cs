//#if UNITY_CLOUD_BUILD
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

class PreBuildCopyBundlesManifest : IPreprocessBuildWithReport
{
	public int callbackOrder => 10;
	
	public void OnPreprocessBuild(BuildReport report)
	{
		CopyBundlesManifest();
	}
	
	[MenuItem("Bundle/Copy Manifest to StreamingAssets")]
	static void CopyBundlesManifest()
	{
		Debug.Log("[PreBuildCopyBundlesManifest] CopyBundlesManifest - start");
		
		string path = Path.Combine(Application.streamingAssetsPath, "Android");
		FileUtil.DeleteFileOrDirectory(path);
		FileUtil.DeleteFileOrDirectory($"{path}.meta");
		path = Path.Combine(Application.streamingAssetsPath, "iOS");
		FileUtil.DeleteFileOrDirectory(path);
		FileUtil.DeleteFileOrDirectory($"{path}.meta");
		
		string platform = AssetBundles.Utility.GetPlatformName();
		path = Path.Combine(Application.streamingAssetsPath, platform);
		FileUtil.DeleteFileOrDirectory(path);
		FileUtil.DeleteFileOrDirectory($"{path}.meta");
		
		Directory.CreateDirectory(path);
		
		FileUtil.CopyFileOrDirectory(Path.Combine(AssetBundles.Utility.AssetBundlesOutputPath, platform, platform), Path.Combine(path, platform));
		FileUtil.CopyFileOrDirectory(Path.Combine(AssetBundles.Utility.AssetBundlesOutputPath, platform, $"{platform}.manifest"), Path.Combine(path, $"{platform}.manifest"));
		
		Debug.Log("[PreBuildCopyBundlesManifest] CopyBundlesManifest - done - " + platform);
		
		AssetDatabase.Refresh();
	}
}
//#endif 