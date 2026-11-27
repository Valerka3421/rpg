// uGUI-порт render/SkillScreen.cs (дерево навыков) на UiKitCanvas.
// Вкладки «Класс|Ковка|Алхимия|Инженерия» синхронизируются с game.SkillTab
// (клик меняет его так же, как desktop); клики по узлам/классам формируют те
// же команды «learn:<key>»/«promote:<key>» и исполняются через
// GameController.HandleSkillClick. Гекс-координаты узлов (центр + кольца)
// перенесены 1:1 из desktop. Визуал строится один раз в Build(), каждый кадр
// Refresh() обновляет цвета/тексты/тултипы; ховер и клики — UiInput по тем же
// прямоугольникам, ховер-анимация — сменой bg.color (BgSlot/BgSlotHot/BgSlotSel).
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using UnityEngine;
using UnityEngine.UI;
using UColor = UnityEngine.Color;
using URect = UnityEngine.Rect;
using XnaRect = Microsoft.Xna.Framework.Rectangle;

namespace Aethelgard
{
    public class SkillsUi : MonoBehaviour
    {
        private GameController _game = null!;
        private UiKitCanvas _kit = null!;

        // Вкладки (0=класс, 1..3 — ремёсла): подписи и ключи как на desktop
        private static readonly string[] TabNames = { "Класс", "Ковка", "Алхимия", "Инженерия" };
        private static readonly string[] TabKeys = { "", "blacksmith", "alchemy", "magic_engineering" };

        // Видимость элементов управляется списками: все визуалы вкладки
        // активны только когда вкладка выбрана; строки рецептов прячутся
        // индивидуально, если пул не использован полностью.
        private readonly List<GameObject>[] _tabVis = CreateVis();
        private static List<GameObject>[] CreateVis() =>
            new List<GameObject>[4] { new(), new(), new(), new() };

        private Image[] _tabBg = null!;
        private Text[] _tabLabel = null!;
        private Image[] _tabMark = null!;            // золотое подчёркивание выбранной
        private XnaRect[] _tabRects = null!;

        // Шапка
        private Text _subText = null!;
        private Image _badgeBg = null!;
        private Text _badgeText = null!;

        // Классовое древо (гекс-позиции desktop, пул 12 узлов)
        private Text _treeEmpty = null!, _treeHint = null!;
        private Image[] _nodeBg = new Image[12];
        private Text[] _nodeName = new Text[12];
        private Text[] _nodeReq = new Text[12];

        // Эволюция класса
        private Text _promoLock = null!, _promoHint = null!, _promoLevel = null!;
        private Image _promoBarFill = null!;
        private Image[] _promoBg = new Image[4];
        private Text[] _promoText = new Text[4];
        private XnaRect[] _promoRects = new XnaRect[4];
        private GameObject[] _promoGo = new GameObject[4];

        // Вкладки ремёсел: шапка ветки + XP-бар + список рецептов (пул 24)
        private Text[] _branchName = new Text[3];
        private Text[] _branchDesc = new Text[3];
        private Text[] _branchLvl = new Text[3];
        private Image[] _branchBarFill = new Image[3];
        private Image[] _recipeBg = new Image[24];
        private Text[] _recipeName = new Text[24];
        private Text[] _recipeMats = new Text[24];
        private XnaRect[] _recipeRects = new XnaRect[24];

        public static SkillsUi Create(GameController game)
        {
            var go = new GameObject("SkillsUi");
            var ui = go.AddComponent<SkillsUi>();
            ui._game = game;
            ui._kit = new UiKitCanvas("SkillsCanvas");
            ui.Build();
            return ui;
        }

        public void SetVisible(bool on) => _kit.SetActive(on);

        // ── Геометрия (дизайн 1920×1080, координаты от верхнего края) ──

        private XnaRect PanelRect() => new(180, 65, 1560, 950);

        private XnaRect TabRect(int i)
        {
            var p = PanelRect();
            return new(p.X + UiKitCanvas.Pad + i * 150, p.Y + 84, 150, UiKitCanvas.TabH);
        }

        private XnaRect ContentRect()
        {
            var p = PanelRect();
            int y = p.Y + 84 + UiKitCanvas.TabH + UiKitCanvas.Gap + 4;
            return new(p.X + UiKitCanvas.Pad, y, p.Width - UiKitCanvas.Pad * 2,
                p.Y + p.Height - UiKitCanvas.Pad - y);
        }

