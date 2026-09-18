#nullable enable
using System.IO;
using Muhanok.Composition;
using Muhanok.Infrastructure;
using Muhanok.Presentation;
using UnityEditor;
using UnityEngine;

namespace Muhanok.Editor
{
    /// Assets/Settings/ 에 기본 설정 에셋 3개를 만들고 서로 연결한다. 이미 있으면 건드리지 않는다.
    /// 씬 준비 절차(CLAUDE.md §14.1)의 4번에서 GameConfig.asset을 쓴다.
    public static class SettingsAssetsMenu
    {
        private const string Folder = "Assets/Settings";

        [MenuItem("Muhanok/Create Default Settings Assets")]
        public static void CreateDefaults()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "Settings");

            var tuning = LoadOrCreate<TuningProfile>(Path.Combine(Folder, "TuningProfile.asset"));
            var presentation = LoadOrCreate<PresentationProfile>(Path.Combine(Folder, "PresentationProfile.asset"));
            var config = LoadOrCreate<GameConfig>(Path.Combine(Folder, "GameConfig.asset"));

            var changed = false;
            if (config.tuning == null) { config.tuning = tuning; changed = true; }
            if (config.presentation == null) { config.presentation = presentation; changed = true; }
            if (changed) EditorUtility.SetDirty(config);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = config;
            Debug.Log("[Muhanok] settings assets ready: " + Folder);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }
    }
}
