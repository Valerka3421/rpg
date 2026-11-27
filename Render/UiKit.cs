using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Aethelgard.Render;

/// <summary>
/// UiKit — новая единая UI-база всех экранов (замена разнобою
/// ModalScreenBase/ShopScreen/InventoryScreen/... с их собственными
/// панелями и палитрами). Immediate-mode поверх SpriteBatch:
///   kit.Begin(sb, device, mouse); ... kit.Panel(...); kit.Button(...); ... kit.End();
///
/// Дизайн-правила:
///  • тёмный «сумеречный» фон + золотые акценты — единая палитра для всех экранов;
///  • hover/press на каждом интерактивном элементе (кнопка, вкладка, слот, строка);
///  • тултипы собираются в очередь и рисуются поверх всего в End();
///  • всё в координатах дизайна 1920×1080, шрифты через Fonts (H1/H2/Body/Small/Tiny);
///  • никаких строковых литералов «в обход» — цвета только из Theme.
/// </summary>
public sealed class UiKit
{
    /// <summary>Единственный экземпляр кита на игру.</summary>
    public static readonly UiKit Kit = new();

    // ── Палитра (единая для всей игры) ──
    public static class Theme
    {
        public static readonly Color Bg = new(16, 14, 12);            // фон модали
        public static readonly Color BgSoft = new(24, 21, 17);        // подложка секций
        public static readonly Color BgSlot = new(30, 27, 22);        // слоты/строки
        public static readonly Color BgSlotHot = new(46, 40, 30);     // слот под курсором
        public static readonly Color BgSlotSel = new(58, 48, 30);     // выбранный слот
        public static readonly Color Border = new(84, 70, 48);        // рамка панели
        public static readonly Color BorderSoft = new(56, 47, 34);    // рамка секций
        public static readonly Color Gold = new(222, 180, 96);        // акцент
        public static readonly Color GoldBright = new(244, 210, 130); // акцент hover
        public static readonly Color Text = new(224, 216, 198);
        public static readonly Color TextDim = new(158, 148, 128);
        public static readonly Color TextMuted = new(108, 99, 84);
        public static readonly Color Hp = new(206, 76, 62);
        public static readonly Color Mana = new(96, 140, 222);
        public static readonly Color Xp = new(126, 188, 108);
        public static readonly Color Danger = new(214, 88, 70);
        public static readonly Color Shadow = new(0, 0, 0, 150);
        public static readonly Color Dim = new(0, 0, 0, 170);         // затемнение под модалью
    }

    // ── Ритм сетки ──
    public const int Pad = 16;          // внутренний отступ панелей
    public const int Gap = 10;          // между секциями/слотами
    public const int RowH = 26;         // строка списка
    public const int Slot = 56;         // слот инвентаря
    public const int TabH = 34;         // высота вкладки

    private SpriteBatch _sb = null!;
    private GraphicsDevice _device = null!;
    private MouseState _mouse;
    private Texture2D? _blank;
    private bool _inFrame;

    // Очередь тултипов (рисуются в End поверх всего)
    private readonly List<(Rectangle Anchor, string Title, List<(string Text, Color C)> Lines)> _tooltips = new();

    private readonly HashSet<Rectangle> _hotThisFrame = new();

    private Texture2D Blank
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

    public void Begin(SpriteBatch sb, GraphicsDevice device, MouseState mouse)
    {
        _sb = sb; _device = device; _mouse = mouse; _inFrame = true;
        _tooltips.Clear();
        _hotThisFrame.Clear();
    }

    /// <summary>Идёт ли сейчас кадр отрисовки (между Begin и End). Additive-доступ.</summary>
    public bool InFrame => _inFrame;

    public void End()
    {
        foreach (var (anchor, title, lines) in _tooltips) DrawTooltip(anchor, title, lines);
        _tooltips.Clear();
        _inFrame = false;
    }

    // ── Примитивы ──

    private void Rect(Rectangle r, Color c) => _sb.Draw(Blank, r, c);

    private void Rect(int x, int y, int w, int h, Color c) => _sb.Draw(Blank, new Rectangle(x, y, w, h), c);

