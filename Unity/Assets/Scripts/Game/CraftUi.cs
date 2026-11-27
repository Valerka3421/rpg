// uGUI-порт render/ModalScreens.cs → CraftScreen на UiKitCanvas.
// Табы веток из RecipeData.CraftTabs (клик меняет game.CraftTab), список
// рецептов с тултипами материалов, батч [–/+] и страницы кнопками (клавиши
// обрабатывает GameController.HandleCraftInput), мини-игра точности —
// только отображение: полоса с зонами качества и маркер по game.MinigamePos
// (Space обрабатывается в GameController). Визуал строится в Build(),
// каждый кадр обновляется Refresh(); ховер/клики — UiInput.
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using UnityEngine;
using UnityEngine.UI;
using UColor = UnityEngine.Color;
using URect = UnityEngine.Rect;
using XnaRect = Microsoft.Xna.Framework.Rectangle;

namespace Aethelgard
{
    public class CraftUi : MonoBehaviour
    {
        private GameController _game = null!;
        private UiKitCanvas _kit = null!;

        private readonly List<GameObject> _listVis = new();   // визуал списка рецептов
        private readonly List<GameObject> _miniVis = new();   // визуал мини-игры

        private Image[] _tabBg = null!;
        private Text[] _tabLabel = null!;
        private XnaRect[] _tabRects = null!;

        // Строка «Партия/Ветка» + кнопки батча
        private Text _batchText = null!;
        private Image _btnMinus = null!, _btnPlus = null!;
        private XnaRect _minusRect, _plusRect;
        private Image _btnPrev = null!, _btnNext = null!;
        private XnaRect _prevRect, _nextRect;

        // Список рецептов (пул = Config.CraftPageSize)
        private Image[] _rowBg = new Image[Config.CraftPageSize];
        private Image[] _rowStrip = new Image[Config.CraftPageSize];
        private Image[] _rowSwatch = new Image[Config.CraftPageSize];
        private Text[] _rowName = new Text[Config.CraftPageSize];
        private Text[] _rowReqs = new Text[Config.CraftPageSize];
        private Text[] _rowMats = new Text[Config.CraftPageSize];
        private XnaRect[] _rowRects = new XnaRect[Config.CraftPageSize];
        private Text _footerText = null!;

        // Мини-игра: маркер двигается по MinigamePos
        private Text _miniTitle = null!, _miniBatch = null!;
        private Image _marker = null!;

        public static CraftUi Create(GameController game)
        {
            var go = new GameObject("CraftUi");
            var ui = go.AddComponent<CraftUi>();
            ui._game = game;
            ui._kit = new UiKitCanvas("CraftCanvas");
            ui.Build();
            return ui;
        }

        public void SetVisible(bool on) => _kit.SetActive(on);

        // ── Геометрия ──

        private XnaRect PanelRect() => new(210, 60, 1500, 840);

        private XnaRect TabRect(int i)
        {
            var p = PanelRect();
            return new(p.X + 170 + i * 140, p.Y + 14, 140, UiKitCanvas.TabH);
        }

        private XnaRect RowRect(int i)
        {
            var p = PanelRect();
            int y = p.Y + 14 + UiKitCanvas.TabH + 10 + 30;
            return new(p.X + 20, y + i * 62, p.Width - 40, 56);
        }

        private (int X, int Y, int W, int H) BarGeom()
        {
            var p = PanelRect();
            return (p.X + 60, p.Y + 224, p.Width - 120, 30);
        }

        // ── Построение визуала ──

