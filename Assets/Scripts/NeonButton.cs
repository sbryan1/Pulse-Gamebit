using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Hover / keyboard-select effect for the neon menu buttons:
// the button grows slightly and its frame glows at full strength.
public class NeonButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public Image frame;          // optional: the glowing outline to brighten
    public Color color = Color.white;
    public float hoverScale = 1.06f;

    bool hovered, selected;

    void Start()
    {
        if (frame != null) frame.color = Dim();
    }

    void Update()
    {
        bool hot = hovered || selected;
        float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);

        float s = Mathf.Lerp(transform.localScale.x, hot ? hoverScale : 1f, k);
        transform.localScale = new Vector3(s, s, 1f);

        if (frame != null)
            frame.color = Color.Lerp(frame.color, hot ? color : Dim(), k);
    }

    // A button hidden while hovered (e.g. switching menu pages) gets no exit event.
    void OnDisable()
    {
        hovered = selected = false;
        transform.localScale = Vector3.one;
        if (frame != null) frame.color = Dim();
    }

    Color Dim() => new Color(color.r, color.g, color.b, 0.7f);

    public void OnPointerEnter(PointerEventData e) => hovered = true;
    public void OnPointerExit(PointerEventData e)  => hovered = false;
    public void OnSelect(BaseEventData e)          => selected = true;
    public void OnDeselect(BaseEventData e)        => selected = false;
}
