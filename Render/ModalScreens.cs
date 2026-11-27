using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Aethelgard.Render;

/// <summary>
/// Базовая рамка модального окна. Панели/текст/полосы рисует UiKit —
/// база хранит только устройство, спрайт-батч и шрифт (LoadFont —
/// внешний контракт, вызывается Desktop/Game.cs для всех экранов).
/// </summary>
public abstract class ModalScreenBase
{
    protected readonly GraphicsDevice _device;
    protected readonly SpriteBatch _sb;
    protected SpriteFont? _font;

    /// <summary>Общий экземпляр UiKit для всех наследников.</summary>
    protected static readonly UiKit K = UiKit.Kit;

    protected ModalScreenBase(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device; _sb = sb;
    }

    public void LoadFont(SpriteFont font) => _font = font;
}

/// <summary>
/// Экран крафта: вкладки веток (all + 3), список рецептов с тултипами
/// (материалы, что получится), партия [–], мини-игра точности (Space)
/// и качество +N (parity с craft_controller.py).
/// </summary>
public class CraftScreen : ModalScreenBase
{
    public CraftScreen(GraphicsDevice device, SpriteBatch sb) : base(device, sb) { }

    public void Draw(GameController game, MouseState mouse)
    {
        K.Begin(_sb, _device, mouse);

        var panel = new Rectangle(210, 60, 1500, 840);
        K.Panel(panel);

        // ── Шапка: заголовок + вкладки веток ──
        K.Text("КРАФТ", new Vector2(panel.X + 24, panel.Y + 16),
            UiKit.Theme.GoldBright, 2);

        var tabs = RecipeData.CraftTabs;
        string[] labels = tabs.Select(t => t.Name).ToArray();
        int clicked = K.Tabs(panel.X + 170, panel.Y + 14, labels, game.CraftTab, 140);
        if (clicked >= 0 && clicked != game.CraftTab)
            game.CraftTab = clicked;

        // Информация о ветке и партии — под вкладками
        int y = panel.Y + 14 + UiKit.TabH + 10;
        K.Text($"Партия: {game.CraftQty}  ([ / ])   Ветка: {CraftBranchInfo(game)}",
            new Vector2(panel.X + 24, y), UiKit.Theme.TextDim, 4, shadow: false);
        y += 30;

        if (game.MinigameActive)
        {
            DrawMinigame(game, panel);
            K.End();
            K.EndInputFrame();
            return;
        }

        // ── Список рецептов: гейт прогрессии — уровень персонажа (LevelReq)
        //    + уровень ветки крафта (SkillReq) + чертёж ──
        var list = game.CraftableList();
        int pageStart = game.CraftPage * Config.CraftPageSize;
        int maxPage = Math.Max(0, (list.Count - 1) / Config.CraftPageSize);
        if (game.CraftPage > maxPage) game.CraftPage = maxPage;

        for (int i = 0; i < Config.CraftPageSize; i++)
        {
            int idx = pageStart + i;
            if (idx >= list.Count) break;
            var (key, def) = list[idx];
            string branch = ItemData.CategoryOf(key);
            int branchLevel = game.Player.CraftLevels.GetValueOrDefault(branch, 1);
            bool byBranch = def.SkillReq <= branchLevel;
            bool byChar = game.Player.Level >= def.LevelReq;
            bool byBlueprint = string.IsNullOrEmpty(def.BlueprintRequired)
                || game.Player.Blueprints.Contains(def.BlueprintRequired);
            bool canCraft = byBranch && byChar && byBlueprint;

            string lockText = !byChar ? $"нужен ур. {def.LevelReq}"
                : !byBranch ? $"нужна ветка ур. {def.SkillReq}"
                : !byBlueprint ? "нужен чертёж" : "";

            var variants = ItemData.RecipesOf(key);
            bool afford = variants.Count == 0 || CanAfford(game, variants[0].Materials);

            int rowY = y + i * 62;
            var rowRect = new Rectangle(panel.X + 20, rowY, panel.Width - 40, 56);

            // Строка рецепта: цветная плашка-иконка, имя, требования справа
            bool hot = K.Hoverable(rowRect);
            Color bg = canCraft
                ? (hot ? UiKit.Theme.BgSlotHot : new Color(30, 35, 25, 140))
                : UiKit.Theme.BgSlot;
            FillRect(rowRect, bg);
            FrameRect(rowRect, hot ? new Color(UiKit.Theme.Gold, 150) : UiKit.Theme.BorderSoft);
            FillRect(new Rectangle(rowRect.X, rowRect.Y, 3, rowRect.Height),
                canCraft ? (afford ? UiKit.Theme.Xp : UiKit.Theme.Gold)
                         : UiKit.Theme.TextMuted);

            FillRect(new Rectangle(rowRect.X + 12, rowRect.Y + 11, 34, 34),
                ItemData.DisplayColor(key));
            K.Text(ItemData.DisplayName(key) + (canCraft ? "" : $"  [закрыто: {lockText}]"),
                new Vector2(rowRect.X + 56, rowRect.Y + 4),
                canCraft ? UiKit.Theme.Text : UiKit.Theme.TextMuted, 3);

            // Требования — отдельная колонка справа
            string reqs = $"Ветка ур. {def.SkillReq} · Tier {def.Tier} · {BranchName(branch)}";
            K.Text(reqs, new Vector2(rowRect.Right - K.FontWidth(reqs, 4) - 12, rowRect.Y + 8),
                byBranch ? UiKit.Theme.TextDim : UiKit.Theme.Danger, 4, shadow: false);

            // Ингредиенты — на второй строке (зелёные если хватает, красные если нет)
            if (variants.Count > 0)
            {
                string mats = string.Join(", ", variants[0].Materials
                    .Select(m => $"{ItemData.DisplayName(m.Key)}×{m.Value}"));
                K.Text(mats, new Vector2(rowRect.X + 56, rowRect.Y + 30),
                    canCraft && afford ? UiKit.Theme.Xp : UiKit.Theme.Danger, 4, shadow: false);
            }

            // Тултип: полный список материалов + что получится
            if (K.Hoverable(rowRect))
            {
                var lines = new List<(string, Color)>();
                if (variants.Count > 0)
                {
                    foreach (var m in variants[0].Materials)
                    {
                        int have = game.Player.ItemCount(m.Key);
                        lines.Add(($"  {ItemData.DisplayName(m.Key)}×{m.Value} (есть {have})",
                            have >= m.Value ? UiKit.Theme.Xp : UiKit.Theme.Danger));
                    }
                }
                if (!string.IsNullOrEmpty(def.Desc))
                    lines.Add((def.Desc, UiKit.Theme.TextDim));
                if (!canCraft)
                    lines.Add(($"Требуется: {lockText}", UiKit.Theme.Danger));
                K.Tooltip(rowRect, ItemData.DisplayName(key), lines);
            }
        }

        K.Text($"Стр. {game.CraftPage + 1}/{maxPage + 1}  (←/→)   Space/1–8: крафт   K/Esc: выход",
            new Vector2(panel.X + 24, panel.Bottom - 36), UiKit.Theme.TextMuted, 4, shadow: false);

        K.End();
        K.EndInputFrame();
    }