    private static SpriteFont F(int size) => size switch
    {
        1 => Fonts.H1, 2 => Fonts.H2, 3 => Fonts.Body,
        4 => Fonts.Small, _ => Fonts.Tiny,
    };

    /// <summary>Текст с тенью (читаемость на любом фоне).</summary>
    public void Text(string s, Vector2 pos, Color? color = null, int size = 3, bool shadow = true)
    {
        if (shadow) _sb.DrawString(F(size), s, pos + new Vector2(1, 1), Theme.Shadow);
        _sb.DrawString(F(size), s, pos, color ?? Theme.Text);
    }

    public void TextCentered(string s, Rectangle rect, Color? color = null, int size = 3, bool shadow = true)
    {
        var f = F(size);
        var sz = f.MeasureString(s);
        var pos = new Vector2(rect.X + (rect.Width - sz.X) / 2f, rect.Y + (rect.Height - sz.Y) / 2f);
        Text(s, pos, color, size, shadow);
    }

    /// <summary>Панель: фон + рамка + тень. Базовый контейнер всех экранов.</summary>
    public void Panel(Rectangle r, bool dimWorldBehind = true)
    {
        if (dimWorldBehind) Rect(new Rectangle(0, 0, AethelgardGame.ScreenW, AethelgardGame.ScreenH), Theme.Dim);
        // Тень
        Rect(r.X + 4, r.Y + 4, r.Width, r.Height, Theme.Shadow);
        // Фон
        Rect(r, Theme.Bg);
        // Рамка (толще по горизонтали — «шапка» и «подвал» читаются как панели)
        Rect(r.X, r.Y, r.Width, 2, Theme.Border);
        Rect(r.X, r.Bottom - 2, r.Width, 2, Theme.Border);
        Rect(r.X, r.Y, 2, r.Height, Theme.BorderSoft);
        Rect(r.Right - 2, r.Y, 2, r.Height, Theme.BorderSoft);
        // Золотой штрих под шапкой
        Rect(r.X + 2, r.Y + 2, r.Width - 4, 1, new Color(Theme.Gold, 90));
    }

    /// <summary>Секция внутри панели: подложка + тонкая рамка.</summary>
    public void Section(Rectangle r, string? title = null, int titleSize = 4)
    {
        Rect(r, Theme.BgSoft);
        Rect(r.X, r.Y, r.Width, 1, Theme.BorderSoft);
        Rect(r.X, r.Bottom - 1, r.Width, 1, Theme.BorderSoft);
        Rect(r.X, r.Y, 1, r.Height, Theme.BorderSoft);
        Rect(r.Right - 1, r.Y, 1, r.Height, Theme.BorderSoft);
        if (title != null)
            Text(title, new Vector2(r.X + 8, r.Y + 5), Theme.Gold, titleSize, shadow: false);
    }

    /// <summary>Полоса прогресса.</summary>
    public void Bar(int x, int y, int w, int h, double frac, Color color)
    {
        frac = Math.Clamp(frac, 0, 1);
        Rect(x, y, w, h, new Color(0, 0, 0, 140));
        Rect(x + 1, y + 1, w - 2, h - 2, new Color(30, 26, 20));
        int fill = (int)((w - 2) * frac);
        if (fill > 0) Rect(x + 1, y + 1, fill, h - 2, color);
        Rect(x, y, w, 1, new Color(255, 255, 255, 26));
    }

    // ── Интерактивные элементы ──

    public bool Hot(Rectangle r) => r.Contains(_mouse.Position);

    /// <summary>
    /// Кнопка. Возвращает true в кадре клика (edge — сам отслеживает прошлую ЛКМ).
    /// state: 0 обычная, 1 выделенная (gold), 2 disabled.
    /// </summary>
    public bool Button(Rectangle r, string label, int state = 0, int size = 3, bool enabled = true)
    {
        bool hot = enabled && Hot(r) && !_hotThisFrame.Contains(r);
        if (hot) _hotThisFrame.Add(r);

        Color bg = state == 1 ? new Color(64, 52, 28)
                 : hot ? Theme.BgSlotHot
                 : Theme.BgSlot;
        Color border = state == 1 ? Theme.Gold : hot ? Theme.GoldBright : Theme.BorderSoft;
        Color fg = !enabled ? Theme.TextMuted
                 : state == 1 ? Theme.GoldBright
                 : hot ? Theme.Text : Theme.TextDim;

        Rect(r.X + 2, r.Y + 2, r.Width, r.Height, Theme.Shadow);
        Rect(r, bg);
        Frame(r, border);

        if (hot) Rect(r.X, r.Y, r.Width, 2, Theme.GoldBright);
        TextCentered(label, r, fg, size);
        return hot && _mouse.LeftButton == ButtonState.Pressed && !_prevLeft;
    }

