// 【役割】カメラ設定項目へのポインター操作を設定UIへ伝える。
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CameraSettingsHover : MonoBehaviour, IPointerEnterHandler
{
    public CameraSettingsUI owner;
    public int index;
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.HoverSelect(index);
        }
    }
}
