// uGUI-порт render/InventoryScreen.cs: слева — снаряжение (силуэт +
// ячейки Голова/Тело/Руки/Ноги/Обувь, клик = снять), справа — сетка
// рюкзака 10×10 со страницами. Визуал строится один раз в Build(),
// каждый кадр Refresh() обновляет тексты/цвета и выполняет хит-тесты
// через UiInput по тем же прямоугольникам. Тултипы — UiKitCanvas.
// Геометрия хранится в XnaRect (для UiInput); примитивы UiKitCanvas
// принимают UnityEngine.Rect — конвертация в UR().
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UColor = UnityEngine.Color;
using Screens = Aethelgard.Systems.ScreenMode;
using XnaRect = Microsoft.Xna.Framework.Rectangle;

namespace Aethelgard
{
    public class InventoryUi : MonoBehaviour
    {
        public const int GridCols = 10;
        public const int GridRows = 10;

        private GameController _game = null!;
        private UiKitCanvas _kit = null!;

        // ── Шапка ──
        private Text _classLabel = null!, _goldLabel = null!, _countLabel = null!;
        private Image _closeBg = null!;

        // ── Снаряжение ──
        private Text _className = null!, _classHint = null!;
        private readonly (string Key, string Label, int Col, int Row)[] _equipSlots =
        {
            ("head", "Голова", 0, 0),
            ("armor", "Тело", 0, 1),
            ("weapon", "Руки", 1, 1),
            ("legs", "Ноги", 0, 2),
            ("boots", "Обувь", 0, 3),
        };
        private readonly XnaRect[] _equipRects = new XnaRect[5];
        private readonly Image[] _equipBgs = new Image[5];
        private readonly Text[] _equipGlyphs = new Text[5];
        private readonly Text[] _equipNames = new Text[5];

        // ── Рюкзак ──
        private readonly XnaRect[] _bagRects = new XnaRect[GridCols * GridRows];
        private readonly Image[] _bagBgs = new Image[GridCols * GridRows];
        private readonly Text[] _bagGlyphs = new Text[GridCols * GridRows];
        private readonly Text[] _bagCounts = new Text[GridCols * GridRows];
        private Text _emptyLabel = null!;
        private Image _prevPageBg = null!, _nextPageBg = null!;
        private Text _pageLabel = null!;
        private XnaRect _invRect, _bagGridRect, _closeRect;

        private int _prevWheel;         // для дельты колеса (UiInput.Wheel кумулятивен)
        private bool _hoverAny;         // был ли ховер в этом кадре (для тултипа)

        public static InventoryUi Create(GameController game)
        {
            var go = new GameObject("InventoryUi");
            var ui = go.AddComponent<InventoryUi>();
            ui._game = game;
            ui.Build();
            return ui;
        }

        // ── Хелперы геометрии ──
        private static int RX(XnaRect r) => r.X + r.Width;
        private static int BY(XnaRect r) => r.Y + r.Height;
        private static Rect UR(XnaRect r) => new(r.X, r.Y, r.Width, r.Height);

        // ── Построение визуала один раз ──
        private void Build()
        {
            _kit = new UiKitCanvas("InventoryCanvas");

            var panel = new XnaRect(60, 40,
                AethelgardGame.ScreenW - 120, AethelgardGame.ScreenH - 80);
            _closeRect = new XnaRect(RX(panel) - 44, panel.Y + 12, 32, 32);
            _kit.Panel(UR(panel));

            // Шапка
            _kit.Label(UR(new XnaRect(panel.X + 20, panel.Y + 14, 500, 36)),
                "ИНВЕНТАРЬ", 2, UiKitCanvas.Theme.GoldBright);
            _classLabel = _kit.Label(
                UR(new XnaRect(panel.X + 20, panel.Y + 48, 500, 20)),
                "Персонаж", 4, UiKitCanvas.Theme.TextDim);
            _goldLabel = _kit.Label(
                UR(new XnaRect(RX(panel) - 480, panel.Y + 16, 440, 24)), "", 3,
                UiKitCanvas.Theme.Gold, TextAnchor.UpperRight);
            _countLabel = _kit.Label(
                UR(new XnaRect(RX(panel) - 480, panel.Y + 44, 440, 18)), "", 4,
                UiKitCanvas.Theme.TextDim, TextAnchor.UpperRight);
            _closeBg = _kit.Button(UR(_closeRect), "×", 3);

            int contentY = panel.Y + 72 + UiKitCanvas.Gap;
            int contentH = BY(panel) - contentY - 40;

            // Две колонки, как на desktop
            int equipW = panel.Width * 34 / 100;
            int invW = panel.Width - UiKitCanvas.Pad * 2 - equipW - UiKitCanvas.Gap;
            int equipX = panel.X + UiKitCanvas.Pad;
            int invX = equipX + equipW + UiKitCanvas.Gap;
            var equipRect = new XnaRect(equipX, contentY, equipW, contentH);
            _invRect = new XnaRect(invX, contentY, invW, contentH);
            _kit.Section(UR(equipRect), "Снаряжение");
            _kit.Section(UR(_invRect), "Рюкзак");

            BuildEquipmentPanel(equipRect);
            BuildBagGrid();

            // Подсказки внизу панели
            _kit.Label(UR(new XnaRect(panel.X + UiKitCanvas.Pad, BY(panel) - 30,
                    panel.Width - UiKitCanvas.Pad * 2, 22)),
                "ЛКМ — использовать · Q/E — страницы · U/V/H/B/N — снять снаряжение · Esc — выход",
                4, UiKitCanvas.Theme.TextMuted);
        }

