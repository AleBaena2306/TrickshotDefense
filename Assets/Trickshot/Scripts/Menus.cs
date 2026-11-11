using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TrickshotDefense
{
    // Pantallas fuera de la partida: menú principal, niveles, equipo (tienda) y ajustes.
    // El estadio 3D queda detrás; las pantallas se montan de nuevo cada vez que se abren.
    public class Menus
    {
        private readonly Game game;
        private readonly RectTransform root;
        private RectTransform screen;
        private TextMeshProUGUI trophyText;
        private int shownTrophies = -1;
        private RectTransform trophyPill;

        public Menus(Game game, RectTransform canvas)
        {
            this.game = game;
            root = Ui.Stretch(Ui.Rect("Menus", canvas));
        }

        public void HideAll()
        {
            if (screen != null) Object.Destroy(screen.gameObject);
            screen = null;
            root.gameObject.SetActive(false);
        }

        private RectTransform NewScreen(string name, float dim = 0.35f)
        {
            if (screen != null) Object.Destroy(screen.gameObject);
            root.gameObject.SetActive(true);
            screen = Ui.Stretch(Ui.Rect(name, root));
            var bg = Ui.Flat("Dim", screen, new Color(0.04f, 0.07f, 0.14f, dim), true);
            Ui.Stretch(bg.rectTransform);
            TrophyPill();
            return screen;
        }

        private void TrophyPill()
        {
            var bg = Ui.Box("Trophies", screen, Ui.A(Ui.Navy, 0.9f));
            Ui.Place(bg.rectTransform, 1f, 1f, new Vector2(250, 90), new Vector2(-24, -24));
            var ic = Ui.Icon("trophy", bg.transform, 62, Ui.Gold);
            Ui.Place(ic.rectTransform, 0f, 0.5f, new Vector2(62, 62), new Vector2(14, 0));
            trophyText = Ui.LabelIn(bg.transform, game.Save.trophies.ToString(), 56, Ui.White, 1f, 0.5f, new Vector2(160, 76), new Vector2(-16, 2), true, TextAlignmentOptions.Right);
            trophyPill = bg.rectTransform;
            shownTrophies = game.Save.trophies;
        }

        public void Tick()
        {
            if (screen == null || trophyText == null) return;
            if (game.Save.trophies != shownTrophies)
            {
                shownTrophies = game.Save.trophies;
                trophyText.text = shownTrophies.ToString();
                Fx.Punch(trophyPill, 0.15f);
            }
        }

        private UiButton Back(RectTransform parent)
        {
            var b = Ui.Button(parent, null, Ui.PanelLight, () => game.Transition(ShowMain), "back", 56);
            Ui.Place(b.image.rectTransform, 0f, 1f, new Vector2(110, 96), new Vector2(24, -24));
            return b;
        }

        private TextMeshProUGUI Title(RectTransform parent, string text)
        {
            return Ui.LabelIn(parent, text, 96, Ui.White, 0.5f, 1f, new Vector2(1000, 120), new Vector2(0, -20), true);
        }

        // ---------------- Principal ----------------
        public void ShowMain()
        {
            var s = NewScreen("Main", 0.25f);
            var band = Ui.Box("Band", s, Ui.A(Ui.Navy, 0.9f));
            Ui.Place(band.rectTransform, 0f, 0.5f, new Vector2(760, 1100), new Vector2(-40, 0));
            band.rectTransform.localRotation = Quaternion.Euler(0, 0, -4f);

            var logo = Ui.Rect("Logo", s);
            Ui.Place(logo, 0f, 1f, new Vector2(700, 300), new Vector2(40, -60));
            Ui.LabelIn(logo, "TRICKSHOT", 150, Ui.Yellow, 0.5f, 1f, new Vector2(700, 160), new Vector2(0, 0), true);
            Ui.LabelIn(logo, "DEFENSE", 110, Ui.White, 0.5f, 1f, new Vector2(700, 120), new Vector2(0, -140), true);
            Fx.Pop(logo, 0.4f);

            float y = -400;
            var play = Ui.Button(s, "PLAY", Ui.Yellow, () => game.Transition(ShowLevels), "play", 72);
            Ui.Place(play.image.rectTransform, 0f, 1f, new Vector2(560, 150), new Vector2(110, y));
            Fx.Pop(play.transform, 0.3f, 0.1f);
            y -= 170;
            var arcade = Ui.Button(s, "ARCADE", Ui.Blue, () => game.StartArcade(), "robot", 56);
            Ui.Place(arcade.image.rectTransform, 0f, 1f, new Vector2(560, 120), new Vector2(110, y));
            if (game.Save.arcadeBest > 0)
                Ui.LabelIn(arcade.transform, "BEST " + game.Save.arcadeBest, 30, Ui.White, 1f, 1f, new Vector2(160, 40), new Vector2(-16, -10), true, TextAlignmentOptions.Right);
            bool arcadeOpen = game.Save.levelReached >= 3 || game.Save.arcadeBest > 0;
            if (!arcadeOpen)
            {
                arcade.Interactable = false;
                arcade.label.text = "ARCADE";
                Ui.LabelIn(arcade.transform, "CLEAR LEVEL 2", 26, Ui.White, 1f, 0f, new Vector2(220, 36), new Vector2(-16, 18), true, TextAlignmentOptions.Right);
            }
            Fx.Pop(arcade.transform, 0.3f, 0.16f);
            y -= 140;
            var team = Ui.Button(s, "TEAM", Ui.Green, () => game.Transition(ShowTeam), "shirt", 56);
            Ui.Place(team.image.rectTransform, 0f, 1f, new Vector2(400, 120), new Vector2(110, y));
            if (CanUnlockSomething()) Badge(team.image.rectTransform);
            Fx.Pop(team.transform, 0.3f, 0.22f);
            var settings = Ui.Button(s, null, Ui.PanelLight, () => game.Transition(ShowSettings), "gear", 56);
            Ui.Place(settings.image.rectTransform, 0f, 1f, new Vector2(140, 120), new Vector2(530, y));
            Fx.Pop(settings.transform, 0.3f, 0.26f);
        }

        private bool CanUnlockSomething()
        {
            var bal = game.Bal;
            for (int i = 0; i < bal.athletes.Length; i++)
                if (!game.Save.unlocked[i] && game.Save.trophies >= bal.athletes[i].unlockTrophies) return true;
            return false;
        }

        private static void Badge(RectTransform parent)
        {
            var dot = Ui.Box("Badge", parent, Ui.Red);
            Ui.Place(dot.rectTransform, 1f, 1f, new Vector2(40, 40), new Vector2(10, 10));
            Ui.LabelIn(dot.transform, "!", 32, Ui.White, 0.5f, 0.5f, new Vector2(40, 40), Vector2.zero, true);
        }

        // ---------------- Niveles ----------------
        public void ShowLevels()
        {
            var s = NewScreen("Levels", 0.55f);
            Back(s);
            Title(s, "LEVELS");
            int cols = 5;
            Vector2 size = new Vector2(250, 220);
            float gx = 30, gy = 30;
            float totalW = cols * size.x + (cols - 1) * gx;
            for (int i = 0; i < TrickshotBalance.LevelCount; i++)
            {
                int level = i + 1;
                int c = i % cols, r = i / cols;
                bool open = level <= game.Save.levelReached;
                bool boss = level % 5 == 0;
                Color col = !open ? Ui.Disabled : boss ? Ui.Red : Ui.Blue;
                var b = Ui.Button(s, open ? level.ToString() : null, col, () => game.StartLevel(level), open ? null : "lock", 80);
                Ui.Place(b.image.rectTransform, 0.5f, 0.5f, size,
                    new Vector2(-totalW * 0.5f + size.x * 0.5f + c * (size.x + gx), 250 - r * (size.y + gy)));
                if (!open) { b.Interactable = false; b.SetColor(Ui.Disabled); }
                else
                {
                    Stretch(b.label.rectTransform, 0, 60, 0, 0);
                    int stars = game.Save.stars[i];
                    for (int k = 0; k < 3; k++)
                    {
                        var st = Ui.Icon("star", b.transform, 44, k < stars ? Ui.Gold : Ui.A(Ui.Navy, 0.45f));
                        Ui.Place(st.rectTransform, 0.5f, 0f, new Vector2(44, 44), new Vector2((k - 1) * 50, 26));
                    }
                    if (boss) Ui.LabelIn(b.transform, "BOSS", 26, Ui.White, 0.5f, 1f, new Vector2(120, 34), new Vector2(0, -8), true);
                }
                Fx.Pop(b.transform, 0.25f, i * 0.02f);
            }
        }

        private static void Stretch(RectTransform rt, float l, float b, float r, float t) => Ui.Stretch(rt, l, b, r, t);

        // ---------------- Equipo (tienda) ----------------
        public void ShowTeam()
        {
            var s = NewScreen("Team", 0.6f);
            Back(s);
            Title(s, "TEAM");
            var bal = game.Bal;
            float w = 330, gap = 24;
            float total = bal.athletes.Length * w + (bal.athletes.Length - 1) * gap;
            for (int i = 0; i < bal.athletes.Length; i++)
            {
                var def = bal.athletes[i];
                var sport = def.sport;
                bool unlocked = game.Save.unlocked[i];
                var card = Ui.Box("Athlete_" + def.displayName, s, Ui.A(Ui.Navy, 0.94f));
                Ui.Place(card.rectTransform, 0.5f, 0.5f, new Vector2(w, 520), new Vector2(-total * 0.5f + w * 0.5f + i * (w + gap), 110));
                var stripe = Ui.Box("Stripe", card.transform, def.color);
                Ui.Place(stripe.rectTransform, 0.5f, 1f, new Vector2(w - 24, 12), new Vector2(0, -12));
                var pic = Ui.Picture("UI/Portraits/" + def.model, card.transform, 220);
                Ui.Place(pic.rectTransform, 0.5f, 1f, new Vector2(220, 220), new Vector2(0, -30));
                if (!unlocked) pic.color = new Color(0f, 0f, 0f, 0.6f);
                Ui.LabelIn(card.transform, def.displayName, 50, Ui.White, 0.5f, 1f, new Vector2(w - 30, 60), new Vector2(0, -250), true);
                Ui.LabelIn(card.transform, def.blurb, 30, Ui.Soft, 0.5f, 1f, new Vector2(w - 40, 110), new Vector2(0, -312), false, TextAlignmentOptions.Top, true);
                if (unlocked)
                {
                    Ui.LabelIn(card.transform, "IN THE TEAM", 34, Ui.Green, 0.5f, 0f, new Vector2(w - 30, 70), new Vector2(0, 24), true);
                }
                else
                {
                    int cost = def.unlockTrophies;
                    var b = Ui.Button(card.transform, cost.ToString(), Ui.Yellow, () =>
                    {
                        if (game.Unlock(sport)) { Sfx.Play("unlock"); game.Transition(ShowTeam); }
                    }, "trophy", 44);
                    Ui.Place(b.image.rectTransform, 0.5f, 0f, new Vector2(w - 60, 90), new Vector2(0, 20));
                    b.Interactable = game.Save.trophies >= cost;
                }
                Fx.Pop(card.transform, 0.25f, i * 0.04f);
            }

            // Equipaciones: el color de la prenda de todos los deportistas
            var kitsTitle = Ui.LabelIn(s, "KITS", 56, Ui.White, 0.5f, 0f, new Vector2(400, 70), new Vector2(0, 230), true);
            kitsTitle.alpha = 0.9f;
            float kw = 200, kg = 20;
            float kt = bal.kits.Length * kw + (bal.kits.Length - 1) * kg;
            for (int i = 0; i < bal.kits.Length; i++)
            {
                int k = i;
                var kit = bal.kits[i];
                bool owned = game.Save.kits[i];
                bool equipped = game.Save.kit == i;
                var b = Ui.Button(s, owned ? (equipped ? "ON" : "") : kit.price.ToString(), kit.color, () =>
                {
                    if (game.BuyOrEquipKit(k)) { Sfx.Play("buy"); game.Transition(ShowTeam); }
                }, owned ? (equipped ? "check" : null) : "trophy", 40);
                Ui.Place(b.image.rectTransform, 0.5f, 0f, new Vector2(kw, 120), new Vector2(-kt * 0.5f + kw * 0.5f + i * (kw + kg), 80));
                if (!owned && game.Save.trophies < kit.price) b.Interactable = false;
                if (!owned && game.Save.trophies < kit.price) b.SetColor(Ui.A(kit.color, 0.45f));
                if (equipped)
                {
                    var ring = Ui.Box("Equipped", b.transform, Ui.White);
                    ring.transform.SetAsFirstSibling();
                    Ui.Stretch(ring.rectTransform, -8, -8, -8, -8);
                }
            }
        }

        // ---------------- Ajustes ----------------
        public void ShowSettings()
        {
            var s = NewScreen("Settings", 0.6f);
            Back(s);
            Title(s, "SETTINGS");
            var p = Ui.Box("Panel", s, Ui.A(Ui.Navy, 0.94f));
            Ui.Place(p.rectTransform, 0.5f, 0.5f, new Vector2(900, 560), new Vector2(0, -20));
            Row(p.rectTransform, "MUSIC", "music", game.Save.music, on => game.SetMusic(on), -60);
            Row(p.rectTransform, "SOUNDS", "sound", game.Save.sfx, on => game.SetSfx(on), -190);
            Row(p.rectTransform, "VIBRATION", "vibrate", game.Save.vibration, on => game.SetVibration(on), -320);
            Ui.LabelIn(p.transform, "TRICKSHOT DEFENSE · KOVU'S PRIDE", 30, Ui.Soft, 0.5f, 0f, new Vector2(800, 50), new Vector2(0, 30), true);
        }

        private void Row(RectTransform parent, string label, string icon, bool on, System.Action<bool> set, float y)
        {
            var ic = Ui.Icon(icon, parent, 70, Ui.White);
            Ui.Place(ic.rectTransform, 0f, 1f, new Vector2(70, 70), new Vector2(70, y - 15));
            Ui.LabelIn(parent, label, 56, Ui.White, 0f, 1f, new Vector2(420, 100), new Vector2(170, y), true, TextAlignmentOptions.Left);
            UiButton b = null;
            bool state = on;
            b = Ui.Button(parent, on ? "ON" : "OFF", on ? Ui.Green : Ui.Disabled, () =>
            {
                state = !state;
                set(state);
                b.SetColor(state ? Ui.Green : Ui.Disabled);
                b.label.text = state ? "ON" : "OFF";
            }, null, 48);
            Ui.Place(b.image.rectTransform, 1f, 1f, new Vector2(220, 100), new Vector2(-70, y));
        }
    }
}
