using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A colour picker: a saturation / brightness field with a hue bar under it, built in code like the rest
/// of the menus.
///
/// The field is two gradients stacked rather than one generated bitmap - a horizontal white-to-hue ramp
/// with a transparent-to-black ramp over it, which is exactly what saturation and brightness do to a
/// colour. Generating a small texture per hue change is then cheap enough to do while the player drags,
/// and a hue bar is the same trick rotated.
///
/// The hue bar is a uGUI <see cref="Slider"/> so that a keyboard or gamepad can move it - a gamepad can
/// reach every part of the screen. The field itself is dragged with the pointer only, and the palette
/// beside it is what a gamepad uses for anything but hue.
///
/// Whoever builds this owns the callback: <see cref="onChanged"/> fires while the player drags, so the
/// truck can follow the pointer, and <see cref="onReleased"/> fires once the drag ends, which is the
/// moment worth writing to disk.
/// </summary>
[DisallowMultipleComponent]
public class ColourPicker : MonoBehaviour
{
    /// <summary>Called on every change, so the preview can follow the pointer.</summary>
    public System.Action<Color> onChanged;

    /// <summary>Called when the player stops dragging.</summary>
    public System.Action onReleased;

    // ---------------------------------------------------------------- look

    [Header("Colours")]
    public Color cursorColor = Color.white;
    public Color cursorOutlineColor = new Color(0.012f, 0.024f, 0.055f, 0.9f);

    // ---------------------------------------------------------------- layout

    [Header("Layout (reference resolution is 1920x1080)")]
    public float fieldHeight = 152f;
    public float hueHeight = 22f;
    public float gap = 12f;
    public float cursorSize = 22f;
    public float hueHandleWidth = 12f;

    // ---------------------------------------------------------------- state

    private RawImage field;
    private Slider hue;
    private RectTransform fieldRect;
    private RectTransform cursor;

    private Texture2D fieldTexture;
    private Texture2D shadeTexture;
    private Texture2D hueTexture;

    private readonly List<Object> generated = new List<Object>();
    private readonly Color[] fieldPixels = new Color[2];

    private float hueValue = 0.02f;
    private float saturation = 1f;
    private float brightness = 1f;

    private bool built;
    private bool suspendNotify;

    /// <summary>The colour the picker currently stands on.</summary>
    public Color Value { get { return Color.HSVToRGB(hueValue, saturation, brightness); } }

    /// <summary>The hue bar, for a screen that wants to wire it into its own navigation.</summary>
    public Selectable HueBar { get { return hue; } }

    /// <summary>True while the pointer is on the field or the hue bar, so a screen can leave it alone.</summary>
    public bool IsDragging { get; private set; }

    // ---------------------------------------------------------------- building

    /// <summary>
    /// Builds the picker inside <paramref name="parent"/>, which must already hold the space the picker
    /// wants: the field spans its width and the hue bar sits under it.
    /// </summary>
    public void Build(RectTransform parent)
    {
        if (built || parent == null) return;
        built = true;

        fieldRect = CreateRect("Field", parent);
        fieldRect.anchorMin = new Vector2(0f, 1f);
        fieldRect.anchorMax = new Vector2(1f, 1f);
        fieldRect.pivot = new Vector2(0f, 1f);
        fieldRect.sizeDelta = new Vector2(0f, fieldHeight);
        fieldRect.anchoredPosition = Vector2.zero;

        field = fieldRect.gameObject.AddComponent<RawImage>();
        field.texture = fieldTexture = CreateTexture("Picker Field", 2, 1);
        field.raycastTarget = true;

        // The pointer lands on the field itself, which is also what carries the drag handler.
        FieldInput input = fieldRect.gameObject.AddComponent<FieldInput>();
        input.picker = this;

        // Shade over the ramp: the top of the field keeps the full colour and the bottom is black.
        RectTransform shadeRect = CreateRect("Shade", fieldRect);
        Stretch(shadeRect);

        RawImage shade = shadeRect.gameObject.AddComponent<RawImage>();
        shade.texture = shadeTexture = CreateTexture("Picker Shade", 1, 2);
        shade.raycastTarget = false;

        shadeTexture.SetPixels(new[]
        {
            new Color(0f, 0f, 0f, 1f),   // bottom: as dark as it gets
            new Color(0f, 0f, 0f, 0f),   // top: untouched
        });
        shadeTexture.Apply(false, false);

        BuildCursor(fieldRect);
        BuildHueBar(parent);

        ApplyVisuals();
    }