    private bool _prevLeft;

    /// <summary>Обязателен в конце Update-пути кадра: срезает edge кликов.</summary>
    public void EndInputFrame() => _prevLeft = _mouse.LeftButton == ButtonState.Pressed;

    /// <summary>
    /// Строка списка (квесты, рецепты, локации). Возвращает клик.
    /// selected — подсветка активной строки.
    /// </summary>
    public bool Row(Rectangle r, string label, string? trailing = null, bool selected = false, bool enabled = true)
    {
        bool hot = enabled && Hot(r);
        Color bg = selected ? Theme.BgSlotSel : hot ? Theme.BgSlotHot : Theme.BgSlot;
        Rect(r, bg);
        if (selected) Rect(r.X, r.Y, 3, r.Height, Theme.Gold);
        else if (hot) Rect(r.X, r.Y, 3, r.Height, new Color(Theme.Gold, 140));
        Frame(r, selected ? new Color(Theme.Gold, 150) : Theme.BorderSoft);
        Text(label, new Vector2(r.X + 10, r.Y + (r.Height - F(3).LineSpacing) / 2),
            !enabled ? Theme.TextMuted : selected ? Theme.GoldBright : Theme.Text, 3);
        if (trailing != null)
        {
            var tsz = F(3).MeasureString(trailing);
            Text(trailing, new Vector2(r.Right - tsz.X - 10, r.Y + (r.Height - F(3).LineSpacing) / 2),
                selected ? Theme.Gold : Theme.TextDim, 3, shadow: false);
        }
        return enabled && hot && _mouse.LeftButton == ButtonState.Pressed && !_prevLeft;
    }

    /// <summary>
    /// Вкладки. Возвращает индекс кликнутой вкладки или -1.
    /// </summary>
    public int Tabs(int x, int y, string[] labels, int selected, int widthEach = 106)
    {
        int clicked = -1;
        for (int i = 0; i < labels.Length; i++)
        {
            var r = new Rectangle(x + i * (widthEach + 6), y, widthEach, TabH);
            bool sel = i == selected;
            bool hot = Hot(r);
            Color bg = sel ? new Color(64, 52, 28) : hot ? Theme.BgSlotHot : Theme.BgSoft;
            Color border = sel ? Theme.Gold : hot ? new Color(Theme.Gold, 120) : Theme.BorderSoft;
            Color fg = sel ? Theme.GoldBright : hot ? Theme.Text : Theme.TextDim;
            Rect(r, bg);
            Frame(r, border);
            if (sel) Rect(r.X, r.Bottom - 3, r.Width, 3, Theme.Gold);
            TextCentered(labels[i], r, fg, 4);
            if (hot && _mouse.LeftButton == ButtonState.Pressed && !_prevLeft) clicked = i;
        }
        return clicked;
    }

    /// <summary>
    /// Слот предмета (инвентарь/магазин/крафт). Возвращает hover;
    /// клик обрабатывает вызывающий код через возвращаемое click.
    /// </summary>
    public bool ItemSlot(Rectangle r, Texture2D? icon, string? count, bool selected = false, bool enabled = true)
    {
        bool hot = enabled && Hot(r);
        Color bg = selected ? Theme.BgSlotSel : hot ? Theme.BgSlotHot : Theme.BgSlot;
        Rect(r.X + 2, r.Y + 2, r.Width, r.Height, Theme.Shadow);
        Rect(r, bg);
        Frame(r, selected ? Theme.Gold : hot ? new Color(Theme.Gold, 140) : Theme.BorderSoft);
        if (selected) Rect(r.X, r.Y, r.Width, 2, Theme.Gold);
        if (icon != null)
        {
            int isz = Math.Min(r.Width, r.Height) - 16;
            _sb.Draw(icon, new Rectangle(r.Center.X - isz / 2, r.Center.Y - isz / 2 - 4, isz, isz), Color.White);
        }
        if (!string.IsNullOrEmpty(count))
        {
            var csz = F(4).MeasureString(count);
            Text(count, new Vector2(r.Right - csz.X - 4, r.Bottom - csz.Y - 2), Theme.Gold, 4);
        }
        return hot;
    }

