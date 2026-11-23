using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Aethelgard.Render;

/// <summary>
/// Базовая рамка модального окна (общая с screens_renderer/_base.py).
/// </summary>
public abstract class ModalScreenBase
{
    protected readonly GraphicsDevice _device;
    protected readonly SpriteBatch _sb;
    protected SpriteFont? _font;
    private Texture2D? _blank;

    protected ModalScreenBase(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device; _sb = sb;
    }

    public void LoadFont(SpriteFont font) => _font = font;

    protected Texture2D Blank
    {
        get
        {
            if (_blank == null)
            {
                _blank = new Texture2D(_device, 1, 1);
                _blank.SetData(new[] { Color.White });
            }
            return _blank;
        }
    }

    protected void Panel(Rectangle r, Color? bg = null)
    {
        _sb.Draw(Blank, r, bg ?? new Color(22, 19, 16, 245));
        _sb.Draw(Blank, new Rectangle(r.X, r.Y, r.Width, 2), Config.BigmapBorder);
        _sb.Draw(Blank, new Rectangle(r.X, r.Y + r.Height - 2, r.Width, 2), Config.BigmapBorder);
        _sb.Draw(Blank, new Rectangle(r.X, r.Y, 2, r.Height), Config.BigmapBorder);
        _sb.Draw(Blank, new Rectangle(r.X + r.Width - 2, r.Y, 2, r.Height), Config.BigmapBorder);
    }

    protected void Text(string s, Vector2 pos, Color? color = null) =>
        _sb.DrawString(_font, s, pos, color ?? Config.ColText);

    protected void Bar(int x, int y, int w, int h, double frac, Color color)
    {
        frac = Math.Clamp(frac, 0, 1);
        _sb.Draw(Blank, new Rectangle(x, y, w, h), new Color(30, 25, 20));
        _sb.Draw(Blank, new Rectangle(x, y, (int)(w * frac), h), color);
    }
}

/// <summary>
/// Экран крафта: вкладки (all + 3 ветки), список рецептов, партии [–],
/// мини-игра точности (Space) и качество +N (parity с craft_controller.py).
/// </summary>
public class CraftScreen : ModalScreenBase
{
    public CraftScreen(GraphicsDevice device, SpriteBatch sb) : base(device, sb) { }

    public void Draw(GameController game, MouseState mouse)
    {
        var panel = new Rectangle(140, 40, 1000, 560);
        Panel(panel);

        if (_font == null) return;
        int y = panel.Y + 16;

        Text("КРАФТ", new Vector2(panel.X + 20, y), Config.ColGold);
        var tabs = RecipeData.CraftTabs;
        for (int i = 0; i < tabs.Length; i++)
        {
            Color c = i == game.CraftTab ? Config.ColGold : Config.ColDim;
            Text($"[{tabs[i].Key}] {tabs[i].Name}",
                new Vector2(panel.X + 140 + i * 200, y), c);
        }
        y += 26;
        Text($"Партия: {game.CraftQty}  ([ / ])   Ветка: {CraftBranchInfo(game)}",
            new Vector2(panel.X + 20, y), Config.ColDim);
        y += 22;

        if (game.MinigameActive)
        {
            DrawMinigame(game, panel);
            return;
        }

        // Список рецептов
        var list = game.CraftableList();
        int pageStart = game.CraftPage * Config.CraftPageSize;
        int branchLevel = 0;
        int maxPage = Math.Max(0, (list.Count - 1) / Config.CraftPageSize);
        if (game.CraftPage > maxPage) game.CraftPage = maxPage;

        for (int i = 0; i < Config.CraftPageSize; i++)
        {
            int idx = pageStart + i;
            if (idx >= list.Count) break;
            var (key, def) = list[idx];
            string branch = ItemData.CategoryOf(key);
            branchLevel = game.Player.CraftLevels.GetValueOrDefault(branch, 1);
            bool canCraft = def.SkillReq <= branchLevel
                && (string.IsNullOrEmpty(def.BlueprintRequired)
                    || game.Player.Blueprints.Contains(def.BlueprintRequired));
            Color c = canCraft ? Config.ColText : Config.ColDim;
            int rowY = y + i * 42;

            // Иконка
            _sb.Draw(Blank, new Rectangle(panel.X + 20, rowY, 30, 30), ItemData.DisplayColor(key));
            Text($"{i + 1}. {ItemData.DisplayName(key)}",
                new Vector2(panel.X + 62, rowY), c);
            Text($"Lvl {def.SkillReq}  Tier {def.Tier}  [{branch}]",
                new Vector2(panel.X + 400, rowY), Config.ColDim);

            // Ингредиенты
            var variants = ItemData.RecipesOf(key);
            if (variants.Count > 0)
            {
                string mats = string.Join(", ", variants[0].Materials
                    .Select(m => $"{ItemData.DisplayName(m.Key)}×{m.Value}"));
                Text(mats, new Vector2(panel.X + 560, rowY),
                    CanAfford(game, variants[0].Materials) ? Config.ColHeal : Config.ColDmg);
            }
        }

        Text($"Стр. {game.CraftPage + 1}/{maxPage + 1}  (←/→)   Space/1–8: крафт   K/Esc: выход",
            new Vector2(panel.X + 20, panel.Bottom - 30), Config.ColDim);
    }