        private XnaRect NodeRect(int nx, int ny) => new(nx - 56, ny - 56 - 8, 112, 126);

        // Кольца гекс-сетки desktop: 0 центр, 1–5 ближнее (190/150), 6–11 дальнее (380/220)
        private static List<(int X, int Y)> HexPositions(int centerX, int centerY)
        {
            var positions = new List<(int X, int Y)> { (centerX, centerY) };
            for (int i = 0; i < 5; i++)
            {
                double angle = 2 * Math.PI * i / 5 - Math.PI / 2;
                positions.Add((centerX + (int)(Math.Cos(angle) * 190),
                    centerY + (int)(Math.Sin(angle) * 150)));
            }
            for (int i = 0; i < 6; i++)
            {
                double angle = 2 * Math.PI * i / 6 + Math.PI / 6;
                positions.Add((centerX + (int)(Math.Cos(angle) * 380),
                    centerY + (int)(Math.Sin(angle) * 220)));
            }
            return positions;
        }

        // ── Построение визуала (один раз) ──

        private void Build()
        {
            var p = PanelRect();

            _kit.Panel(UR(p));

            // Шапка
            _kit.Label(new URect(p.X + 20, p.Y + 14, 500, 40), "ДРЕВО НАВЫКОВ", 2,
                UiKitCanvas.Theme.GoldBright);
            _subText = _kit.Label(new URect(p.X + 20, p.Y + 48, 900, 22), "", 4,
                UiKitCanvas.Theme.TextDim);
            // Бейдж очков: bg + текст (скрываются вместе; Button создал бы
            // рамку отдельными сиблингами, которые нельзя спрятать)
            _badgeBg = _kit.RectImage(
                UR(new XnaRect(p.X + p.Width - 320, p.Y + 16, 300, 36)),
                UiKitCanvas.Theme.BgSlot);
            _badgeText = _kit.Centered(
                UR(new XnaRect(p.X + p.Width - 320, p.Y + 16, 300, 36)), "", 4,
                UiKitCanvas.Theme.Gold);

            // Вкладки
            _tabBg = new Image[4];
            _tabLabel = new Text[4];
            _tabMark = new Image[4];
            _tabRects = new XnaRect[4];
            for (int i = 0; i < 4; i++)
            {
                _tabRects[i] = TabRect(i);
                _tabBg[i] = _kit.Tab(UR(_tabRects[i]), "", false);
                _tabLabel[i] = _kit.Centered(UR(_tabRects[i]), "", 4, UiKitCanvas.Theme.TextDim);
                // Своя метка выбранной вкладки поверх рамки кита
                _tabMark[i] = _kit.RectImage(
                    new URect(_tabRects[i].X, _tabRects[i].Y + UiKitCanvas.TabH - 3,
                        _tabRects[i].Width, 3), UiKitCanvas.Theme.Gold);
                _tabMark[i].gameObject.SetActive(false);
            }

            // Скрытие вкладок: кит создаёт тени/рамки отдельными сиблингами
            // под корнем канваса, поэтому собираем ВСЕ новые элементы билдера
            var known = new HashSet<GameObject>();
            foreach (Transform t in _kit.Root) known.Add(t.gameObject);

            BuildClassTab();
            AddNew(_tabVis[0], known);
            for (int t = 1; t < 4; t++)
            {
                BuildCraftTab(t - 1);
                AddNew(_tabVis[t], known);
            }

            SetVisible(false);
        }

        // Новые (с момента прошлого снимка) прямые дети корня кита → в список видимости
        private void AddNew(List<GameObject> vis, HashSet<GameObject> known)
        {
            foreach (Transform t in _kit.Root)
                if (!known.Contains(t.gameObject))
                {
                    vis.Add(t.gameObject);
                    known.Add(t.gameObject);
                }
        }

