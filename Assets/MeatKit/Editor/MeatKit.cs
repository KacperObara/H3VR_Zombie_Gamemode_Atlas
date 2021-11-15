﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetsTools.NET;
using UnityEditor;
using UnityEngine;

namespace MeatKit
{
    public static partial class MeatKit
    {
        public const string MeatKitDir = "Assets/MeatKit/";
        private static readonly string ManagedDirectory = Path.Combine(Application.dataPath, "MeatKit/Managed/");

        private static bool ShowErrorIfH3VRNotImported()
        {
#if (H3VR_IMPORTED == false)
            EditorUtility.DisplayDialog("Cannot continue.", "You don't have the H3 scripts imported. Please do that before trying to export anything.", "Ok");
            return true;
#endif
            return false;
        }


        [MenuItem("MeatKit/Scripts/Import Game", priority = 0)]
        public static void ImportAssemblies()
        {
            var gameManagedLocation =
                EditorUtility.OpenFolderPanel("Select H3VR Managed directory", string.Empty, "Managed");
            if (string.IsNullOrEmpty(gameManagedLocation)) return;
            ImportAssemblies(gameManagedLocation, ManagedDirectory);
        }

        [MenuItem("MeatKit/Scripts/Import Single", priority = 0)]
        public static void ImportSingleAssembly()
        {
            var assemblyLocation =
                EditorUtility.OpenFilePanel("Select assembly", null, "dll");
            if (string.IsNullOrEmpty(assemblyLocation)) return;
            ImportSingleAssembly(assemblyLocation, ManagedDirectory);
        }

        [MenuItem("MeatKit/Scripts/Export", priority = 0)]
        public static void ExportEditorScripts()
        {
            // Make sure the scripts are imported and there are no errors before exporting
            if (ShowErrorIfH3VRNotImported()) return;
            if (!BuildSettings.Instance.EnsureValidForEditor()) return;
            ExportEditorAssembly(BundleOutputPath);
        }


        [MenuItem("MeatKit/Asset Bundle/Export", priority = 1)]
        public static void ExportBundle()
        {
            var assetBundlePath = EditorUtility.OpenFilePanel("Select asset bundle", Application.dataPath, "");
            var settings = BuildSettings.Instance;
            var replaceMap = new Dictionary<string, string>
            {
                {"Assembly-CSharp.dll", settings.PackageName + ".dll"},
                {"Assembly-CSharp-firstpass.dll", settings.PackageName + "-firstpass.dll"},
                {"H3VRCode-CSharp.dll", "Assembly-CSharp.dll"},
                {"H3VRCode-CSharp-firstpass.dll", "Assembly-CSharp-firstpass.dll"}
            };

            ProcessBundle(assetBundlePath, assetBundlePath, replaceMap, AssetBundleCompressionType.LZ4);
        }

        [MenuItem("MeatKit/Asset Bundle/Import", priority = 1)]
        public static void ImportBundle()
        {
            var assetBundlePath = EditorUtility.OpenFilePanel("Select asset bundle", Application.dataPath, "");
            var replaceMap = new Dictionary<string, string>
            {
                {"Assembly-CSharp.dll", "H3VRCode-CSharp.dll"},
                {"Assembly-CSharp-firstpass.dll", "H3VRCode-CSharp-firstpass.dll"}
            };

            ProcessBundle(assetBundlePath, assetBundlePath + "-imported", replaceMap, AssetBundleCompressionType.LZ4);
        }

        [MenuItem("MeatKit/Build/Configure", priority = 2)]
        public static void ConfigureBuild()
        {
            Selection.activeObject = BuildSettings.Instance;
        }

        [MenuItem("MeatKit/Build/Clean", priority = 2)]
        public static void CleanBuild()
        {
            if (Directory.Exists(BundleOutputPath)) Directory.Delete(BundleOutputPath, true);
            Directory.CreateDirectory(BundleOutputPath);
        }

        [MenuItem("MeatKit/Build/Build", priority = 2)]
        public static void DoBuild()
        {
            // Make sure the scripts are imported.
            if (ShowErrorIfH3VRNotImported()) return;

            // If there's anything invalid in the settings don't continue
            var settings = BuildSettings.Instance;
            if (!settings.EnsureValidForEditor()) return;

            // Clean the output folder
            CleanBuild();

            // And export the assembly to the folder
            ExportEditorAssembly(BundleOutputPath);

            // Then get their asset bundle configurations
            var bundles = settings.BuildItems
                .Select(x => x.ConfigureBuild())
                .Where(x => x != null)
                .Select(x => x.Value).ToArray();

            BuildPipeline.BuildAssetBundles(BundleOutputPath, bundles, BuildAssetBundleOptions.None,
                BuildTarget.StandaloneWindows64);

            // Cleanup the unused files created with building the bundles
            foreach (var file in Directory.GetFiles(BundleOutputPath, "*.manifest"))
                File.Delete(file);
            File.Delete(Path.Combine(BundleOutputPath, "AssetBundles"));

            // With the bundles done building we can process them
            var replaceMap = new Dictionary<string, string>
            {
                {"Assembly-CSharp.dll", settings.PackageName + ".dll"},
                {"Assembly-CSharp-firstpass.dll", settings.PackageName + "-firstpass.dll"},
                {"H3VRCode-CSharp.dll", "Assembly-CSharp.dll"},
                {"H3VRCode-CSharp-firstpass.dll", "Assembly-CSharp-firstpass.dll"}
            };

            foreach (var bundle in bundles)
            {
                var path = Path.Combine(BundleOutputPath, bundle.assetBundleName);
                ProcessBundle(path, path, replaceMap, settings.BundleCompressionType);
            }

            // Now we can write the Thunderstore stuff to the folder
            settings.WriteThunderstoreManifest(BundleOutputPath + "manifest.json");
            File.Copy(AssetDatabase.GetAssetPath(settings.Icon), BundleOutputPath + "icon.png");
            File.Copy(AssetDatabase.GetAssetPath(settings.ReadMe), BundleOutputPath + "README.md");
        }

        [MenuItem("MeatKit/Clear Cache", priority = 3)]
        public static void ClearCache()
        {
            AssetDatabase.SaveAssets();

            if (Directory.Exists(ManagedDirectory))
                Directory.Delete(ManagedDirectory, true);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, "");
            AssetDatabase.Refresh();
        }
    }
}
