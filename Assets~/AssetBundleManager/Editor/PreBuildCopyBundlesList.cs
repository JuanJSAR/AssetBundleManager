using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PreBuildCopyBundlesList", menuName = "ScriptableObjects/PreBuildCopyBundlesList", order = 1)]
public class PreBuildCopyBundlesList : ScriptableObject
{
    public string[] Bundles;
}
