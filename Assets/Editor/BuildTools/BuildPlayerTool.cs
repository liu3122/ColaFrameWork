﻿//----------------------------------------------
//            ColaFramework
// Copyright © 2018-2049 ColaFramework 马三小伙儿
//----------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;
using Plugins.XAsset;

namespace ColaFramework.ToolKit { 
    public static class BuildPlayerTool
    {
        public static string overloadedDevelopmentServerURL = "";

        public static void CopyAssetBundlesTo(string outputPath)
        {
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);
            var source = Path.Combine(Environment.CurrentDirectory, Utility.AssetBundles, GetPlatformName());
            if (!Directory.Exists(source))
                Debug.Log("No assetBundle output folder, try to build the assetBundles first.");
            if (Directory.Exists(outputPath))
                FileUtil.DeleteFileOrDirectory(outputPath);
            FileUtil.CopyFileOrDirectory(source, outputPath);
        }

        public static string GetPlatformName()
        {
            return GetPlatformForAssetBundles(EditorUserBuildSettings.activeBuildTarget);
        }

        private static string GetPlatformForAssetBundles(BuildTarget target)
        {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (target)
            {
                case BuildTarget.Android:
                    return "Android";
                case BuildTarget.iOS:
                    return "iOS";
                case BuildTarget.WebGL:
                    return "WebGL";
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return "Windows";
#if UNITY_2017_3_OR_NEWER
                case BuildTarget.StandaloneOSX:
                    return "OSX";
#else
                case BuildTarget.StandaloneOSXIntel:
                case BuildTarget.StandaloneOSXIntel64:
                case BuildTarget.StandaloneOSX:
                    return "OSX";
#endif
                default:
                    return null;
            }
        }

        private static string[] GetLevelsFromBuildSettings()
        {
            return EditorBuildSettings.scenes.Select(scene => scene.path).ToArray();
        }

        private static string GetAssetBundleManifestFilePath()
        {
            var relativeAssetBundlesOutputPathForPlatform = Path.Combine(Utility.AssetBundles, GetPlatformName());
            return Path.Combine(relativeAssetBundlesOutputPathForPlatform, GetPlatformName()) + ".manifest";
        }

