using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

public class InitAssetBundleManagerPackage
{
	private const string PACKAGE_ASSETS_PATH = "Packages/com.miniit.asset-bundle-manager/Assets~";
	
#if UNITY_EDITOR && !UNITY_CLOUD_BUILD
	[InitializeOnLoadMethod]
	static void InitPackage()
	{
		CopyDirectory(Path.GetFullPath(PACKAGE_ASSETS_PATH), Application.dataPath);
	}
#endif
	
	private static void CopyDirectory(string src, string dst)
	{
		DirectoryInfo dir = new DirectoryInfo(src);
		
		if (!Directory.Exists(dst))
			Directory.CreateDirectory(dst);
		
		FileInfo[] files = dir.GetFiles();
		foreach (FileInfo file in files)
		{
			string filepath = Path.Combine(dst, file.Name);
			if (!File.Exists(filepath))
				file.CopyTo(filepath, false);
		}
		
		DirectoryInfo[] dirs = dir.GetDirectories();
		foreach (DirectoryInfo subdir in dirs)
		{
			CopyDirectory(subdir.FullName, Path.Combine(dst, subdir.Name));
		}
	}
	
}