        private void BuildEquipmentPanel(XnaRect r)
        {
            var cls = ClassData.Data(_game.Player.ClassKey);

            // Силуэт персонажа: вместо спрайта — рамка с именем класса.
            int charSize = Math.Min(140, Math.Max(90, (int)(r.Height * 0.20)));
            charSize = Math.Min(charSize, r.Width - 40);
            int charX = r.X + (r.Width - charSize) / 2;
            int charY = r.Y + 32;
            var charBox = new XnaRect(charX - 8, charY - 8, charSize + 16, charSize + 16);
            _kit.Section(UR(charBox));
            _className = _kit.Centered(UR(charBox), cls.Name, 3, UiKitCanvas.Theme.TextDim);

            // Ячейки экипировки — та же геометрия, что в InventoryScreen
            int slotsY = charY + charSize + 28;
            int slotSize = Math.Max(36, Math.Min(52, (r.Width - 40) / 2 - 8));
            const int cellGap = 12;
            int rowStep = slotSize + 34;
            int groupW = 2 * slotSize + cellGap;
            int gx = r.X + (r.Width - groupW) / 2;

            for (int i = 0; i < _equipSlots.Length; i++)
            {
                var (slotKey, label, gcol, grow) = _equipSlots[i];
                int sx = gx + gcol * (slotSize + cellGap);
                int sy = slotsY + grow * rowStep;
                var rect = new XnaRect(sx, sy, slotSize, slotSize);
                _equipRects[i] = rect;

                _kit.Label(UR(new XnaRect(sx, sy - 16, slotSize + 60, 14)),
                    label, 4, UiKitCanvas.Theme.TextDim);
                _equipBgs[i] = _kit.Slot(UR(rect));
                _equipGlyphs[i] = _kit.Centered(UR(rect), "", 3, UiKitCanvas.Theme.TextDim);
                _equipNames[i] = _kit.Label(
                    UR(new XnaRect(sx - 12, sy + slotSize + 2, slotSize + 24, 14)),
                    "", 4, UiKitCanvas.Theme.TextDim, TextAnchor.UpperCenter, wrap: false);
            }

            // Подсказка класса — внизу панели
            string weapons = cls.Weapons.Length > 0
                ? string.Join(", ", Array.ConvertAll(cls.Weapons,
                    k => ClassData.WeaponNames.GetValueOrDefault(k, k)))
                : "—";
            string armor = cls.ArmorTypes.Length > 0
                ? string.Join(", ", Array.ConvertAll(cls.ArmorTypes,
                    k => ClassData.ArmorNames.GetValueOrDefault(k, k)))
                : "—";
            _classHint = _kit.Label(
                UR(new XnaRect(r.X + 12, BY(r) - 64, r.Width - 24, 56)),
                $"Оружие: {weapons}. Броня: {armor}.", 5, UiKitCanvas.Theme.TextMuted);
        }