        public static void BuildStandalonePlayer()
        {
            var outputPath = EditorUtility.SaveFolderPanel("Choose Location of the Built Game", "", "");
            if (outputPath.Length == 0)
                return;

            var levels = GetLevelsFromBuildSettings();
            if (levels.Length == 0)
            {
                Debug.Log("Nothing to build.");
                return;
            }

            var targetName = GetBuildTargetName(EditorUserBuildSettings.activeBuildTarget);
            if (targetName == null)
                return;
#if UNITY_5_4 || UNITY_5_3 || UNITY_5_2 || UNITY_5_1 || UNITY_5_0
			BuildOptions option = EditorUserBuildSettings.development ? BuildOptions.Development : BuildOptions.None;
			BuildPipeline.BuildPlayer(levels, outputPath + targetName, EditorUserBuildSettings.activeBuildTarget, option);
#else
            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = levels,
                locationPathName = outputPath + targetName,
                assetBundleManifestPath = GetAssetBundleManifestFilePath(),
                target = EditorUserBuildSettings.activeBuildTarget,
                options = EditorUserBuildSettings.development ? BuildOptions.Development : BuildOptions.None
            };
            BuildPipeline.BuildPlayer(buildPlayerOptions);
#endif
        }

        public static string CreateAssetBundleDirectory()
        {
            // Choose the output path according to the build target.
            var outputPath = Path.Combine(Utility.AssetBundles, GetPlatformName());
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            return outputPath;
        }

        private static Dictionary<string, string> GetVersions(AssetBundleManifest manifest)
        {
            var items = manifest.GetAllAssetBundles();
            return items.ToDictionary(item => item, item => manifest.GetAssetBundleHash(item).ToString());
        }

        private static void LoadVersions(string versionsTxt, IDictionary<string, string> versions)
        {
            if (versions == null)
                throw new ArgumentNullException("versions");
            if (!File.Exists(versionsTxt))
                return;
            using (var s = new StreamReader(versionsTxt))
            {
                string line;
                while ((line = s.ReadLine()) != null)
                {
                    if (line == string.Empty)
                        continue;
                    var fields = line.Split(':');
                    if (fields.Length > 1)
                        versions.Add(fields[0], fields[1]);
                }
            }
        }

        private static void SaveVersions(string versionsTxt, Dictionary<string, string> versions)
        {
            if (File.Exists(versionsTxt))
                File.Delete(versionsTxt);
            using (var s = new StreamWriter(versionsTxt))
            {
                foreach (var item in versions)
                    s.WriteLine(item.Key + ':' + item.Value);
                s.Flush();
                s.Close();
            }
        }

        public static void RemoveUnusedAssetBundleNames()
        {
            AssetDatabase.RemoveUnusedAssetBundleNames();
        }

        public static void SetAssetBundleNameAndVariant(string assetPath, string bundleName, string variant)
        {
            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null) return;
            importer.assetBundleName = bundleName;
            importer.assetBundleVariant = variant;
        }

        public static void BuildManifest()
        {
            var manifest = GetManifest();

            var assetPath = TrimedAssetDirName(AssetDatabase.GetAssetPath(manifest));
            var bundleName = Path.GetFileNameWithoutExtension(assetPath).ToLower();
            SetAssetBundleNameAndVariant(assetPath, bundleName, null);

            AssetDatabase.RemoveUnusedAssetBundleNames();
            var bundles = AssetDatabase.GetAllAssetBundleNames();

            List<string> dirs = new List<string>();
            List<AssetData> assets = new List<AssetData>();

            for (int i = 0; i < bundles.Length; i++)
            {
                if(i ==27 || bundles[i].Contains("manifest.asset"))
                {

                }
                var paths = AssetDatabase.GetAssetPathsFromAssetBundle(bundles[i]);
                foreach (var path in paths)
                {
                    var dir = TrimedAssetDirName(path);
                    var index = dirs.FindIndex((o) => o.Equals(dir));
                    if (index == -1)
                    {
                        index = dirs.Count;
                        dirs.Add(dir);
                    }

                    var asset = new AssetData();
                    asset.bundle = i;
                    asset.dir = index;
                    asset.name = Path.GetFileName(path);

                    assets.Add(asset);
                }
            }

            manifest.bundles = bundles;
            manifest.dirs = dirs.ToArray();
            manifest.assets = assets.ToArray();

            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static string TrimedAssetDirName(string assetDirName)
        {
            assetDirName = assetDirName.Replace("\\", "/");
            assetDirName = assetDirName.Replace(Constants.GameAssetBasePath, "");
            return Path.GetDirectoryName(assetDirName).Replace("\\", "/"); ;
        }

        public static void BuildAssetBundles()
        {
            // Choose the output path according to the build target.
            var outputPath = CreateAssetBundleDirectory();

            const BuildAssetBundleOptions options = BuildAssetBundleOptions.ChunkBasedCompression;

            var manifest =
                BuildPipeline.BuildAssetBundles(outputPath, options,
                    EditorUserBuildSettings.activeBuildTarget);
            var versionsTxt = outputPath + "/versions.txt";
            var versions = new Dictionary<string, string>();
            LoadVersions(versionsTxt, versions);

            var buildVersions = GetVersions(manifest);

            var updates = new List<string>();

            foreach (var item in buildVersions)
            {
                string hash;
                var isNew = true;
                if (versions.TryGetValue(item.Key, out hash))
                    if (hash.Equals(item.Value))
                        isNew = false;
                if (isNew)
                    updates.Add(item.Key);
            }

            if (updates.Count > 0)
            {
                using (var s = new StreamWriter(File.Open(outputPath + "/updates.txt", FileMode.Append)))
                {
                    s.WriteLine(DateTime.Now.ToFileTime() + ":");
                    foreach (var item in updates)
                        s.WriteLine(item);
                    s.Flush();
                    s.Close();
                }

                SaveVersions(versionsTxt, buildVersions);
            }
            else
            {
                Debug.Log("nothing to update.");
            }

            string[] ignoredFiles = { GetPlatformName(), "versions.txt", "updates.txt", "manifest" };

            var files = Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories);

            var deletes = (from t in files
                           let file = t.Replace('\\', '/').Replace(outputPath.Replace('\\', '/') + '/', "")
                           where !file.EndsWith(".manifest", StringComparison.Ordinal) && !Array.Exists(ignoredFiles, s => s.Equals(file))
                           where !buildVersions.ContainsKey(file)
                           select t).ToList();

            foreach (var delete in deletes)
            {
                if (!File.Exists(delete))
                    continue;
                File.Delete(delete);
                File.Delete(delete + ".manifest");
            }

            deletes.Clear();
        }

        private static string GetBuildTargetName(BuildTarget target)
        {
            var name = PlayerSettings.productName + "_" + PlayerSettings.bundleVersion;
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (target)
            {
                case BuildTarget.Android:
                    return "/" + name + PlayerSettings.Android.bundleVersionCode + ".apk";

                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return "/" + name + PlayerSettings.Android.bundleVersionCode + ".exe";

#if UNITY_2017_3_OR_NEWER
                case BuildTarget.StandaloneOSX:
                    return "/" + name + ".app";

#else
                    case BuildTarget.StandaloneOSXIntel:
                    case BuildTarget.StandaloneOSXIntel64:
                    case BuildTarget.StandaloneOSX:
                                        return "/" + name + ".app";

#endif

                case BuildTarget.WebGL:
                case BuildTarget.iOS:
                    return "";
                // Add more build targets for your own.
                default:
                    Debug.Log("Target not implemented.");
                    return null;
            }
        }

        private static T GetAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
            }

            return asset;
        }

        public static AssetsManifest GetManifest()
        {
            return GetAsset<AssetsManifest>(Utility.AssetsManifestAsset);
        }

        public static string GetServerURL()
        {
            string downloadURL;
            if (string.IsNullOrEmpty(overloadedDevelopmentServerURL) == false)
            {
                downloadURL = overloadedDevelopmentServerURL;
            }
            else
            {
                IPHostEntry host;
                string localIP = "";
                host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (IPAddress ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        localIP = ip.ToString();
                        break;
                    }
                }

                downloadURL = "http://" + localIP + ":7888/";
            }

            return downloadURL;
        }

        /// <summary>
        /// 清除所有的AB Name
        /// </summary>
        public static void ClearAllAssetBundleName()
        {
            string[] oldAssetBundleNames = AssetDatabase.GetAllAssetBundleNames();
            var length = oldAssetBundleNames.Length;
            for (int i =0; i < length; i++)
            {
                EditorUtility.DisplayProgressBar("清除AssetBundleName", "正在清除AssetBundleName", i *1f / length);
                AssetDatabase.RemoveAssetBundleName(oldAssetBundleNames[i], true);
            }
            EditorUtility.ClearProgressBar();
        }
    }
}