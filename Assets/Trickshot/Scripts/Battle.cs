using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TrickshotDefense
{
    public enum Mode { Levels, Arcade }

    // Una partida: robots, deportistas, monedas, oleadas y la entrada sobre el campo.
    // La interfaz (Hud) escucha sus eventos y llama a sus métodos públicos.
    public class Battle : MonoBehaviour
    {
        public TrickshotBalance Bal { get; private set; }
        public Mode Mode { get; private set; }
        public int Level { get; private set; }            // 1..15 en el modo Niveles
        public int Coins { get; private set; }
        public int BaseHp { get; private set; }
        public int BaseHpMax { get; private set; }
        public int Wave { get; private set; }             // oleada actual (1..), 0 antes de empezar
        public int WaveCount { get; private set; }        // 0 = infinitas (Arcade)
        public bool WaitingForStart { get; private set; } // antes de la primera oleada
        public float NextWaveIn { get; private set; }     // cuenta atrás entre oleadas (-1 si no hay)
        public bool Over { get; private set; }
        public bool Won { get; private set; }
        public int SelectedSlot { get; private set; } = -1;
        public int Kills { get; private set; }
        public float Elapsed { get; private set; }
        public bool SpeedUp { get; private set; }
        public bool Paused { get; private set; }
        public bool InputLocked { get; set; }             // el tutorial bloquea la entrada sobre el campo

        public readonly List<Enemy> Enemies = new List<Enemy>(64);
        public readonly Athlete[] Athletes = new Athlete[6];
        public readonly bool[] SlotOpen = new bool[6];

        public event Action Changed;                      // monedas, vida, oleada, selección
        public event Action<bool> Ended;                  // true = victoria
        public event Action<Enemy> Killed;
        public event Action<int> Built;                   // posición
        public event Action BaseHit;

        private Color kitColor;
        private List<List<TrickshotBalance.Spawn>> waves;
        private List<TrickshotBalance.Spawn> current;
        private int spawnIndex;
        private float waveClock;
        private float hpMul = 1f;
        private System.Random rng;
        private Camera cam;
        private Transform hudCanvas;
        private float shake;
        private readonly GameObject[] slotModels = new GameObject[6];
        private readonly GameObject[] slotLocks = new GameObject[6];
        private GameObject selector;
        private float anim;

        public static Battle Create(Mode mode, int level, Color kit, Transform hudCanvas)
        {
            var go = new GameObject("Battle");
            var b = go.AddComponent<Battle>();
            b.Bal = TrickshotBalance.Load();
            b.Mode = mode;
            b.Level = level;
            b.kitColor = kit;
            b.hudCanvas = hudCanvas;
            b.cam = Camera.main;
            b.Setup();
            return b;
        }

        private void Setup()
        {
            rng = new System.Random(Environment.TickCount);
            BaseHpMax = BaseHp = Bal.baseHp;
            if (Mode == Mode.Levels)
            {
                waves = TrickshotBalance.ParseLevel(Bal.levels[Mathf.Clamp(Level - 1, 0, Bal.levels.Length - 1)]);
                WaveCount = waves.Count;
                Coins = Bal.startCoins + Bal.startCoinsPerLevel * (Level - 1);
                hpMul = 1f + Bal.levelHpGrowth * (Level - 1);
            }
            else
            {
                WaveCount = 0;
                Coins = Bal.arcadeStartCoins;
            }
            for (int i = 0; i < SlotOpen.Length; i++) SlotOpen[i] = i < Bal.openSlots;
            for (int i = 0; i < World.Slots.Length; i++)
            {
                var s = Models.Spawn("slot", transform);
                s.transform.position = World.Slots[i];
                var col = s.AddComponent<SphereCollider>();
                col.radius = 1.1f;
                col.center = new Vector3(0f, 0.6f, 0f);
                slotModels[i] = s;
                var lk = Models.Spawn("prim_cube", s.transform);
                lk.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                lk.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                lk.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                Models.TintAll(lk.GetComponentsInChildren<Renderer>(), Ui.Disabled);
                slotLocks[i] = lk;
            }
            selector = Models.Spawn("prim_disc", transform);
            Models.TintAll(selector.GetComponentsInChildren<Renderer>(), Ui.Yellow);
            selector.SetActive(false);
            RefreshSlots();
            WaitingForStart = true;
            NextWaveIn = -1f;
            Time.timeScale = 1f;
        }

        private void RefreshSlots()
        {
            for (int i = 0; i < slotModels.Length; i++)
            {
                Models.ClearTint(slotModels[i].GetComponentsInChildren<Renderer>());
                if (!SlotOpen[i]) Models.TintAll(slotModels[i].GetComponentsInChildren<Renderer>(), Ui.Hex(0x7D8799));
                slotLocks[i].SetActive(!SlotOpen[i]);
            }
        }

        // ---------------- Economía ----------------
        public int SlotPrice(int slot)
        {
            int bought = 0;
            for (int i = Bal.openSlots; i < SlotOpen.Length; i++) if (SlotOpen[i]) bought++;
            return Bal.slotPrices[Mathf.Clamp(bought, 0, Bal.slotPrices.Length - 1)];
        }

        public bool BuySlot(int slot)
        {
            if (Over || SlotOpen[slot]) return false;
            int price = SlotPrice(slot);
            if (Coins < price) return false;
            Coins -= price;
            SlotOpen[slot] = true;
            RefreshSlots();
            Puff.Ring(World.Slots[slot], Ui.Yellow, 2.4f);
            Sfx.Play("buy");
            Changed?.Invoke();
            return true;
        }

        public bool CanBuild(Sport s) => Coins >= Bal.Athlete(s).cost;

        public bool Build(int slot, Sport sport)
        {
            if (Over || slot < 0 || !SlotOpen[slot] || Athletes[slot] != null) return false;
            var def = Bal.Athlete(sport);
            if (Coins < def.cost) return false;
            Coins -= def.cost;
            var a = Athlete.Create(this, def, slot, kitColor);
            Athletes[slot] = a;
            Puff.Ring(World.Slots[slot], def.color, 2.2f);
            Puff.Burst(World.Slots[slot] + Vector3.up * 0.4f, Ui.White, 8, 0.12f, 4f);
            Sfx.Play("place");
            KovusPride.Web.Haptics.Pulse(15);
            Analytics.AthletePlaced(sport, Mode, Level);
            Built?.Invoke(slot);
            Changed?.Invoke();
            return true;
        }

        public bool Upgrade(int slot)
        {
            var a = slot >= 0 ? Athletes[slot] : null;
            if (Over || a == null || a.Tier >= 2) return false;
            int cost = a.UpgradeCost;
            if (Coins < cost) return false;
            Coins -= cost;
            a.Upgrade(cost);
            Sfx.Play("upgrade");
            Analytics.AthleteUpgraded(a.Def.sport, a.Tier + 1);
            Changed?.Invoke();
            return true;
        }

        public void Sell(int slot)
        {
            var a = slot >= 0 ? Athletes[slot] : null;
            if (Over || a == null) return;
            Coins += a.SellValue;
            Puff.Burst(a.transform.position + Vector3.up, Ui.Gold, 8, 0.14f, 4f);
            Destroy(a.gameObject);
            Athletes[slot] = null;
            Sfx.Play("sell");
            Changed?.Invoke();
        }

        public float TeamIntervalMul(Sport s)
        {
            int n = 0;
            foreach (var a in Athletes) if (a != null && a.Def.sport == s) n++;
            return n >= 2 ? Bal.teamBonusInterval : 1f;
        }

        public int TeamCount(Sport s)
        {
            int n = 0;
            foreach (var a in Athletes) if (a != null && a.Def.sport == s) n++;
            return n;
        }

        public void SetKit(Color c)
        {
            kitColor = c;
            foreach (var a in Athletes) if (a != null) a.SetKit(c);
        }

        // ---------------- Oleadas ----------------
        public void StartNextWave(bool early = false)
        {
            if (Over) return;
            if (early && NextWaveIn > 0f)
            {
                int bonus = Mathf.CeilToInt(NextWaveIn) * 3;
                if (bonus > 0) { Coins += bonus; FloatText.Show(hudCanvas, World.Slots[0] + Vector3.up * 3f, "+" + bonus, Ui.Gold, 44f); }
            }
            WaitingForStart = false;
            NextWaveIn = -1f;
            Wave++;
            if (Mode == Mode.Levels) current = waves[Wave - 1];
            else
            {
                current = Bal.ArcadeWave(Wave, rng);
                hpMul = 1f + Bal.arcadeHpGrowth * (Wave - 1);
            }
            spawnIndex = 0;
            waveClock = 0f;
            Sfx.Play("wave");
            Changed?.Invoke();
        }

        private bool WaveSpawning => current != null && spawnIndex < current.Count;

        private void TickWaves(float dt)
        {
            if (WaitingForStart || Over) return;
            if (WaveSpawning)
            {
                waveClock += dt;
                while (spawnIndex < current.Count && current[spawnIndex].time <= waveClock)
                {
                    var sp = current[spawnIndex++];
                    var def = Bal.Enemy(sp.kind);
                    float lane = World.Lanes[rng.Next(World.Lanes.Length)];
                    if (sp.kind == EnemyKind.Boss) lane = 0f;
                    Enemies.Add(Enemy.Create(this, def, hpMul, lane));
                }
                return;
            }
            if (Enemies.Count > 0) return;

            bool last = Mode == Mode.Levels && Wave >= WaveCount;
            if (last) { Finish(true); return; }
            if (NextWaveIn < 0f)
            {
                NextWaveIn = Bal.wavePause;
                foreach (var a in Athletes) if (a != null) a.Celebrate();
                Changed?.Invoke();
            }
            NextWaveIn -= dt;
            if (NextWaveIn <= 0f) StartNextWave();
        }

        // ---------------- Sucesos ----------------
        public void OnEnemyKilled(Enemy e)
        {
            Enemies.Remove(e);
            Kills++;
            Coins += e.Def.coins;
            FloatText.Show(hudCanvas, e.HitPoint + Vector3.up * 0.6f, "+" + e.Def.coins, Ui.Gold, e.Def.kind == EnemyKind.Boss ? 64f : 38f);
            Sfx.Play(e.Def.kind == EnemyKind.Boss ? "boss_down" : "robot_down", 0.8f);
            if (e.Def.kind == EnemyKind.Boss) { Shake(0.35f); KovusPride.Web.Haptics.Pulse(60); }
            Killed?.Invoke(e);
            Changed?.Invoke();
        }

        public void OnEnemyReachedBase(Enemy e)
        {
            Enemies.Remove(e);
            if (Over) return;
            BaseHp = Mathf.Max(0, BaseHp - e.Def.baseDamage);
            Shake(0.3f);
            Sfx.Play("base_hit");
            KovusPride.Web.Haptics.Pulse(40);
            BaseHit?.Invoke();
            Changed?.Invoke();
            if (BaseHp <= 0) Finish(false);
        }

        public void OnHit(Vector3 at, Sport sport)
        {
            Puff.Burst(at, Ui.White, 3, 0.1f, 3.5f, 0.3f);
            Sfx.Play("hit_" + sport.ToString().ToLowerInvariant(), 0.55f);
        }

        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }

        private void Finish(bool won)
        {
            if (Over) return;
            Over = true;
            Won = won;
            SelectedSlot = -1;
            selector.SetActive(false);
            Time.timeScale = 1f;
            Paused = false; SpeedUp = false;
            if (won) foreach (var a in Athletes) if (a != null) a.Celebrate();
            Ended?.Invoke(won);
        }

        // Arcade: continuar una vez tras ver un anuncio (vida de la base a la mitad, robots en pantalla fuera)
        public void Revive()
        {
            if (!Over || Won) return;
            Over = false;
            BaseHp = Mathf.Max(1, BaseHpMax / 2);
            foreach (var e in Enemies.ToArray()) if (e != null) Destroy(e.gameObject);
            Enemies.Clear();
            current = null;
            NextWaveIn = Bal.wavePause;
            Changed?.Invoke();
        }

        public int Stars => !Won ? 0 : BaseHp >= BaseHpMax ? 3 : BaseHp * 2 >= BaseHpMax ? 2 : 1;

        // ---------------- Velocidad y pausa ----------------
        public void ToggleSpeed() { SpeedUp = !SpeedUp; ApplyTime(); Changed?.Invoke(); }

        public void SetPaused(bool p) { Paused = p; ApplyTime(); Changed?.Invoke(); }

        private void ApplyTime() { Time.timeScale = Paused ? 0f : SpeedUp ? 2f : 1f; }

        // ---------------- Selección ----------------
        public void Select(int slot)
        {
            SelectedSlot = slot;
            selector.SetActive(slot >= 0);
            if (slot >= 0) selector.transform.position = World.Slots[slot] + Vector3.up * 0.02f;
            if (slot >= 0) Sfx.Play("select", 0.6f);
            Changed?.Invoke();
        }

        public int FirstFreeSlot()
        {
            for (int i = 0; i < Athletes.Length; i++) if (SlotOpen[i] && Athletes[i] == null) return i;
            return -1;
        }

        // ---------------- Bucle ----------------
        private void Update()
        {
            float dt = Time.deltaTime;
            anim += Time.unscaledDeltaTime;
            if (!Over && !Paused) Elapsed += Time.unscaledDeltaTime;

            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                var e = Enemies[i];
                if (e == null) { Enemies.RemoveAt(i); continue; }
                e.Tick(dt, cam);
            }
            foreach (var a in Athletes) if (a != null) a.Tick(dt);
            TickWaves(dt);

            if (selector.activeSelf)
            {
                float s = 1.7f + Mathf.Sin(anim * 6f) * 0.08f;
                selector.transform.localScale = new Vector3(s, 1f, s);
            }
            if (!Over && !Paused && !InputLocked) HandleInput();
            if (cam != null)
            {
                Vector3 basePos = World.CameraPos;
                if (shake > 0f)
                {
                    shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 1.4f);
                    basePos += (Vector3)(UnityEngine.Random.insideUnitCircle * shake * 0.6f);
                }
                cam.transform.position = basePos;
            }
        }

        private void HandleInput()
        {
            Vector2 screen;
            if (PointerPressed(out screen))
            {
                if (EventSystem.current != null && IsOverUi(screen)) return;
                int hitSlot = SlotAt(screen);
                if (hitSlot >= 0) Select(hitSlot == SelectedSlot ? -1 : hitSlot);
                else if (SelectedSlot >= 0) Select(-1);
            }
            int key = NumberKey();
            if (key >= 1 && key <= 5)
            {
                int slot = SelectedSlot >= 0 && Athletes[SelectedSlot] == null && SlotOpen[SelectedSlot] ? SelectedSlot : FirstFreeSlot();
                var sport = (Sport)(key - 1);
                if (slot >= 0 && Game.I.Save.unlocked[(int)sport] && Build(slot, sport)) Select(-1);
            }
            if (KeyDown('u') && SelectedSlot >= 0) Upgrade(SelectedSlot);
            if (KeyDown(' ')) { if (WaitingForStart || NextWaveIn > 0f) StartNextWave(true); else ToggleSpeed(); }
        }

        private static readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private static bool IsOverUi(Vector2 screen)
        {
            var ped = new PointerEventData(EventSystem.current) { position = screen };
            uiHits.Clear();
            EventSystem.current.RaycastAll(ped, uiHits);
            return uiHits.Count > 0;
        }

        public int SlotAt(Vector2 screen)
        {
            if (cam == null) return -1;
            Ray ray = cam.ScreenPointToRay(screen);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 200f))
            {
                for (int i = 0; i < slotModels.Length; i++)
                    if (hit.collider != null && hit.collider.gameObject == slotModels[i]) return i;
            }
            // Margen: el podio más cercano al punto tocado sobre el suelo
            float t;
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out t))
            {
                Vector3 p = ray.GetPoint(t);
                for (int i = 0; i < World.Slots.Length; i++)
                    if (World.FlatDistance(p, World.Slots[i]) < 1.5f) return i;
            }
            return -1;
        }

        public Vector2 SlotScreen(int slot)
        {
            if (cam == null) return Vector2.zero;
            return cam.WorldToScreenPoint(World.Slots[slot] + Vector3.up * 0.5f);
        }

        // ---------------- Entrada (Input System, con alternativa al Input clásico) ----------------
        public static bool PointerPressed(out Vector2 pos)
        {
            pos = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) { pos = ts.primaryTouch.position.ReadValue(); return true; }
            var m = Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame) { pos = m.position.ReadValue(); return true; }
            return false;
