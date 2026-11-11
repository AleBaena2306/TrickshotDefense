using System.Collections.Generic;
using UnityEngine;

namespace TrickshotDefense
{
    // Modelos de Resources/Models (FBX generados con Tools/Blender/build_models.py)
    public static class Models
    {
        private static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static MaterialPropertyBlock block;

        // Devuelve un contenedor (ejes limpios: X derecha, Y arriba, Z delante) con el modelo dentro, como hijo «Model»
        public static GameObject Spawn(string name, Transform parent)
        {
            GameObject prefab;
            if (!prefabs.TryGetValue(name, out prefab))
            {
                prefab = Resources.Load<GameObject>("Models/" + name);
                prefabs[name] = prefab;
                if (prefab == null) Debug.LogWarning("[Trickshot] Falta el modelo Resources/Models/" + name);
            }
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            GameObject model;
            if (prefab != null) model = Object.Instantiate(prefab, holder.transform);
            else
            {
                model = GameObject.CreatePrimitive(PrimitiveType.Cube);   // para que el juego funcione aunque falte un modelo
                var col = model.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
                model.transform.SetParent(holder.transform, false);
                model.transform.localScale = Vector3.one * 0.6f;
                model.transform.localPosition = new Vector3(0, 0.3f, 0);
            }
            model.name = "Model";
            if (prefab != null) model.transform.localPosition = Vector3.zero;
            return holder;
        }

