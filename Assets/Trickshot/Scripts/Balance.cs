using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrickshotDefense
{
    public enum Sport { Soccer = 0, Tennis = 1, Basket = 2, Bowling = 3, PingPong = 4 }
    public enum EnemyKind { Basic = 0, Runner = 1, Armored = 2, Flyer = 3, Boss = 4 }

    [Serializable]
    public class AthleteDef
    {
        public Sport sport;
        public string displayName;
        public string model;            // Resources/Models/<model>
        [TextArea] public string blurb; // una línea para la tienda y la barra de construcción
        public int unlockTrophies;      // 0 = disponible desde el principio
        public int cost;                // moneda de partida
        public float damage;
        public float interval;          // segundos entre ataques
        public float range;
        public float projectileSpeed;
        public bool hitsFlyers;
        public bool ignoresArmor;
        // Lo propio de cada deporte: fútbol = empuje (m); tenis = rebotes; baloncesto = radio (m);
        // bolos = ancho de la bola (m); ping-pong = multiplicador contra voladores
        public float special;
        public float specialTier3;      // valor de «special» en el nivel 3
        public string tier3Name;
        public Color color = Color.white;
    }

    [Serializable]
    public class EnemyDef
    {
        public EnemyKind kind;
        public string displayName;
        public string model;
        public float hp;
        public float speed;        // m/s
        public int coins;
        public int baseDamage;     // vidas que quita a la base si llega
        public float armor;        // 0..1: parte del daño directo que bloquea
        public bool flying;
        public float scale = 1f;
        public float barHeight = 1.8f;
    }

    [Serializable]
    public class KitDef
    {
        public string displayName;
        public Color color;
        public int price;
    }

    // Todos los números del juego. TRICKSHOT > Setup crea el asset en Assets/Trickshot/Resources/TrickshotBalance.asset
    // para ajustarlos desde el Inspector; si no existe, se usan estos valores por defecto.
    public class TrickshotBalance : ScriptableObject
    {
        [Header("Partida")]
        public int baseHp = 20;
        public int startCoins = 120;
        public int startCoinsPerLevel = 10;
        public int openSlots = 3;
        public int[] slotPrices = { 80, 140, 220 };
        public float[] upgradeCostMul = { 1.2f, 2.0f };       // coste de subir a nivel 2 y 3 (× coste base)
        public float[] tierDamageMul = { 1f, 1.6f, 2.4f };
        public float[] tierIntervalMul = { 1f, 0.9f, 0.8f };
        public float sellRefund = 0.6f;
        public float teamBonusInterval = 0.85f;                // dos o más del mismo deporte: × intervalo
        public float levelHpGrowth = 0.12f;                    // vida de los robots: +12 % por nivel
        public float wavePause = 3.5f;

        [Header("Arcade")]
        public float arcadeHpGrowth = 0.08f;
        public int arcadeStartCoins = 200;
        public int arcadeBossEvery = 10;

        [Header("Trofeos")]
        public int trophiesFirstClear = 20;
        public int trophiesPerLevel = 5;
        public int trophiesPerStar = 5;
        public int trophiesReplayPerStar = 3;
        public int trophiesPerArcadeWave = 2;

        public AthleteDef[] athletes = DefaultAthletes();
        public EnemyDef[] enemies = DefaultEnemies();
        public KitDef[] kits = DefaultKits();

        // Niveles: oleadas separadas por «|»; cada oleada, grupos «<letra><cantidad>:<segundos entre robots>».
        // b = básico, r = corredor, a = blindado, f = volador, B = jefe. Los grupos de una oleada salen a la vez,
        // cada uno 2 s después del anterior.
        [TextArea(1, 3)] public string[] levels = DefaultLevels();

        public const int LevelCount = 15;

        public AthleteDef Athlete(Sport s) => athletes[(int)s];
        public EnemyDef Enemy(EnemyKind k) => enemies[(int)k];

        private static TrickshotBalance cached;
        public static TrickshotBalance Load()
        {
            if (cached != null) return cached;
            cached = Resources.Load<TrickshotBalance>("TrickshotBalance");
            if (cached == null) cached = CreateInstance<TrickshotBalance>();
            if (cached.athletes == null || cached.athletes.Length < 5) cached.athletes = DefaultAthletes();
            if (cached.enemies == null || cached.enemies.Length < 5) cached.enemies = DefaultEnemies();
            if (cached.kits == null || cached.kits.Length < 1) cached.kits = DefaultKits();
            if (cached.levels == null || cached.levels.Length < LevelCount) cached.levels = DefaultLevels();
            return cached;
        }

        private static Color Hex(string h)
        {
            // A mano (sin ColorUtility): se llama desde inicializadores de campos del ScriptableObject
            int v = Convert.ToInt32(h.TrimStart('#'), 16);
            return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1f);
        }

        public static AthleteDef[] DefaultAthletes() => new[]
        {
            new AthleteDef { sport = Sport.Soccer, displayName = "STRIKER", model = "athlete_soccer",
                blurb = "Power shots that knock robots back.", unlockTrophies = 0, cost = 50, damage = 10, interval = 1.0f,
                range = 9.5f, projectileSpeed = 16, hitsFlyers = true, special = 1.1f, specialTier3 = 2.2f,
                tier3Name = "ROCKET SHOT", color = Hex("#35C26B") },
            new AthleteDef { sport = Sport.Tennis, displayName = "ACE", model = "athlete_tennis",
                blurb = "Serves that bounce between robots.", unlockTrophies = 0, cost = 70, damage = 7, interval = 1.1f,
                range = 10.5f, projectileSpeed = 20, hitsFlyers = true, special = 3, specialTier3 = 5,
                tier3Name = "RALLY x5", color = Hex("#D6E63A") },
            new AthleteDef { sport = Sport.Basket, displayName = "DUNKER", model = "athlete_basket",
                blurb = "Lobs that land on a whole group. Beats shields.", unlockTrophies = 30, cost = 110, damage = 16, interval = 2.4f,
                range = 9f, projectileSpeed = 9, hitsFlyers = false, ignoresArmor = true, special = 1.7f, specialTier3 = 2.5f,
                tier3Name = "SLAM ZONE", color = Hex("#F08A24") },
            new AthleteDef { sport = Sport.Bowling, displayName = "ROLLER", model = "athlete_bowling",
                blurb = "A ball that rolls through the whole line.", unlockTrophies = 120, cost = 100, damage = 14, interval = 2.2f,
                range = 12f, projectileSpeed = 11, hitsFlyers = false, special = 0.75f, specialTier3 = 1.25f,
                tier3Name = "STRIKE!", color = Hex("#8A6BFF") },
            new AthleteDef { sport = Sport.PingPong, displayName = "SPINNER", model = "athlete_pingpong",
                blurb = "Rapid fire. Shreds flying drones.", unlockTrophies = 260, cost = 80, damage = 3, interval = 0.25f,
                range = 7.5f, projectileSpeed = 24, hitsFlyers = true, special = 1.5f, specialTier3 = 2.2f,
                tier3Name = "DRONE HUNTER", color = Hex("#EF4E4E") },
        };

        public static EnemyDef[] DefaultEnemies() => new[]
        {
            new EnemyDef { kind = EnemyKind.Basic, displayName = "DUMMY BOT", model = "robot_basic", hp = 30, speed = 1.3f, coins = 8, baseDamage = 1, barHeight = 1.95f },
            new EnemyDef { kind = EnemyKind.Runner, displayName = "CONE BOT", model = "robot_runner", hp = 14, speed = 2.6f, coins = 6, baseDamage = 1, barHeight = 1.3f },
            new EnemyDef { kind = EnemyKind.Armored, displayName = "SHIELD BOT", model = "robot_armored", hp = 70, speed = 0.9f, coins = 16, baseDamage = 2, armor = 0.6f, barHeight = 1.7f },
            new EnemyDef { kind = EnemyKind.Flyer, displayName = "DRONE", model = "robot_flyer", hp = 22, speed = 1.8f, coins = 10, baseDamage = 1, flying = true, barHeight = 0.55f },
            new EnemyDef { kind = EnemyKind.Boss, displayName = "BALL CANNON", model = "robot_boss", hp = 900, speed = 0.55f, coins = 150, baseDamage = 10, armor = 0.2f, scale = 1.25f, barHeight = 3.3f },
        };

        public static KitDef[] DefaultKits() => new[]
        {
            new KitDef { displayName = "CLASSIC RED", color = Hex("#E8483F"), price = 0 },
            new KitDef { displayName = "ROYAL BLUE", color = Hex("#2F6FDE"), price = 40 },
            new KitDef { displayName = "EMERALD", color = Hex("#1FA971"), price = 60 },
            new KitDef { displayName = "SUNSET", color = Hex("#FF8A2A"), price = 80 },
            new KitDef { displayName = "VIOLET", color = Hex("#8A4DF0"), price = 100 },
            new KitDef { displayName = "CHAMPION GOLD", color = Hex("#F5C542"), price = 200 },
        };

        public static string[] DefaultLevels() => new[]
        {
            /* 1 */ "b4:1.8 | b6:1.4 | b6:1.1 r3:0.9 | b8:1.0 r4:0.8",
            /* 2 */ "b6:1.3 | r8:0.7 | b8:1.0 r6:0.8 | b10:0.9 r6:0.7 | b12:0.8",
            /* 3 */ "b8:1.1 | f4:1.4 | b8:1.0 f4:1.3 | r10:0.6 f5:1.2 | b12:0.8 f6:1.1",
            /* 4 */ "b8:1.0 a2:3.0 | r10:0.6 | a4:2.5 b6:1.0 | f6:1.1 a3:2.5 | b12:0.8 a4:2.2 r6:0.7",
            /* 5 */ "b10:0.9 | a4:2.2 r8:0.6 | f8:1.0 b8:0.9 | a5:2.0 f5:1.2 | B1:1 b10:1.2 r8:0.9",
            /* 6 */ "r14:0.5 | b12:0.8 f6:1.0 | a6:1.9 | b14:0.7 r10:0.5 | f10:0.8 a4:2.2 | b16:0.7 a5:2.0",
            /* 7 */ "b12:0.8 a3:2.4 | f10:0.8 | r16:0.45 a4:2.2 | b16:0.7 f8:0.9 | a8:1.7 | b18:0.6 f8:0.9 a5:2.0",
            /* 8 */ "a6:1.8 | r18:0.45 f6:1.0 | b18:0.6 | a6:1.8 f10:0.8 | b16:0.6 r14:0.45 | a8:1.6 b14:0.7 f8:0.9",
            /* 9 */ "b16:0.6 f8:0.9 | a8:1.6 r12:0.5 | f14:0.7 | b20:0.55 a6:1.8 | r20:0.4 f10:0.8 | a10:1.5 b16:0.6 f10:0.8",
            /* 10 */ "b16:0.6 a4:2.0 | f12:0.7 r14:0.45 | a8:1.6 b14:0.6 | f12:0.7 a6:1.8 | B1:1 a6:2.2 f10:1.0 | B1:1 b20:0.7 r16:0.6",
            /* 11 */ "r22:0.38 | a10:1.5 f8:0.9 | b22:0.5 | f16:0.6 a6:1.8 | b20:0.5 r18:0.4 a6:1.8 | a12:1.4 f12:0.7",
            /* 12 */ "b20:0.5 a6:1.7 | f16:0.6 | a12:1.4 r16:0.4 | b24:0.45 f12:0.7 | a10:1.4 f14:0.65 | b24:0.45 a10:1.5 r20:0.4",
            /* 13 */ "a10:1.4 f10:0.8 | r26:0.35 b12:0.7 | f18:0.55 | a14:1.3 b18:0.5 | f16:0.6 r20:0.38 a8:1.6 | b28:0.42 a12:1.4 f14:0.65",
            /* 14 */ "b24:0.45 f12:0.7 | a14:1.3 | r28:0.33 f14:0.65 | b26:0.42 a12:1.4 | f20:0.5 a10:1.5 | b28:0.4 r24:0.35 a12:1.4 f14:0.65",
            /* 15 */ "b24:0.45 a8:1.5 | f18:0.55 r20:0.38 | a14:1.3 b20:0.5 | B1:1 f14:0.8 a8:1.8 | b28:0.42 r24:0.35 f14:0.65 | B2:9 a12:1.6 b24:0.6 f14:0.8",
        };

        // ---------------- Oleadas ----------------
        public struct Spawn { public EnemyKind kind; public float time; }

        public static List<List<Spawn>> ParseLevel(string text)
        {
            var waves = new List<List<Spawn>>();
            foreach (string w in text.Split('|'))
            {
                var list = new List<Spawn>();
                int g = 0;
                foreach (string raw in w.Trim().Split(' '))
                {
                    string tok = raw.Trim();
                    if (tok.Length < 2) continue;
                    EnemyKind kind;
                    switch (tok[0])
                    {
                        case 'r': kind = EnemyKind.Runner; break;
                        case 'a': kind = EnemyKind.Armored; break;
                        case 'f': kind = EnemyKind.Flyer; break;
                        case 'B': kind = EnemyKind.Boss; break;
                        default: kind = EnemyKind.Basic; break;
                    }
                    string[] parts = tok.Substring(1).Split(':');
                    int count;
                    if (!int.TryParse(parts[0], out count)) count = 1;
                    float gap = 1f;
                    if (parts.Length > 1) float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out gap);
                    for (int i = 0; i < count; i++) list.Add(new Spawn { kind = kind, time = g * 2f + i * gap });
                    g++;
                }
                list.Sort((a, b) => a.time.CompareTo(b.time));
                if (list.Count > 0) waves.Add(list);
            }
            return waves;
        }

        // Arcade: cada oleada tiene un «presupuesto» que crece; los tipos de robot se van abriendo con las oleadas
        public List<Spawn> ArcadeWave(int wave, System.Random rng)
        {
            var list = new List<Spawn>();
            float budget = 8f + wave * 4.5f * Mathf.Pow(1.05f, wave);
            float gapBase = Mathf.Max(0.3f, 1.1f - wave * 0.03f);
            if (wave % arcadeBossEvery == 0)
            {
                int bosses = Mathf.Min(3, wave / arcadeBossEvery);
                for (int i = 0; i < bosses; i++) list.Add(new Spawn { kind = EnemyKind.Boss, time = 2f + i * 8f });
                budget *= 0.5f;
            }
            float t = 0f;
            int guard = 0;
            while (budget > 0f && guard++ < 400)
            {
                EnemyKind k = EnemyKind.Basic;
                double r = rng.NextDouble();
                if (wave >= 2 && r < 0.28) k = EnemyKind.Runner;
                else if (wave >= 4 && r < 0.46) k = EnemyKind.Flyer;
                else if (wave >= 6 && r < 0.62) k = EnemyKind.Armored;
                float cost = k == EnemyKind.Runner ? 1.4f : k == EnemyKind.Flyer ? 2.6f : k == EnemyKind.Armored ? 5f : 2f;
                budget -= cost;
                list.Add(new Spawn { kind = k, time = t });
                t += gapBase * (k == EnemyKind.Armored ? 1.8f : k == EnemyKind.Runner ? 0.6f : 1f);
            }
            list.Sort((a, b) => a.time.CompareTo(b.time));
            return list;
        }
    }

    // ---------------- Guardado ----------------
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int trophies;
        public int levelReached = 1;                 // nivel más alto desbloqueado (1..15)
        public int[] stars = new int[TrickshotBalance.LevelCount];
        public int arcadeBest;
        public bool[] unlocked = new bool[5];
        public bool[] kits = new bool[6];
        public int kit;
        public bool music = true, sfx = true, vibration = true;
        public bool tutorialDone;
        public int games;

        public const string Key = "trickshot.save.v1";

        public static SaveData Load()
        {
            SaveData d = null;
            try
            {
                string json = KovusPride.Publishing.StorePlatform.LoadString(Key, "");
                if (!string.IsNullOrEmpty(json)) d = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e) { Debug.LogWarning("[Save] " + e.Message); }
            if (d == null) d = new SaveData();
            var bal = TrickshotBalance.Load();
            if (d.stars == null || d.stars.Length != TrickshotBalance.LevelCount) Array.Resize(ref d.stars, TrickshotBalance.LevelCount);
            if (d.unlocked == null || d.unlocked.Length != bal.athletes.Length) Array.Resize(ref d.unlocked, bal.athletes.Length);
            if (d.kits == null || d.kits.Length != bal.kits.Length) Array.Resize(ref d.kits, bal.kits.Length);
            for (int i = 0; i < bal.athletes.Length; i++) if (bal.athletes[i].unlockTrophies <= 0) d.unlocked[i] = true;
            d.kits[0] = true;
            d.kit = Mathf.Clamp(d.kit, 0, bal.kits.Length - 1);
            d.levelReached = Mathf.Clamp(d.levelReached, 1, TrickshotBalance.LevelCount);
            return d;
        }

        public void Save()
        {
            try { KovusPride.Publishing.StorePlatform.SaveString(Key, JsonUtility.ToJson(this)); }
            catch (Exception e) { Debug.LogWarning("[Save] " + e.Message); }
        }
    }
}