        private void BuildClassTab()
        {
            var vis = _tabVis[0];
            var content = ContentRect();

            _treeEmpty = _kit.Label(
                new URect(content.X + 10, content.Y + 10, 700, 22),
                "У этого класса нет древа навыков.", 4, UiKitCanvas.Theme.TextMuted);
            vis.Add(_treeEmpty.gameObject);

            // Эволюция класса (видна всегда, если есть следующие ступени)
            var def = ClassData.Data(_game.Player.ClassKey);
            bool hasPromo = def.Next.Length > 0;
            int promoH = hasPromo ? 110 : 0;
            int treeH = content.Height - promoH;

            if (hasPromo)
            {
                int promoY = content.Y + treeH + UiKitCanvas.Gap;
                int promoRectH = promoH - UiKitCanvas.Gap;
                int x = content.X;
                int w = content.Width;

                vis.Add(_kit.Section(new URect(x, promoY, w, promoRectH),
                    "ЭВОЛЮЦИЯ КЛАССА").gameObject);

                // Бар эволюции: прогресс уровня персонажа до нужной ступени
                var bar = _kit.Bar(new URect(x + w - 330, promoY + 34, 310, 10),
                    UiKitCanvas.Theme.Gold);
                _promoBarFill = bar.fill;
                vis.Add(bar.bg.gameObject);
                vis.Add(((RectTransform)bar.fill.transform.parent).gameObject);
                _promoLevel = _kit.Label(new URect(x + w - 330, promoY + 46, 310, 18),
                    "", 4, UiKitCanvas.Theme.TextDim);
                vis.Add(_promoLevel.gameObject);

                _promoLock = _kit.Label(new URect(x + 230, promoY + 8, 500, 18),
                    "", 4, UiKitCanvas.Theme.Danger);
                vis.Add(_promoLock.gameObject);

                // Кнопки классов-наследников (пул 4)
                int bx = x + 10;
                for (int i = 0; i < 4; i++)
                {
                    var r = new XnaRect(bx, promoY + 30, 240, 52);
                    _promoRects[i] = r;
                    _promoBg[i] = _kit.Slot(UR(r));
                    _promoText[i] = _kit.Centered(UR(r), "", 3, UiKitCanvas.Theme.Text);
                    _promoGo[i] = _promoBg[i].gameObject;
                    vis.Add(_promoGo[i]);
                    vis.Add(_promoText[i].gameObject);
                    bx += r.Width + 12;
                }

                _promoHint = _kit.Label(
                    new URect(x + 10, promoY + promoRectH - 26, w - 20, 18),
                    "Клик — сменить класс (навыки и снаряжение сохраняются)", 4,
                    UiKitCanvas.Theme.TextMuted);
                vis.Add(_promoHint.gameObject);
            }

            // Узлы древа: фон + подписи (иконки skills/class в Unity не рисуем)
            int centerX = content.X + content.Width / 2;
            int centerY = content.Y + treeH / 2 - 8;
            var positions = HexPositions(centerX, centerY);
            for (int i = 0; i < 12; i++)
            {
                var (nx, ny) = positions[i];
                var r = NodeRect(nx, ny);
                _nodeBg[i] = _kit.Slot(UR(r));
                _nodeName[i] = _kit.Centered(
                    UR(new XnaRect(r.X, ny + 56 + 6, r.Width, 18)), "", 4,
                    UiKitCanvas.Theme.TextDim);
                _nodeReq[i] = _kit.Centered(
                    UR(new XnaRect(r.X, ny + 56 - 26, r.Width, 18)), "", 4,
                    UiKitCanvas.Theme.Text);
                vis.Add(_nodeBg[i].gameObject);
                vis.Add(_nodeName[i].gameObject);
                vis.Add(_nodeReq[i].gameObject);
            }

            _treeHint = _kit.Label(
                new URect(content.X + 10, content.Y + treeH - 26, content.Width - 20, 18),
                "Наведи курсор для подробностей · Клик — изучить (нужны очко навыков и уровень)",
                4, UiKitCanvas.Theme.TextMuted);
            vis.Add(_treeHint.gameObject);
        }