        private void BuildBagGrid()
        {
            var invRect = _invRect;
            int innerY = invRect.Y + 34;
            int availH = BY(invRect) - innerY - 44;
            const int gapCell = 6;
            int cellW = Math.Min(72, (invRect.Width - UiKitCanvas.Pad * 2 - gapCell * (GridCols - 1)) / GridCols);
            int cellH = Math.Min(72, Math.Max(40, (availH - gapCell * (GridRows - 1)) / GridRows));
            int gridW = GridCols * cellW + gapCell * (GridCols - 1);
            int gridX = invRect.X + (invRect.Width - gridW) / 2;
            _bagGridRect = new XnaRect(gridX, innerY, gridW,
                GridRows * cellH + gapCell * (GridRows - 1));

            int pageSize = Config.InventoryPageSize;
            for (int idx = 0; idx < pageSize; idx++)
            {
                int row = idx / GridCols, col = idx % GridCols;
                var rect = new XnaRect(gridX + col * (cellW + gapCell),
                    innerY + row * (cellH + gapCell), cellW, cellH);
                _bagRects[idx] = rect;
                _bagBgs[idx] = _kit.Slot(UR(rect));
                _bagGlyphs[idx] = _kit.Centered(UR(rect), "", 3, UiKitCanvas.Theme.TextDim);
                _bagCounts[idx] = _kit.Label(
                    UR(new XnaRect(RX(rect) - 26, BY(rect) - 20, 24, 16)),
                    "", 5, UiKitCanvas.Theme.Text, TextAnchor.LowerRight, wrap: false);
            }

            _emptyLabel = _kit.Centered(
                UR(new XnaRect(invRect.X, innerY, invRect.Width, cellH * 2)),
                "Рюкзак пуст", 3, UiKitCanvas.Theme.TextMuted);

            // Пагинация
            int pageY = BY(invRect) - 38;
            const int bw = 40, bh = 28;
            _prevPageBg = _kit.Button(UR(new XnaRect(gridX, pageY, bw, bh)), "‹", 3);
            _nextPageBg = _kit.Button(UR(new XnaRect(RX(_bagGridRect) - bw, pageY, bw, bh)), "›", 3);
            _pageLabel = _kit.Centered(
                UR(new XnaRect(gridX + bw + 8, pageY, gridW - bw * 2 - 16, bh)),
                "", 4, UiKitCanvas.Theme.TextDim);
        }