        private void Build()
        {
            var p = PanelRect();

            _kit.Panel(UR(p));

            _kit.Label(new URect(p.X + 24, p.Y + 16, 200, 34), "КРАФТ", 2,
                UiKitCanvas.Theme.GoldBright);

            // Вкладки веток
            var tabs = RecipeData.CraftTabs;
            _tabBg = new Image[tabs.Length];
            _tabLabel = new Text[tabs.Length];
            _tabRects = new XnaRect[tabs.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                _tabRects[i] = TabRect(i);
                _tabBg[i] = _kit.Tab(UR(_tabRects[i]), "", false);
                _tabLabel[i] = _kit.Centered(UR(_tabRects[i]), tabs[i].Name, 4,
                    UiKitCanvas.Theme.TextDim);
            }

            // Строка партии/ветки + кнопки батча [–/+]
            int batchY = p.Y + 14 + UiKitCanvas.TabH + 10;
            _batchText = _kit.Label(new URect(p.X + 24, batchY, 900, 20), "", 4,
                UiKitCanvas.Theme.TextDim);
            // Кнопки батча [–/+] и страниц ←/→ — справа, в один ряд
            _minusRect = new XnaRect(p.X + p.Width - 208, batchY - 4, 36, 28);
            _plusRect = new XnaRect(p.X + p.Width - 164, batchY - 4, 36, 28);
            _btnMinus = _kit.Button(UR(_minusRect), "–", 4);
            _btnPlus = _kit.Button(UR(_plusRect), "+", 4);

            // Кнопки страниц (дубль клавиш ←/→)
            _prevRect = new XnaRect(p.X + p.Width - 120, batchY - 4, 36, 28);
            _nextRect = new XnaRect(p.X + p.Width - 76, batchY - 4, 36, 28);
            _btnPrev = _kit.Button(UR(_prevRect), "‹", 4);
            _btnNext = _kit.Button(UR(_nextRect), "›", 4);

            BuildList();
            // Кит создаёт тени/рамки/подсветки отдельными сиблингами под корнем —
            // собираем только новые (элементы мини-игры) для её скрытия
            var known = new HashSet<GameObject>();
            foreach (Transform t in _kit.Root) known.Add(t.gameObject);
            BuildMinigame();
            foreach (Transform t in _kit.Root)
                if (!known.Contains(t.gameObject))
                    _miniVis.Add(t.gameObject);

            SetVisible(false);
        }

        private void BuildList()
        {
            for (int i = 0; i < _rowBg.Length; i++)
            {
                var r = RowRect(i);
                _rowRects[i] = r;
                _rowBg[i] = _kit.RectImage(UR(r), UiKitCanvas.Theme.BgSlot);
                // Цветная плашка-статус слева, как на desktop
                _rowStrip[i] = _kit.RectImage(new URect(r.X, r.Y, 3, r.Height),
                    UiKitCanvas.Theme.TextMuted);
                _rowSwatch[i] = _kit.RectImage(
                    new URect(r.X + 12, r.Y + 11, 34, 34), UColor.white);
                _rowName[i] = _kit.Label(new URect(r.X + 56, r.Y + 4, r.Width - 260, 22),
                    "", 3, UiKitCanvas.Theme.Text);
                _rowReqs[i] = _kit.Label(
                    new URect(r.X + r.Width - 300, r.Y + 8, 288, 18), "", 4,
                    UiKitCanvas.Theme.TextDim, TextAnchor.UpperRight, wrap: false);
                _rowMats[i] = _kit.Label(new URect(r.X + 56, r.Y + 30, r.Width - 66, 18),
                    "", 4, UiKitCanvas.Theme.Xp);
                _listVis.Add(_rowBg[i].gameObject);
                _listVis.Add(_rowStrip[i].gameObject);
                _listVis.Add(_rowSwatch[i].gameObject);
                _listVis.Add(_rowName[i].gameObject);
                _listVis.Add(_rowReqs[i].gameObject);
                _listVis.Add(_rowMats[i].gameObject);
            }

            var p = PanelRect();
            _footerText = _kit.Label(
                new URect(p.X + 24, p.Y + p.Height - 40, p.Width - 48, 20), "", 4,
                UiKitCanvas.Theme.TextMuted);
            _listVis.Add(_footerText.gameObject);
        }