    /// <summary>Поставить тултип в очередь (нарисуется в End поверх всего).</summary>
    public void Tooltip(Rectangle anchor, string title, params (string, Color)[] lines)
        => Tooltip(anchor, title, new List<(string, Color)>(lines));

    public void Tooltip(Rectangle anchor, string title, List<(string Text, Color C)> lines)
    {
        if (_hotThisFrame.Contains(anchor)) return;   // один тултип на элемент
        _hotThisFrame.Add(anchor);
        _tooltips.Add((anchor, title, lines));
    }

    private void DrawTooltip(Rectangle anchor, string title, List<(string Text, Color C)> lines)
    {
        var f = F(4);
        int w = 260, lh = 18;
        int h = 30 + lines.Count * lh + 10;
        int x = anchor.Right + 8;
        int y = anchor.Y;
        if (x + w > AethelgardGame.ScreenW - 8) x = anchor.X - w - 8;
        if (x < 8) x = 8;
        if (y + h > AethelgardGame.ScreenH - 8) y = AethelgardGame.ScreenH - h - 8;

        Rect(x + 3, y + 3, w, h, Theme.Shadow);
        Rect(x, y, w, h, Theme.Bg);
        Frame(new Rectangle(x, y, w, h), Theme.Border);
        Rect(x, y, w, 2, Theme.Gold);

        var tsz = f.MeasureString(title);
        Text(title, new Vector2(x + 10, y + 8), Theme.GoldBright, 4);
        Rect(x + 8, y + 28, w - 16, 1, Theme.BorderSoft);
        int ly = y + 34;
        foreach (var (text, c) in lines)
        {
            _sb.DrawString(f, text, new Vector2(x + 10, ly), c);
            ly += lh;
        }
    }

    // ── Хелперы для экранов, строящих сложные элементы ──

    /// <summary>Hover-состояние прямоугольника (регистрирует элемент hot).</summary>
    public bool Hoverable(Rectangle r) => Hot(r);

    /// <summary>Клик по прямоугольнику в этом кадре (edge ЛКМ).</summary>
    public bool Clicked(Rectangle r) =>
        Hot(r) && _mouse.LeftButton == ButtonState.Pressed && !_prevLeft;

    /// <summary>Иконка по центру прямоугольника.</summary>
    public void SlotIcon(Rectangle r, Texture2D? icon, int size = 36)
    {
        if (icon == null) return;
        _sb.Draw(icon, new Rectangle(r.Center.X - size / 2, r.Center.Y - size / 2, size, size),
            Color.White);
    }

    /// <summary>Ширина строки заданным размером шрифта.</summary>
    public float FontWidth(string s, int size = 3) => F(size).MeasureString(s).X;

    /// <summary>Чип-подсказка клавиши: [Y] написать.</summary>
    public void KeyHint(int x, int y, string key, string label)
    {
        var keyRect = new Rectangle(x, y, (int)F(4).MeasureString(key).X + 12, 20);
        Rect(keyRect, Theme.BgSlot);
        Frame(keyRect, Theme.BorderSoft);
        TextCentered(key, keyRect, Theme.Gold, 4);
        int lx = keyRect.Right + 6;
        var lsz = F(4).MeasureString(label);
        Text(label, new Vector2(lx, y + (keyRect.Height - lsz.Y) / 2), Theme.TextDim, 4, shadow: false);
    }

    private void Frame(Rectangle r, Color c)
    {
        Rect(r.X, r.Y, r.Width, 1, c);
        Rect(r.X, r.Bottom - 1, r.Width, 1, c);
        Rect(r.X, r.Y, 1, r.Height, c);
        Rect(r.Right - 1, r.Y, 1, r.Height, c);
    }
}
