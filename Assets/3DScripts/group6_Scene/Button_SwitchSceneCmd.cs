using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;	// シーン切り替えに必要

// UIボタンから呼び出す、シーンを切り換える命令
public class Button_SwitchSceneCmd : MonoBehaviour
{
	public string sceneName = "";  // シーン名：Inspectorで指定

	[Header("遷移先ローディング")]
	[Tooltip("オンの場合、ステージへ直接移動せず専用Loadingシーンを経由します。基本はオフです。")]
	[SerializeField] private bool useLoadingScene = false;

	[Tooltip("専用Loadingシーンの名前")]
	[SerializeField] private string loadingSceneName = "Loading";

	[Tooltip("オンにしたボタンだけ、遷移先のOpeningNewsSequenceへ読み込み完了を通知します。基本はオフのまま使用します。")]
	[SerializeField] private bool sendCompleteLoadingAfterSceneLoad = false;

	private static bool pendingCompleteLoadingRequest;

	public void SwitchScene()
	{
		if (useLoadingScene)
		{
			if (LoadingSceneRequest.IsLoading) return;
			if (string.IsNullOrEmpty(sceneName))
			{
				Debug.LogError(
					"[Button_SwitchSceneCmd] Loadingシーン経由では遷移先Scene Nameを設定してください。",
					this
				);
				return;
			}

			if (string.IsNullOrEmpty(loadingSceneName))
			{
				Debug.LogError(
					"[Button_SwitchSceneCmd] Loading Scene Nameが未設定です。",
					this
				);
				return;
			}

			if (!Application.CanStreamedLevelBeLoaded(sceneName) ||
			    !Application.CanStreamedLevelBeLoaded(loadingSceneName))
			{
				Debug.LogError("[Button_SwitchSceneCmd] 遷移先またはLoadingシーンをBuild Settingsで確認してください。", this);
				return;
			}
			Time.timeScale = 1f;
			AudioListener.pause = false;
			pendingCompleteLoadingRequest = false;
			LoadingSceneRequest.Begin(sceneName);
			SceneManager.LoadScene(loadingSceneName);
			return;
		}

		// Title2には遷移先のOpeningNewsSequenceがまだ存在しないため、
		// 遷移後に一度だけ受け取れる静的な通知予約として保存します。
		pendingCompleteLoadingRequest =
			sendCompleteLoadingAfterSceneLoad;

		if (sceneName != "")  // シーン名があれば、切り換える
		{
			SceneManager.LoadScene (sceneName);
		} else
		{	// シーン名がなければ、次のシーンへ切り換える
			int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
			if (nextIndex < SceneManager.sceneCountInBuildSettings){
				SceneManager.LoadScene(nextIndex);
			} else
			{	// 次のシーンがなければ、最初のシーンに切り換える
				SceneManager.LoadScene(0);
			}
		}
	}

	/// <summary>
	/// 遷移先のOpeningNewsSequenceが完了通知予約を一度だけ受け取ります。
	/// 受け取った時点で予約を消去するため、次回の遷移には残りません。
	/// </summary>
	public static bool ConsumeCompleteLoadingRequest()
	{
		bool requested = pendingCompleteLoadingRequest;
		pendingCompleteLoadingRequest = false;
		return requested;
	}
}