    private bool CanAfford(GameController game, Dictionary<string, int> mats) =>
        mats.All(m => game.Player.ItemCount(m.Key) >= m.Value);

    private string CraftBranchInfo(GameController game)
    {
        string? branch = game.CraftTab == 0 ? null : RecipeData.SkillOrder[game.CraftTab - 1];
        if (branch == null) return "all";
        int lvl = game.Player.CraftLevels.GetValueOrDefault(branch, 1);
        int xp = game.Player.CraftXp.GetValueOrDefault(branch);
        return $"{branch} lvl {lvl} ({xp}/{RecipeData.SkillXpToNext(lvl)})";
    }

    /// <summary>Мини-игра точности: бегающий индикатор, Space — остановить.</summary>
    private void DrawMinigame(GameController game, Rectangle panel)
    {
        int y = panel.Y + 120;
        Text($"Крафт: {ItemData.DisplayName(game.MinigameItem ?? "")}",
            new Vector2(panel.X + 40, y), Config.ColGold);
        y += 30;
        Text("Останови индикатор в центре (Space). Точность → качество +N",
            new Vector2(panel.X + 40, y), Config.ColDim);
        y += 34;

        int barX = panel.X + 60, barW = panel.Width - 120;
        Bar(barX, y, barW, 26, 1, new Color(40, 34, 28));
        // зоны качества (0.42 / 0.72 / 0.93)
        Bar(barX + (int)(barW * 0.5 - barW * 0.5 * 0.42), y, (int)(barW * 0.42), 26, 0,
            new Color(60, 50, 30));
        Bar(barX + (int)(barW * 0.5 - barW * 0.5 * 0.72), y, (int)(barW * 0.72), 26, 0,
            new Color(90, 75, 35));
        Bar(barX + (int)(barW * 0.5 - barW * 0.5 * 0.93), y, (int)(barW * 0.93), 26, 0,
            new Color(140, 110, 45));

        // Индикатор
        int ix = barX + (int)(barW * game.MinigamePos) - 3;
        _sb.Draw(Blank, new Rectangle(ix, y - 6, 6, 38), Config.ColText);

        y += 60;
        Text($"Партия: {game.CraftQty} × {ItemData.DisplayName(game.MinigameItem ?? "")}",
            new Vector2(panel.X + 40, y), Config.ColDim);
        Text("Esc: отмена", new Vector2(panel.X + 40, y + 24), Config.ColDim);
    }
}

/// <summary>
/// Экран характеристик (C): распределение свободных очков (↑↓/1–6/Enter).
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
        var panel = new Rectangle(320, 80, 640, 480);

        // Тень панели и золотая шапка
        _sb.Draw(Blank, new Rectangle(panel.X + 6, panel.Y + 8,
            panel.Width, panel.Height), new Color(0, 0, 0, 130));
        Panel(panel);
        _sb.Draw(Blank, new Rectangle(panel.X, panel.Y, panel.Width, 4),
            Config.ColGold);
        if (_font == null) return;

        var p = game.Player;
        int y = panel.Y + 18;
        Text($"{p.ClassName}  Ур. {p.Level}  {p.ClassKey}", new Vector2(panel.X + 24, y), Config.ColGold);
        y += 26;
        Text($"Свободных очков: {p.FreePoints}    Очков навыков: {p.SkillPoints}",
            new Vector2(panel.X + 24, y), p.FreePoints > 0 ? Config.ColHeal : Config.ColDim);
        y += 10;

        int[] values = { p.TotalStr, p.TotalAgi, p.TotalVit, p.TotalLuk, p.TotalDfStat, p.TotalInt };
        for (int i = 0; i < Stats.Length; i++)
        {
            bool selected = game.StatSelected == i;
            int rowH = 28;
            var rowRect = new Rectangle(panel.X + 16, y - 3, panel.Width - 32, rowH);

            if (selected)
            {
                // Подсветка выбранной строки + золотой маркер
                _sb.Draw(Blank, rowRect, new Color(52, 43, 30));
                _sb.Draw(Blank, new Rectangle(rowRect.X, rowRect.Y, 4, rowRect.Height),
                    new Color(206, 166, 82, 200));
            }
            else if (p.FreePoints > 0)
            {
                // Строки, куда можно вложить очко, слегка акцентированы
                _sb.Draw(Blank, new Rectangle(rowRect.X, rowRect.Y, rowRect.Width, 1),
                    new Color(60, 52, 40));
            }

            Color c = selected ? Config.ColGold : Config.ColText;
            Text($"{(selected ? "> " : "  ")}{i + 1}. {Stats[i].Label}",
                new Vector2(panel.X + 30, y), c);
            Text($"{values[i]}",
                new Vector2(panel.X + panel.Width - 110, y), c);
            y += rowH;
        }

        y += 12;
        Text($"HP {p.Hp}/{p.MaxHp}   MP {p.Mana}/{p.MaxMana}   ATK {p.Atk}   DEF {p.Df}",
            new Vector2(panel.X + 24, y), Config.ColText);
        y += 24;
        Text($"Уклонение {p.DodgeChance * 100:F0}%   Крит {p.CritChance * 100:F0}%",
            new Vector2(panel.X + 24, y), Config.ColDim);
        y += 40;

        Text("Перки:", new Vector2(panel.X + 24, y), Config.ColDim);
        y += 22;
        var allPerks = new (string Name, bool On)[]
        {
            ("Двойной удар", p.DoubleStrike), ("Регенерация", p.Regen),
            ("Вампиризм", p.Vampire), ("Шипы", p.Thorns), ("Жадность", p.Greedy),
        };
        string[] perks = allPerks.Where(x => x.On).Select(x => x.Name).ToArray();
        Text(perks.Length > 0 ? string.Join(", ", perks) : "нет",
            new Vector2(panel.X + 24, y), Config.ColText);
        y += 36;

        Text($"Убийств: {game.KillsTotal}   Крафт: {game.ItemsCrafted}   Квестов: {game.QuestsDone}",
            new Vector2(panel.X + 24, y), Config.ColDim);
        Text("Enter/1–6: вложить очко   C/Esc: выход",
            new Vector2(panel.X + 24, panel.Bottom - 30), Config.ColDim);
    }
}

