using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrickshotDefense
{
    // Interfaz de la partida: marcador, botón de oleada, barra de construcción y mejora, pausa, resultados y tutorial
    public class Hud
    {
        private readonly Game game;
        private readonly RectTransform root;
        private Battle battle;

        private TextMeshProUGUI hpText, coinText, waveText, nextText, startLabel;
        private RectTransform hpPill, coinPill;
        private UiButton speedBtn, pauseBtn, startBtn;
        private RectTransform buildBar;
        private RectTransform overlay;     // pausa y resultados
        private int shownCoins = -1, shownHp = -1;
        private int barSlot = -2;
        private bool barDirty;

        // Tutorial
        private int tutStep = -1;
        private RectTransform tutCard, hand;
        private CanvasGroup tutGroup;
        private TextMeshProUGUI tutText;

        public Hud(Game game, RectTransform canvas)
        {
            this.game = game;
            root = Ui.Stretch(Ui.Rect("Hud", canvas));
            root.gameObject.SetActive(false);
            BuildTop();
            BuildStart();
            buildBar = Ui.Rect("BuildBar", root);
            Ui.Place(buildBar, 0.5f, 0f, new Vector2(1500, 250), new Vector2(0, 24));
        }

        // ---------------- Montaje ----------------
        private RectTransform Pill(string name, string icon, Color iconColor, out TextMeshProUGUI text, float x)
        {
            var bg = Ui.Box(name, root, Ui.A(Ui.Navy, 0.88f));
            Ui.Place(bg.rectTransform, 0f, 1f, new Vector2(220, 84), new Vector2(x, -24));
            var ic = Ui.Icon(icon, bg.transform, 58, iconColor);
            Ui.Place(ic.rectTransform, 0f, 0.5f, new Vector2(58, 58), new Vector2(14, 0));
            text = Ui.LabelIn(bg.transform, "0", 54, Ui.White, 1f, 0.5f, new Vector2(136, 70), new Vector2(-14, 2), true, TextAlignmentOptions.Right);
            return bg.rectTransform;
        }

        private void BuildTop()
        {
            hpPill = Pill("Hp", "heart", Ui.Red, out hpText, 24);
            coinPill = Pill("Coins", "coin", Ui.Gold, out coinText, 260);

            var wave = Ui.Box("Wave", root, Ui.A(Ui.Navy, 0.88f));
            Ui.Place(wave.rectTransform, 0.5f, 1f, new Vector2(420, 84), new Vector2(0, -24));
            waveText = Ui.LabelIn(wave.transform, "WAVE 1/5", 54, Ui.White, 0.5f, 0.5f, new Vector2(390, 70), new Vector2(0, 2), true);
            nextText = Ui.LabelIn(root, "", 38, Ui.Yellow, 0.5f, 1f, new Vector2(600, 50), new Vector2(0, -118), true);

            pauseBtn = Ui.Button(root, null, Ui.PanelLight, () => OpenPause(), "pause", 56);
            Ui.Place(pauseBtn.image.rectTransform, 1f, 1f, new Vector2(96, 96), new Vector2(-24, -20));
            speedBtn = Ui.Button(root, "x1", Ui.PanelLight, () => battle?.ToggleSpeed(), "fast", 44);
            Ui.Place(speedBtn.image.rectTransform, 1f, 1f, new Vector2(170, 96), new Vector2(-136, -20));
        }

        private void BuildStart()
        {
            startBtn = Ui.Button(root, "START", Ui.Yellow, () => battle?.StartNextWave(true), "play", 60);
            Ui.Place(startBtn.image.rectTransform, 1f, 0f, new Vector2(380, 120), new Vector2(-30, 30));
            startLabel = startBtn.label;
        }

        // ---------------- Mostrar / ocultar ----------------
        public void Show(Battle b)
        {
            if (battle != null) Unhook();
            battle = b;
            battle.Changed += OnChanged;
            battle.Killed += OnKilled;
            battle.BaseHit += OnBaseHit;
            battle.Built += OnBuilt;
            root.gameObject.SetActive(true);
            CloseOverlay();
            EndTutorial(false);
            shownCoins = shownHp = -1;
            barSlot = -2;
            OnChanged();
        }

        public void Hide()
        {
            if (battle != null) Unhook();
            battle = null;
            CloseOverlay();
            EndTutorial(false);
            root.gameObject.SetActive(false);
        }

        private void Unhook()
        {
            battle.Changed -= OnChanged;
            battle.Killed -= OnKilled;
            battle.BaseHit -= OnBaseHit;
            battle.Built -= OnBuilt;
        }

        // ---------------- Refresco ----------------
        private void OnChanged()
        {
            if (battle == null) return;
            if (battle.Coins != shownCoins)
            {
                if (shownCoins >= 0) Fx.Punch(coinPill, battle.Coins > shownCoins ? 0.08f : 0.12f);
                shownCoins = battle.Coins;
                coinText.text = shownCoins.ToString();
                barDirty = true;
            }
            if (battle.BaseHp != shownHp)
            {
                if (shownHp >= 0) Fx.Punch(hpPill, 0.18f);
                shownHp = battle.BaseHp;
                hpText.text = shownHp.ToString();
                hpText.color = shownHp * 4 <= battle.BaseHpMax ? Ui.Red : Ui.White;
            }
            waveText.text = battle.Mode == Mode.Levels
                ? $"WAVE {Mathf.Max(1, battle.Wave)}/{battle.WaveCount}"
                : $"WAVE {Mathf.Max(1, battle.Wave)}";
            speedBtn.label.text = battle.SpeedUp ? "x2" : "x1";
            speedBtn.SetColor(battle.SpeedUp ? Ui.Blue : Ui.PanelLight);
            if (battle.SelectedSlot != barSlot) barDirty = true;
        }

        public void Tick()
        {
            if (battle == null || !root.gameObject.activeSelf) return;
            bool showStart = !battle.Over && (battle.WaitingForStart || battle.NextWaveIn > 0f);
            Ui.SetActive(startBtn, showStart && tutStep != 0 && tutStep != 1);
            if (showStart)
            {
                startLabel.text = battle.WaitingForStart ? "START" : "NEXT";
                nextText.text = battle.WaitingForStart ? (battle.Mode == Mode.Arcade ? "PLACE YOUR TEAM" : "") : $"NEXT WAVE IN {Mathf.CeilToInt(battle.NextWaveIn)}";
            }
            else nextText.text = "";
            if (barDirty) { barDirty = false; RebuildBar(); }
            if (!battle.Over && overlay == null && (Battle.KeyDown('p') || (Battle.KeyDown('e') && KovusPride.Publishing.StorePlatform.AllowsEscapeKey))) OpenPause();
            TickTutorial();
        }

        private void OnKilled(Enemy e)
        {
            if (tutStep == 2) TutorialNext();
        }

        private void OnBaseHit() { }

        private void OnBuilt(int slot)
        {
            if (tutStep == 1) TutorialNext();
        }

        // ---------------- Barra inferior ----------------
        private void RebuildBar()
        {
            for (int i = buildBar.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(buildBar.GetChild(i).gameObject);
            barSlot = battle.SelectedSlot;
            if (battle.Over || barSlot < 0) return;
            if (!battle.SlotOpen[barSlot]) BarLocked(barSlot);
            else if (battle.Athletes[barSlot] == null) BarBuild(barSlot);
            else BarUpgrade(barSlot);
        }

        private void BarLocked(int slot)
        {
            var card = Ui.Box("Locked", buildBar, Ui.A(Ui.Navy, 0.94f), true);
            Ui.Place(card.rectTransform, 0.5f, 0f, new Vector2(620, 190), Vector2.zero);
            Ui.LabelIn(card.transform, "NEW PODIUM", 50, Ui.White, 0.5f, 1f, new Vector2(560, 60), new Vector2(0, -16), true);
            int price = battle.SlotPrice(slot);
            var b = Ui.Button(card.transform, price.ToString(), Ui.Yellow, () => { if (battle.BuySlot(slot)) battle.Select(slot); }, "coin", 48);
            Ui.Place(b.image.rectTransform, 0.5f, 0f, new Vector2(300, 96), new Vector2(0, 18));
            b.Interactable = battle.Coins >= price;
            Fx.Pop(card.transform, 0.22f);
        }

        private void BarBuild(int slot)
        {
            var bal = game.Bal;
            float w = 270f, gap = 14f;
            float total = bal.athletes.Length * w + (bal.athletes.Length - 1) * gap;
            for (int i = 0; i < bal.athletes.Length; i++)
            {
                var def = bal.athletes[i];
                bool unlocked = game.Save.unlocked[i];
                bool afford = battle.Coins >= def.cost;
                var sport = def.sport;
                var card = Ui.Box("Card_" + def.displayName, buildBar, Ui.A(Ui.Navy, 0.94f), true);
                Ui.Place(card.rectTransform, 0.5f, 0f, new Vector2(w, 230), new Vector2(-total * 0.5f + w * 0.5f + i * (w + gap), 0));
                var stripe = Ui.Box("Stripe", card.transform, unlocked ? def.color : Ui.Disabled);
                Ui.Place(stripe.rectTransform, 0.5f, 1f, new Vector2(w - 20, 10), new Vector2(0, -10));
                var pic = Ui.Picture("UI/Portraits/" + def.model, card.transform, 130);
                Ui.Place(pic.rectTransform, 0.5f, 1f, new Vector2(130, 130), new Vector2(0, -22));
                if (!unlocked) pic.color = new Color(0f, 0f, 0f, 0.55f);
                Ui.LabelIn(card.transform, def.displayName, 36, unlocked ? Ui.White : Ui.Soft, 0.5f, 0f, new Vector2(w - 20, 40), new Vector2(0, 62), true);
                if (unlocked)
                {
                    // Moneda a la izquierda del centro y precio a su derecha (sin solaparse)
                    var ic = Ui.Icon("coin", card.transform, 38, Ui.Gold);
                    Ui.PlaceP(ic.rectTransform, 0.5f, 0f, 1f, 0f, new Vector2(38, 38), new Vector2(-8, 18));
                    var price = Ui.Label(card.transform, def.cost.ToString(), 40, afford ? Ui.Gold : Ui.Red, true, TextAlignmentOptions.Left);
                    Ui.PlaceP(price.rectTransform, 0.5f, 0f, 0f, 0f, new Vector2(120, 46), new Vector2(4, 12));
                    var key = Ui.LabelIn(card.transform, (i + 1).ToString(), 28, Ui.Soft, 1f, 1f, new Vector2(40, 40), new Vector2(-10, -16), true);
                    key.alpha = 0.7f;
                    var btn = card.gameObject.AddComponent<UiButton>();
                    btn.image = card;
                    btn.baseColor = card.color;
                    btn.onClick = () => { if (battle.Build(slot, sport)) battle.Select(-1); };
                    if (!afford) btn.Interactable = false;
                    if (!afford) card.color = Ui.A(Ui.Navy, 0.75f);
                }
                else
                {
                    var lockIc = Ui.Icon("lock", card.transform, 38, Ui.Soft);
                    Ui.PlaceP(lockIc.rectTransform, 0.5f, 0f, 1f, 0f, new Vector2(38, 38), new Vector2(-8, 18));
                    var need = Ui.Label(card.transform, def.unlockTrophies.ToString(), 36, Ui.Soft, true, TextAlignmentOptions.Left);
                    Ui.PlaceP(need.rectTransform, 0.5f, 0f, 0f, 0f, new Vector2(120, 46), new Vector2(4, 12));
                }
                Fx.Pop(card.transform, 0.22f, i * 0.03f);
            }
        }

        private void BarUpgrade(int slot)
        {
            var a = battle.Athletes[slot];
            var def = a.Def;
            var card = Ui.Box("Upgrade", buildBar, Ui.A(Ui.Navy, 0.94f), true);
            Ui.Place(card.rectTransform, 0.5f, 0f, new Vector2(1060, 220), Vector2.zero);
            var stripe = Ui.Box("Stripe", card.transform, def.color);
            Ui.Place(stripe.rectTransform, 0f, 0.5f, new Vector2(12, 190), new Vector2(14, 0));
            var pic = Ui.Picture("UI/Portraits/" + def.model, card.transform, 170);
            Ui.Place(pic.rectTransform, 0f, 0.5f, new Vector2(170, 170), new Vector2(34, 0));
            Ui.LabelIn(card.transform, def.displayName, 52, Ui.White, 0f, 1f, new Vector2(330, 60), new Vector2(216, -18), true, TextAlignmentOptions.Left);
            // Nivel: tres estrellas
            for (int i = 0; i < 3; i++)
            {
                var st = Ui.Icon("star", card.transform, 40, i <= a.Tier ? Ui.Gold : Ui.Disabled);
                Ui.Place(st.rectTransform, 0f, 1f, new Vector2(40, 40), new Vector2(218 + i * 44, -84));
            }
            string info = a.Tier >= 2 ? def.tier3Name : (a.Tier == 1 ? "NEXT: " + def.tier3Name : def.blurb);
            int team = battle.TeamCount(def.sport);
            if (team >= 2) info += $"\n<color=#FFC93C>TEAM BONUS: FASTER</color>";
            Ui.LabelIn(card.transform, info, 30, Ui.Soft, 0f, 0f, new Vector2(390, 90), new Vector2(216, 18), false, TextAlignmentOptions.TopLeft, true);

            if (a.Tier < 2)
            {
                int cost = a.UpgradeCost;
                var up = Ui.Button(card.transform, "UPGRADE  " + cost, Ui.Green, () => battle.Upgrade(slot), "up", 42);
                Ui.Place(up.image.rectTransform, 1f, 1f, new Vector2(400, 100), new Vector2(-24, -18));
                up.Interactable = battle.Coins >= cost;
            }
            else
            {
                Ui.LabelIn(card.transform, "MAX LEVEL", 44, Ui.Gold, 1f, 1f, new Vector2(400, 100), new Vector2(-24, -18), true);
            }
            var sell = Ui.Button(card.transform, "SELL  +" + a.SellValue, Ui.Red, () => battle.Sell(slot), "coin", 36);
            Ui.Place(sell.image.rectTransform, 1f, 0f, new Vector2(400, 84), new Vector2(-24, 18));
            Fx.Pop(card.transform, 0.22f);
        }

        // ---------------- Superposiciones ----------------
        private RectTransform OpenOverlay(float dim = 0.72f)
        {
            CloseOverlay();
            overlay = Ui.Stretch(Ui.Rect("Overlay", root));
            var bg = Ui.Flat("Dim", overlay, new Color(0.03f, 0.05f, 0.1f, dim), true);
            Ui.Stretch(bg.rectTransform);
            return overlay;
        }

        private void CloseOverlay()
        {
            if (overlay != null) UnityEngine.Object.Destroy(overlay.gameObject);
            overlay = null;
        }

        private RectTransform Panel(RectTransform parent, Vector2 size)
        {
            var p = Ui.Box("Panel", parent, Ui.Panel, true);
            Ui.Place(p.rectTransform, 0.5f, 0.5f, size, Vector2.zero);
            var top = Ui.Box("Top", p.transform, Ui.Yellow);
            Ui.Place(top.rectTransform, 0.5f, 1f, new Vector2(size.x - 40, 12), new Vector2(0, -14));
            Fx.Pop(p.transform, 0.3f);
            return p.rectTransform;
        }

        public void OpenPause()
        {
            if (battle == null || battle.Over || overlay != null) return;
            battle.SetPaused(true);
            var o = OpenOverlay();
            var p = Panel(o, new Vector2(760, 640));
            Ui.LabelIn(p, "PAUSED", 96, Ui.White, 0.5f, 1f, new Vector2(600, 110), new Vector2(0, -40), true);
            var resume = Ui.Button(p, "RESUME", Ui.Yellow, ClosePause, "play", 56);
            Ui.Place(resume.image.rectTransform, 0.5f, 1f, new Vector2(520, 120), new Vector2(0, -170));
            var restart = Ui.Button(p, "RESTART", Ui.PanelLight, () => { CloseOverlay(); game.Restart(); }, "back", 46);
            Ui.Place(restart.image.rectTransform, 0.5f, 1f, new Vector2(520, 100), new Vector2(0, -310));
            var menu = Ui.Button(p, "MENU", Ui.PanelLight, () => { CloseOverlay(); game.Quit(); }, "home", 46);
            Ui.Place(menu.image.rectTransform, 0.5f, 1f, new Vector2(520, 100), new Vector2(0, -430));
            Toggle(p, "music", game.Save.music, on => game.SetMusic(on), new Vector2(-70, 40));
            Toggle(p, "sound", game.Save.sfx, on => game.SetSfx(on), new Vector2(70, 40));
            KovusPride.Publishing.StorePlatform.GameplayStop();
        }

        private void ClosePause()
        {
            CloseOverlay();
            if (battle != null) { battle.SetPaused(false); KovusPride.Publishing.StorePlatform.GameplayStart(); }
        }

        public static UiButton Toggle(RectTransform parent, string icon, bool on, Action<bool> set, Vector2 pos)
        {
            UiButton b = null;
            bool state = on;
            b = Ui.Button(parent, null, on ? Ui.Blue : Ui.Disabled, () =>
            {
                state = !state;
                set(state);
                b.SetColor(state ? Ui.Blue : Ui.Disabled);
            }, icon, 52);
            Ui.Place(b.image.rectTransform, 0.5f, 0f, new Vector2(110, 100), pos);
            return b;
        }

        public void ShowResults(bool won, int trophies)
        {
            if (battle == null) return;
            EndTutorial(true);
            var o = OpenOverlay(0.6f);
            var p = Panel(o, new Vector2(900, 700));
            bool arcade = battle.Mode == Mode.Arcade;
            string title = arcade ? "GAME OVER" : won ? "VICTORY!" : "DEFEATED";
            Ui.LabelIn(p, title, 110, won ? Ui.Yellow : Ui.White, 0.5f, 1f, new Vector2(820, 130), new Vector2(0, -36), true);

            if (arcade)
            {
                Ui.LabelIn(p, $"WAVE {battle.Wave}", 80, Ui.White, 0.5f, 1f, new Vector2(700, 90), new Vector2(0, -170), true);
                Ui.LabelIn(p, game.LastNewRecord ? "NEW RECORD!" : $"BEST: WAVE {game.Save.arcadeBest}", 42, game.LastNewRecord ? Ui.Gold : Ui.Soft,
                    0.5f, 1f, new Vector2(700, 50), new Vector2(0, -258), true);
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    var st = Ui.Icon("star", p, 120, i < battle.Stars ? Ui.Gold : Ui.Disabled);
                    Ui.Place(st.rectTransform, 0.5f, 1f, new Vector2(120, 120), new Vector2((i - 1) * 140, i == 1 ? -170 : -190));
                    if (i < battle.Stars) Fx.Pop(st.transform, 0.35f, 0.25f + i * 0.2f);
                }
                if (!won) Ui.LabelIn(p, $"WAVE {battle.Wave}/{battle.WaveCount}", 46, Ui.Soft, 0.5f, 1f, new Vector2(700, 60), new Vector2(0, -320), true);
            }

            // Trofeos ganados (con opción de duplicarlos viendo un anuncio)
            var trophyRow = Ui.Rect("Trophies", p);
            Ui.Place(trophyRow, 0.5f, 1f, new Vector2(500, 90), new Vector2(0, -360));
            var tIc = Ui.Icon("trophy", trophyRow, 72, Ui.Gold);
            Ui.Place(tIc.rectTransform, 0f, 0.5f, new Vector2(72, 72), new Vector2(90, 0));
            var tText = Ui.LabelIn(trophyRow, "+" + trophies, 72, Ui.Gold, 0f, 0.5f, new Vector2(280, 90), new Vector2(180, 2), true, TextAlignmentOptions.Left);

            float y = -470;
            if (trophies > 0 && Game.RewardedReady)
            {
                UiButton dbl = null;
                dbl = Ui.Button(p, "x2 TROPHIES", Ui.Green, () =>
                {
                    dbl.Interactable = false;
                    game.DoubleTrophiesWithAd(ok =>
                    {
                        Analytics.RewardedOffer("double_trophies", ok);
                        if (ok) { tText.text = "+" + game.LastTrophies; Fx.Punch(trophyRow, 0.25f, 0.35f); UnityEngine.Object.Destroy(dbl.gameObject); }
                        else dbl.Interactable = true;
                    });
                }, "video", 44);
                Ui.Place(dbl.image.rectTransform, 0.5f, 1f, new Vector2(420, 96), new Vector2(0, y));
                y -= 110;
            }
            if (arcade && !won && game.CanRevive)
            {
                UiButton rev = null;
                rev = Ui.Button(p, "CONTINUE", Ui.Green, () =>
                {
                    rev.Interactable = false;
                    game.ReviveWithAd(ok =>
                    {
                        Analytics.RewardedOffer("arcade_revive", ok);
                        if (ok) { CloseOverlay(); OnChanged(); }
                        else rev.Interactable = true;
                    });
                }, "video", 44);
                Ui.Place(rev.image.rectTransform, 0.5f, 1f, new Vector2(420, 96), new Vector2(0, y));
                y -= 110;
            }

            var menu = Ui.Button(p, null, Ui.PanelLight, () => { CloseOverlay(); game.AfterResults(); }, "home", 56);
            Ui.Place(menu.image.rectTransform, 0.5f, 0f, new Vector2(120, 110), new Vector2(-230, 40));
            bool hasNext = !arcade && won && battle.Level < TrickshotBalance.LevelCount;
            var main = Ui.Button(p, hasNext ? "NEXT" : "RETRY", Ui.Yellow, () =>
            {
                CloseOverlay();
                if (hasNext) game.NextLevel(); else game.Restart();
            }, hasNext ? "play" : "back", 56);
            Ui.Place(main.image.rectTransform, 0.5f, 0f, new Vector2(400, 110), new Vector2(60, 40));
            Sfx.Play(won ? "victory" : "defeat");
        }

        // ---------------- Tutorial (primer nivel, la primera vez) ----------------
        // Pasos: 0 intro · 1 colocar un deportista · 2 empezar la oleada y derribar el primer robot · 3 mejoras · fin
        public void StartTutorial()
        {
            tutStep = 0;
            Analytics.TutorialStep(0);
            ShowTip("ROBOTS ARE COMING FOR YOUR TROPHY!\n<size=70%>Build a team of athletes to stop them.</size>", true);
        }

        private void ShowTip(string text, bool tapToContinue)
        {
            if (tutCard == null)
            {
                tutCard = Ui.Stretch(Ui.Rect("Tutorial", root));
                tutGroup = tutCard.gameObject.AddComponent<CanvasGroup>();
                var dim = Ui.Flat("Black", tutCard, new Color(0f, 0f, 0f, 0.78f), true);
                Ui.Stretch(dim.rectTransform);
                var btn = dim.gameObject.AddComponent<UiButton>();
                btn.image = dim; btn.baseColor = dim.color; btn.silent = true;
                btn.onClick = () => { if (tapToContinue) TutorialNext(); };
                tutText = Ui.LabelIn(tutCard, "", 72, Ui.White, 0.5f, 0.5f, new Vector2(1400, 360), new Vector2(0, 40), true, TextAlignmentOptions.Center, true);
                Ui.LabelIn(tutCard, "TAP TO CONTINUE", 36, Ui.Yellow, 0.5f, 0.5f, new Vector2(800, 60), new Vector2(0, -220), true);
            }
            // El botón de fondo captura el estado de este paso
            var dimBtn = tutCard.GetChild(0).GetComponent<UiButton>();
            dimBtn.onClick = () => { if (tapToContinue) TutorialNext(); };
            tutCard.gameObject.SetActive(true);
            tutText.text = text;
            tutGroup.alpha = 0f;
            Fx.Fade(tutGroup, 1f, 0.35f);
            if (battle != null) { battle.InputLocked = true; battle.SetPaused(true); }
        }

        private void HideTip()
        {
            if (tutCard == null) return;
            var g = tutGroup;
            Fx.Fade(g, 0f, 0.25f, () => { if (g != null) g.gameObject.SetActive(false); });
            if (battle != null) { battle.InputLocked = false; battle.SetPaused(false); }
        }

        private void TutorialNext()
        {
            if (tutStep < 0 || battle == null) return;
            tutStep++;
            Analytics.TutorialStep(tutStep);
            switch (tutStep)
            {
                case 1:
                    HideTip();
                    // Señala el primer podio libre y lo deja seleccionado
                    battle.Select(battle.FirstFreeSlot());
                    ShowHand(true);
                    nextText.text = "";
                    break;
                case 2:
                    ShowHand(false);
                    Fx.After(0.4f, () => { if (tutStep == 2) Ui.SetActive(startBtn, true); });
                    break;
                case 3:
                    Fx.After(1.2f, () =>
                    {
                        if (tutStep != 3 || battle == null || battle.Over) return;
                        ShowTip("EVERY ROBOT DROPS COINS.\n<size=70%>Spend them on more athletes, or tap one to upgrade it.</size>", true);
                    });
                    break;
                case 4:
                    HideTip();
                    EndTutorial(true);
                    break;
            }
        }

        private void EndTutorial(bool completed)
        {
            if (tutStep >= 0 && completed) { Analytics.TutorialComplete(); game.Save.tutorialDone = true; game.Save.Save(); }
            tutStep = -1;
            ShowHand(false);
            if (tutCard != null) tutCard.gameObject.SetActive(false);
            if (battle != null) battle.InputLocked = false;
        }

        private void ShowHand(bool on)
        {
            if (on && hand == null)
            {
                var img = Ui.Icon("hand", root, 120, Ui.White);
                hand = img.rectTransform;
            }
            if (hand != null) hand.gameObject.SetActive(on);
        }

        private void TickTutorial()
        {
            if (hand == null || !hand.gameObject.activeSelf || battle == null) return;
            int slot = battle.SelectedSlot >= 0 ? battle.SelectedSlot : battle.FirstFreeSlot();
            if (slot < 0) return;
            // Señala la primera tarjeta de la barra si está abierta; si no, el podio
            Vector2 local;
            Vector2 screen = battle.SlotScreen(slot);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out local);
            if (buildBar.childCount > 0)
            {
                var first = (RectTransform)buildBar.GetChild(0);
                Vector3 w = first.TransformPoint(first.rect.center);
                local = root.InverseTransformPoint(w);
            }
            float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * 24f;
            hand.anchorMin = hand.anchorMax = new Vector2(0.5f, 0.5f);
            hand.anchoredPosition = local + new Vector2(40f, -60f - bob);
        }
    }
}
