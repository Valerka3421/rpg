using Aethelgard.Render;
using System.Linq;
using Aethelgard.Systems;
using UnityEngine;
using UnityEngine.UI;
using XnaMouse = Microsoft.Xna.Framework.Input.MouseState;
using Screens = Aethelgard.Systems.ScreenMode;

namespace Aethelgard.UI;

/// <summary>
/// Дерево навыков — улучшенная версия (порт Render/SkillScreen.cs):
/// 4 таба (Класс / Ковка / Алхимия / Инженерия), 12 узлов класса,
/// расположенных вокруг центрального «Путь»-узла, PNG-иконки из
/// Resources/SkillIcons, линии-связи между уровнями, тултипы с эффектами,
/// бейдж доступных очков, состояния узлов: изучен / доступен / заблокирован.
/// Промоушен класса на 5/15 уровне (ClassChangeLevels).
/// </summary>
public class SkillUI
{
    private readonly UIManager _ui;
    private readonly UITheme _t;
    private readonly GameController _game;

    public RectTransform Root = null!;
    private GameObject? _panel;
    private string _sig = "";
    private string? _learnCmd;
    private string? _promoteCmd;
    private bool _close;
    private static Sprite? _dotSprite;

    public SkillUI(UIManager ui)
    {
        _ui = ui; _t = ui.Theme; _game = ui.Game;
    }

    public void Build()
    {
        var go = UiKit.Node("Skills", null);
        Root = UiKit.Attach(go, _ui.Root);
        Root.gameObject.SetActive(false);
    }

    public void Draw(XnaMouse mouse, XnaMouse prevMouse)
    {
        var p = _game.Player;
        if (p == null) return;
        if (_game.SkillTab >= 1) _skillsTab = _game.SkillTab;
        string sig = $"{_game.SkillTab}|{p.SkillPoints}|{p.Level}|{p.LearnedNodes.Count}|" +
                     $"{CraftBranchLevel(p)}";
        if (sig != _sig)
        {
            _sig = sig;
            Rebuild(p);
        }

        if (_learnCmd != null)
        {
            string cmd = _learnCmd; _learnCmd = null;
            _game.HandleSkillClick(cmd);
            _sig = "";
        }
        if (_promoteCmd != null)
        {
            string cmd = _promoteCmd; _promoteCmd = null;
            _game.HandleSkillClick(cmd);
            _sig = "";
        }
        if (_close) { _close = false; _game.ScreenMode = Screens.Game; _sig = ""; }
    }

    private int _skillsTab;

    private int CraftBranchLevel(Player p)
    {
        var branches = SkillTree.CraftBranches;
        if (_game.SkillTab < 1) return 0;
        var keys = RecipeData.CraftTabs;
        if (_game.SkillTab - 1 >= keys.Length) return 0;
        string key = keys[_game.SkillTab - 1].Key;
        var br = branches.GetValueOrDefault(key);
        return br?.Level ?? 1;
    }

    private void Rebuild(Player p)
    {
        if (_panel != null) UnityEngine.Object.Destroy(_panel);
        _panel = UiKit.Panel(Root, 180, 40, 1560, 1000, _t, "SkillPanel");
        var parent = _panel.transform;

        // Шапка: название, очки, бейдж
        UiKit.Label(parent, 0, 24, 1560, 40, "НАВЫКИ",
            _t.H2Size, _t.Gold, _t, TextAnchor.MiddleCenter, "Title");
        var badge = UiKit.Panel(parent, 1290, 24, 240, 44, _t, "PointsBadge");
        UiKit.Label(badge.transform, 0, 0, 240, 44,
            $"ОЧКОВ: {p.SkillPoints}", _t.BodySize,
            p.SkillPoints > 0 ? _t.Gold : _t.InkFaint, _t,
            TextAnchor.MiddleCenter, "BadgeText");

        // Табы
        var tabs = new[] { "КЛАСС", "КОВКА", "АЛХИМИЯ", "ИНЖЕНЕРИЯ" };
        int tx = 80;
        for (int i = 0; i < tabs.Length; i++)
        {
            int idx = i;
            bool active = _game.SkillTab == i;
            var btn = UiKit.WidgetButton(parent, tx, 80, 340, 46,
                active ? tabs[i] : tabs[i].ToLowerInvariant(), _t.BodySize, _t);
            btn.onClick.AddListener(() => { _game.SkillTab = idx; _sig = ""; });
            if (active) UiKit.Solid(btn.transform, 0, 0, 340, 6, _t.GoldSoft, "ActiveLine");
            tx += 360;
        }

        if (_game.SkillTab == 0) DrawClassTree(parent, p);
        else DrawCraftBranch(parent, p, _game.SkillTab - 1);

        // Промоушен класса
        var cls = ClassData.Get(p.ClassKey);
        if (cls != null && cls.Next is { Length: > 0 } nexts &&
            ClassData.ClassChangeLevels.Contains(p.ClassChanges == 0 ? 5 : 15))
        {
            int py = 880;
            UiKit.Label(parent, 60, py, 1000, 24,
                $"ПЕРЕХОД НА НОВЫЙ КЛАСС (ур. {(p.ClassChanges == 0 ? 5 : 15)}):",
                _t.SmallSize, _t.GoldSoft, _t, TextAnchor.UpperLeft, "PromoTitle");
            int nx = 60;
            foreach (var nk in nexts)
            {
                var nd = ClassData.Get(nk);
                if (nd == null) continue;
                var btn = UiKit.WidgetButton(parent, nx, py + 30, 400, 48,
                    $"{nd.Name} — {nd.Perk}", _t.TinySize, _t);

                string captured = nk;
                btn.onClick.AddListener(() =>
                    _promoteCmd = $"promote:{captured}");
                btn.interactable = p.Level >= (p.ClassChanges == 0 ? 5 : 15);
                nx += 420;
            }
        }

        var close = UiKit.WidgetButton(parent, 630, 900, 300, 46, "Закрыть (Esc)",
            _t.SmallSize, _t);
        close.onClick.AddListener(() => _close = true);
    }