        // ── Обновление каждый кадр (вызывается извне) ──
        public void Refresh()
        {
            if (_kit == null) return;
            var player = _game.Player;
            _hoverAny = false;

            // Шапка
            var cls = ClassData.Data(player.ClassKey);
            _classLabel.text = player.ClassKey != null ? cls.Name : "Персонаж";
            _className.text = cls.Name;
            _goldLabel.text = $"Золото: {player.Gold}";
            var stacks = player.InventoryStacks();
            int total = _game.InventoryPagesCount();
            int page = Math.Min(_game.CurrentPage, total - 1);
            _countLabel.text = $"Предметов: {stacks.Count}   Стр. {page + 1}/{total}";
            _pageLabel.text = $"Стр. {page + 1}/{total}";

            // Закрытие
            if (UiInput.Clicked(_closeRect))
                _game.ScreenMode = Screens.Game;

            // Пагинация: кнопки + колесо мыши над рюкзаком
            bool prevOk = _game.CurrentPage > 0, nextOk = _game.CurrentPage < total - 1;
            _prevPageBg.color = prevOk ? UiKitCanvas.Theme.BgSlot : UiKitCanvas.Theme.BgSoft;
            _nextPageBg.color = nextOk ? UiKitCanvas.Theme.BgSlot : UiKitCanvas.Theme.BgSoft;
            if (prevOk && UiInput.Clicked(PrevRect())) _game.ChangeInventoryPage(-1);
            if (nextOk && UiInput.Clicked(NextRect())) _game.ChangeInventoryPage(1);
            int wheel = UiInput.Wheel - _prevWheel;
            if (wheel != 0 && UiInput.Hot(_bagGridRect))
                _game.ChangeInventoryPage(wheel > 0 ? 1 : -1);
            _prevWheel = UiInput.Wheel;

            // Сетка рюкзака
            int start = _game.CurrentPage * Config.InventoryPageSize;
            int pageSize = Config.InventoryPageSize;
            _emptyLabel.gameObject.SetActive(stacks.Count == 0);
            for (int idx = 0; idx < pageSize; idx++)
            {
                int absolute = start + idx;
                bool filled = absolute < stacks.Count;
                var bg = _bagBgs[idx];
                var glyph = _bagGlyphs[idx];
                var count = _bagCounts[idx];

                if (!filled)
                {
                    bg.color = UiKitCanvas.Theme.BgSoft;
                    glyph.text = "";
                    count.text = "";
                    continue;
                }

                var (key, cnt) = stacks[absolute];
                bool hot = UiInput.Hot(_bagRects[idx]);
                bg.color = hot ? UiKitCanvas.Theme.BgSlotHot : UiKitCanvas.Theme.BgSlot;
                glyph.text = ItemGlyph(key);
                count.text = cnt > 1 ? cnt.ToString() : "";

                if (hot)
                {
                    ShowItemTooltip(player, key, cnt, _bagRects[idx], unequipHint: false);
                    if (UiInput.Clicked(_bagRects[idx])) _game.UseInventoryItem(key);
                }
            }

            // Снаряжение
            for (int i = 0; i < _equipSlots.Length; i++)
            {
                string slotKey = _equipSlots[i].Key;
                string? itemKey = slotKey switch
                {
                    "head" => player.Head,
                    "armor" => player.Armor,
                    "weapon" => player.Weapon,
                    "legs" => player.Legs,
                    "boots" => player.Boots,
                    _ => null,
                };
                var bg = _equipBgs[i];
                var glyph = _equipGlyphs[i];
                var name = _equipNames[i];

                if (string.IsNullOrEmpty(itemKey))
                {
                    bg.color = UiKitCanvas.Theme.BgSoft;
                    glyph.text = "—";
                    name.text = "";
                    continue;
                }

                bool hot = UiInput.Hot(_equipRects[i]);
                bg.color = hot ? UiKitCanvas.Theme.BgSlotSel : UiKitCanvas.Theme.BgSlot;
                glyph.text = ItemGlyph(itemKey);
                name.text = ItemData.DisplayName(itemKey);
                name.color = ItemData.DisplayColor(itemKey);

                if (hot)
                {
                    ShowItemTooltip(player, itemKey, 1, _equipRects[i], unequipHint: true);
                    if (UiInput.Clicked(_equipRects[i])) _game.UnequipInventorySlot(slotKey);
                }
            }

            if (!_hoverAny) _kit.HideTooltip();
        }

        private XnaRect PrevRect() => new(_bagGridRect.X, BY(_invRect) - 38, 40, 28);

        private XnaRect NextRect() => new(RX(_bagGridRect) - 40, BY(_invRect) - 38, 40, 28);

        public void SetVisible(bool on)
        {
            _kit.SetActive(on);
            if (on)
            {
                _prevWheel = UiInput.Wheel;  // не прыгать страницей из-за накопленного колеса
                Refresh();
            }
            else _kit.HideTooltip();
        }

        // ── Тултипы ──

        /// <summary>Тултип предмета: характеристики + сравнение с надетым.</summary>
        private void ShowItemTooltip(Player player, string key, int count,
            XnaRect anchor, bool unequipHint)
        {
            _hoverAny = true;
            var (baseKey, rarity) = ItemData.ParseItemKey(key);
            var it = ItemData.Get(baseKey);
            var lines = new List<string>();
            if (it == null)
            {
                _kit.ShowTooltip(UR(anchor), ItemData.DisplayName(key), lines);
                return;
            }

            if (count > 1) lines.Add($"Количество: {count}");
            if (!string.IsNullOrEmpty(it.Desc)) lines.Add(it.Desc);

            int bonus = it.IntValue + rarity;
            if (bonus != 0 && IsEquipmentType(it.Kind))
                lines.Add($"+{bonus} {(it.Kind == ItemData.ItemType.Weapon ? "ATK" : "DEF")}");
            foreach (var (lbl, val) in ExtraStats(it))
                lines.Add($"{lbl} {val:+0;-0}");

            if (it.Heal > 0) lines.Add($"+{it.Heal} HP");
            if (it.Mana > 0) lines.Add($"+{it.Mana} маны");

            if (it.LevelReq > 1)
            {
                bool ok = player.Level >= it.LevelReq;
                lines.Add(ok ? $"Требуется уровень {it.LevelReq}"
                             : $"Требуется уровень {it.LevelReq} — мало!");
            }
            var (canUse, why) = ClassData.CanUse(player.ClassKey, key);
            if (!canUse) lines.Add($"Класс не может: {why}");

            // Сравнение с надетым — только для снаряжения
            string? slot = it.Kind switch
            {
                ItemData.ItemType.Weapon => "weapon",
                ItemData.ItemType.Armor => "armor",
                ItemData.ItemType.Helmet => "head",
                ItemData.ItemType.Leggings => "legs",
                ItemData.ItemType.Boots => "boots",
                _ => null,
            };
            if (slot != null)
            {
                foreach (var (name, val, pct) in CompareItem(player, key, slot))
                {
                    string sign = val > 0 ? "▲ +" : val < 0 ? "▼ " : "   ";
                    lines.Add($"{sign}{name} {Math.Abs(val)}{(pct ? "%" : "")}");
                }
            }

            if (unequipHint) lines.Add("Клик — снять");
            else if (!IsEquipmentType(it.Kind)) lines.Add("Клик — использовать");

            _kit.ShowTooltip(UR(anchor), ItemData.DisplayName(key), lines);
        }