        private void BuildCraftTab(int idx)
        {
            var vis = _tabVis[idx + 1];
            var content = ContentRect();
            int x = content.X, y = content.Y, w = content.Width;

            _branchName[idx] = _kit.Label(new URect(x + 74, y + 10, w - 90, 24), "", 3,
                UiKitCanvas.Theme.GoldBright);
            _branchDesc[idx] = _kit.Label(new URect(x + 74, y + 36, w - 90, 18), "", 4,
                UiKitCanvas.Theme.TextDim);
            _branchLvl[idx] = _kit.Label(new URect(x + 74, y + 56, w - 90, 18), "", 4,
                UiKitCanvas.Theme.Text);

            var bar = _kit.Bar(new URect(x + 10, y + 84, w - 20, 12), UiKitCanvas.Theme.Xp);
            _branchBarFill[idx] = bar.fill;

            vis.Add(_branchName[idx].gameObject);
            vis.Add(_branchDesc[idx].gameObject);
            vis.Add(_branchLvl[idx].gameObject);
            vis.Add(bar.bg.gameObject);
            vis.Add(((RectTransform)bar.fill.transform.parent).gameObject);

            int listY = y + 136;
            for (int i = 0; i < _recipeBg.Length; i++)
            {
                var r = new XnaRect(x + 10, listY + i * 56, w - 20, 52);
                _recipeRects[i] = r;
                _recipeBg[i] = _kit.Slot(UR(r));
                _recipeName[i] = _kit.Label(new URect(r.X + 8, r.Y + 6, r.Width - 16, 20),
                    "", 4, UiKitCanvas.Theme.Text);
                _recipeMats[i] = _kit.Label(new URect(r.X + 8, r.Y + 28, r.Width - 16, 18),
                    "", 4, UiKitCanvas.Theme.Xp);
                vis.Add(_recipeBg[i].gameObject);
                vis.Add(_recipeName[i].gameObject);
                vis.Add(_recipeMats[i].gameObject);
            }
        }

        // ── Кадровое обновление: цвета/тексты/тултипы/клики ──

        private void Refresh()
        {
            var p = _game.Player;
            var def = ClassData.Data(p.ClassKey);

            // Шапка
            _subText.text = $"{def.Name}  ·  Уровень {p.Level}";
            bool hasPoints = p.SkillPoints > 0;
            _badgeBg.gameObject.SetActive(hasPoints);
            _badgeText.gameObject.SetActive(hasPoints);
            if (hasPoints) _badgeText.text = $"Очки навыков: {p.SkillPoints}";

            // Вкладки: подпись с уровнем ветки, клик меняет game.SkillTab
            int tab = Mathf.Clamp(_game.SkillTab, 0, 3);
            for (int i = 0; i < 4; i++)
            {
                string label = TabNames[i];
                if (i > 0)
                {
                    int lvl = p.CraftLevels.GetValueOrDefault(TabKeys[i], 1);
                    label += $" ({lvl})";
                }
                _tabLabel[i].text = label;
                bool sel = i == tab;
                _tabBg[i].color = sel
                    ? new UColor(64f / 255f, 52f / 255f, 28f / 255f)
                    : UiKitCanvas.Theme.BgSoft;
                _tabLabel[i].color = sel ? UiKitCanvas.Theme.GoldBright : UiKitCanvas.Theme.TextDim;
                _tabMark[i].gameObject.SetActive(sel);
                if (UiInput.Clicked(_tabRects[i]) && !sel)
                {
                    _game.SkillTab = i;
                    tab = i;
                }
            }

            // Видимость: элементы выбранной вкладки активны, остальные — нет
            // (до контента — отдельные элементы могут переопределить состояние)
            for (int i = 0; i < 4; i++)
                foreach (var go in _tabVis[i])
                    go.SetActive(i == tab);

            bool tooltipShown = false;
            if (tab == 0)
                tooltipShown = RefreshClassTab(def);
            else
                tooltipShown = RefreshCraftTab(tab - 1, p);

            if (!tooltipShown) _kit.HideTooltip();
        }

