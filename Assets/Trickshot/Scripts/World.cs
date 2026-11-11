using System.Collections.Generic;
using UnityEngine;

namespace TrickshotDefense
{
    // Disposición del campo (metros, espacio de Unity). La pista va a lo largo del eje X: los robots entran por la
    // derecha (+X) y avanzan hacia el club (base) a la izquierda. Las posiciones de defensa están a los dos lados
    // de la pista. La cámara mira desde +Z (delante) y desde arriba.
    public static class World
    {
        public const float BaseX = -12.5f;      // centro del club
        public const float BaseReachX = -9.4f;  // un robot que llega aquí golpea la copa
        public const float SpawnX = 17f;
        public static readonly float[] Lanes = { -1.3f, 0f, 1.3f };
        public const float FlyHeight = 2.1f;

        // Seis podios: los tres primeros abiertos; los otros se compran durante la partida
        public static readonly Vector3[] Slots =
        {
            new Vector3(-7.0f, 0f, 3.7f), new Vector3(-7.0f, 0f, -3.7f), new Vector3(-2.2f, 0f, 3.7f),
            new Vector3(-2.2f, 0f, -3.7f), new Vector3(2.6f, 0f, 3.7f), new Vector3(2.6f, 0f, -3.7f),
        };

        // Ancho horizontal mínimo que debe verse (para que en 4:3 también quepa todo el campo)
        public const float ViewWidth = 33f;
        public static readonly Vector3 CameraPos = new Vector3(1.2f, 15.5f, 17.5f);
        public static readonly Vector3 CameraLook = new Vector3(1.2f, 0f, 0.6f);

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }

    // Efectos sencillos sin partículas ni materiales nuevos: piezas de los modelos que crecen, se mueven y se encogen
    public class Puff : MonoBehaviour
    {
        private float t, life;
        private Vector3 vel, startScale;
        private float spin;

        public static void Burst(Vector3 pos, Color color, int count, float size, float speed, float life = 0.45f)
        {
            for (int i = 0; i < count; i++)
            {
                var go = Models.Spawn("prim_cube", null);
                go.transform.position = pos;
                go.transform.rotation = Random.rotation;
                Models.TintAll(go.GetComponentsInChildren<Renderer>(), color);
                var p = go.AddComponent<Puff>();
                p.life = life * Random.Range(0.75f, 1.2f);
                Vector3 dir = Random.onUnitSphere; dir.y = Mathf.Abs(dir.y) + 0.3f;
                p.vel = dir.normalized * speed * Random.Range(0.6f, 1.1f);
                p.startScale = Vector3.one * size * Random.Range(0.7f, 1.2f);
                p.spin = Random.Range(-540f, 540f);
                go.transform.localScale = p.startScale;
            }
        }

        public static void Ring(Vector3 pos, Color color, float radius)
        {
            var go = Models.Spawn("prim_disc", null);
            go.transform.position = pos + Vector3.up * 0.06f;
            Models.TintAll(go.GetComponentsInChildren<Renderer>(), color);
            var p = go.AddComponent<Puff>();
            p.life = 0.35f;
            p.startScale = new Vector3(radius, 1f, radius);
            p.spin = 0f;
            p.vel = Vector3.zero;
            go.transform.localScale = Vector3.one * 0.1f;
            p.ring = true;
        }

        private bool ring;

        private void Update()
        {
            t += Time.deltaTime;
            float k = t / life;
            if (k >= 1f) { Destroy(gameObject); return; }
            if (ring)
            {
                float s = Fx.OutCubic(k);
                transform.localScale = new Vector3(startScale.x * s, 1f, startScale.z * s) * (k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f * 0.6f);
                return;
            }
            vel += Vector3.down * 9f * Time.deltaTime;
            transform.position += vel * Time.deltaTime;
            transform.Rotate(Vector3.up, spin * Time.deltaTime, Space.World);
            transform.localScale = startScale * (1f - k);
        }
    }

    // Número o texto que sube y se desvanece sobre un punto del mundo (monedas, daño crítico)
    public class FloatText : MonoBehaviour
    {
        private float t;
        private Vector3 world;
        private TMPro.TextMeshProUGUI label;
        private RectTransform rt;
        private static Camera cam;

        public static void Show(Transform canvas, Vector3 worldPos, string text, Color color, float size = 40f)
        {
            if (canvas == null) return;
            var l = Ui.Label(canvas, text, size, color, true);
            l.rectTransform.sizeDelta = new Vector2(300, 70);
            var f = l.gameObject.AddComponent<FloatText>();
            f.world = worldPos; f.label = l; f.rt = l.rectTransform;
            f.Place();
        }

        private void Place()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(world);
            var canvasRt = (RectTransform)rt.parent;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out local);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = local + Vector2.up * (t * 90f);
        }

        private void Update()
        {
            t += Time.unscaledDeltaTime;
            if (t > 0.9f) { Destroy(gameObject); return; }
            Place();
            var c = label.color; c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.3f; label.color = c;
            rt.localScale = Vector3.one * (t < 0.12f ? Fx.OutBack(t / 0.12f) : 1f);
        }
    }

    // Barra de vida sobre un robot: dos cubos finos (fondo y relleno) que miran a la cámara
    public class HealthBar
    {
        private readonly Transform root, fill;
        private readonly Renderer[] fillRenderers;
        private float shown = 1f;

        public HealthBar(Transform parent, float height, float width)
        {
            var go = new GameObject("HealthBar");
            root = go.transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(0f, height, 0f);
            var back = Models.Spawn("prim_cube", root);
            back.transform.localScale = new Vector3(width + 0.08f, 0.16f, 0.05f);
            Models.TintAll(back.GetComponentsInChildren<Renderer>(), Ui.Navy);
            var f = Models.Spawn("prim_cube", root);
            fill = f.transform;
            fill.localPosition = new Vector3(0f, 0f, -0.04f);
            fill.localScale = new Vector3(width, 0.1f, 0.05f);
            fillRenderers = f.GetComponentsInChildren<Renderer>();
            Models.TintAll(fillRenderers, Ui.Green);
            this.width = width;
            root.gameObject.SetActive(false);
        }

        private readonly float width;

        public void Set(float ratio, Camera cam)
        {
            ratio = Mathf.Clamp01(ratio);
            if (ratio >= 0.999f) { root.gameObject.SetActive(false); return; }
            root.gameObject.SetActive(true);
            shown = Mathf.MoveTowards(shown, ratio, Time.deltaTime * 2.5f);
            fill.localScale = new Vector3(width * ratio, 0.1f, 0.05f);
            fill.localPosition = new Vector3(-(width * (1f - ratio)) * 0.5f, 0f, -0.04f);
            Models.TintAll(fillRenderers, ratio > 0.5f ? Ui.Green : ratio > 0.25f ? Ui.Yellow : Ui.Red);
            if (cam != null) root.rotation = cam.transform.rotation;
        }
    }

    public static class ListPool
    {
        public static readonly List<Enemy> Enemies = new List<Enemy>(64);
    }
}