    // ── Классовое дерево: центральный узел + 12 вокруг, линии-связи ──

    private void DrawClassTree(Transform parent, Player p)
    {
        var nodes = SkillTree.GetNodes(p).OrderBy(n => n.Level).ToArray();
        var cls = ClassData.Get(p.ClassKey);
        Color accent = cls?.Color.U() ?? _t.GoldSoft;

        // Центр дерева
        const int cx = 760, cy = 460;

        // Линии-связи: узлы выстроены по уровням (1,3,5,7,9,12...) —
        // соединяем последовательно лёгкими линиями
        var positions = new (SkillTree.Node node, Vector2 pos)[nodes.Length];
        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            double angle = (n.Position % 8) / 8f * 360f;
            float rad = (float)(angle * Mathf.Deg2Rad);
            int ring = i < 8 ? 150 : 260;
            positions[i] = (n, new Vector2(
                cx + Mathf.Cos(rad) * ring,
                cy + Mathf.Sin(rad) * ring));
        }
        // Линии между соседними узлами по уровням
        for (int i = 1; i < positions.Length; i++)
        {
            var a = positions[i - 1].pos;
            var b = positions[i].pos;
            DrawLine(parent, a, b,
                p.LearnedNodes.Contains(nodes[i].Key) ? accent : _t.Bronze, 2);
        }

