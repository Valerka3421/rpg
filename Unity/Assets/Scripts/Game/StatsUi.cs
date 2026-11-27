// uGUI-порт render/ModalScreens.cs:StatsScreen (клавиша C): распределение
// свободных очков — строки характеристик с ховером (ховер ставит
// game.StatSelected, вложение очков делает GameController), полосы
// относительных значений, секции «Боевая готовность» / «Перки» /
// «Навыки ремесла». Визуал строится один раз в Build(), Refresh()
// обновляет тексты/цвета/бары и хит-тесты через UiInput.
using UnityEngine;
using UnityEngine.UI;
using UColor = UnityEngine.Color;
using XnaRect = Microsoft.Xna.Framework.Rectangle;

namespace Aethelgard
{
    public class StatsUi : MonoBehaviour
    {
        private GameController _game = null!;
        private UiKitCanvas _kit = null!;

        // Характеристики — как на desktop (StatsScreen.Stats)
        private static readonly (string Key, string Label)[] Stats =
        {
            ("str", "Сила (ATK)"), ("agi", "Ловкость (уклонение)"),
            ("vit", "Телосложение (HP)"), ("luk", "Удача (крит)"),
            ("df_stat", "Защита (DEF)"), ("int", "Интеллект (мана/магия)"),
        };

        private XnaRect _panel;
        private Text _header = null!, _points = null!;

        // Строки характеристик: bg, левый акцент, имя, бар, значение
        private readonly Image[] _statBgs = new Image[6];
        private readonly Image[] _statAccents = new Image[6];
        private readonly Text[] _statNames = new Text[6];
        private readonly Image[] _statFills = new Image[6];
        private readonly Text[] _statVals = new Text[6];
        private readonly XnaRect[] _statRects = new XnaRect[6];

        // Боевая готовность
        private Text _hpText = null!, _mpText = null!, _combatLine = null!;
        private Image _hpFill = null!, _mpFill = null!;

        // Перки
        private Text _perks = null!;

        // Навыки ремесла: 3 ветки в 2 колонки
        private Text[] _skillNames = null!, _skillXps = null!;
        private Image[] _skillFills = null!;

        private Text _footStats = null!;

        private bool _hoverAny;

        public static StatsUi Create(GameController game)
        {
            var go = new GameObject("StatsUi");
            var ui = go.AddComponent<StatsUi>();
            ui._game = game;
            ui.Build();
            return ui;
        }