        private void BuildMinigame()
        {
            var p = PanelRect();
            int y0 = p.Y + 150;

            _miniTitle = _kit.Label(new URect(p.X + 40, y0, p.Width - 80, 34), "", 2,
                UiKitCanvas.Theme.GoldBright);
            _miniVis.Add(_miniTitle.gameObject);

            var hint = _kit.Label(
                new URect(p.X + 40, y0 + 40, p.Width - 80, 20),
                "Останови индикатор в центре (Space). Точность → качество +N", 4,
                UiKitCanvas.Theme.TextDim);
            _miniVis.Add(hint.gameObject);

            // Полоса: тёмная основа + вложенные зоны качества
            // (0.42 «хорошо» / 0.72 «отлично» / 0.93 «идеал»)
            var (barX, barY, barW, barH) = BarGeom();
            _miniVis.Add(_kit.RectImage(new URect(barX, barY, barW, barH),
                new UColor(24f / 255f, 21f / 255f, 17f / 255f)).gameObject);

            var (w1, w2, w3) = ((int)(barW * 0.42f), (int)(barW * 0.72f), (int)(barW * 0.93f));
            var z1 = _kit.Bar(new URect(barX + barW / 2 - w1 / 2, barY, w1, barH),
                new UColor(72f / 255f, 62f / 255f, 36f / 255f));
            var z2 = _kit.Bar(new URect(barX + barW / 2 - w2 / 2, barY, w2, barH),
                new UColor(130f / 255f, 106f / 255f, 48f / 255f));
            var z3 = _kit.Bar(new URect(barX + barW / 2 - w3 / 2, barY, w3, barH),
                new UColor(196f / 255f, 158f / 255f, 74f / 255f));
            UiKitCanvas.SetBar(z1.fill, 1);
            UiKitCanvas.SetBar(z2.fill, 1);
            UiKitCanvas.SetBar(z3.fill, 1);
            _miniVis.Add(z1.bg.gameObject);
            _miniVis.Add(z2.bg.gameObject);
            _miniVis.Add(z3.bg.gameObject);

            // Легенда зон
            var good = _kit.Label(new URect(barX + 8, barY + barH + 8, 120, 18),
                "хорошо", 4, new UColor(150f / 255f, 135f / 255f, 95f / 255f));
            var ideal = _kit.Label(new URect(barX + barW / 2 - 60, barY + barH + 8, 120, 18),
                "идеал", 4, UiKitCanvas.Theme.GoldBright, TextAnchor.UpperCenter, wrap: false);
            var great = _kit.Label(new URect(barX + barW - 128, barY + barH + 8, 120, 18),
                "отлично", 4, new UColor(196f / 255f, 158f / 255f, 74f / 255f),
                TextAnchor.UpperRight, wrap: false);
            _miniVis.Add(good.gameObject);
            _miniVis.Add(ideal.gameObject);
            _miniVis.Add(great.gameObject);

            // Индикатор-маркер (позиция обновляется в Refresh по MinigamePos)
            _marker = _kit.RectImage(new URect(barX, barY - 12, 6, barH + 12),
                UiKitCanvas.Theme.Text);
            _miniVis.Add(_marker.gameObject);

            int by = barY + barH + 56;
            _miniBatch = _kit.Label(new URect(p.X + 40, by, p.Width - 80, 20), "", 4,
                UiKitCanvas.Theme.TextDim);
            var cancel = _kit.Label(new URect(p.X + 40, by + 24, p.Width - 80, 20),
                "Space: остановить   Esc: отмена", 4, UiKitCanvas.Theme.TextMuted);
            _miniVis.Add(_miniBatch.gameObject);
            _miniVis.Add(cancel.gameObject);
        }

        // ── Кадровое обновление ──

