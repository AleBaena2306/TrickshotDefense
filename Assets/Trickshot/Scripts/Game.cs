using System;
using KovusPride.Publishing;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrickshotDefense
{
    // Punto de entrada: monta la cámara, el estadio y la interfaz por código, y lleva el flujo menú → partida → resultados.
    // Se crea solo al cargar la escena (no hace falta configurar nada en ella).
    public class Game : MonoBehaviour
    {
        public static Game I { get; private set; }
        public SaveData Save { get; private set; }
        public TrickshotBalance Bal { get; private set; }
        public Battle Battle { get; private set; }
        public RectTransform Canvas { get; private set; }
        public Color Kit => Bal.kits[Mathf.Clamp(Save.kit, 0, Bal.kits.Length - 1)].color;

        private Camera cam;
        private CanvasScaler scaler;
        private CanvasGroup fade;
        private Menus menus;
        private Hud hud;
        private Vector2Int lastScreen;
        private bool revivedThisRun;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<Game>() != null) return;
            new GameObject("Trickshot").AddComponent<Game>();
        }

        private void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            StorePlatform.Init();
            StorePlatform.PauseRequested += OnPlatformPause;
            StorePlatform.MuteRequested += OnPlatformMute;
            Bal = TrickshotBalance.Load();
            Save = SaveData.Load();
            Sfx.Enabled = Save.sfx;
            Music.Enabled = Save.music;
            KovusPride.Web.Haptics.Enabled = Save.vibration;
            KovusPride.Web.Ads.TestAds = true;   // H5 Games Ads de prueba hasta que AdSense apruebe la cuenta
            KovusPride.Web.Ads.Create(transform);

            BuildStage();
            BuildCanvas();
            menus = new Menus(this, Canvas);
            hud = new Hud(this, Canvas);
            fade.transform.SetAsLastSibling();
            ShowMenu();
            StorePlatform.GameReady();
        }

        private void OnDestroy()
        {
            StorePlatform.PauseRequested -= OnPlatformPause;
            StorePlatform.MuteRequested -= OnPlatformMute;
        }

        // ---------------- Escenario ----------------
        private void BuildStage()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var cgo = new GameObject("Main Camera");
                cgo.tag = "MainCamera";
                cam = cgo.AddComponent<Camera>();
                cgo.AddComponent<AudioListener>();
            }
            cam.transform.position = World.CameraPos;
            cam.transform.rotation = Quaternion.LookRotation(World.CameraLook - World.CameraPos, Vector3.up);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Ui.Hex(0x8FD3F4);
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 120f;
            FitCamera();

            Light sun = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                var lgo = new GameObject("Sun");
                sun = lgo.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            sun.intensity = 1.25f;
            sun.color = Ui.Hex(0xFFF4E2);
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Ui.Hex(0x9AA6B8);

            var stage = new GameObject("Stage").transform;
            var field = Models.Spawn("field", stage);
            field.transform.position = Vector3.zero;
            var club = Models.Spawn("base", stage);
            club.transform.position = new Vector3(World.BaseX, 0f, 0f);
            Models.Tint(club, "Kit", Ui.Hex(0xE8483F));
        }

        // Ajusta el campo de visión para que siempre se vea el ancho del campo, sea cual sea la proporción
        private void FitCamera()
        {
            if (cam == null) return;
            float aspect = Mathf.Max(0.5f, cam.aspect);
            float dist = Vector3.Distance(World.CameraPos, World.CameraLook);
            float hFov = 2f * Mathf.Atan(World.ViewWidth * 0.5f / dist);
            float vFov = 2f * Mathf.Atan(Mathf.Tan(hFov * 0.5f) / aspect) * Mathf.Rad2Deg;
            cam.fieldOfView = Mathf.Clamp(vFov, 34f, 70f);
        }

        private void BuildCanvas()
        {
            var go = new GameObject("UI", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Ui.RefW, Ui.RefH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            go.AddComponent<GraphicRaycaster>();
            Canvas = (RectTransform)go.transform;
            FitScaler();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            var f = Ui.Flat("Fade", Canvas, Color.black, false);
            Ui.Stretch(f.rectTransform);
            fade = f.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 1f;
            fade.blocksRaycasts = false;
            Fx.Fade(fade, 0f, 0.5f);
        }

        private void FitScaler()
        {
            if (scaler == null) return;
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            scaler.matchWidthOrHeight = aspect >= Ui.RefW / Ui.RefH ? 1f : 0f;
        }

        private void Update()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size != lastScreen) { lastScreen = size; FitCamera(); FitScaler(); }
            hud.Tick();
            menus.Tick();
        }

        // ---------------- Fundido a negro entre pantallas ----------------
        public void Transition(Action middle)
        {
            fade.transform.SetAsLastSibling();
            fade.blocksRaycasts = true;
            Fx.Fade(fade, 1f, 0.22f, () =>
            {
                middle?.Invoke();
                fade.transform.SetAsLastSibling();
                Fx.Fade(fade, 0f, 0.28f, () => fade.blocksRaycasts = false);
            });
        }

        // ---------------- Flujo ----------------
        public void ShowMenu()
        {
            EndBattle();
            hud.Hide();
            menus.ShowMain();
            Music.Play("menu");
        }

        public void StartLevel(int level)
        {
            Transition(() =>
            {
                menus.HideAll();
                BeginBattle(Mode.Levels, level);
            });
        }

        public void StartArcade()
        {
            Transition(() =>
            {
                menus.HideAll();
                BeginBattle(Mode.Arcade, 0);
            });
        }

        private void BeginBattle(Mode mode, int level)
        {
            EndBattle();
            revivedThisRun = false;
            Battle = Battle.Create(mode, level, Kit, Canvas);
            Battle.Ended += OnBattleEnded;
            hud.Show(Battle);
            Music.Play("game");
            Save.games++;
            Save.Save();
            StorePlatform.GameplayStart();
            Analytics.GameStart(mode, level);
            bool tutorial = mode == Mode.Levels && level == 1 && !Save.tutorialDone;
            if (tutorial) hud.StartTutorial();
        }

        private void EndBattle()
        {
            if (Battle == null) return;
            Battle.Ended -= OnBattleEnded;
            Destroy(Battle.gameObject);
            Battle = null;
            Time.timeScale = 1f;
        }

        public void Restart()
        {
            if (Battle == null) return;
            var mode = Battle.Mode; int level = Battle.Level;
            if (!Battle.Over) Analytics.GameEnd(Battle, "quit");
            Transition(() => BeginBattle(mode, level));
        }

        public void Quit()
        {
            if (Battle != null && !Battle.Over) Analytics.GameEnd(Battle, "quit");
            StorePlatform.GameplayStop();
            Transition(ShowMenu);
        }

        public void NextLevel()
        {
            if (Battle == null) return;
            int next = Mathf.Min(TrickshotBalance.LevelCount, Battle.Level + 1);
            ShowInterstitialThen(() => StartLevel(next));
        }

        public void AfterResults()
        {
            ShowInterstitialThen(() => { StorePlatform.GameplayStop(); Transition(ShowMenu); });
        }

        private void ShowInterstitialThen(Action then)
        {
            // Solo en pausas naturales (entre partidas) y con el límite de frecuencia de la capa común
            if (StorePlatform.CanShowInterstitial) StorePlatform.ShowInterstitial("level_end", then);
            else then();
        }

        // ---------------- Resultados ----------------
        public int LastTrophies { get; private set; }
        public bool LastNewRecord { get; private set; }

        private void OnBattleEnded(bool won)
        {
            var b = Battle;
            StorePlatform.GameplayStop();
            int trophies = 0;
            LastNewRecord = false;
            if (b.Mode == Mode.Levels)
            {
                int idx = b.Level - 1;
                if (won)
                {
                    bool first = Save.stars[idx] == 0;
                    trophies = first
                        ? Bal.trophiesFirstClear + Bal.trophiesPerLevel * b.Level + Bal.trophiesPerStar * b.Stars
                        : Bal.trophiesReplayPerStar * b.Stars;
                    if (b.Stars > Save.stars[idx]) Save.stars[idx] = b.Stars;
                    if (b.Level >= Save.levelReached && b.Level < TrickshotBalance.LevelCount) Save.levelReached = b.Level + 1;
                    if (b.Level == 1) Save.tutorialDone = true;
                }
            }
            else
            {
                int waves = Mathf.Max(0, b.Wave - 1);
                trophies = waves * Bal.trophiesPerArcadeWave;
                if (b.Wave > Save.arcadeBest) { Save.arcadeBest = b.Wave; LastNewRecord = true; StorePlatform.Happytime(); }
                StorePlatform.SubmitScore("best", b.Wave);
            }
            LastTrophies = trophies;
            Save.trophies += trophies;
            Save.Save();
            Analytics.GameEnd(b, won ? "win" : "lose");
            if (won && b.Mode == Mode.Levels && b.Stars == 3) StorePlatform.Happytime();
            Fx.After(won ? 1.1f : 0.9f, () => hud.ShowResults(won, trophies));
        }

        public bool CanRevive => Battle != null && Battle.Mode == Mode.Arcade && !revivedThisRun && RewardedReady;

        public static bool RewardedReady => KovusPride.Web.Ads.Instance != null && KovusPride.Web.Ads.Instance.IsReady;

        public void ReviveWithAd(Action<bool> done)
        {
            if (!CanRevive) { done?.Invoke(false); return; }
            KovusPride.Web.Ads.Instance.ShowRewarded("arcade_revive", ok =>
            {
                if (ok && Battle != null)
                {
                    revivedThisRun = true;
                    Battle.Revive();
                    StorePlatform.GameplayStart();
                }
                done?.Invoke(ok);
            });
        }

        public void DoubleTrophiesWithAd(Action<bool> done)
        {
            if (!RewardedReady || LastTrophies <= 0) { done?.Invoke(false); return; }
            KovusPride.Web.Ads.Instance.ShowRewarded("double_trophies", ok =>
            {
                if (ok) { Save.trophies += LastTrophies; LastTrophies *= 2; Save.Save(); }
                done?.Invoke(ok);
            });
        }

        // ---------------- Tienda ----------------
        public bool Unlock(Sport s)
        {
            int i = (int)s;
            var def = Bal.athletes[i];
            if (Save.unlocked[i] || Save.trophies < def.unlockTrophies) return false;
            Save.trophies -= def.unlockTrophies;
            Save.unlocked[i] = true;
            Save.Save();
            Analytics.ShopUnlock("athlete_" + s.ToString().ToLowerInvariant(), def.unlockTrophies);
            return true;
        }

        public bool BuyOrEquipKit(int i)
        {
            if (i < 0 || i >= Bal.kits.Length) return false;
            if (!Save.kits[i])
            {
                if (Save.trophies < Bal.kits[i].price) return false;
                Save.trophies -= Bal.kits[i].price;
                Save.kits[i] = true;
                Analytics.ShopUnlock("kit_" + i, Bal.kits[i].price);
            }
            Save.kit = i;
            Save.Save();
            if (Battle != null) Battle.SetKit(Kit);
            return true;
        }

        // ---------------- Ajustes ----------------
        public void SetMusic(bool on) { Save.music = on; Music.Enabled = on; Save.Save(); }
        public void SetSfx(bool on) { Save.sfx = on; Sfx.Enabled = on; Save.Save(); }
        public void SetVibration(bool on) { Save.vibration = on; KovusPride.Web.Haptics.Enabled = on; Save.Save(); }

        private void OnPlatformPause(bool pause)
        {
            if (Battle == null || Battle.Over) return;
            if (pause && !Battle.Paused) hud.OpenPause();
        }

        private void OnPlatformMute(bool mute)
        {
            AudioListener.volume = mute ? 0f : 1f;
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus && Battle != null && !Battle.Over && !Battle.Paused) hud.OpenPause();
        }
    }

    // Eventos de analítica propios del juego (además de game_start / game_end). Sin datos personales.
    public static class Analytics
    {
        private static string ModeName(Mode m) => m == Mode.Levels ? "levels" : "arcade";

        public static void GameStart(Mode mode, int level) =>
            KovusPride.Web.Analytics.Log("game_start", ("mode", ModeName(mode)), ("level_id", mode == Mode.Levels ? level : 0));

        public static void GameEnd(Battle b, string result)
        {
            KovusPride.Web.Analytics.Log("game_end", ("mode", ModeName(b.Mode)), ("level_id", b.Mode == Mode.Levels ? b.Level : 0),
                ("duration_sec", Mathf.RoundToInt(b.Elapsed)), ("result", result));
            if (b.Mode == Mode.Levels && result == "win")
                KovusPride.Web.Analytics.Log("level_complete", ("level_id", b.Level), ("stars", b.Stars), ("duration_sec", Mathf.RoundToInt(b.Elapsed)));
            else if (b.Mode == Mode.Levels && result == "lose")
                KovusPride.Web.Analytics.Log("level_fail", ("level_id", b.Level), ("wave", b.Wave));
            else if (b.Mode == Mode.Arcade && result != "quit")
                KovusPride.Web.Analytics.Log("arcade_wave_reached", ("wave", b.Wave));
        }

        public static void AthletePlaced(Sport s, Mode m, int level) =>
            KovusPride.Web.Analytics.Log("athlete_placed", ("sport", s.ToString().ToLowerInvariant()), ("mode", ModeName(m)), ("level_id", level));

        public static void AthleteUpgraded(Sport s, int tier) =>
            KovusPride.Web.Analytics.Log("athlete_upgraded", ("sport", s.ToString().ToLowerInvariant()), ("tier", tier));

        public static void ShopUnlock(string item, int cost) =>
            KovusPride.Web.Analytics.Log("shop_unlock", ("item", item), ("cost", cost));

        public static void TutorialStep(int step) => KovusPride.Web.Analytics.Log("tutorial_step", ("step", step));
        public static void TutorialComplete() => KovusPride.Web.Analytics.Log("tutorial_complete");
        public static void RewardedOffer(string placement, bool accepted) =>
            KovusPride.Web.Analytics.Log("rewarded_offer", ("placement", placement), ("accepted", accepted));
    }
}
