// 【役割】病院の片開き・両開き扉を、プレイヤーの接近方向に応じて開閉する。
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 病院用の自動開閉ドア。各Door Transformの原点（蝶番）を支点にY回転します。
/// プレイヤーがいる側を判定し、ドアノブ側がプレイヤーから離れる方向へ開きます。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class ProximityHospitalDoor : MonoBehaviour
{
    [Header("回転する扉（蝶番原点）")]
    [Tooltip("片開きはDoor、両開きはLeft/Rightを登録します。窓が別部品なら同じ側へ登録します。")]
    [SerializeField]
    Transform[] doorParts;
    [Tooltip("DoorD_V2のように左右扉の原点が中央にあるモデルで、各扉の外端を蝶番として使用します。")]
    [SerializeField]
    bool useOuterEdgeHinges;
    [Header("開閉")]
    [Tooltip("Y軸の最大開き角度。符号はプレイヤー位置から自動決定します。")]
    [SerializeField, Range(0f, 170f)]
    float openAngle = 90f;
    [SerializeField, Min(1f)]
    float angularSpeed = 120f;
    [SerializeField, Min(0f)]
    float closeDelay = .35f;
    [Header("開く効果音")]
    [SerializeField]
    AudioClip openSound;
    [SerializeField, Range(0f, 1f)]
    float openSoundVolume = .8f;
    [SerializeField, Min(0f)]
    float soundMinDistance = 1f;
    [SerializeField, Min(.1f)]
    float soundMaxDistance = 12f;
    readonly HashSet<Collider> playerContacts = new HashSet<Collider>();
    Quaternion[] closedRotations;
    Vector3[] closedWorldPositions;
    Quaternion[] closedWorldRotations;
    Vector3[] hingeWorldPositions;
    float[] directionSigns;
    float currentAngle;
    Coroutine closeRoutine;
    AudioSource audioSource;
    public BoxCollider DetectionRange => GetComponent<BoxCollider>();

    // コンポーネントの追加・リセット時に使う既定設定を用意します。
    void Reset()
    {
        DetectionRange.isTrigger = true;
        DetectionRange.center = new Vector3(0f, 1f, 0f);
        DetectionRange.size = new Vector3(3f, 2.5f, 3f);
    }

    // 初期化時に呼ばれます。コンポーネントの参照や、この処理で使う初期状態を準備します。
    void Awake()
    {
        DetectionRange.isTrigger = true;
        if (doorParts == null)
        {
            doorParts = new Transform[0];
        }

        closedRotations = new Quaternion[doorParts.Length];
        closedWorldPositions = new Vector3[doorParts.Length];
        closedWorldRotations = new Quaternion[doorParts.Length];
        hingeWorldPositions = new Vector3[doorParts.Length];
        directionSigns = new float[doorParts.Length];
        Bounds allDoorBounds = CalculateAllDoorBounds();
        for (int i = 0; i < doorParts.Length; i++)
        {
            Transform part = doorParts[i];
            if (part == null)
            {
                continue;
            }

            closedRotations[i] = part.localRotation;
            closedWorldPositions[i] = part.position;
            closedWorldRotations[i] = part.rotation;
            Bounds partBounds = CalculateBounds(part);
            float side = Vector3.Dot(partBounds.center - allDoorBounds.center, transform.right) >= 0f ? 1f : -1f;
            // 同じ側の扉と窓は完全に同じ外端蝶番を共有し、開閉中の分離を防ぐ。
            hingeWorldPositions[i] = allDoorBounds.center + transform.right * side * ProjectedExtent(allDoorBounds, transform.right);
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = soundMinDistance;
        audioSource.maxDistance = soundMaxDistance;
    }

    // 毎フレーム呼ばれます。以下の条件を確認し、入力や現在の状態に応じた処理を進めます。
    void Update()
    {
        float target = playerContacts.Count > 0 ? openAngle : 0f;
        currentAngle = Mathf.MoveTowards(currentAngle, target, angularSpeed * Time.deltaTime);
        for (int i = 0; i < doorParts.Length; i++)
        {
            Transform part = doorParts[i];
            if (part == null)
            {
                continue;
            }

            float angle = currentAngle * directionSigns[i];
            if (useOuterEdgeHinges)
            {
                Quaternion delta = Quaternion.AngleAxis(angle, transform.up);
                part.position = hingeWorldPositions[i] + delta * (closedWorldPositions[i] - hingeWorldPositions[i]);
                part.rotation = delta * closedWorldRotations[i];
            }
            else
            {
                part.localRotation = closedRotations[i] * Quaternion.Euler(0f, angle, 0f);
            }
        }
    }

    // トリガー範囲へ入った相手を受け取り、以下の条件に合う場合の処理を行います。
    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other) || !playerContacts.Add(other))
        {
            return;
        }

        if (closeRoutine != null)
        {
            StopCoroutine(closeRoutine);
            closeRoutine = null;
        }

        if (playerContacts.Count != 1)
        {
            return;
        }

        ChooseDirections(other.transform.position);
        if (openSound != null)
        {
            audioSource.clip = openSound;
            audioSource.volume = openSoundVolume;
            audioSource.Play();
        }
    }

    // トリガー範囲から出た相手に対する処理を行います。
    void OnTriggerExit(Collider other)
    {
        if (!playerContacts.Remove(other) || playerContacts.Count > 0)
        {
            return;
        }

        if (closeRoutine != null)
        {
            StopCoroutine(closeRoutine);
        }

        closeRoutine = StartCoroutine(CloseAfterDelay());
    }

    void ChooseDirections(Vector3 playerPosition)
    {
        for (int i = 0; i < doorParts.Length; i++)
        {
            Transform part = doorParts[i];
            if (part == null)
            {
                continue;
            }

            Vector3 visualCenter = FindVisualCenter(part);
            Vector3 pivot = useOuterEdgeHinges ? hingeWorldPositions[i] : part.position;
            Vector3 axis = useOuterEdgeHinges ? transform.up : part.up;
            Vector3 arm = visualCenter - pivot;
            Vector3 positiveCenter = pivot + Quaternion.AngleAxis(openAngle, axis) * arm;
            Vector3 negativeCenter = pivot + Quaternion.AngleAxis(-openAngle, axis) * arm;
            directionSigns[i] = (positiveCenter - playerPosition).sqrMagnitude >= (negativeCenter - playerPosition).sqrMagnitude ? 1f : -1f;
        }
    }

    static Vector3 FindVisualCenter(Transform part)
    {
        Renderer[] renderers = part.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return part.position + part.right;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds.center;
    }

    Bounds CalculateAllDoorBounds()
    {
        bool found = false;
        Bounds result = new Bounds(transform.position, Vector3.zero);
        foreach (Transform part in doorParts)
        {
            if (part == null)
            {
                continue;
            }

            Bounds bounds = CalculateBounds(part);
            if (!found)
            {
                result = bounds;
                found = true;
            }
            else
            {
                result.Encapsulate(bounds);
            }
        }

        return result;
    }

    static Bounds CalculateBounds(Transform part)
    {
        Renderer[] renderers = part.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(part.position, Vector3.one * .1f);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    static float ProjectedExtent(Bounds bounds, Vector3 axis)
    {
        axis = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
        return Vector3.Dot(bounds.extents, axis);
    }

    IEnumerator CloseAfterDelay()
    {
        yield return new WaitForSeconds(closeDelay);
        playerContacts.Clear();
        closeRoutine = null;
    }

    static bool IsPlayer(Collider other) => other.CompareTag("Player") || other.transform.root.CompareTag("Player") || other.GetComponentInParent<PlayerHealth>() != null;
}