        private void Refresh()
        {
            var p = _game.Player;

            // Вкладки: клик меняет game.CraftTab (как клавиши Q/E на desktop)
            int tab = Mathf.Clamp(_game.CraftTab, 0, RecipeData.CraftTabs.Length - 1);
            for (int i = 0; i < RecipeData.CraftTabs.Length; i++)
            {
                bool sel = i == tab;
                _tabLabel[i].color = sel ? UiKitCanvas.Theme.GoldBright : UiKitCanvas.Theme.TextDim;
                _tabBg[i].color = sel
                    ? new UColor(64f / 255f, 52f / 255f, 28f / 255f)
                    : UiKitCanvas.Theme.BgSoft;
                if (UiInput.Clicked(_tabRects[i]) && !sel)
                {
                    _game.CraftTab = i;
                    tab = i;
                }
            }

            // Партия [–/+]: те же пределы, что у клавиш в HandleCraftInput
            _batchText.text =
                $"Партия: {_game.CraftQty}  ([ / ])   Ветка: {BranchInfo()}";
            if (UiInput.Clicked(_minusRect))
                _game.CraftQty = Math.Max(1, _game.CraftQty - 1);
            if (UiInput.Clicked(_plusRect))
                _game.CraftQty = Math.Min(9, _game.CraftQty + 1);
            _btnMinus.color = UiInput.Hot(_minusRect)
                ? UiKitCanvas.Theme.BgSlotHot : UiKitCanvas.Theme.BgSlot;
            _btnPlus.color = UiInput.Hot(_plusRect)
                ? UiKitCanvas.Theme.BgSlotHot : UiKitCanvas.Theme.BgSlot;

            bool tooltipShown = false;
            if (_game.MinigameActive)
            {
                SetListVisible(false);
                tooltipShown = false;
                RefreshMinigame();
            }
            else
            {
                SetListVisible(true);
                tooltipShown = RefreshList(p);
            }

            if (!tooltipShown) _kit.HideTooltip();
        }

        private void SetListVisible(bool on)
        {
            foreach (var go in _listVis) go.SetActive(on);
            foreach (var go in _miniVis) go.SetActive(!on);
        }

        private void ToggleMini(bool on)
        {
            // не используется напрямую: видимость мини-игры управляется
            // парой SetListVisible/RefreshList — метод оставлен для ясности
            foreach (var go in _miniVis) go.SetActive(on);
        }

        // Список рецептов: гейт прогрессии (LevelReq + SkillReq + чертёж)
        private bool RefreshList(Entities.Player p)
        {
            bool tooltipShown = false;

            var list = _game.CraftableList();
            int pageStart = _game.CraftPage * Config.CraftPageSize;
            int maxPage = Math.Max(0, (list.Count - 1) / Config.CraftPageSize);
            if (_game.CraftPage > maxPage) _game.CraftPage = maxPage;
            pageStart = _game.CraftPage * Config.CraftPageSize;

            for (int i = 0; i < Config.CraftPageSize; i++)
            {
                bool used = pageStart + i < list.Count;
                _rowBg[i].gameObject.SetActive(used);
                _rowStrip[i].gameObject.SetActive(used);
                _rowSwatch[i].gameObject.SetActive(used);
                _rowName[i].gameObject.SetActive(used);
                _rowReqs[i].gameObject.SetActive(used);
                _rowMats[i].gameObject.SetActive(used);
                if (!used) continue;

                int idx = pageStart + i;
                var (key, def) = list[idx];
                string branch = ItemData.CategoryOf(key);
                int branchLevel = p.CraftLevels.GetValueOrDefault(branch, 1);
                bool byBranch = def.SkillReq <= branchLevel;
                bool byChar = p.Level >= def.LevelReq;
                bool byBlueprint = string.IsNullOrEmpty(def.BlueprintRequired)
                    || p.Blueprints.Contains(def.BlueprintRequired);
                bool canCraft = byBranch && byChar && byBlueprint;

                string lockText = !byChar ? $"нужен ур. {def.LevelReq}"
                    : !byBranch ? $"нужна ветка ур. {def.SkillReq}"
                    : !byBlueprint ? "нужен чертёж" : "";

                var variants = ItemData.RecipesOf(key);
                bool afford = variants.Count == 0 || variants[0].Materials.All(m =>
                    p.ItemCount(m.Key) >= m.Value);

                var rect = _rowRects[i];
                bool hot = UiInput.Hot(rect);
                // Ховер — сменой bg.color (BgSlot/BgSlotHot)
                _rowBg[i].color = canCraft
                    ? (hot ? UiKitCanvas.Theme.BgSlotHot
                           : new UColor(30f / 255f, 35f / 255f, 25f / 255f, 140f / 255f))
                    : UiKitCanvas.Theme.BgSlot;
                _rowStrip[i].color = canCraft
                    ? (afford ? UiKitCanvas.Theme.Xp : UiKitCanvas.Theme.Gold)
                    : UiKitCanvas.Theme.TextMuted;
                _rowSwatch[i].color = (UColor)ItemData.DisplayColor(key);
                _rowName[i].text = ItemData.DisplayName(key) +
                    (canCraft ? "" : $"  [закрыто: {lockText}]");
                _rowName[i].color = canCraft ? UiKitCanvas.Theme.Text : UiKitCanvas.Theme.TextMuted;
                _rowReqs[i].text =
                    $"Ветка ур. {def.SkillReq} · Tier {def.Tier} · {BranchName(branch)}";
                _rowReqs[i].color = byBranch ? UiKitCanvas.Theme.TextDim : UiKitCanvas.Theme.Danger;
                if (variants.Count > 0)
                {
                    string mats = string.Join(", ", variants[0].Materials
                        .Select(m => $"{ItemData.DisplayName(m.Key)}×{m.Value}"));
                    _rowMats[i].text = mats;
                    _rowMats[i].color = canCraft && afford
                        ? UiKitCanvas.Theme.Xp : UiKitCanvas.Theme.Danger;
                }
                else
                    _rowMats[i].text = "";

                if (hot)
                {
                    // Тултип: материалы (с остатком) + описание + причина блокировки
                    var lines = new List<string>();
                    if (variants.Count > 0)
                        foreach (var m in variants[0].Materials)
                            lines.Add($"  {ItemData.DisplayName(m.Key)}×{m.Value} " +
                                $"(есть {p.ItemCount(m.Key)})");
                    if (!string.IsNullOrEmpty(def.Desc))
                        lines.Add(def.Desc);
                    if (!canCraft)
                        lines.Add($"Требуется: {lockText}");
                    _kit.ShowTooltip(UR(rect), ItemData.DisplayName(key), lines);
                    tooltipShown = true;
                    if (UiInput.Clicked(rect))
                        _game.StartCraft(i);
                }
            }

            int maxPage2 = Math.Max(0, (list.Count - 1) / Config.CraftPageSize);
            _footerText.text =
                $"Стр. {_game.CraftPage + 1}/{maxPage2 + 1}  (←/→)   Space/1–8: крафт   K/Esc: выход";

            // Кнопки страниц (дубль клавиш)
            if (UiInput.Clicked(_prevRect))
                _game.CraftPage = Math.Max(0, _game.CraftPage - 1);
            if (UiInput.Clicked(_nextRect))
                _game.CraftPage++;
            _btnPrev.color = UiInput.Hot(_prevRect)
                ? UiKitCanvas.Theme.BgSlotHot : UiKitCanvas.Theme.BgSlot;
            _btnNext.color = UiInput.Hot(_nextRect)
                ? UiKitCanvas.Theme.BgSlotHot : UiKitCanvas.Theme.BgSlot;

            return tooltipShown;
        }

