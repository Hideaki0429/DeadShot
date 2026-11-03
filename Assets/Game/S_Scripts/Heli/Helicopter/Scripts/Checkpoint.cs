// 【役割】チェックポイントへの到達を判定する。
using System;
using UnityEngine;
using System.Collections;

public class Checkpoint : MonoBehaviour
{
    public Action CheckpointActivated;
    // Use this for initialization
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
    }

    // トリガー範囲へ入った相手を受け取り、以下の条件に合う場合の処理を行います。
    private void OnTriggerEnter()
    {
        Debug.Log("Activate");
        if (CheckpointActivated != null)
        {
            CheckpointActivated();
        }
    }
}
