using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Prosedürel butonlara "basıldı" hissi verir: dokununca hafif küçülür + koyulaşır, bırakınca eski haline döner.
/// Renk geri bildirimi Pulse (nabız) ile ÇAKIŞMAZ (Pulse yalnız ölçek oynatır) → nabızlı durakta da basış belli olur.
/// Patika level duraklarına eklenir (MainMenuController.BuildPath). Kilitli duraklara EKLENMEZ.
/// </summary>
[RequireComponent(typeof(Image))]
public class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public float darken = 0.7f;
    public float pressScale = 0.9f;

    Image img;
    Color baseColor;
    Vector3 baseScale;
    bool captured, pressed;

    void Awake() { img = GetComponent<Image>(); Capture(); }
    void OnEnable() { Capture(); }

    void Capture()
    {
        if (captured || img == null) return;
        baseColor = img.color; baseScale = transform.localScale; captured = true;
    }

    public void OnPointerDown(PointerEventData e)
    {
        Capture();
        pressed = true;
        if (img) img.color = new Color(baseColor.r * darken, baseColor.g * darken, baseColor.b * darken, baseColor.a);
        transform.localScale = baseScale * pressScale;
    }

    public void OnPointerUp(PointerEventData e) => Release();
    public void OnPointerExit(PointerEventData e) { if (pressed) Release(); }

    void Release()
    {
        pressed = false;
        if (img) img.color = baseColor;
        transform.localScale = baseScale;
    }
}