        // Узлы
        foreach (var (node, pos) in positions)
        {
            DrawNode(parent, p, node, pos, accent);
        }
    }

    private void DrawNode(Transform parent, Player p, SkillTree.Node node,
        Vector2 pos, Color accent)
    {
        bool learned = SkillTree.HasNode(p, node.Key);
        bool canLearn = !learned && p.Level >= node.Level && p.SkillPoints > 0;
        bool blocked = !learned && p.Level < node.Level;

        const int size = 96;
        var holder = UiKit.Node("Node_" + node.Key, null);
        UiKit.Place(holder, parent, (int)(pos.x - size / 2),
            (int)(1000 - pos.y - size / 2), size, size);
        UiKit.Img(holder, learned ? _t.RowSelect : blocked ? _t.Inset : _t.RowHover);
        UiKit.Frame((RectTransform)holder.transform,
            learned ? accent : canLearn ? _t.GoldSoft : _t.Bronze,
            learned || canLearn ? 2 : 1);

        // Иконка из Resources (parity: assets/skills/class/<class>_<idx>.png)
        var icon = LoadSkillIcon(p.ClassKey, node.Position);
        if (icon != null)
        {
            var imgGo = UiKit.Node("Icon", null);
            var irt = UiKit.Place(imgGo, holder.transform, 18, 18, 60, 60);
            var img = imgGo.AddComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;
            img.color = blocked
                ? new Color(0.4f, 0.4f, 0.4f)
                : Color.white;
            img.raycastTarget = false;
        }
        else
        {
            UiKit.Label(holder.transform, 0, 0, size, size,
                node.Level.ToString(), _t.H2Size,
                blocked ? _t.InkFaint : accent, _t, TextAnchor.MiddleCenter, "Lvl");
        }

        // Имя под узлом
        UiKit.Label(holder.transform, -60, size, 220, 20,
            learned ? node.Name : $"{node.Name} (ур.{node.Level})",
            _t.TinySize, learned ? _t.Ink : _t.InkDim, _t,
            TextAnchor.MiddleCenter, "NodeName");

        // Клик — изучить
        var btn = holder.AddComponent<Button>();
        btn.onClick.AddListener(() =>
        {
            if (canLearn) _learnCmd = $"learn:{node.Key}";
        });

        // Тултип с эффектами
        string effects = node.Effects != null && node.Effects.Count > 0
            ? string.Join("\n", node.Effects.Select(e =>
                $"• {EffectName(e.Key)}: {(e.Value >= 0.5 ? "+" : "")}" +
                (e.Value < 0.5
                    ? $"{Mathf.RoundToInt((float)(e.Value * 100))}%"
                    : $"+{e.Value}")))
            : node.Perk ?? "";
        var tip = UiKit.Label(holder.transform, -80, -110, 260, 100,
            $"<b>{node.Name}</b>\nУр. {node.Level}\n{effects}\n" +
            (learned ? "<i>Изучено</i>" : canLearn ? "<i>Нажмите, чтобы изучить</i>"
                : "<i>Недоступно</i>"),
            _t.TinySize, _t.Ink, _t, TextAnchor.UpperLeft, "Tooltip");
        tip.gameObject.SetActive(false);
        tip.supportRichText = true;
        var trigger = holder.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var entry = new UnityEngine.EventSystems.EventTrigger.Entry
        { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ => tip.gameObject.SetActive(true));
        trigger.triggers.Add(entry);
        var exit = new UnityEngine.EventSystems.EventTrigger.Entry
        { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => tip.gameObject.SetActive(false));
        trigger.triggers.Add(exit);
    }

    private void DrawLine(Transform parent, Vector2 a, Vector2 b, Color c, int thickness)
    {
        var go = UiKit.Node("Link", null);
        var rt = UiKit.Attach(go, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); // точка для линий дерева
        Vector2 mid = (a + b) / 2f;
        // Координаты: дизайн (x, y вверх) → anchoredPosition
        rt.anchoredPosition = new Vector2(mid.x - 780, 500 - mid.y); // панель 1560×1000
        float len = Vector2.Distance(a, b);
        rt.sizeDelta = new Vector2(len, thickness);
        rt.pivot = new Vector2(0.5f, 0.5f);
        float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
        rt.localRotation = Quaternion.Euler(0, 0, ang);
        UiKit.Img(go, c);
        go.GetComponent<Image>().raycastTarget = false;
    }

    private static Sprite? LoadSkillIcon(string classKey, int idx)
    {
        string name = $"{classKey}_{idx}";
        var sp = Resources.Load<Sprite>($"SkillIcons/Class/{name}");
        if (sp == null)
        {
            var tex = Resources.Load<Texture2D>($"SkillIcons/Class/{name}");
            if (tex != null)
                sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 64f);
        }
        return sp;
    }

    private static string EffectName(string key) => key switch
    {
        "atk" => "Атака",
        "df" => "Защита",
        "hp" => "Здоровье",
        "mana" => "Мана",
        "crit" => "Крит. удар",
        "dodge" => "Уклонение",
        "lifesteal" => "Вампиризм",
        "attack_speed" => "Скорость атаки",
        "move_speed" => "Скорость движения",
        "gold_pct" => "Золото",
        "xp_pct" => "Опыт",
        "craft_xp" => "Опыт ремёсел",
        "thorns" => "Шипы",
        "summon" => "Призыв",
        _ => key,
    };

    // ── Ремесленные ветки (Ковка/Алхимия/Инженерия) ──

    private void DrawCraftBranch(Transform parent, Player p, int tab)
    {
        var branches = SkillTree.CraftBranches;
        var tabs = RecipeData.CraftTabs;
        if (tab >= tabs.Length) return;
        var br = branches.GetValueOrDefault(tabs[tab].Key);
        if (br == null) return;
        int lvl = br.Level;
        int xp = br.Xp;
        int xpNext = Math.Max(1, br.XpNext);

        var accent = _t.GoldSoft;

        UiKit.Label(parent, 80, 160, 800, 30, br.Name.ToUpperInvariant(),
            _t.H2Size, accent, _t, TextAnchor.UpperLeft, "BrName");
        UiKit.Label(parent, 80, 200, 900, 24, br.Desc, _t.SmallSize, _t.InkDim, _t,
            TextAnchor.UpperLeft, "BrDesc");

        // 25 уровней — 25 пипсов, текущий уровень золотой
        const int px0 = 80, py0 = 250, pip = 44;
        for (int i = 0; i < 25; i++)
        {
            int col = i % 13, row = i / 13;
            var pipGo = UiKit.Node("Pip" + i, null);
            UiKit.Place(pipGo, parent, px0 + col * pip, py0 + row * pip, 34, 34);
            UiKit.Img(pipGo, i < lvl ? accent : _t.Inset);
            UiKit.Frame((RectTransform)pipGo.transform,
                i < lvl ? _t.Gold : _t.Bronze, 1);
        }

        var xpBar = UiKit.Bar(parent, 80, 360, 560, 26, _t, accent);
        xpBar.fill.rectTransform.anchorMax = new Vector2(
            Mathf.Clamp01((float)xp / xpNext), 1f);
        xpBar.overlay.text = $"УР. {lvl} · {xp}/{xpNext} XP";

        // Рецепты по уровням ветки
        int y = 420;
        foreach (var kv in br.Recipes.OrderBy(kv => kv.Key))
        {
            bool unlocked = lvl >= kv.Key;
            UiKit.Label(parent, 80, y, 900, 22,
                $"Ур. {kv.Key}: {string.Join(", ", kv.Value.Select(k => ItemData.DisplayName(k)))}",
                _t.SmallSize, unlocked ? _t.Ink : _t.InkFaint, _t,
                TextAnchor.UpperLeft, "R" + kv.Key);
            y += 26;
        }
    }
}
