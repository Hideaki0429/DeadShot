using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class GameInputSetup
{
    static GameInputSetup() { EditorApplication.delayCall += EnsureSettings; }
    private static void EnsureSettings()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        const string path = "Assets/Game/Resources/GameInputManager.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) { UpdateTutorialHints(); return; }
        if (!AssetDatabase.IsValidFolder("Assets/Game/Resources"))
            AssetDatabase.CreateFolder("Assets/Game", "Resources");
        GameObject root = new GameObject("Game Input Manager");
        try
        {
            root.AddComponent<GameInputManager>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("[GameInput] 入力設定Prefabを作成しました: " + path);
        }
        finally { Object.DestroyImmediate(root); }
        UpdateTutorialHints();
    }
    private static void UpdateTutorialHints()
    {
        foreach (TutorialUIManager2 tutorial in Object.FindObjectsOfType<TutorialUIManager2>(true))
        {
            if (tutorial.gameObject.scene.name != "Stage_city") continue;
            var serialized = new SerializedObject(tutorial);
            var instructions = serialized.FindProperty("tutorialInstructions");
            if (instructions == null) continue;
            for (int i = 0; i < instructions.arraySize; i++)
            {
                var step = instructions.GetArrayElementAtIndex(i);
                var key = step.FindPropertyRelative("requiredKey");
                var hint = step.FindPropertyRelative("controlHint");
                var message = step.FindPropertyRelative("captainMessage");
                if (key.intValue == (int)KeyCode.C) key.intValue = (int)KeyCode.Mouse1;
                if (key.intValue == (int)KeyCode.Q) key.intValue = (int)KeyCode.E;
                string text = hint.stringValue;
                text = text.Replace("WASD/↑←↓→", "WASD（Shift：走る）")
                    .Replace("構え/解除：C", "構え/解除：右クリック")
                    .Replace("視点切り替え: Q", "視点切り替え: E")
                    .Replace("エンターキー、右クリック", "左クリック")
                    .Replace("エンタキー、右クリック", "左クリック")
                    .Replace("リロード：自動（AUTO)", "リロード：R（弾切れ時は自動）");
                hint.stringValue = text;
                message.stringValue = message.stringValue.Replace("Qで視点", "Eで視点");
            }
            if (serialized.ApplyModifiedProperties())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(tutorial.gameObject.scene);
                Debug.Log("[GameInput] Stage_cityのチュートリアル案内を更新しました。シーン保存で保持できます。");
            }
        }
    }
}