    private void BuildCursor(RectTransform parent)
    {
        cursor = CreateRect("Cursor", parent);
        cursor.anchorMin = Vector2.zero;
        cursor.anchorMax = Vector2.zero;
        cursor.pivot = new Vector2(0.5f, 0.5f);
        cursor.sizeDelta = new Vector2(cursorSize, cursorSize);

        // A dark disc behind a white ring, so the marker reads against any colour it is sitting on.
        Image disc = CreateImage("Outline", cursor, cursorOutlineColor, CreateDiscSprite(32));
        Stretch(disc.rectTransform);

        Image ring = CreateImage("Ring", cursor, cursorColor, CreateRingSprite(32, 2.5f));
        Stretch(ring.rectTransform);
    }

    private void BuildHueBar(RectTransform parent)
    {
        RectTransform bar = CreateRect("Hue", parent);
        bar.anchorMin = new Vector2(0f, 1f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(0f, 1f);
        bar.sizeDelta = new Vector2(0f, hueHeight);
        bar.anchoredPosition = new Vector2(0f, -(fieldHeight + gap));

        hue = bar.gameObject.AddComponent<Slider>();
        hue.direction = Slider.Direction.LeftToRight;
        hue.wholeNumbers = false;
        hue.minValue = 0f;
        hue.maxValue = 1f;

        // The gradient comes first and the handle after it, because in a canvas the later sibling is the
        // one drawn on top - the handle has to stay visible over the bar it slides on.
        RectTransform barRect = CreateRect("Gradient", bar);
        Stretch(barRect);

        RawImage hueBar = barRect.gameObject.AddComponent<RawImage>();
        hueBar.texture = hueTexture = CreateTexture("Picker Hue", 180, 1);
        hueBar.raycastTarget = true;

        // The handle is the slider's own graphic; the bar underneath is only the gradient it slides on.
        RectTransform handle = CreateRect("Handle", bar);
        handle.sizeDelta = new Vector2(hueHandleWidth, 6f);

        Image handleFill = handle.gameObject.AddComponent<Image>();
        handleFill.color = cursorColor;

        Image handleOutline = CreateImage("Outline", handle, cursorOutlineColor, null);
        Stretch(handleOutline.rectTransform);
        handleOutline.rectTransform.sizeDelta = new Vector2(4f, 4f);
        handleOutline.raycastTarget = false;

        Color[] pixels = new Color[hueTexture.width];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.HSVToRGB(i / (float)(pixels.Length - 1), 1f, 1f);

        hueTexture.SetPixels(pixels);
        hueTexture.Apply(false, false);

        hue.targetGraphic = handleFill;
        hue.handleRect = handle;
        hue.fillRect = null;

        ColorBlock colours = hue.colors;
        colours.normalColor = Color.white;
        colours.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colours.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colours.selectedColor = Color.white;
        colours.fadeDuration = 0.1f;
        hue.colors = colours;

        hue.onValueChanged.AddListener(OnHueChanged);

        // The hue bar's own drags count too, so the screen does not fight the pointer for the value.
        HueInput hueInput = bar.gameObject.AddComponent<HueInput>();
        hueInput.picker = this;
    }

    // ---------------------------------------------------------------- value

    /// <summary>
    /// Moves the picker onto a colour - the palette and the truck's own paint both come through here, so
    /// the picker always shows the colour the selected part actually wears.
    /// </summary>
    public void SetValue(Color colour, bool notify)
    {
        if (!built) return;

        float h, s, v;
        Color.RGBToHSV(colour, out h, out s, out v);

        hueValue = Mathf.Clamp01(h);
        saturation = Mathf.Clamp01(s);
        brightness = Mathf.Clamp01(v);

        ApplyVisuals();

        if (notify) Notify();
    }

    private void SetField(float s, float v)
    {
        saturation = Mathf.Clamp01(s);
        brightness = Mathf.Clamp01(v);

        ApplyVisuals();
        Notify();
    }

    private void OnHueChanged(float value)
    {
        if (suspendNotify) return;

        hueValue = Mathf.Clamp01(value);
        ApplyVisuals();
        Notify();
    }

    private void Notify()
    {
        if (onChanged != null) onChanged(Value);
    }

    /// <summary>Redraws the ramps and puts the cursor and the hue handle where the value says.</summary>
    private void ApplyVisuals()
    {
        fieldPixels[0] = Color.white;
        fieldPixels[1] = Color.HSVToRGB(hueValue, 1f, 1f);
        fieldTexture.SetPixels(fieldPixels);
        fieldTexture.Apply(false, false);

        // The field's own corners are the value: the cursor's anchor is simply (saturation, brightness).
        cursor.anchorMin = new Vector2(saturation, brightness);
        cursor.anchorMax = new Vector2(saturation, brightness);

        if (!Mathf.Approximately(hue.value, hueValue))
        {
            suspendNotify = true;
            hue.value = hueValue;
            suspendNotify = false;
        }
    }

    // ---------------------------------------------------------------- pointer

    private void BeginDrag()
    {
        IsDragging = true;
    }

    private void EndDrag()
    {
        IsDragging = false;

        if (onReleased != null) onReleased();
    }

    private void DragTo(PointerEventData eventData)
    {
        if (!built) return;

        IsDragging = true;

        Vector2 point;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(fieldRect, eventData.position,
                eventData.pressEventCamera, out point))
            return;

        // Read through the rect rather than its pivot, so the arithmetic does not care where the field
        // was anchored on screen.
        Rect rect = fieldRect.rect;

        SetField(Mathf.InverseLerp(rect.xMin, rect.xMax, point.x),
                 Mathf.InverseLerp(rect.yMin, rect.yMax, point.y));
    }