    // Вспомогательные примитивы (тень/рамка поверх UiKit-палитры)
    private void FillRect(Rectangle r, Color c) => _sb.Draw(KBlank, r, c);

    private Texture2D? _blank;
    private Texture2D KBlank
    {
        get
        {
            if (_blank == null)
            {
                _blank = new Texture2D(_device, 1, 1);
                _blank.SetData(new[] { Color.White });
                GpuReset.Register(_device, () =>
                {
                    try { _blank?.Dispose(); } catch { }
                    _blank = null;
                });
            }
            return _blank;
        }
    }

    private void FrameRect(Rectangle r, Color c)
    {
        FillRect(new Rectangle(r.X, r.Y, r.Width, 1), c);
        FillRect(new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
        FillRect(new Rectangle(r.X, r.Y, 1, r.Height), c);
        FillRect(new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
    }

    private string BranchName(string branch) =>
        RecipeData.Skills.TryGetValue(branch, out var s) ? s.Name : branch;

    private bool CanAfford(GameController game, Dictionary<string, int> mats) =>
        mats.All(m => game.Player.ItemCount(m.Key) >= m.Value);

    private string CraftBranchInfo(GameController game)
    {
        string? branch = game.CraftTab == 0 ? null : RecipeData.SkillOrder[game.CraftTab - 1];
        if (branch == null) return "все";
        int lvl = game.Player.CraftLevels.GetValueOrDefault(branch, 1);
        int xp = game.Player.CraftXp.GetValueOrDefault(branch);
        return $"{BranchName(branch)} ур. {lvl} ({xp}/{RecipeData.SkillXpToNext(lvl)})";
    }

    /// <summary>Мини-игра точности: бегающий индикатор, Space — остановить.</summary>
    private void DrawMinigame(GameController game, Rectangle panel)
    {
        int y = panel.Y + 150;
        K.Text($"Крафт: {ItemData.DisplayName(game.MinigameItem ?? "")}",
            new Vector2(panel.X + 40, y), UiKit.Theme.GoldBright, 2);
        y += 40;
        K.Text("Останови индикатор в центре (Space). Точность → качество +N",
            new Vector2(panel.X + 40, y), UiKit.Theme.TextDim, 4, shadow: false);
        y += 34;

        // Чёткая полоса: тёмная основа + вложенные зоны качества
        // (0.42 «хорошо» / 0.72 «отлично» / 0.93 «идеал») — чем уже зона, тем золотее
        int barX = panel.X + 60, barW = panel.Width - 120, barH = 30;
        FillRect(new Rectangle(barX, y, barW, barH), new Color(24, 21, 17));
        FrameRect(new Rectangle(barX, y, barW, barH), UiKit.Theme.Border);
        K.Bar(barX + (int)(barW * 0.5 - barW * 0.5 * 0.42), y, (int)(barW * 0.42), barH, 1,
            new Color(72, 62, 36));
        K.Bar(barX + (int)(barW * 0.5 - barW * 0.5 * 0.72), y, (int)(barW * 0.72), barH, 1,
            new Color(130, 106, 48));
        K.Bar(barX + (int)(barW * 0.5 - barW * 0.5 * 0.93), y, (int)(barW * 0.93), barH, 1,
            new Color(196, 158, 74));

        // Легенда зон под полосой
        K.Text("хорошо", new Vector2(barX + 8, y + barH + 8),
            new Color(150, 135, 95), 4, shadow: false);
        string ideal = "идеал";
        K.Text(ideal, new Vector2(barX + barW / 2 - K.FontWidth(ideal, 4) / 2, y + barH + 8),
            UiKit.Theme.GoldBright, 4, shadow: false);
        K.Text("отлично", new Vector2(barX + barW - K.FontWidth("отлично", 4) - 8, y + barH + 8),
            new Color(196, 158, 74), 4, shadow: false);

        // Индикатор — белая риска с тенью, заметная на любом фоне
        y -= 6;
        int ix = barX + (int)(barW * game.MinigamePos) - 3;
        FillRect(new Rectangle(ix + 1, y - 6 + 2, 6, barH + 12), UiKit.Theme.Shadow);
        FillRect(new Rectangle(ix, y - 6, 6, barH + 12), UiKit.Theme.Text);
        FillRect(new Rectangle(ix, y - 6, 6, 2), UiKit.Theme.GoldBright);
        y += barH + 56;

        K.Text($"Партия: {game.CraftQty} × {ItemData.DisplayName(game.MinigameItem ?? "")}",
            new Vector2(panel.X + 40, y), UiKit.Theme.TextDim, 4, shadow: false);
        K.Text("Space: остановить   Esc: отмена",
            new Vector2(panel.X + 40, y + 24), UiKit.Theme.TextMuted, 4, shadow: false);
    }
}

/// <summary>
/// Экран характеристик (C): распределение свободных очков (↑↓/1–6/Enter).
/// Характеристики — строки с hover и полосами, боевые показатели — секции.
/// </summary>
public class StatsScreen : ModalScreenBase
{
    public StatsScreen(GraphicsDevice device, SpriteBatch sb) : base(device, sb) { }

    private static readonly (string Key, string Label)[] Stats =
    {
        ("str", "Сила (ATK)"), ("agi", "Ловкость (уклонение)"),
        ("vit", "Телосложение (HP)"), ("luk", "Удача (крит)"),
        ("df_stat", "Защита (DEF)"), ("int", "Интеллект (мана/магия)"),
    };

    public void Draw(GameController game, MouseState mouse)
    {
        K.Begin(_sb, _device, mouse);

        var panel = new Rectangle((AethelgardGame.ScreenW - 900) / 2,
            (AethelgardGame.ScreenH - 800) / 2, 900, 800);
        K.Panel(panel);

        var p = game.Player;

        // ── Шапка: класс, уровень, свободные очки ──
        K.Text($"{p.ClassName}  ·  Ур. {p.Level}", new Vector2(panel.X + 24, panel.Y + 16),
            UiKit.Theme.GoldBright, 2);
        K.Text($"Свободных очков: {p.FreePoints}    Очков навыков: {p.SkillPoints}",
            new Vector2(panel.X + 24, panel.Y + 50),
            p.FreePoints > 0 ? UiKit.Theme.Xp : UiKit.Theme.TextDim, 4, shadow: false);

        int y = panel.Y + 78;

        // ── Характеристики: строки с hover-выбором и полосой относительного значения ──
        int[] values = { p.TotalStr, p.TotalAgi, p.TotalVit, p.TotalLuk, p.TotalDfStat, p.TotalInt };
        int maxVal = Math.Max(1, values.Max());
        for (int i = 0; i < Stats.Length; i++)
        {
            int rowH = 44;
            var rowRect = new Rectangle(panel.X + 16, y, panel.Width - 32, rowH);

            // Наведение мыши выбирает строку (клавиатура — GameController)
            if (rowRect.Contains(mouse.Position))
                game.StatSelected = i;
            bool selected = game.StatSelected == i;

            FillRect(rowRect, selected ? UiKit.Theme.BgSlotSel : UiKit.Theme.BgSlot);
            FrameRect(rowRect, selected ? new Color(UiKit.Theme.Gold, 150) : UiKit.Theme.BorderSoft);
            FillRect(new Rectangle(rowRect.X, rowRect.Y, 3, rowRect.Height),
                selected ? UiKit.Theme.Gold : new Color(UiKit.Theme.Gold, 60));

            K.Text($"{i + 1}. {Stats[i].Label}", new Vector2(rowRect.X + 14, y + 4),
                selected ? UiKit.Theme.GoldBright : UiKit.Theme.Text, 3);

            // Полоса относительного значения + число справа
            K.Bar(rowRect.Right - 220, y + 10, 120, 14, (double)values[i] / maxVal,
                selected ? UiKit.Theme.Gold : UiKit.Theme.Gold * 0.7f);
            string val = values[i].ToString();
            K.Text(val, new Vector2(rowRect.Right - 14 - K.FontWidth(val, 3), y + 6),
                selected ? UiKit.Theme.GoldBright : UiKit.Theme.Text, 3);

            y += rowH + 4;
        }

        // ── Секция «Боевая готовность»: HP/MP полосами + производные ──
        y += 10;
        var combatRect = new Rectangle(panel.X + 16, y, panel.Width - 32, 108);
        K.Section(combatRect, "Боевая готовность");
        int cx = combatRect.X + 14, cy = combatRect.Y + 30, cw = combatRect.Width - 28;
        K.Text($"HP {p.Hp}/{p.MaxHp}", new Vector2(cx, cy), UiKit.Theme.Hp, 4, shadow: false);
        K.Bar(cx + 150, cy + 2, cw - 150, 14, (double)p.Hp / Math.Max(1, p.MaxHp), UiKit.Theme.Hp);
        cy += 26;
        K.Text($"MP {p.Mana}/{p.MaxMana}", new Vector2(cx, cy), UiKit.Theme.Mana, 4, shadow: false);
        K.Bar(cx + 150, cy + 2, cw - 150, 14, (double)p.Mana / Math.Max(1, p.MaxMana), UiKit.Theme.Mana);
        cy += 28;
        K.Text($"ATK {p.Atk}   DEF {p.Df}   Уклонение {p.DodgeChance * 100:F0}%   Крит {p.CritChance * 100:F0}%",
            new Vector2(cx, cy), UiKit.Theme.Text, 4, shadow: false);
        y = combatRect.Bottom + UiKit.Gap;

        // ── Секция «Перки» ──
        var perkRect = new Rectangle(panel.X + 16, y, panel.Width - 32, 66);
        K.Section(perkRect, "Перки");
        var allPerks = new (string Name, bool On)[]
        {
            ("Двойной удар", p.DoubleStrike), ("Регенерация", p.Regen),
            ("Вампиризм", p.Vampire), ("Шипы", p.Thorns), ("Жадность", p.Greedy),
        };
        string[] perks = allPerks.Where(x => x.On).Select(x => x.Name).ToArray();
        K.Text(perks.Length > 0 ? string.Join(" · ", perks) : "нет",
            new Vector2(perkRect.X + 14, perkRect.Y + 28),
            perks.Length > 0 ? UiKit.Theme.Xp : UiKit.Theme.TextMuted, 4, shadow: false);
        y = perkRect.Bottom + UiKit.Gap;

        // ── Секция «Навыки ремесла»: уровни веток крафта с полосами XP ──
        int skillRows = (RecipeData.SkillOrder.Length + 1) / 2;
        var skillRect = new Rectangle(panel.X + 16, y, panel.Width - 32, 26 + skillRows * 30 + 8);
        K.Section(skillRect, "Навыки ремесла");
        int col0 = skillRect.X + 14;
        int col1 = skillRect.X + skillRect.Width / 2 + 8;
        int halfW = skillRect.Width / 2 - 30;
        for (int i = 0; i < RecipeData.SkillOrder.Length; i++)
        {
            string branch = RecipeData.SkillOrder[i];
            var skill = RecipeData.Skills.GetValueOrDefault(branch);
            int lvl = p.CraftLevels.GetValueOrDefault(branch, 1);
            int xp = p.CraftXp.GetValueOrDefault(branch);
            int next = RecipeData.SkillXpToNext(lvl);
            int sx = i % 2 == 0 ? col0 : col1;
            int sy = skillRect.Y + 28 + (i / 2) * 30;
            K.Text($"{skill?.Name ?? branch} ур. {lvl}", new Vector2(sx, sy),
                UiKit.Theme.Text, 4, shadow: false);
            K.Bar(sx + 150, sy + 2, halfW - 150, 12,
                next > 0 ? (double)xp / next : 0, UiKit.Theme.Xp);
            string xpText = $"{xp}/{next}";
            K.Text(xpText, new Vector2(sx + halfW - K.FontWidth(xpText, 4), sy),
                UiKit.Theme.TextDim, 4, shadow: false);
        }
        y = skillRect.Bottom + 6;

        K.Text($"Убийств: {game.KillsTotal}   Крафт: {game.ItemsCrafted}   Квестов: {game.QuestsDone}",
            new Vector2(panel.X + 24, y), UiKit.Theme.TextDim, 4, shadow: false);
        K.Text("Enter/1–6: вложить очко   C/Esc: выход",
            new Vector2(panel.X + 24, panel.Bottom - 30),
            UiKit.Theme.TextMuted, 4, shadow: false);

        K.End();
        K.EndInputFrame();
    }

    private void FillRect(Rectangle r, Color c) => _sb.Draw(KBlank, r, c);

    private Texture2D? _blank;
    private Texture2D KBlank
    {
        get
        {
            if (_blank == null)
            {
                _blank = new Texture2D(_device, 1, 1);
                _blank.SetData(new[] { Color.White });
                GpuReset.Register(_device, () =>
                {
                    try { _blank?.Dispose(); } catch { }
                    _blank = null;
                });
            }
            return _blank;
        }
    }

    private void FrameRect(Rectangle r, Color c)
    {
        FillRect(new Rectangle(r.X, r.Y, r.Width, 1), c);
        FillRect(new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
        FillRect(new Rectangle(r.X, r.Y, 1, r.Height), c);
        FillRect(new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
    }
}

/// <summary>
/// Меню паузы (Esc в игре) — паритет с python menu_controller.MENU_PAUSE:
/// вернуться в игру / сохранить / загрузить / настройки / выйти в меню.
/// Центрированная панель, кнопки с hover; клавиатура (↑↓/Enter) не изменилась.
/// </summary>
public class PauseScreen : ModalScreenBase
{
    public int Selected;

    private Rectangle _panel;

    public PauseScreen(GraphicsDevice device, SpriteBatch sb) : base(device, sb) { }

    private static readonly string[] Items =
    {
        "Вернуться в игру", "Сохранить игру", "Загрузить игру",
        "Настройки", "Выйти в главное меню",
    };

    // Пауза рисуется как главное меню (400×56 при y=200+i*80) —
    // координаты должны совпадать с MainMenuScreen.DrawPause.
    private Rectangle ItemRect(int i) =>
        new((AethelgardGame.ScreenW - 400) / 2, 200 + i * 80, 400, 56);

    private bool Hovered(Rectangle r, MouseState mouse) =>
        mouse.X >= r.X && mouse.X < r.Right && mouse.Y >= r.Y && mouse.Y < r.Bottom;

    /// <summary>Обработка клавиатуры: возвращает индекс выбранного пункта или null.</summary>
    public int? HandleInput(KeyboardState keys, KeyboardState prevKeys)
    {
        bool up = (keys.IsKeyDown(Keys.Up) && !prevKeys.IsKeyDown(Keys.Up))
            || (keys.IsKeyDown(Keys.W) && !prevKeys.IsKeyDown(Keys.W));
        bool down = (keys.IsKeyDown(Keys.Down) && !prevKeys.IsKeyDown(Keys.Down))
            || (keys.IsKeyDown(Keys.S) && !prevKeys.IsKeyDown(Keys.S));
        bool confirm = (keys.IsKeyDown(Keys.Enter) && !prevKeys.IsKeyDown(Keys.Enter))
            || (keys.IsKeyDown(Keys.Space) && !prevKeys.IsKeyDown(Keys.Space));
        if (up) Selected = (Selected + Items.Length - 1) % Items.Length;
        if (down) Selected = (Selected + 1) % Items.Length;
        if (confirm) return Selected;
        return null;
    }

    /// <summary>Мышь: наведение выделяет пункт, клик подтверждает. Возвращает индекс или null.</summary>
    public int? HandleMouse(MouseState mouse, MouseState prevMouse)
    {
        for (int i = 0; i < Items.Length; i++)
        {
            if (Hovered(ItemRect(i), mouse))
                Selected = i;
        }
        bool click = mouse.LeftButton == ButtonState.Pressed
            && prevMouse.LeftButton != ButtonState.Pressed;
        if (click)
        {
            for (int i = 0; i < Items.Length; i++)
                if (Hovered(ItemRect(i), mouse))
                    return i;
        }
        return null;
    }

    /// <summary>Отрисовка панели паузы (фон рисует вызывающий код).</summary>
    public void Draw(GameController game)
    {
        var mouse = Mouse.GetState();
        K.Begin(_sb, _device, mouse);

        _panel = new Rectangle((AethelgardGame.ScreenW - 560) / 2,
            (AethelgardGame.ScreenH - 520) / 2, 560, 520);
        K.Panel(_panel);

        K.TextCentered("ПАУЗА", new Rectangle(_panel.X, _panel.Y + 18, _panel.Width, 40),
            UiKit.Theme.GoldBright, 2, shadow: false);
        K.TextCentered("игра приостановлена",
            new Rectangle(_panel.X, _panel.Y + 60, _panel.Width, 20),
            UiKit.Theme.TextMuted, 4, shadow: false);

        for (int i = 0; i < Items.Length; i++)
        {
            var rect = ItemRect(i);
            bool sel = i == Selected;
            // Кнопка с hover/press-фидбеком; выделенный пункт — золотой
            K.Button(rect, Items[i], state: sel ? 1 : 0, size: 3);
        }

        K.TextCentered("↑/↓ или мышь — выбор · Enter/клик — подтвердить · Esc — вернуться",
            new Rectangle(_panel.X, _panel.Bottom - 44, _panel.Width, 24),
            UiKit.Theme.TextMuted, 4, shadow: false);

        K.End();
        K.EndInputFrame();
    }
}