        private bool RefreshClassTab(ClassData.Def def)
        {
            var p = _game.Player;
            var content = ContentRect();
            bool tooltipShown = false;

            var nodes = SkillTree.GetNodes(p);
            _treeEmpty.gameObject.SetActive(nodes.Length == 0);
            if (nodes.Length == 0)
            {
                // Пустое древо: спрятать узлы и секцию эволюции
                for (int i = 0; i < 12; i++)
                {
                    _nodeBg[i].gameObject.SetActive(false);
                    _nodeName[i].gameObject.SetActive(false);
                    _nodeReq[i].gameObject.SetActive(false);
                }
                foreach (var go in _promoGo) go?.SetActive(false);
                foreach (var t in _promoText) t?.gameObject.SetActive(false);
                _promoHint?.gameObject.SetActive(false);
                _promoLevel?.gameObject.SetActive(false);
                return false;
            }

            // Эволюция класса
            var promoKeys = def.Next;
            bool hasPromo = promoKeys.Length > 0;
            int promoH = hasPromo ? 110 : 0;
            int treeH = content.Height - promoH;

            if (hasPromo)
            {
                int idx = Math.Min(p.ClassChanges, ClassData.ClassChangeLevels.Length - 1);
                int promoReqLevel = ClassData.ClassChangeLevels[idx];
                var promos = ClassData.AvailablePromotions(p);
                bool unlocked = promos.Length > 0;
                _promoLock.text = unlocked ? "" : $"доступно с уровня {promoReqLevel}";
                _promoLock.gameObject.SetActive(!unlocked);
                UiKitCanvas.SetBar(_promoBarFill,
                    Math.Clamp((double)p.Level / Math.Max(1, promoReqLevel), 0, 1));
                _promoLevel.text = $"Уровень {p.Level}/{promoReqLevel}";

                for (int i = 0; i < 4; i++)
                {
                    bool used = i < promoKeys.Length;
                    _promoGo[i].SetActive(used);
                    _promoText[i].gameObject.SetActive(used);
                    if (!used) continue;
                    var pk = promoKeys[i];
                    var pdef = ClassData.Get(pk);
                    _promoText[i].text = pdef?.Name ?? pk;
                    bool hot = unlocked && UiInput.Hot(_promoRects[i]);
                    // Ховер — сменой цвета плашки (BgSlot/BgSlotHot)
                    _promoBg[i].color = !unlocked ? UiKitCanvas.Theme.BgSlot
                        : hot ? UiKitCanvas.Theme.BgSlotHot : UiKitCanvas.Theme.BgSlot;
                    _promoText[i].color = !unlocked ? UiKitCanvas.Theme.TextMuted
                        : hot ? UiKitCanvas.Theme.GoldBright : UiKitCanvas.Theme.Text;
                    if (hot && UiInput.Clicked(_promoRects[i]))
                        _game.HandleSkillClick("promote:" + pk);
                }
            }

            // Узлы древа
            int centerX = content.X + content.Width / 2;
            int centerY = content.Y + treeH / 2 - 8;
            var positions = HexPositions(centerX, centerY);

            for (int i = 0; i < 12; i++)
            {
                bool used = i < nodes.Length;
                _nodeBg[i].gameObject.SetActive(used);
                _nodeName[i].gameObject.SetActive(used);
                _nodeReq[i].gameObject.SetActive(used);
                if (!used) continue;

                var node = nodes[i];
                bool learned = p.LearnedNodes.Contains(node.Key);
                bool available = !learned && p.Level >= node.Level && p.SkillPoints > 0;
                bool locked = p.Level < node.Level;

                var r = NodeRect(positions[i].X, positions[i].Y);
                bool hot = UiInput.Hot(r);

                // Заливка узла как на desktop; ховер — цветом слота кита
                UColor fill = learned
                    ? new UColor(70f / 255f, 110f / 255f, 66f / 255f)
                    : available ? new UColor(84f / 255f, 68f / 255f, 34f / 255f)
                    : new UColor(38f / 255f, 34f / 255f, 28f / 255f);
                if (hot) fill = learned ? UiKitCanvas.Theme.BgSlotSel : UiKitCanvas.Theme.BgSlotHot;
                _nodeBg[i].color = fill;

                _nodeName[i].text = node.Name.Length > 20 ? node.Name[..19] + "…" : node.Name;
                _nodeName[i].color = learned ? UiKitCanvas.Theme.Xp
                    : hot ? UiKitCanvas.Theme.GoldBright : UiKitCanvas.Theme.TextDim;

                _nodeReq[i].text = learned ? "изучено" : $"Ур.{node.Level}";
                _nodeReq[i].color = learned ? UiKitCanvas.Theme.Xp
                    : locked ? UiKitCanvas.Theme.Danger : UiKitCanvas.Theme.Text;

                if (hot)
                {
                    ShowNodeTooltip(r, node, learned, available, locked);
                    tooltipShown = true;
                    if (UiInput.Clicked(r) && !learned)
                        _game.HandleSkillClick("learn:" + node.Key);
                }
            }
            return tooltipShown;
        }

        private void ShowNodeTooltip(XnaRect anchor, SkillTree.Node node,
            bool learned, bool available, bool locked)
        {
            var lines = new List<string> { node.Desc };
            if (learned) lines.Add("● Изучено");
            else if (locked)
            {
                lines.Add($"Требуется уровень {node.Level}");
                lines.Add("● Заблокировано");
            }
            else if (_game.Player.SkillPoints < 1)
                lines.Add("Нет очков навыков");
            else
                lines.Add("● Доступно — кликните, чтобы изучить");
            _kit.ShowTooltip(UR(anchor), node.Name, lines);
        }

