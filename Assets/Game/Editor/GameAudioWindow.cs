using UnityEditor;
using UnityEngine;

public class GameAudioWindow : EditorWindow
{
    Vector2 scroll;
    bool existing;
    string search = "";
    [MenuItem("Dead Shot/Audio/SE Management")]
    static void Open() { GetWindow<GameAudioWindow>("SE 一括管理"); }
    void OnGUI()
    {
        EditorGUILayout.HelpBox("新規SEはバンクで登録。既存SEは開いているシーンの元の設定を直接編集します。別シーン・PrefabのSEはそのシーンまたはPrefabを開いてください。", MessageType.Info);
        if (GUILayout.Button("SEバンクを選択")) GameAudioSetup.SelectBank();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        var bank = AssetDatabase.LoadAssetAtPath<GameSoundBank>(GameAudioSetup.BankPath);
        if (bank != null)
        {
            using (var so = new SerializedObject(bank))
            {
                so.Update();
                EditorGUILayout.PropertyField(so.FindProperty("sounds"), new GUIContent("共通SE（飲む音・回復音を含む）"), true);
                EditorGUILayout.PropertyField(so.FindProperty("menuBgm"), new GUIContent("インベントリ・設定BGM"));
                EditorGUILayout.PropertyField(so.FindProperty("menuBgmVolume"), new GUIContent("画面BGM音量"));
                EditorGUILayout.PropertyField(so.FindProperty("menuBgmFadeSeconds"), new GUIContent("画面BGMフェード秒数"));
                EditorGUILayout.PropertyField(so.FindProperty("voiceInterval"));
                EditorGUILayout.PropertyField(so.FindProperty("maxSimultaneousVoices"));
                EditorGUILayout.PropertyField(so.FindProperty("bloodDripDelay"));
                so.ApplyModifiedProperties();
            }
        }
        existing = EditorGUILayout.Foldout(existing, "既存のSE・AudioSource（現在のシーン）");
        if (existing)
        {
            search = EditorGUILayout.TextField("名前で絞り込み", search);
            foreach (var component in FindObjectsOfType<MonoBehaviour>(true))
            {
                if (component == null || (search.Length > 0 && !(component.name + component.GetType().Name).ToLower().Contains(search.ToLower()))) continue;
                using (var so = new SerializedObject(component))
                {
                    var p = so.GetIterator();
                    bool label = false;
                    while (p.NextVisible(true))
                    {
                        if (p.propertyType != SerializedPropertyType.ObjectReference ||
                            (!p.type.Contains("AudioClip") && !p.type.Contains("AudioSource"))) continue;
                        if (!label) { EditorGUILayout.ObjectField(component.name + " / " + component.GetType().Name, component, typeof(MonoBehaviour), true); label = true; }
                        EditorGUILayout.PropertyField(p, true);
                    }
                    so.ApplyModifiedProperties();
                }
            }
            foreach (var source in FindObjectsOfType<AudioSource>(true))
            {
                if (search.Length > 0 && !source.name.ToLower().Contains(search.ToLower())) continue;
                EditorGUILayout.ObjectField("音源: " + source.name, source, typeof(AudioSource), true);
                using (var so = new SerializedObject(source))
                {
                    foreach (var field in new[] { "m_audioClip", "m_Volume", "m_Pitch", "m_OutputAudioMixerGroup" })
                    {
                        var p = so.FindProperty(field);
                        if (p != null) EditorGUILayout.PropertyField(p);
                    }
                    so.ApplyModifiedProperties();
                }
            }
        }
        EditorGUILayout.EndScrollView();
    }
}
