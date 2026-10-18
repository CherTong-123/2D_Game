using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;

public class ImageHover : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private UnityEngine.UI.Image targetImage;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite hoverSprite;
    public void OnPointerEnter(PointerEventData eventData)
    {
        targetImage.sprite = hoverSprite;
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        targetImage.sprite = normalSprite;
    }
}