        // Вкладка ремесла: шапка ветки, XP-бар, список рецептов с тултипами
        private bool RefreshCraftTab(int idx, Entities.Player p)
        {
            string branchKey = TabKeys[idx + 1];
            if (!SkillTree.CraftBranches.TryGetValue(branchKey, out var branch))
                return false;

            int lvl = p.CraftLevels.GetValueOrDefault(branchKey, 1);
            int xp = p.CraftXp.GetValueOrDefault(branchKey, 0);
            int xpNext = branch.XpNext * lvl;

            _branchName[idx].text = branch.Name;
            _branchDesc[idx].text = branch.Desc;
            _branchLvl[idx].text = $"Уровень: {lvl}/25   Опыт: {xp}/{xpNext}";
            UiKitCanvas.SetBar(_branchBarFill[idx], xpNext > 0 ? (double)xp / xpNext : 0);

            bool tooltipShown = false;
            int row = 0;
            foreach (var (reqLevel, recipes) in branch.Recipes)
            {
                foreach (var recipeKey in recipes)
                {
                    if (row >= _recipeBg.Length) break;
                    var rdef = ItemData.Get(recipeKey);
                    if (rdef == null) continue;

                    // Рецепт открыт, когда и ветка, и уровень персонажа достаточны
                    bool byBranch = lvl >= reqLevel;
                    bool byChar = p.Level >= rdef.LevelReq;
                    bool unlocked = byBranch && byChar;
                    string lockText = !byChar ? $"нужен ур. {rdef.LevelReq}"
                        : !byBranch ? $"нужна ветка ур. {reqLevel}" : "";

                    var rect = _recipeRects[row];
                    bool hot = UiInput.Hot(rect);
                    // Ховер — цветом слота; закрытые рецепты — тёмная подложка
                    _recipeBg[row].color = unlocked
                        ? (hot ? UiKitCanvas.Theme.BgSlotHot : UiKitCanvas.Theme.BgSlot)
                        : UiKitCanvas.Theme.BgSoft;
                    _recipeName[row].text = unlocked ? rdef.Name : $"{rdef.Name}  ({lockText})";
                    _recipeName[row].color = unlocked
                        ? (hot ? UiKitCanvas.Theme.GoldBright : UiKitCanvas.Theme.Text)
                        : UiKitCanvas.Theme.TextMuted;

                    var variants = ItemData.RecipesOf(recipeKey);
                    if (variants.Count > 0)
                    {
                        string mats = string.Join(", ", variants[0].Materials
                            .Select(m => $"{ItemData.DisplayName(m.Key)}×{m.Value}"));
                        bool afford = variants[0].Materials.All(m =>
                            p.Inventory.GetValueOrDefault(m.Key) >= m.Value);
                        _recipeMats[row].text = mats;
                        _recipeMats[row].color = unlocked
                            ? (afford ? UiKitCanvas.Theme.Xp : UiKitCanvas.Theme.Danger)
                            : UiKitCanvas.Theme.TextMuted;
                    }
                    else
                        _recipeMats[row].text = "";

                    if (hot)
                    {
                        var lines = new List<string>();
                        if (variants.Count > 0)
                            foreach (var m in variants[0].Materials)
                                lines.Add($"  {ItemData.DisplayName(m.Key)}×{m.Value} " +
                                    $"(есть {p.ItemCount(m.Key)})");
                        if (!string.IsNullOrEmpty(rdef.Desc))
                            lines.Add(rdef.Desc);
                        _kit.ShowTooltip(UR(rect), rdef.Name, lines);
                        tooltipShown = true;
                    }
                    row++;
                }
                if (row >= _recipeBg.Length) break;
            }
            // Неиспользованные строки пула прячутся (поверх видимости вкладки)
            for (int i = row; i < _recipeBg.Length; i++)
            {
                _recipeBg[i].gameObject.SetActive(false);
                _recipeName[i].gameObject.SetActive(false);
                _recipeMats[i].gameObject.SetActive(false);
            }
            return tooltipShown;
        }

        // XnaRect → URect (визуал кита живёт в UnityEngine.Rect)
        private static URect UR(XnaRect r) => new((float)r.X, (float)r.Y, r.Width, r.Height);

        private void Update() => Refresh();
    }
}