        private void Build()
        {
            _kit = new UiKitCanvas("StatsCanvas");

            int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;
            _panel = new XnaRect((sw - 900) / 2, (sh - 800) / 2, 900, 800);
            _kit.Panel(_panel);

            // Шапка: класс/уровень + свободные очки
            _header = _kit.Label(new XnaRect(_panel.X + 24, _panel.Y + 16, 700, 32),
                "", 2, UiKitCanvas.Theme.GoldBright);
            _points = _kit.Label(new XnaRect(_panel.X + 24, _panel.Y + 50, 700, 20),
                "", 4, UiKitCanvas.Theme.TextDim);

            int y = _panel.Y + 78;
            const int rowH = 44;
            for (int i = 0; i < Stats.Length; i++)
            {
                var row = new XnaRect(_panel.X + 16, y, _panel.Width - 32, rowH);
                _statRects[i] = row;
                _statBgs[i] = _kit.RectImage(row, UiKitCanvas.Theme.BgSlot);
                _statAccents[i] = _kit.RectImage(
                    new XnaRect(row.X, row.Y, 3, row.Height), UiKitCanvas.Theme.Gold);
                _statNames[i] = _kit.Label(
                    new XnaRect(row.X + 14, row.Y + 4, row.Width - 280, 24),
                    $"{i + 1}. {Stats[i].Label}", 3, UiKitCanvas.Theme.Text);
                // Полоса относительного значения + число справа
                var bar = _kit.Bar(new XnaRect(row.X + row.Width - 220, row.Y + 10,
                    120, 14), UiKitCanvas.Theme.Gold);
                _statFills[i] = bar.fill;
                _statVals[i] = _kit.Label(
                    new XnaRect(row.X + row.Width - 90, row.Y + 6, 76, 22),
                    "", 3, UiKitCanvas.Theme.Text, TextAnchor.UpperRight);
                y += rowH + 4;
            }

            // ── Боевая готовность ──
            y += 10;
            var combatRect = new XnaRect(_panel.X + 16, y, _panel.Width - 32, 108);
            _kit.Section(combatRect, "Боевая готовность");
            int cx = combatRect.X + 14, cw = combatRect.Width - 28;
            int cy = combatRect.Y + 30;
            _hpText = _kit.Label(new XnaRect(cx, cy, 150, 18), "", 4, UiKitCanvas.Theme.Hp);
            _hpFill = _kit.Bar(new XnaRect(cx + 150, cy + 2, cw - 150, 14),
                UiKitCanvas.Theme.Hp).fill;
            cy += 26;
            _mpText = _kit.Label(new XnaRect(cx, cy, 150, 18), "", 4, UiKitCanvas.Theme.Mana);
            _mpFill = _kit.Bar(new XnaRect(cx + 150, cy + 2, cw - 150, 14),
                UiKitCanvas.Theme.Mana).fill;
            cy += 28;
            _combatLine = _kit.Label(new XnaRect(cx, cy, cw, 18), "", 4,
                UiKitCanvas.Theme.Text);

            // ── Перки ──
            y = combatRect.Y + combatRect.Height + UiKitCanvas.Gap;
            var perkRect = new XnaRect(_panel.X + 16, y, _panel.Width - 32, 66);
            _kit.Section(perkRect, "Перки");
            _perks = _kit.Label(new XnaRect(perkRect.X + 14, perkRect.Y + 28,
                perkRect.Width - 28, 20), "", 4, UiKitCanvas.Theme.TextMuted);

            // ── Навыки ремесла ──
            y = perkRect.Y + perkRect.Height + UiKitCanvas.Gap;
            int skillRows = (RecipeData.SkillOrder.Length + 1) / 2;
            var skillRect = new XnaRect(_panel.X + 16, y, _panel.Width - 32,
                26 + skillRows * 30 + 8);
            _kit.Section(skillRect, "Навыки ремесла");
            int col0 = skillRect.X + 14;
            int col1 = skillRect.X + skillRect.Width / 2 + 8;
            int halfW = skillRect.Width / 2 - 30;
            _skillNames = new Text[RecipeData.SkillOrder.Length];
            _skillXps = new Text[RecipeData.SkillOrder.Length];
            _skillFills = new Image[RecipeData.SkillOrder.Length];
            for (int i = 0; i < RecipeData.SkillOrder.Length; i++)
            {
                int sx = i % 2 == 0 ? col0 : col1;
                int sy = skillRect.Y + 28 + (i / 2) * 30;
                _skillNames[i] = _kit.Label(new XnaRect(sx, sy, 150, 18), "", 4,
                    UiKitCanvas.Theme.Text);
                _skillFills[i] = _kit.Bar(new XnaRect(sx + 150, sy + 2,
                    halfW - 150, 12), UiKitCanvas.Theme.Xp).fill;
                _skillXps[i] = _kit.Label(new XnaRect(sx + halfW - 70, sy, 70, 18),
                    "", 4, UiKitCanvas.Theme.TextDim, TextAnchor.UpperRight);
            }

            // ── Подвал ──
            y = skillRect.Y + skillRect.Height + 6;
            _footStats = _kit.Label(new XnaRect(_panel.X + 24, y, 700, 18), "", 4,
                UiKitCanvas.Theme.TextDim);
            _kit.Label(new XnaRect(_panel.X + 24, _panel.Y + _panel.Height - 30, 700, 18),
                "Enter/1–6: вложить очко   C/Esc: выход", 4, UiKitCanvas.Theme.TextMuted);
        }