/// <summary>
/// Меню паузы (Esc в игре) — паритет с python menu_controller.MENU_PAUSE:
/// вернуться в игру / сохранить / загрузить / настройки / выйти в меню.
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

    private Rectangle ItemRect(int i) =>
        new(_panel.X + 32, _panel.Y + 76 + i * 40, _panel.Width - 64, 34);

    private bool Hovered(Rectangle r, MouseState mouse) =>
        mouse.X >= r.X && mouse.X < r.Right && mouse.Y >= r.Y && mouse.Y < r.Bottom;

    /// <summary>Обработка клавиатуры: возвращает индекс выбранного пункта или null.</summary>
    public int? HandleInput(KeyboardState keys, KeyboardState prevKeys)
    {
        if (keys.IsKeyDown(Keys.Up) && !prevKeys.IsKeyDown(Keys.Up))
            Selected = (Selected + Items.Length - 1) % Items.Length;
        if (keys.IsKeyDown(Keys.Down) && !prevKeys.IsKeyDown(Keys.Down))
            Selected = (Selected + 1) % Items.Length;
        if (keys.IsKeyDown(Keys.Enter) && !prevKeys.IsKeyDown(Keys.Enter))
            return Selected;
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
        // Затемнение всего экрана — фон (мир) просвечивает слабо
        _sb.Draw(Blank, new Rectangle(0, 0,
            AethelgardGame.ScreenW, AethelgardGame.ScreenH), new Color(0, 0, 0, 130));

        _panel = new Rectangle(430, 140, 420, 360);

        // Тень панели
        _sb.Draw(Blank, new Rectangle(_panel.X + 6, _panel.Y + 8,
            _panel.Width, _panel.Height), new Color(0, 0, 0, 140));
        Panel(_panel);
        if (_font == null) return;

        // Цветная шапка
        _sb.Draw(Blank, new Rectangle(_panel.X, _panel.Y, _panel.Width, 4),
            Config.ColGold);

        int y = _panel.Y + 24;
        string title = "ПАУЗА";
        var size = _font.MeasureString(title);
        Text(title, new Vector2(_panel.X + _panel.Width / 2 - size.X / 2, y),
            Config.ColGold);

        for (int i = 0; i < Items.Length; i++)
        {
            var rect = ItemRect(i);
            bool sel = i == Selected;
            if (sel)
            {
                _sb.Draw(Blank, rect, new Color(52, 43, 30));
                _sb.Draw(Blank, new Rectangle(rect.X, rect.Y, 4, rect.Height),
                    new Color(206, 166, 82, 200));
            }
            Color c = sel ? Config.ColGold : Config.ColText;
            Text(Items[i], new Vector2(rect.X + 20, rect.Y + 8), c);
        }

        Text("↑/↓ или мышь — выбор · Enter/клик — подтвердить · Esc — вернуться",
            new Vector2(_panel.X + 20, _panel.Bottom - 34), Config.ColDim);
    }
}
