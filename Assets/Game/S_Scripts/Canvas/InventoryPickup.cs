// 【役割】取得対象や支給品箱の内容、取得可否と取得時の処理を管理する。
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public sealed class InventoryPickup : MonoBehaviour
{
    [Serializable]
    public struct Reward
    {
        public string itemId;
        [Min(1)]
        public int amount;
        public Reward(string id, int count)
        {
            itemId = id;
            amount = count;
        }
    }

    internal static readonly HashSet<InventoryPickup> Active = new HashSet<InventoryPickup>();
    public PlayerInventory inventory;
    public string itemId;
    [Min(1)]
    public int amount = 1;
    [Min(.05f)]
    public float holdSeconds = 1f;
    public AudioClip pickupSe;
    [Range(0, 1)]
    public float seVolume = .7f;
    [Header("支給品ボックス")]
    public bool isSupplyContainer;
    public string containerPrompt = "支給品を受け取る";
    public Reward[] containerRewards;
    public Transform[] lidParts;
    [Tooltip("Top部品を開くローカル回転量。Zを正方向へ大きくして開きます。")]
    public Vector3 lidOpenEuler = new Vector3(0f, 0f, 105f);
    [Min(.01f)]
    public float lidOpenDuration = .45f;
    [Tooltip("フタを開き始めたときの音")]
    public AudioClip lidOpenSe;
    [Range(0f, 1f)]
    public float lidOpenSeVolume = .8f;
    private readonly HashSet<Collider> contacts = new HashSet<Collider>();
    private bool collected;
    public SphereCollider Trigger => GetComponent<SphereCollider>();
    public bool Available => !collected && isActiveAndEnabled && CompareTag("Item");

    public string MarkerPrompt
    {
        get
        {
            if (isSupplyContainer)
            {
                return containerPrompt;
            }

            PlayerInventory source = inventory != null ? inventory : FindObjectOfType<PlayerInventory>();
            if (source == null || !source.HasPickedUp(itemId))
            {
                return "";
            }

            PlayerInventory.Definition definition = source.Find(itemId);
            return definition != null ? definition.displayName : itemId;
        }
    }

    // コンポーネントの追加・リセット時に使う既定設定を用意します。
    void Reset()
    {
        GetComponent<SphereCollider>().isTrigger = true;
        GetComponent<SphereCollider>().radius = 1f;
    }

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        Trigger.isTrigger = true;
    }

    // コンポーネントが有効になったときの処理です。
    void OnEnable()
    {
        Active.Add(this);
    }

    // コンポーネントが無効になったときの処理です。
    void OnDisable()
    {
        Active.Remove(this);
        contacts.Clear();
    }

    // トリガー範囲へ入った相手を受け取り、以下の条件に合う場合の処理を行います。
    void OnTriggerEnter(Collider other)
    {
        contacts.Add(other);
    }

    void OnTriggerStay(Collider other)
    {
        contacts.Add(other);
    }

    // トリガー範囲から出た相手に対する処理を行います。
    void OnTriggerExit(Collider other)
    {
        contacts.Remove(other);
    }

    public bool InRange(Transform player)
    {
        if (!Available || !Trigger.enabled)
        {
            return false;
        }

        contacts.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        foreach (var c in contacts)
        {
            for (Transform t = c.transform; t != null; t = t.parent)
            {
                if (t == player && t.CompareTag("Player"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal bool CompletePickup()
    {
        if (!Available)
        {
            return false;
        }

        if (inventory == null)
        {
            inventory = FindObjectOfType<PlayerInventory>();
        }

        if (inventory == null)
        {
            return false;
        }

        if (isSupplyContainer)
        {
            return CompleteContainerPickup();
        }

        if (!inventory.AddItem(itemId, amount))
        {
            return false;
        }

        bool firstStagePickup = inventory.RegisterPickupAndIsFirst(itemId);
        collected = true;
        if (pickupSe != null)
        {
            var go = new GameObject("Item Pickup SE");
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0;
            audio.ignoreListenerPause = true;
            audio.clip = pickupSe;
            audio.volume = seVolume;
            audio.Play();
            Destroy(go, pickupSe.length + .2f);
        }

        Debug.Log("[Inventory] 取得: " + itemId + " x" + amount, this);
        var definition = inventory.Find(itemId);
        if (firstStagePickup)
        {
            ItemInspectionUI.ShowPickup(definition, gameObject, amount);
        }
        else
        {
            ItemPickupNotificationUI.Show(definition, amount);
        }

        gameObject.SetActive(false);
        return true;
    }

    bool CompleteContainerPickup()
    {
        bool receivedAny = false;
        if (containerRewards != null)
        {
            foreach (Reward reward in containerRewards)
            {
                if (string.IsNullOrWhiteSpace(reward.itemId) || reward.amount <= 0)
                {
                    continue;
                }

                if (!inventory.AddItem(reward.itemId, reward.amount))
                {
                    continue;
                }

                receivedAny = true;
                bool firstPickup = inventory.RegisterPickupAndIsFirst(reward.itemId);
                PlayerInventory.Definition definition = inventory.Find(reward.itemId);
                if (firstPickup)
                {
                    ItemInspectionUI.ShowPickup(definition, null, reward.amount);
                }
                else
                {
                    ItemPickupNotificationUI.Show(definition, reward.amount);
                }

                Debug.Log("[SupplyBox] 取得: " + reward.itemId + " x" + reward.amount, this);
            }
        }

        if (!receivedAny)
        {
            return false;
        }

        collected = true;
        Trigger.enabled = false;
        contacts.Clear();
        StartCoroutine(OpenLid());
        if (lidOpenSe != null)
        {
            var soundObject = new GameObject("Supply Box Open SE");
            soundObject.transform.position = transform.position;
            AudioSource source = soundObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.ignoreListenerPause = true;
            source.clip = lidOpenSe;
            source.volume = lidOpenSeVolume;
            source.Play();
            Destroy(soundObject, lidOpenSe.length + .2f);
        }

        if (pickupSe != null)
        {
            AudioSource.PlayClipAtPoint(pickupSe, transform.position, seVolume);
        }

        return true;
    }

    IEnumerator OpenLid()
    {
        if (lidParts == null || lidParts.Length == 0)
        {
            yield break;
        }

        Quaternion[] starts = new Quaternion[lidParts.Length];
        Quaternion[] ends = new Quaternion[lidParts.Length];
        for (int i = 0; i < lidParts.Length; i++)
        {
            if (lidParts[i] == null)
            {
                continue;
            }

            starts[i] = lidParts[i].localRotation;
            ends[i] = starts[i] * Quaternion.Euler(lidOpenEuler);
        }

        float elapsed = 0f;
        while (elapsed < lidOpenDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / lidOpenDuration));
            for (int i = 0; i < lidParts.Length; i++)
            {
                if (lidParts[i] != null)
                {
                    lidParts[i].localRotation = Quaternion.Slerp(starts[i], ends[i], t);
                }
            }

            yield return null;
        }

        // 補間の最終フレームが時間を跨いだ場合も、指定値へ確実に合わせる。
        for (int i = 0; i < lidParts.Length; i++)
        {
            if (lidParts[i] != null)
            {
                lidParts[i].localRotation = ends[i];
            }
        }
    }
}