        // Tiñe los materiales cuyo nombre empieza por «materialName» (p. ej. «Kit») sin crear materiales nuevos
        public static void Tint(GameObject root, string materialName, Color color)
        {
            if (block == null) block = new MaterialPropertyBlock();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || (materialName != null && !mats[i].name.StartsWith(materialName))) continue;
                    block.Clear();
                    block.SetColor(BaseColor, color);
                    block.SetColor(ColorId, color);
                    r.SetPropertyBlock(block, i);
                }
            }
        }

        public static void ClearTint(Renderer[] renderers)
        {
            for (int k = 0; k < renderers.Length; k++)
            {
                var r = renderers[k];
                if (r == null) continue;
                int n = r.sharedMaterials.Length;
                for (int i = 0; i < n; i++) r.SetPropertyBlock(null, i);
            }
        }

        public static void TintAll(Renderer[] renderers, Color color)
        {
            if (block == null) block = new MaterialPropertyBlock();
            block.Clear();
            block.SetColor(BaseColor, color);
            block.SetColor(ColorId, color);
            for (int k = 0; k < renderers.Length; k++)
            {
                var r = renderers[k];
                if (r == null) continue;
                int n = r.sharedMaterials.Length;
                for (int i = 0; i < n; i++) r.SetPropertyBlock(block, i);
            }
        }
    }

    // Animación por código de modelos hechos de piezas rígidas. Los giros se dan en el espacio del contenedor
    // (X derecha, Y arriba, Z delante), sea cual sea la orientación con la que Unity importó cada pieza.
    public class Rig
    {
        private class Part { public Transform t; public Quaternion baseLocal; public Quaternion parentRel; }

        public readonly Transform holder;
        public readonly Transform model;
        private readonly Dictionary<string, Part> parts = new Dictionary<string, Part>();
        private readonly Vector3 modelBaseScale;
        private readonly Quaternion modelBaseRot;

        public Rig(GameObject holderGo)
        {
            holder = holderGo.transform;
            model = holder.Find("Model");
            modelBaseScale = model != null ? model.localScale : Vector3.one;
            modelBaseRot = model != null ? model.localRotation : Quaternion.identity;
            Quaternion inv = Quaternion.Inverse(holder.rotation);
            foreach (var t in holderGo.GetComponentsInChildren<Transform>(true))
            {
                if (t == holder || t == model || parts.ContainsKey(t.name)) continue;
                parts[t.name] = new Part { t = t, baseLocal = t.localRotation, parentRel = inv * t.parent.rotation };
            }
        }

        public bool Has(string part) => parts.ContainsKey(part);

        public Transform PartTransform(string part)
        {
            Part p;
            return parts.TryGetValue(part, out p) ? p.t : null;
        }

        // Gira la pieza «deg» grados alrededor de un eje del contenedor, respecto a su pose de reposo
        public void Rot(string part, Vector3 axis, float deg)
        {
            Part p;
            if (!parts.TryGetValue(part, out p) || p.t == null) return;
            Vector3 ax = Quaternion.Inverse(p.parentRel) * axis;
            p.t.localRotation = Quaternion.AngleAxis(deg, ax) * p.baseLocal;
        }

        public void Reset()
        {
            foreach (var kv in parts) if (kv.Value.t != null) kv.Value.t.localRotation = kv.Value.baseLocal;
            Offset(0f, 1f);
            Tilt(0f, 0f, 0f);
        }

        // Inclina el modelo entero (todas las piezas cuelgan de la raíz, así que los giros del cuerpo van aquí).
        // twist: alrededor de Y (arriba); lean: alrededor de X (derecha, hacia delante = positivo); roll: alrededor de Z
        public void Tilt(float twist, float lean, float roll)
        {
            if (model == null) return;
            model.localRotation = Quaternion.AngleAxis(twist, Vector3.up) * Quaternion.AngleAxis(lean, Vector3.right)
                                  * Quaternion.AngleAxis(roll, Vector3.forward) * modelBaseRot;
        }

        // Salto (altura) y aplastamiento (escala vertical) de todo el modelo
        public void Offset(float y, float squash)
        {
            if (model == null) return;
            model.localPosition = new Vector3(0f, y, 0f);
            float inv = 1f + (1f - squash) * 0.5f;
            // El modelo puede venir con el eje vertical cambiado: se escala por igual en los tres ejes cuando hay duda
            model.localScale = new Vector3(modelBaseScale.x * inv, modelBaseScale.y * squash, modelBaseScale.z * inv);
        }

        // 0..1 → va atrás (back), golpea (fwd) y vuelve a 0
        public static float Swing(float p, float back, float fwd)
        {
            if (p < 0.3f) return Mathf.Lerp(0f, back, p / 0.3f);
            if (p < 0.55f) return Mathf.Lerp(back, fwd, (p - 0.3f) / 0.25f);
            return Mathf.Lerp(fwd, 0f, (p - 0.55f) / 0.45f);
        }

        // ---------------- Deportistas ----------------
        // time: reloj; attack: 0..1 (o -1 si no ataca); celebrate: 0..1 (o -1)
        public void AnimateAthlete(Sport sport, float time, float attack, float celebrate)
        {
            float breathe = Mathf.Sin(time * 2.2f);
            Rot("Head", Vector3.right, breathe * 2f);
            float armL = breathe * 4f, armR = -breathe * 4f, legR = 0f, twist = 0f, lean = 0f, hop = 0f, squash = 1f + breathe * 0.012f;

            if (attack >= 0f)
            {
                switch (sport)
                {
                    case Sport.Soccer:
                        legR = Swing(attack, 40f, -80f);
                        armL = Swing(attack, -25f, 30f);
                        lean = Swing(attack, -6f, 8f);
                        break;
                    case Sport.Tennis:
                    case Sport.PingPong:
                        armR = Swing(attack, 55f, -125f);
                        twist = Swing(attack, 22f, -28f);
                        break;
                    case Sport.Basket:
                        armR = Swing(attack, 20f, -165f);
                        armL = Swing(attack, 20f, -150f);
                        hop = Mathf.Sin(Mathf.Clamp01(attack) * Mathf.PI) * 0.35f;
                        break;
                    case Sport.Bowling:
                        armR = Swing(attack, 65f, -85f);
                        lean = Swing(attack, -5f, 22f);
                        break;
                }
            }
            if (celebrate >= 0f)
            {
                float j = Mathf.Abs(Mathf.Sin(celebrate * Mathf.PI * 3f));
                hop = j * 0.4f;
                armL = armR = -165f + j * 15f;
                squash = 1f + j * 0.06f;
            }
            Rot("ArmL", Vector3.right, armL);
            Rot("ArmR", Vector3.right, armR);
            Rot("LegR", Vector3.right, legR);
            Tilt(twist, lean, 0f);
            Offset(hop, squash);
        }

        // ---------------- Robots ----------------
        public void AnimateRobot(EnemyKind kind, float time, float hit)
        {
            float squash = 1f - 0.16f * hit;
            float y = 0f, lean = 0f, roll = 0f;
            switch (kind)
            {
                case EnemyKind.Basic:
                    roll = Mathf.Sin(time * 7f) * 4f;
                    Rot("ArmL", Vector3.right, Mathf.Sin(time * 7f) * 28f);
                    Rot("ArmR", Vector3.right, -Mathf.Sin(time * 7f) * 28f);
                    Rot("Head", Vector3.up, Mathf.Sin(time * 3f) * 12f);
                    break;
                case EnemyKind.Runner:
                    y = Mathf.Abs(Mathf.Sin(time * 11f)) * 0.14f;
                    lean = 10f + Mathf.Sin(time * 11f) * 5f;
                    Rot("Wheel", Vector3.right, time * 720f);
                    break;
                case EnemyKind.Armored:
                    roll = Mathf.Sin(time * 4f) * 2.5f;
                    Rot("Shield", Vector3.right, Mathf.Sin(time * 4f) * 4f);
                    break;
                case EnemyKind.Flyer:
                    y = Mathf.Sin(time * 3f) * 0.15f;
                    lean = 10f;
                    for (int i = 1; i <= 4; i++) Rot("Rotor" + i, Vector3.up, time * 1400f + i * 40f);
                    break;
                case EnemyKind.Boss:
                    roll = Mathf.Sin(time * 9f) * 1.2f;
                    Rot("Cannon", Vector3.right, Mathf.Sin(time * 2f) * 6f);
                    for (int i = 1; i <= 4; i++) Rot("Wheel" + i, Vector3.right, time * 200f);
                    break;
            }
            Tilt(0f, lean, roll);
            Offset(y, squash);
        }
    }
}