        private static bool IsEquipmentType(ItemData.ItemType kind) => kind is
            ItemData.ItemType.Weapon or ItemData.ItemType.Armor
            or ItemData.ItemType.Helmet or ItemData.ItemType.Leggings
            or ItemData.ItemType.Boots;

        /// <summary>compare_item: разница статов при замене слота (base_renderer.py).</summary>
        public static List<(string Label, int Val, bool Pct)> CompareItem(
            Player p, string newKey, string slot)
        {
            string? equipped = slot switch
            {
                "weapon" => p.Weapon,
                "armor" => p.Armor,
                "head" => p.Head,
                "legs" => p.Legs,
                "boots" => p.Boots,
                _ => null,
            };
            if (equipped == newKey) return new();

            var effects = p.Effects();
            Player.StatResult Stats(string? weapon, string? armor, string? head,
                string? legs, string? boots) =>
                Player.ComputeStats(p.Str, p.Agi, p.Vit, p.Luk, p.DfStat, p.IntStat,
                    p.Level, weapon, armor, head, legs, boots, effects);

            var cur = Stats(p.Weapon, p.Armor, p.Head, p.Legs, p.Boots);
            var preview = Stats(
                slot == "weapon" ? newKey : p.Weapon,
                slot == "armor" ? newKey : p.Armor,
                slot == "head" ? newKey : p.Head,
                slot == "legs" ? newKey : p.Legs,
                slot == "boots" ? newKey : p.Boots);

            var deltas = new List<(string, int, bool)>();
            int da = preview.Atk - cur.Atk;
            int dd = preview.Df - cur.Df;
            int dh = preview.MaxHp - cur.MaxHp;
            int dm = preview.MaxMana - cur.MaxMana;
            int dodge = (int)Math.Round((preview.Dodge - cur.Dodge) * 100);
            int crit = (int)Math.Round((preview.Crit - cur.Crit) * 100);
            if (da != 0) deltas.Add(("ATK", da, false));
            if (dd != 0) deltas.Add(("DEF", dd, false));
            if (dh != 0) deltas.Add(("HP", dh, false));
            if (dm != 0) deltas.Add(("Мана", dm, false));
            if (Math.Abs(dodge) >= 1) deltas.Add(("Укл", dodge, true));
            if (Math.Abs(crit) >= 1) deltas.Add(("Крит", crit, true));
            return deltas;
        }

        public static IEnumerable<(string Label, double Val)> ExtraStats(ItemData.Def it)
        {
            if (it.CritBonus != 0) yield return ("Крит", it.CritBonus * 100);
            if (it.DodgeBonus != 0) yield return ("Уклонение", it.DodgeBonus * 100);
            if (it.DfBonus != 0) yield return ("DEF", it.DfBonus);
            if (it.HpBonus != 0) yield return ("HP", it.HpBonus);
            if (it.ManaBonus != 0) yield return ("Мана", it.ManaBonus);
        }

        /// <summary>Иконок-текстур нет — глит def.Ch / первая буква имени.</summary>
        public static string ItemGlyph(string key)
        {
            var (baseKey, _) = ItemData.ParseItemKey(key);
            var def = ItemData.Get(baseKey);
            if (def != null && !string.IsNullOrEmpty(def.Ch)) return def.Ch;
            string name = ItemData.DisplayName(key);
            return string.IsNullOrEmpty(name) ? "?" : name[..1];
        }
    }
}