#else
            if (Input.GetMouseButtonDown(0)) { pos = Input.mousePosition; return true; }
            return false;
#endif
        }

        public static int NumberKey()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return 0;
            if (k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame) return 1;
            if (k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame) return 2;
            if (k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame) return 3;
            if (k.digit4Key.wasPressedThisFrame || k.numpad4Key.wasPressedThisFrame) return 4;
            if (k.digit5Key.wasPressedThisFrame || k.numpad5Key.wasPressedThisFrame) return 5;
            return 0;
#else
            for (int i = 1; i <= 5; i++) if (Input.GetKeyDown(KeyCode.Alpha0 + i)) return i;
            return 0;
#endif
        }

        public static bool KeyDown(char c)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return false;
            switch (c)
            {
                case ' ': return k.spaceKey.wasPressedThisFrame;
                case 'u': return k.uKey.wasPressedThisFrame;
                case 'p': return k.pKey.wasPressedThisFrame;
                case 'e': return k.escapeKey.wasPressedThisFrame;
                case 's': return k.sKey.wasPressedThisFrame;
                default: return false;
            }
#else
            switch (c)
            {
                case ' ': return Input.GetKeyDown(KeyCode.Space);
                case 'u': return Input.GetKeyDown(KeyCode.U);
                case 'p': return Input.GetKeyDown(KeyCode.P);
                case 'e': return Input.GetKeyDown(KeyCode.Escape);
                case 's': return Input.GetKeyDown(KeyCode.S);
                default: return false;
            }
#endif
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }
    }
}