        // ── Обновление каждый кадр (вызывается извне) ──
        public void Refresh()
        {
            if (_kit == null) return;
            _hoverAny = false;
            var p = _game.Player;

            _header.text = $"{p.ClassName}  ·  Ур. {p.Level}";
            _points.text = $"Свободных очков: {p.FreePoints}    Очков навыков: {p.SkillPoints}";
            _points.color = p.FreePoints > 0 ? UiKitCanvas.Theme.Xp : UiKitCanvas.Theme.TextDim;

            // ── Характеристики: ховер выбирает строку (вложение — GameController) ──
            int[] values = { p.TotalStr, p.TotalAgi, p.TotalVit, p.TotalLuk,
                p.TotalDfStat, p.TotalInt };
            int maxVal = Math.Max(1, values.Max());
            for (int i = 0; i < Stats.Length; i++)
            {
                if (UiInput.Hot(_statRects[i])) _game.StatSelected = i;
                bool selected = _game.StatSelected == i;

                _statBgs[i].color = selected
                    ? UiKitCanvas.Theme.BgSlotSel : UiKitCanvas.Theme.BgSlot;
                _statAccents[i].color = selected
                    ? UiKitCanvas.Theme.Gold : UiKitCanvas.Alpha(UiKitCanvas.Theme.Gold, 60);
                _statNames[i].color = selected
                    ? UiKitCanvas.Theme.GoldBright : UiKitCanvas.Theme.Text;
                UiKitCanvas.SetBar(_statFills[i], (double)values[i] / maxVal);
                _statVals[i].text = values[i].ToString();
                _statVals[i].color = selected
                    ? UiKitCanvas.Theme.GoldBright : UiKitCanvas.Theme.Text;
            }

            // ── Боевая готовность ──
            _hpText.text = $"HP {p.Hp}/{p.MaxHp}";
            UiKitCanvas.SetBar(_hpFill, (double)p.Hp / Math.Max(1, p.MaxHp));
            _mpText.text = $"MP {p.Mana}/{p.MaxMana}";
            UiKitCanvas.SetBar(_mpFill, (double)p.Mana / Math.Max(1, p.MaxMana));
            _combatLine.text = $"ATK {p.Atk}   DEF {p.Df}   " +
                $"Уклонение {p.DodgeChance * 100:F0}%   Крит {p.CritChance * 100:F0}%";

            // ── Перки ──
            var allPerks = new (string Name, bool On)[]
            {
                ("Двойной удар", p.DoubleStrike), ("Регенерация", p.Regen),
                ("Вампиризм", p.Vampire), ("Шипы", p.Thorns), ("Жадность", p.Greedy),
            };
            var perks = allPerks.Where(x => x.On).Select(x => x.Name).ToArray();
            bool any = perks.Length > 0;
            _perks.text = any ? string.Join(" · ", perks) : "нет";
            _perks.color = any ? UiKitCanvas.Theme.Xp : UiKitCanvas.Theme.TextMuted;

            // ── Навыки ремесла ──
            for (int i = 0; i < RecipeData.SkillOrder.Length; i++)
            {
                string branch = RecipeData.SkillOrder[i];
                var skill = RecipeData.Skills.GetValueOrDefault(branch);
                int lvl = p.CraftLevels.GetValueOrDefault(branch, 1);
                int xp = p.CraftXp.GetValueOrDefault(branch);
                int next = RecipeData.SkillXpToNext(lvl);
                _skillNames[i].text = $"{skill?.Name ?? branch} ур. {lvl}";
                UiKitCanvas.SetBar(_skillFills[i], next > 0 ? (double)xp / next : 0);
                _skillXps[i].text = $"{xp}/{next}";
            }

            _footStats.text = $"Убийств: {_game.KillsTotal}   " +
                $"Крафт: {_game.ItemsCrafted}   Квестов: {_game.QuestsDone}";

            if (!_hoverAny) _kit.HideTooltip();
        }

        public void SetVisible(bool on)
        {
            _kit.SetActive(on);
            if (!on) _kit.HideTooltip();
        }
    }
}
