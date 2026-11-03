using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

[InitializeOnLoad]
public static class GameAudioSetup
{
    const string Folder = "Assets/Game/Resources";
    public const string BankPath = Folder + "/DeadShotSoundBank.asset";
    static GameAudioSetup() { EditorApplication.delayCall += Ensure; }
    [MenuItem("Dead Shot/Audio/Create or Select SE Bank")]
    public static void SelectBank() { Ensure(); Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameSoundBank>(BankPath); }
    static void Ensure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var existing = AssetDatabase.LoadAssetAtPath<GameSoundBank>(BankPath);
        if (existing != null)
        {
            var entries = new System.Collections.Generic.List<GameSoundBank.Entry>(existing.sounds);
            foreach (GameSound sound in Enum.GetValues(typeof(GameSound)))
                if (existing.Find(sound) == null)
                    entries.Add(new GameSoundBank.Entry { sound = sound, clips = new AudioClip[1],
                        spatial = sound == GameSound.GrenadeRoll || sound == GameSound.GrenadeExplosion || sound == GameSound.LightningThunder,
                        maxDistance = sound == GameSound.LightningThunder ? 250 : sound == GameSound.GrenadeExplosion ? 60 : 25 });
            if (entries.Count != existing.sounds.Length)
            {
                existing.sounds = entries.ToArray();
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                Debug.Log("[Audio] Added missing SE slots; existing clips preserved.");
            }
            return;
        }
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Game", "Resources");
        var bank = ScriptableObject.CreateInstance<GameSoundBank>();
        bank.sounds = new GameSoundBank.Entry[Enum.GetValues(typeof(GameSound)).Length];
        for (int i = 0; i < bank.sounds.Length; i++) bank.sounds[i] = new GameSoundBank.Entry {
            sound = (GameSound)i, clips = new AudioClip[i == 5 ? 2 : 1],
            spatial = (i != 1 && i != 2 && i != 3 && i < 7) || (GameSound)i == GameSound.GrenadeRoll || (GameSound)i == GameSound.GrenadeExplosion || (GameSound)i == GameSound.LightningThunder,
            maxDistance = (GameSound)i == GameSound.LightningThunder ? 250 : (GameSound)i == GameSound.GrenadeExplosion ? 60 : 25
        };
        // Editor限定APIで通常のAudioMixerアセットを作成。実行時には公開APIのみを使用。
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        var type = typeof(Editor).Assembly.GetType("UnityEditor.Audio.AudioMixerController");
        var mixer = type.GetMethod("CreateMixerControllerAtPath", flags).Invoke(null, new object[] { Folder + "/DeadShotAudio.mixer" });
        var master = type.GetProperty("masterGroup", flags).GetValue(mixer);
        var groupType = master.GetType();
        var groups = Array.CreateInstance(groupType, 4);
        string[] names = { "Effects", "UI", "Voice", "Ambience" };
        for (int i = 0; i < names.Length; i++)
            groups.SetValue(type.GetMethod("CreateNewGroup", flags).Invoke(mixer, new object[] { names[i], true }), i);
        groupType.GetProperty("children", flags).SetValue(master, groups);
        var parameterType = typeof(Editor).Assembly.GetType("UnityEditor.Audio.ExposedAudioParameter");
        var parameters = Array.CreateInstance(parameterType, 5);
        for (int i = 0; i < 5; i++)
        {
            var group = i == 0 ? master : groups.GetValue(i - 1);
            var parameter = Activator.CreateInstance(parameterType);
            parameterType.GetField("guid", flags).SetValue(parameter, groupType.GetMethod("GetGUIDForVolume", flags).Invoke(group, null));
            parameterType.GetField("name", flags).SetValue(parameter, (i == 0 ? "Master" : names[i - 1]) + "Volume");
            parameters.SetValue(parameter, i);
        }
        type.GetProperty("exposedParameters", flags).SetValue(mixer, parameters);
        bank.mixer = (AudioMixer)mixer;
        bank.effectsGroup = (AudioMixerGroup)groups.GetValue(0);
        bank.uiGroup = (AudioMixerGroup)groups.GetValue(1);
        bank.voiceGroup = (AudioMixerGroup)groups.GetValue(2);
        bank.ambienceGroup = (AudioMixerGroup)groups.GetValue(3);
        EditorUtility.SetDirty(bank.mixer);
        AssetDatabase.CreateAsset(bank, BankPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[DeadShot Audio] SEバンクとMaster/Effects/UI/Voice/AmbienceのMixerを作成しました。音源を登録してください。");
    }
}
