using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrickshotDefense
{
    // UI creada por código. Estilo «marcador deportivo»: paneles azul marino, acento amarillo, textos en
    // Barlow Condensed (títulos en cursiva extra negrita) y botones con canto.
    public static class Ui
    {
        public static readonly Color Navy = Hex(0x0F1A33);
        public static readonly Color Panel = Hex(0x1B2B4D);
        public static readonly Color PanelLight = Hex(0x2A3F6B);
        public static readonly Color Yellow = Hex(0xFFC93C);
        public static readonly Color Green = Hex(0x35C26B);
        public static readonly Color Red = Hex(0xEF4E4E);
        public static readonly Color Blue = Hex(0x3B8BFF);
        public static readonly Color White = Hex(0xFFFFFF);
        public static readonly Color Soft = Hex(0xA9B8D6);
        public static readonly Color Gold = Hex(0xF5C542);
        public static readonly Color Disabled = Hex(0x55627F);

        public const float RefW = 1920f, RefH = 1080f;

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
        public static Color A(Color c, float a) { c.a = a; return c; }

        // ---------------- Sprites generados ----------------
        private static Sprite rounded, depth, circle, gradient, ring;

        public static Sprite Rounded => rounded != null ? rounded : (rounded = MakeRounded(64, 20, 0));
        public static Sprite Depth => depth != null ? depth : (depth = MakeRounded(64, 20, 10));
        public static Sprite Circle => circle != null ? circle : (circle = MakeCircle(128, 0f));
        public static Sprite Ring => ring != null ? ring : (ring = MakeCircle(128, 0.8f));

        public static Sprite Gradient
        {
            get
            {
                if (gradient != null) return gradient;
                var tex = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1, 1, 1, y / 63f));
                tex.Apply();
                gradient = Sprite.Create(tex, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f), 100f);
                return gradient;
            }
        }

        private static float RoundedAlpha(float x, float y, int size, int radius)
        {
            float cx = Mathf.Clamp(x, radius, size - radius), cy = Mathf.Clamp(y, radius, size - radius);
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Clamp01(radius - d + 0.5f);
        }

        // Cuadrado redondeado 9-slice. Con edge > 0, lleva un canto más oscuro debajo (botones).
        private static Sprite MakeRounded(int size, int radius, int edge)
        {
            int w = size, h = size + edge;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float aEdge = edge > 0 ? RoundedAlpha(x + 0.5f, y + 0.5f, size, radius) : 0f;
                    float aFace = RoundedAlpha(x + 0.5f, y + 0.5f - edge, size, radius);
                    if (y - edge < 0) aFace = 0f;
                    if (y >= size) aEdge = 0f;
                    float a = Mathf.Max(aFace, aEdge);
                    float shade = Mathf.Lerp(0.7f, 1f, aFace);
                    byte v = (byte)(255 * shade);
                    px[y * w + x] = new Color32(v, v, v, (byte)(255 * a));
                }
            tex.SetPixels32(px);
            tex.Apply();
            int b = radius + 4;
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b + edge, b, b));
        }

        private static Sprite MakeCircle(int size, float inner)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    float a = Mathf.Clamp01(r - d);
                    if (inner > 0f) a *= Mathf.Clamp01(d - r * inner);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * a));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static readonly Dictionary<string, Sprite> loaded = new Dictionary<string, Sprite>();
        public static Sprite Load(string path)
        {
            Sprite s;
            if (loaded.TryGetValue(path, out s)) return s;
            s = Resources.Load<Sprite>(path);
            if (s == null)
            {
                var t = Resources.Load<Texture2D>(path);
                if (t != null) s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
            }
            loaded[path] = s;
            return s;
        }

        // ---------------- Fuentes ----------------
        private static TMP_FontAsset fontBody, fontTitle;
        public static TMP_FontAsset FontBody => fontBody != null ? fontBody : (fontBody = LoadFont("Fonts/BarlowCondensed-Bold"));
        public static TMP_FontAsset FontTitle => fontTitle != null ? fontTitle : (fontTitle = LoadFont("Fonts/BarlowCondensed-ExtraBoldItalic"));

        // Primero el asset SDF (lo crea TRICKSHOT > Setup); si no está, se genera al vuelo desde el TTF
        private static TMP_FontAsset LoadFont(string path)
        {
            var asset = Resources.Load<TMP_FontAsset>(path + " SDF");
            if (asset != null) return asset;
            var ttf = Resources.Load<Font>(path);
            if (ttf != null)
            {
                asset = TMP_FontAsset.CreateFontAsset(ttf);
                if (asset != null) return asset;
            }
            Debug.LogWarning("[Trickshot] No se encontró la fuente " + path + "; se usa la de TextMeshPro por defecto.");
            return TMP_Settings.defaultFontAsset;
        }

        // ---------------- Piezas ----------------
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        // Ancla y pivote en el mismo punto (0..1) del padre
        public static RectTransform Place(RectTransform rt, float ax, float ay, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(ax, ay);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        // Ancla en un punto del padre, pivote propio (para cajas centradas colocadas respecto a un borde)
        public static RectTransform PlaceP(RectTransform rt, float ax, float ay, float px, float py, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(px, py);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        public static Image Box(string name, Transform parent, Color color, bool raycast = false, float radius = 1f)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = radius;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Flat(string name, Transform parent, Color color, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Icon(string name, Transform parent, float size, Color color)
        {
            var rt = Rect("Icon_" + name, parent);
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Load("UI/Icons/" + name);
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = color;
            if (img.sprite == null) img.enabled = false;
            return img;
        }

        public static Image Picture(string path, Transform parent, float size)
        {
            var rt = Rect("Picture", parent);
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Load(path);
            img.preserveAspect = true;
            img.raycastTarget = false;
            if (img.sprite == null) img.enabled = false;
            return img;
        }

        // Texto centrado con tamaño automático acotado: nunca se sale de su caja
        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, bool title = false,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Rect("Label", parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = title ? FontTitle : FontBody;
            t.text = text;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = Mathf.Max(12f, size * 0.45f);
            t.fontSize = size;
            t.overflowMode = TextOverflowModes.Truncate;
            t.richText = true;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            return t;
        }

        public static TextMeshProUGUI LabelIn(Transform parent, string text, float size, Color color, float ax, float ay, Vector2 box, Vector2 pos,
            bool title = false, TextAlignmentOptions align = TextAlignmentOptions.Center, bool wrap = false)
        {
            var t = Label(parent, text, size, color, title, align, wrap);
            Place(t.rectTransform, ax, ay, box, pos);
            return t;
        }

        public static UiButton Button(Transform parent, string label, Color color, Action onClick, string icon = null, float fontSize = 44f)
        {
            var img = Box("Btn_" + (label ?? icon), parent, color, true);
            img.sprite = Depth;
            var b = img.gameObject.AddComponent<UiButton>();
            b.image = img;
            b.baseColor = color;
            b.onClick = onClick;
            Color ink = (color.r * 0.3f + color.g * 0.6f + color.b * 0.1f) > 0.62f ? Navy : White;
            var content = Stretch(Rect("Content", img.transform), 14, 16, 14, 6);
            if (!string.IsNullOrEmpty(label))
            {
                b.label = Label(content, label, fontSize, ink);
                Stretch(b.label.rectTransform, icon != null ? fontSize * 1.25f : 0, 0, 0, 0);
            }
            if (icon != null)
            {
                b.icon = Icon(icon, content, fontSize, ink);
                if (!string.IsNullOrEmpty(label)) Place(b.icon.rectTransform, 0f, 0.5f, new Vector2(fontSize, fontSize), new Vector2(6, 0));
                else Place(b.icon.rectTransform, 0.5f, 0.5f, new Vector2(fontSize, fontSize), Vector2.zero);
            }
            return b;
        }

        public static void SetActive(Component c, bool on)
        {
            if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
        }
    }

    public class UiButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public Image image;
        public TextMeshProUGUI label;
        public Image icon;
        public Color baseColor;
        public Action onClick;
        public bool silent;
        private bool interactable = true;
        private float press;   // 0..1
        private bool down;

        public bool Interactable
        {
            get { return interactable; }
            set
            {
                interactable = value;
                if (image != null) image.color = value ? baseColor : Ui.Disabled;
            }
        }

        public void SetColor(Color c)
        {
            baseColor = c;
            if (image != null) image.color = interactable ? c : Ui.Disabled;
        }

        public void OnPointerDown(PointerEventData e) { if (interactable) down = true; }
        public void OnPointerUp(PointerEventData e) { down = false; }

        public void OnPointerClick(PointerEventData e)
        {
            if (!interactable) { Sfx.Play("error"); return; }
            if (!silent) Sfx.Play("click");
            if (onClick != null) onClick();
        }

        private void OnDisable() { down = false; press = 0f; transform.localScale = Vector3.one; }

        private void Update()
        {
            float target = down ? 1f : 0f;
            if (Mathf.Approximately(press, target)) return;
            press = Mathf.MoveTowards(press, target, Time.unscaledDeltaTime * 12f);
            transform.localScale = Vector3.one * (1f - 0.06f * press);
        }
    }

    // Corrutinas y animaciones sueltas (tiempo real: funcionan con el juego en pausa)
    public class Fx : MonoBehaviour
    {
        private static Fx inst;
        public static Fx I
        {
            get
            {
                if (inst == null)
                {
                    var go = new GameObject("Fx");
                    DontDestroyOnLoad(go);
                    inst = go.AddComponent<Fx>();
                }
                return inst;
            }
        }

        public static Coroutine Run(IEnumerator e) => I.StartCoroutine(e);

        public static Coroutine Tween(float duration, Action<float> step, Action done = null, float delay = 0f)
            => I.StartCoroutine(TweenCo(duration, step, done, delay));

        private static IEnumerator TweenCo(float duration, Action<float> step, Action done, float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                step(Mathf.Clamp01(t / duration));
                yield return null;
            }
            step(1f);
            if (done != null) done();
        }

        public static Coroutine After(float seconds, Action a) => I.StartCoroutine(AfterCo(seconds, a));
        private static IEnumerator AfterCo(float s, Action a) { yield return new WaitForSecondsRealtime(s); a(); }

        public static float OutBack(float t) { const float c = 1.70158f; t -= 1f; return 1f + (c + 1f) * t * t * t + c * t * t; }
        public static float OutCubic(float t) { t = 1f - t; return 1f - t * t * t; }

        // Aparece con un rebote de escala
        public static void Pop(Transform tr, float duration = 0.28f, float delay = 0f)
        {
            if (tr == null) return;
            tr.localScale = Vector3.zero;
            Tween(duration, t => { if (tr != null) tr.localScale = Vector3.one * OutBack(t); }, null, delay);
        }

        // Golpe de escala (para contadores que cambian)
        public static void Punch(Transform tr, float amount = 0.18f, float duration = 0.22f)
        {
            if (tr == null) return;
            Tween(duration, t => { if (tr != null) tr.localScale = Vector3.one * (1f + amount * Mathf.Sin(t * Mathf.PI)); });
        }

        public static void Fade(CanvasGroup g, float to, float duration, Action done = null)
        {
            if (g == null) return;
            float from = g.alpha;
            Tween(duration, t => { if (g != null) g.alpha = Mathf.Lerp(from, to, t); }, done);
        }
    }
}
