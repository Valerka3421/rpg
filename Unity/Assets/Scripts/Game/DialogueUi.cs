// uGUI-порт render/DialogueScreen.cs: нижняя панель диалога — портретная
// зона с первой буквой имени NPC, имя золотом, перенесённый текст
// (game.DialogueText, перенос делает Unity Text), подсказки клавиш.
// Показывается только при открытом диалоге: SetVisible(false), если
// game.DialogueNpc == null. Refresh() обновляет имя/текст каждый кадр.
using UnityEngine;
using UnityEngine.UI;
using UColor = UnityEngine.Color;
using XnaRect = Microsoft.Xna.Framework.Rectangle;

namespace Aethelgard
{
    public class DialogueUi : MonoBehaviour
    {
        private GameController _game = null!;
        private UiKitCanvas _kit = null!;

        private Text _initial = null!, _name = null!, _body = null!;
        private Image _portraitBg = null!;

        public static DialogueUi Create(GameController game)
        {
            var go = new GameObject("DialogueUi");
            var ui = go.AddComponent<DialogueUi>();
            ui._game = game;
            ui.Build();
            return ui;
        }

        private void Build()
        {
            _kit = new UiKitCanvas("DialogueCanvas");

            int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;
            int boxW = sw - 160, boxH = 200;
            var panel = new XnaRect((sw - boxW) / 2, sh - boxH - 40, boxW, boxH);
            _kit.Panel(panel, dimWorldBehind: false);

            // ── Портретная зона слева: фон + рамка + золотая полоса сверху ──
            int portraitSize = boxH - 32;
            var portraitRect = new XnaRect(panel.X + 16, panel.Y + 16,
                portraitSize, portraitSize);
            _portraitBg = _kit.RectImage(portraitRect, UiKitCanvas.Theme.BgSlot);
            Frame(portraitRect, UiKitCanvas.Theme.Border);
            _kit.RectImage(new XnaRect(portraitRect.X, portraitRect.Y,
                portraitRect.Width, 2), UiKitCanvas.Theme.Gold);
            _initial = _kit.Centered(portraitRect, "", 2, UiKitCanvas.Theme.GoldBright);

            // ── Текстовая зона справа от портрета ──
            int textX = portraitRect.X + portraitRect.Width + 16;
            int textW = panel.X + panel.Width - 16 - textX;
            _name = _kit.Label(new XnaRect(textX, panel.Y + 18, textW, 26),
                "", 3, UiKitCanvas.Theme.GoldBright);
            _kit.RectImage(new XnaRect(textX, panel.Y + 48, textW, 1),
                UiKitCanvas.Theme.BorderSoft);
            // Текст диалога: перенос по ширине делает Unity Text (wrap)
            _body = _kit.Label(new XnaRect(textX, panel.Y + 58, textW - 8,
                panel.Y + panel.Height - 40 - (panel.Y + 58)),
                "", 3, UiKitCanvas.Theme.Text);

            // ── Подсказки клавиш внизу справа ──
            _kit.Label(new XnaRect(panel.X + panel.Width - 260,
                panel.Y + panel.Height - 30, 110, 18),
                "Enter — далее", 4, UiKitCanvas.Theme.TextMuted, TextAnchor.LowerRight);
            _kit.Label(new XnaRect(panel.X + panel.Width - 150,
                panel.Y + panel.Height - 30, 134, 18),
                "Esc — закрыть", 4, UiKitCanvas.Theme.TextMuted, TextAnchor.LowerRight);
        }

        /// <summary>Рамка из тонких полос (desktop Frame) поверх палитры UiKit.</summary>
        private void Frame(XnaRect r, UColor c)
        {
            _kit.RectImage(new XnaRect(r.X, r.Y, r.Width, 1), c);
            _kit.RectImage(new XnaRect(r.X, r.Y + r.Height - 1, r.Width, 1), c);
            _kit.RectImage(new XnaRect(r.X, r.Y, 1, r.Height), c);
            _kit.RectImage(new XnaRect(r.X + r.Width - 1, r.Y, 1, r.Height), c);
        }

        // ── Обновление каждый кадр (вызывается извне) ──
        public void Refresh()
        {
            if (_kit == null) return;
            if (_game.DialogueNpc == null || _game.DialogueText == null)
            {
                SetVisible(false);
                return;
            }
            SetVisible(true);

            _name.text = _game.DialogueNpc;
            string npc = _game.DialogueNpc;
            _initial.text = npc.Substring(0, 1).ToUpperInvariant();
            _body.text = _game.DialogueText;
        }

        public void SetVisible(bool on)
        {
            _kit.SetActive(on);
        }
    }
}
