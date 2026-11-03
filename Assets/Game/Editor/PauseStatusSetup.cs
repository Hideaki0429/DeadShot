using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PauseStatusSetup
{
    static PauseStatusSetup() { EditorApplication.delayCall += Configure; }
    static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Stage_city") return;
        foreach (var p in Object.FindObjectsOfType<PauseMenuController>(true))
        {
            if (p.gameObject.scene != scene || p.mainMenuGroup == null) continue;
            if (p.pauseTimeText != null && p.pauseScoreText != null && p.pauseKillText != null) continue;
            var items = p.mainMenuGroup.transform.Find("項目");
            if (items == null) continue;
            var time = items.Find("制限時間 (1)");
            var score = items.Find("スコア (1)");
            var kills = items.Find("撃破数 (1)");
            if (time == null || score == null || kills == null) continue;
            if (p.pauseTimeText == null) p.pauseTimeText = time.GetComponent<TMP_Text>();
            if (p.pauseScoreText == null) p.pauseScoreText = score.GetComponent<TMP_Text>();
            if (p.pauseKillText == null) p.pauseKillText = kills.GetComponent<TMP_Text>();
            EditorUtility.SetDirty(p);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PauseStatus] タイム・スコア・撃破数の既存TMPを登録しました。");
        }
    }
}