        private void RefreshMinigame()
        {
            var (barX, barY, barW, barH) = BarGeom();
            _miniTitle.text = $"Крафт: {ItemData.DisplayName(_game.MinigameItem ?? "")}";
            _miniBatch.text =
                $"Партия: {_game.CraftQty} × {ItemData.DisplayName(_game.MinigameItem ?? "")}";

            // Маркер по состоянию мини-игры (двигает GameController.UpdateMinigame)
            int ix = barX + (int)(barW * _game.MinigamePos) - 3;
            var rt = _marker.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(ix, AethelgardGame.ScreenH - (barY - 12 + barH + 12));
            rt.sizeDelta = new Vector2(6, barH + 12);
        }

        private string BranchName(string branch) =>
            RecipeData.Skills.TryGetValue(branch, out var s) ? s.Name : branch;

        private string BranchInfo()
        {
            string? branch = _game.CraftTab == 0
                ? null : RecipeData.SkillOrder[_game.CraftTab - 1];
            if (branch == null) return "все";
            int lvl = _game.Player.CraftLevels.GetValueOrDefault(branch, 1);
            int xp = _game.Player.CraftXp.GetValueOrDefault(branch);
            return $"{BranchName(branch)} ур. {lvl} ({xp}/{RecipeData.SkillXpToNext(lvl)})";
        }

        // XnaRect → URect (визуал кита живёт в UnityEngine.Rect)
        private static URect UR(XnaRect r) => new((float)r.X, (float)r.Y, r.Width, r.Height);

        private void Update() => Refresh();
    }
}