    /// <summary>Takes the pointer on the field. Lives on the field itself so the drag comes to it.</summary>
    private class FieldInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public ColourPicker picker;

        public void OnPointerDown(PointerEventData eventData)
        {
            picker.BeginDrag();
            picker.DragTo(eventData);
        }

        public void OnDrag(PointerEventData eventData) { picker.DragTo(eventData); }
        public void OnPointerUp(PointerEventData eventData) { picker.EndDrag(); }
    }

    /// <summary>
    /// The hue bar's own pointer bookkeeping. The slider already handles the drag itself - this only says
    /// whether it is being dragged, which decides whether the screen is allowed to move the value.
    /// </summary>
    private class HueInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public ColourPicker picker;

        public void OnPointerDown(PointerEventData eventData) { picker.BeginDrag(); }
        public void OnPointerUp(PointerEventData eventData) { picker.EndDrag(); }
    }

    // ---------------------------------------------------------------- helpers

    private Texture2D CreateTexture(string name, int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = name;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave;

        generated.Add(texture);
        return texture;
    }

    private Sprite CreateRingSprite(int size, float thickness)
    {
        float outer = size * 0.5f - 1f;
        float inner = outer - thickness;

        return CreateDiscSprite(size, inner, outer);
    }

    private Sprite CreateDiscSprite(int size)
    {
        return CreateDiscSprite(size, 0f, size * 0.5f - 1f);
    }

    /// <summary>Draws a filled circle - or, with an inner radius, a ring - so a marker needs no art.</summary>
    private Sprite CreateDiscSprite(int size, float innerRadius, float outerRadius)
    {
        Texture2D texture = CreateTexture("Picker Marker", size, size);

        float centre = (size - 1) * 0.5f;
        Color32 solid = new Color32(255, 255, 255, 255);
        Color32 clear = new Color32(255, 255, 255, 0);
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - centre;
                float dy = y - centre;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                pixels[y * size + x] = distance <= outerRadius && distance >= innerRadius ? solid : clear;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;

        generated.Add(sprite);
        return sprite;
    }

    private Image CreateImage(string name, Transform parent, Color color, Sprite sprite)
    {
        RectTransform rect = CreateRect(name, parent);

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.sprite = sprite;
        image.raycastTarget = false;

        return image;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;

        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < generated.Count; i++)
            if (generated[i] != null) Destroy(generated[i]);

        generated.Clear();
    }
}
