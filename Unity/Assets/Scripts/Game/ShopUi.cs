// uGUI-порт render/ShopScreen.cs: слева — список (товар в BUY / рюкзак
// в SELL), справа — «чек» выбранного предмета (статы, цена, сравнение),
// шапка — торговец, режим, золото, кнопки режима/реролла, закрытие.
// Визуал строится один раз в Build(), каждый кадр Refresh() обновляет
// содержимое и хит-тесты через UiInput. Клавиатура (S/R/Q/E) остаётся
// в GameController — здесь только мышь.
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
    public class ShopUi : MonoBehaviour
    {
        private const int Rows = 9;          // = Config.ShopPageSize
        private const int RowH = 44;

        private GameController _game = null!;
        private UiKitCanvas _kit = null!;
        private XnaRect _panel;              // модальное окно (для шапки в Refresh)

        // ── Шапка ──
        private Text _merchantLabel = null!, _subLabel = null!, _goldLabel = null!;
        private Text _modeText = null!;
        private Image _closeBg = null!;
        private XnaRect _closeRect, _toggleRect, _rerollRect;

        // ── Список ──
        private readonly XnaRect[] _rowRects = new XnaRect[Rows];
        private readonly Image[] _rowBgs = new Image[Rows];
        private readonly Text[] _rowGlyphs = new Text[Rows];
        private readonly Text[] _rowNames = new Text[Rows];
        private readonly Text[] _rowPrices = new Text[Rows];
        private Text _listTitle = null!, _emptyLabel = null!, _pageLabel = null!;
        private XnaRect _listRect, _listHitRect;

        // ── «Чек» ──
        private Text _dIcon = null!, _dName = null!, _dType = null!, _dDesc = null!;
        private Text _dStats = null!, _dPrice = null!, _dHint = null!;
        private XnaRect _detailRect;

        // Ховер текущего кадра — источник правой панели
        private (string Key, int Price, bool Buy)? _hovered;
        private int _prevWheel;

        public static ShopUi Create(GameController game)
        {
            var go = new GameObject("ShopUi");
            var ui = go.AddComponent<ShopUi>();
            ui._game = game;
            ui.Build();
            return ui;
        }

        // ── Хелперы геометрии ──
        private static int RX(XnaRect r) => r.X + r.Width;
        private static int BY(XnaRect r) => r.Y + r.Height;
        private static Rect UR(XnaRect r) => new(r.X, r.Y, r.Width, r.Height);

        private void Build()
        {
            _kit = new UiKitCanvas("ShopCanvas");

            _panel = new XnaRect(AethelgardGame.ScreenW / 2 - 620, 60, 1240,
                AethelgardGame.ScreenH - 120);
            var panel = _panel;
            _kit.Panel(UR(panel));
            int cx = panel.X + panel.Width / 2;
            _closeRect = new XnaRect(RX(panel) - 44, panel.Y + 12, 32, 32);
            _toggleRect = new XnaRect(cx - 90, panel.Y + 18, 180, 36);
            _rerollRect = new XnaRect(cx + 100, panel.Y + 22, 150, 30);

            // Шапка
            _merchantLabel = _kit.Label(UR(new XnaRect(panel.X + 20, panel.Y + 14, 500, 36)),
                "ТОРГОВЕЦ", 2, UiKitCanvas.Theme.GoldBright);
            _subLabel = _kit.Label(UR(new XnaRect(panel.X + 20, panel.Y + 48, 500, 20)),
                "", 4, UiKitCanvas.Theme.TextDim, wrap: false);
            _kit.Button(UR(_toggleRect), "", 3);
            _modeText = _kit.Centered(UR(_toggleRect), "", 3, UiKitCanvas.Theme.TextDim);
            _kit.Button(UR(_rerollRect), "Обновить (R)", 4);
            _goldLabel = _kit.Label(
                UR(new XnaRect(RX(panel) - 480, panel.Y + 24, 400, 24)),
                "", 3, UiKitCanvas.Theme.Gold, TextAnchor.UpperRight);
            _closeBg = _kit.Button(UR(_closeRect), "×", 3);

            int contentY = panel.Y + 72 + UiKitCanvas.Gap;
            int contentH = BY(panel) - contentY - 34;
            int listW = 520;
            int listX = panel.X + UiKitCanvas.Pad;
            int detailX = listX + listW + UiKitCanvas.Gap;
            int detailW = RX(panel) - UiKitCanvas.Pad - detailX;
            _listRect = new XnaRect(listX, contentY, listW, contentH);
            _listHitRect = new XnaRect(listX, contentY, listW, contentH - 34);
            _detailRect = new XnaRect(detailX, contentY, detailW, contentH);

            // Список: заголовок секции меняется режимом — перекрываем своим текстом
            _kit.Section(UR(_listRect), "");
            _listTitle = _kit.Label(UR(new XnaRect(listX + 8, contentY + 5, listW - 16, 20)),
                "", 4, UiKitCanvas.Theme.Gold);
            _emptyLabel = _kit.Centered(UR(new XnaRect(listX, contentY + 34, listW, RowH)),
                "Рюкзак пуст", 3, UiKitCanvas.Theme.TextMuted);

            int innerY = contentY + 30;
            for (int i = 0; i < Rows; i++)
            {
                int ry = innerY + i * (RowH + 4);
                var rowRect = new XnaRect(listX + 8, ry, listW - 16, RowH);
                _rowRects[i] = rowRect;
                _rowBgs[i] = _kit.RectImage(UR(rowRect), UiKitCanvas.Theme.BgSoft);
                var iconRect = new XnaRect(rowRect.X + 4, ry + 5, 34, 34);
                _rowGlyphs[i] = _kit.Centered(UR(iconRect), "", 3, UiKitCanvas.Theme.TextDim);
                _rowNames[i] = _kit.Label(
                    UR(new XnaRect(rowRect.X + 52, ry + 5, rowRect.Width - 60, 18)),
                    "", 4, UiKitCanvas.Theme.Text, wrap: false);
                _rowPrices[i] = _kit.Label(
                    UR(new XnaRect(rowRect.X + 52, ry + 24, rowRect.Width - 60, 16)),
                    "", 4, UiKitCanvas.Theme.TextDim, wrap: false);
            }
            _pageLabel = _kit.Label(
                UR(new XnaRect(listX + 8, BY(_listRect) - 30, listW - 16, 24)),
                "", 4, UiKitCanvas.Theme.TextMuted, TextAnchor.UpperCenter);

            BuildDetail();

            _kit.Label(UR(new XnaRect(panel.X, BY(panel) - 30, panel.Width, 22)),
                "Клик — купить/продать · S — режим · Q/E — страницы · R — обновить сток · Esc — выход",
                4, UiKitCanvas.Theme.TextMuted, TextAnchor.UpperCenter);
        }

        private void BuildDetail()
        {
            var r = _detailRect;
            _kit.Section(UR(r), "Описание");
            int x = r.X + 12;
            int y = r.Y + 32;
            int w = r.Width - 24;

            _dIcon = _kit.Centered(UR(new XnaRect(x, y, 48, 48)), "", 3, UiKitCanvas.Theme.Text);
            _dName = _kit.Label(UR(new XnaRect(x + 58, y + 2, w - 58, 24)), "", 3,
                UiKitCanvas.Theme.Text, wrap: false);
            _dType = _kit.Label(UR(new XnaRect(x + 58, y + 28, w - 58, 18)), "", 4,
                UiKitCanvas.Theme.TextDim, wrap: false);
            _dDesc = _kit.Label(UR(new XnaRect(x, y + 62, w, 60)), "", 4,
                UiKitCanvas.Theme.TextMuted);
            _dStats = _kit.Label(UR(new XnaRect(x, y + 128, w, BY(r) - y - 128 - 80)), "",
                4, UiKitCanvas.Theme.Text);
            _dPrice = _kit.Label(UR(new XnaRect(x, BY(r) - 48, w, 24)), "", 3,
                UiKitCanvas.Theme.GoldBright, wrap: false);
            _dHint = _kit.Centered(UR(new XnaRect(r.X, r.Y + 40, r.Width, 80)),
                "", 4, UiKitCanvas.Theme.TextMuted);
        }

        // ── Обновление каждый кадр (вызывается извне) ──
        public void Refresh()
        {
            if (_kit == null) return;
            var player = _game.Player;
            _hovered = null;

            bool buying = _game.ShopSubMode == "buy";
            bool cityShop = CityData.IsCityShopNpc(_game.ActiveShopNpc);

            // Шапка
            _merchantLabel.text = (cityShop
                ? CityData.NpcNames.GetValueOrDefault(_game.ActiveShopNpc ?? "", "Торговец")
                : "Торговец").ToUpperInvariant();
            _subLabel.text = cityShop ? "Город Этельгард" : $"Глубина {_game.Depth}";
            _modeText.text = buying ? "ПОКУПКА" : "ПРОДАЖА";
            _goldLabel.text = $"Золото: {player.Gold}";
            _listTitle.text = buying ? "Товары торговца" : "Рюкзак — продать";

            // Закрытие / режим / реролл
            if (UiInput.Clicked(_closeRect)) _game.ScreenMode = Screens.Game;
            if (UiInput.Clicked(_toggleRect)) _game.ToggleShopMode();
            if (!cityShop && UiInput.Clicked(_rerollRect)) _game.RerollShop();

            // Пагинация: колесо над списком (клавиши Q/E обрабатывает GameController)
            int wheel = UiInput.Wheel - _prevWheel;
            if (wheel != 0 && UiInput.Hot(_listHitRect))
            {
                int target = _game.ShopPage + (wheel > 0 ? 1 : -1);
                _game.ShopPage = Math.Max(0, Math.Min(_game.ShopPagesCount() - 1, target));
            }
            _prevWheel = UiInput.Wheel;

            int pageCount = _game.ShopPagesCount();
            int page = Math.Min(_game.ShopPage, Math.Max(0, pageCount - 1));
            _pageLabel.text = pageCount > 1 ? $"Стр. {page + 1}/{pageCount}  (Q/E)" : "";

            // Список: строки 0..8, pageStart = page * ShopPageSize
            var stock = buying ? _game.CurrentShopStock() : null;
            var items = buying ? null : player.InventoryStacks();
            int pageStart = page * Config.ShopPageSize;
            _emptyLabel.gameObject.SetActive(!buying && items!.Count == 0);

            for (int i = 0; i < Rows; i++)
            {
                int idx = pageStart + i;
                var bg = _rowBgs[i];
                var glyph = _rowGlyphs[i];
                var name = _rowNames[i];
                var price = _rowPrices[i];

                if (buying)
                {
                    if (idx >= stock!.Count)
                    {
                        bg.color = UiKitCanvas.Theme.BgSoft;
                        glyph.text = ""; name.text = ""; price.text = "";
                        continue;
                    }
                    var entry = stock[idx];
                    var (baseKey, _) = ItemData.ParseItemKey(entry.Key);
                    var def = ItemData.Get(baseKey);
                    bool learned = def?.Kind == ItemData.ItemType.Book
                        && IsPerkStat(def.Stat) && PerkLearned(player, def.Stat!);
                    bool dim = entry.Sold || learned;

                    glyph.text = InventoryUi.ItemGlyph(entry.Key);
                    name.text = ItemData.DisplayName(entry.Key);
                    name.color = dim ? UiKitCanvas.Theme.TextMuted : ItemData.DisplayColor(entry.Key);
                    price.text = entry.Sold ? "продано" : learned ? "уже изучено" : $"{entry.Price} з.";
                    price.color = dim ? UiKitCanvas.Theme.TextMuted : UiKitCanvas.Theme.Gold;
                    bg.color = UiKitCanvas.Theme.BgSoft;

                    if (!dim && UiInput.Hot(_rowRects[i]))
                    {
                        bg.color = UiKitCanvas.Theme.BgSlotHot;
                        _hovered = (entry.Key, entry.Price, true);
                        if (UiInput.Clicked(_rowRects[i])) _game.BuyItem(i);
                    }
                }
                else
                {
                    if (idx >= items!.Count)
                    {
                        bg.color = UiKitCanvas.Theme.BgSoft;
                        glyph.text = ""; name.text = ""; price.text = "";
                        continue;
                    }
                    var (key, count) = items[idx];
                    int sellPrice = ItemData.SellPrice(key);

                    glyph.text = InventoryUi.ItemGlyph(key);
                    name.text = $"{ItemData.DisplayName(key)}{(count > 1 ? $" ×{count}" : "")}";
                    name.color = ItemData.DisplayColor(key);
                    price.text = $"{sellPrice} з.";
                    price.color = UiKitCanvas.Theme.Gold;
                    bg.color = UiKitCanvas.Theme.BgSoft;

                    if (UiInput.Hot(_rowRects[i]))
                    {
                        bg.color = UiKitCanvas.Theme.BgSlotHot;
                        _hovered = (key, sellPrice, false);
                        if (UiInput.Clicked(_rowRects[i])) _game.SellItem(i);
                    }
                }
            }

            RefreshDetail(buying);
        }

        /// <summary>Правая панель: «чек» hovered предмета (порт DrawDetail).</summary>
        private void RefreshDetail(bool buying)
        {
            if (_hovered == null)
            {
                _dIcon.text = ""; _dName.text = ""; _dType.text = ""; _dDesc.text = "";
                _dStats.text = ""; _dPrice.text = "";
                _dHint.gameObject.SetActive(true);
                _dHint.text = buying
                    ? "Наведите курсор на товар,\nчтобы увидеть характеристики"
                    : "Наведите курсор на предмет\nрюкзака, чтобы продать";
                return;
            }
            _dHint.gameObject.SetActive(false);

            var player = _game.Player;
            var (key, price, isBuy) = _hovered.Value;
            var (baseKey, rarity) = ItemData.ParseItemKey(key);
            var it = ItemData.Get(baseKey);
            if (it == null) return;

            _dIcon.text = InventoryUi.ItemGlyph(key);
            _dName.text = ItemData.DisplayName(key);
            _dName.color = ItemData.DisplayColor(key);
            _dType.text = TypeName(it.Type) + (rarity > 0 ? $"  +{rarity}" : "");
            _dDesc.text = it.Desc ?? "";

            var lines = new List<string>();
            bool equipment = it.Type is "weapon" or "armor" or "head" or "helmet"
                or "legs" or "leggings" or "boots";
            if (equipment)
            {
                int bonus = it.IntValue + rarity;
                if (bonus != 0)
                    lines.Add($"+{bonus} {(it.Type == "weapon" ? "ATK" : "DEF")}");
                foreach (var (lbl, val) in InventoryUi.ExtraStats(it))
                    lines.Add($"{lbl} {val:+0;-0}");

                string? slot = it.Type switch
                {
                    "weapon" => "weapon", "armor" => "armor",
                    "head" or "helmet" => "head", "legs" or "leggings" => "legs", _ => null,
                };
                if (slot != null)
                {
                    foreach (var (name, val, pct) in InventoryUi.CompareItem(player, key, slot))
                    {
                        string sign = val > 0 ? "▲ +" : val < 0 ? "▼ " : "   ";
                        lines.Add($"{sign}{name} {Math.Abs(val)}{(pct ? "%" : "")}");
                    }
                }
            }
            else
            {
                if (it.Heal > 0) lines.Add($"+{it.Heal} HP");
                if (it.Mana > 0) lines.Add($"+{it.Mana} маны");
            }

            if (it.LevelReq > 1)
            {
                bool ok = player.Level >= it.LevelReq;
                lines.Add(ok ? $"Требуется уровень {it.LevelReq}"
                             : $"Требуется уровень {it.LevelReq} — мало!");
            }
            var (canUse, why) = ClassData.CanUse(player.ClassKey, key);
            if (!canUse) lines.Add($"Класс не может носить: {why}");
            _dStats.text = string.Join("\n", lines);

            string priceText = isBuy ? $"Цена: {price} з." : $"Продажа: {price} з.";
            bool afford = !isBuy || player.Gold >= price;
            _dPrice.text = afford ? priceText : $"{priceText} — не хватает золота";
            _dPrice.color = afford ? UiKitCanvas.Theme.GoldBright : UiKitCanvas.Theme.Danger;
        }

        private static string TypeName(string type) => type switch
        {
            "weapon" => "Оружие", "armor" => "Броня", "head" or "helmet" => "Шлем",
            "legs" or "leggings" => "Поножи", "boots" => "Обувь",
            "heal" => "Зелье лечения", "mana" => "Зелье маны",
            "poison" => "Яд", "buff" => "Эликсир", "resource" => "Ресурс",
            "material" => "Материал", "book" => "Книга", "scroll" => "Свиток",
            "rune" => "Руна", "key" => "Ключ", "blueprint" => "Чертёж",
            "food" => "Еда", "gold" => "Золото", _ => "Предмет",
        };

        private static bool IsPerkStat(string? stat) => stat is
            "double_strike" or "regen" or "vampire" or "thorns" or "greedy";

        private static bool PerkLearned(Player p, string stat) => stat switch
        {
            "double_strike" => p.DoubleStrike,
            "regen" => p.Regen,
            "vampire" => p.Vampire,
            "thorns" => p.Thorns,
            "greedy" => p.Greedy,
            _ => false,
        };

        public void SetVisible(bool on)
        {
            _kit.SetActive(on);
            if (on)
            {
                _prevWheel = UiInput.Wheel;
                Refresh();
            }
        }
    }
}
