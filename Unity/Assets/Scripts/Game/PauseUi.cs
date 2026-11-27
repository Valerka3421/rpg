// uGUI-порт render/ModalScreens.cs:PauseScreen (Esc в игре). Центрированная
// панель, 4 пункта (настройки в Unity-меню пока нет): Продолжить / Сохранить /
// Загрузить / В меню. Клавиатуру обрабатывает GameController (HandlePauseInput),
// здесь только мышь: наведение подсвечивает пункт, клик вызывает
// onChoice(index) — действия применяет GameApp (ApplyPauseChoice).
// Геометрия пунктов — как на desktop (400×56 при y = 200 + i*80).
using System;
using UnityEngine;
using UnityEngine.UI;
using UColor = UnityEngine.Color;
using XnaRect = Microsoft.Xna.Framework.Rectangle;

namespace Aethelgard
{
    public class PauseUi : MonoBehaviour
    {
        private static readonly string[] Items =
            { "Продолжить", "Сохранить", "Загрузить", "В меню" };

        private readonly XnaRect[] _rects = new XnaRect[Items.Length];
        private readonly Image[] _bgs = new Image[Items.Length];
        private readonly Text[] _labels = new Text[Items.Length];

        private UiKitCanvas _kit = null!;
        private Action<int> _onChoice;

        public static PauseUi Create(GameController game, Action<int> onChoice)
        {
            var go = new GameObject("PauseUi");
            var ui = go.AddComponent<PauseUi>();
            ui._onChoice = onChoice;
            ui.Build();
            return ui;
        }

        private void Build()
        {
            _kit = new UiKitCanvas("PauseCanvas");

            int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;
            var panel = new XnaRect((sw - 560) / 2, (sh - 520) / 2, 560, 520);
            _kit.Panel(panel);

            _kit.Centered(new XnaRect(panel.X, panel.Y + 18, panel.Width, 40),
                "ПАУЗА", 2, UiKitCanvas.Theme.GoldBright);
            _kit.Centered(new XnaRect(panel.X, panel.Y + 60, panel.Width, 20),
                "игра приостановлена", 4, UiKitCanvas.Theme.TextMuted);

            // Пункты — те же координаты, что на desktop (MainMenuScreen.DrawPause):
            // панель 560×520 центрирована, кнопки 400×56 при y = 200 + i*80.
            for (int i = 0; i < Items.Length; i++)
            {
                _rects[i] = new XnaRect((sw - 400) / 2, 200 + i * 80, 400, 56);
                _bgs[i] = _kit.Button(_rects[i], Items[i], 3);
                _labels[i] = _bgs[i].GetComponentInChildren<Text>();
            }

            _kit.Centered(
                new XnaRect(panel.X, panel.Y + panel.Height - 44, panel.Width, 24),
                "↑/↓ или мышь — выбор · Enter/клик — подтвердить · Esc — вернуться",
                4, UiKitCanvas.Theme.TextMuted);
        }

        // ── Обновление каждый кадр (вызывается извне) ──
        public void Refresh()
        {
            if (_kit == null) return;
            for (int i = 0; i < Items.Length; i++)
            {
                bool hot = UiInput.Hot(_rects[i]);
                _bgs[i].color = hot
                    ? UiKitCanvas.Theme.BgSlotHot : UiKitCanvas.Theme.BgSlot;
                _labels[i].color = hot
                    ? UiKitCanvas.Theme.GoldBright : UiKitCanvas.Theme.TextDim;

                if (hot && UiInput.Clicked(_rects[i]))
                {
                    _onChoice?.Invoke(i);
                    return; // выбор применён — кадр отрисован
                }
            }
        }

        public void SetVisible(bool on)
        {
            _kit.SetActive(on);
        }
    }
}
