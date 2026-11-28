using UnityEngine;

namespace Aethelgard.UI;

/// <summary>
/// Тема интерфейса «Этельгард» — точный порт палитры ветки python-final
/// (render/theme.py + core/config.py). Стиль Stoneshards: тёплая почти-чёрная
/// база, бронза, пергамент, холодные сине-зелёные тени.
///
/// ЭТО ГЛАВНАЯ ТОЧКА НАСТРОЙКИ ВНЕШНЕГО ВИДА UI:
/// актив Resources/UITheme.asset открывается в инспекторе — правка любого
/// поля сразу меняет все меню и окна игры.
/// </summary>
[CreateAssetMenu(fileName = "UITheme", menuName = "Aethelgard/UI Theme")]
public class UITheme : ScriptableObject
{
    // ── render/theme.py (1:1) ──

    [Header("Фон и панели (Theme)")]
    public Color Bg = C(14, 12, 10);
    public Color BgHud = C(9, 8, 7);
    public Color BgMapDim = C(18, 16, 13);
    public Color Panel = C(30, 26, 21);
    public Color PanelDark = C(18, 16, 13);
    public Color PanelHeader = C(42, 36, 28);
    public Color PanelBorder = C(78, 66, 48);
    public Color PanelBorderDark = C(46, 39, 29);

    [Header("Текст (Theme)")]
    public Color Text = C(222, 214, 198);
    public Color TextDim = C(150, 140, 120);
    public Color TextMuted = C(98, 90, 74);
    public Color Title = C(214, 178, 98);
    public Color TitleLight = C(238, 222, 188);
    public Color Section = C(190, 172, 138);

    [Header("Полосы состояния (Theme)")]
    public Color Hp = C(166, 48, 42);
    public Color HpCrit = C(220, 84, 64);
    public Color HpBg = C(44, 22, 18);
    public Color Xp = C(92, 126, 138);
    public Color ManaPy = C(90, 130, 240);           // MP-бар HUD
    public Color ManaBg = C(20, 24, 48);
    public Color ManaLabel = C(120, 160, 255);
    public Color XpBg = C(20, 30, 50);
    public Color ProgressCraft = C(120, 180, 110);   // прогресс крафта

    [Header("Игровые акценты (Theme)")]
    public Color Gold = C(206, 166, 82);
    public Color Dmg = C(214, 82, 66);
    public Color Crit = C(232, 178, 74);
    public Color Heal = C(120, 172, 104);
    public Color Dodge = C(150, 166, 178);
    public Color Win = C(222, 190, 96);
    public Color Merch = C(206, 170, 96);

    [Header("Ряды и клавиши (Theme)")]
    public Color RowHover = C(54, 45, 33);
    public Color RowBorder = C(110, 91, 62);
    public Color KeyBg = C(36, 31, 24);
    public Color KeyBorder = C(72, 62, 46);
    public Color KeyText = C(216, 206, 186);

    [Header("Мир (Theme)")]
    public Color Wall = C(74, 68, 59);
    public Color WallDim = C(32, 29, 25);
    public Color Floor = C(56, 52, 45);
    public Color FloorDim = C(22, 20, 17);
    public Color Player = C(140, 186, 132);
    public Color Stairs = C(206, 166, 82);

    [Header("Кнопки (base_renderer.py, 1:1)")]
    public Color BtnNormalTop = C(44, 39, 31);
    public Color BtnNormalBottom = C(24, 21, 17);
    public Color BtnNormalBorder = C(72, 62, 46);
    public Color BtnNormalText = C(200, 190, 170);
    public Color BtnSelTop = C(96, 76, 44);
    public Color BtnSelBottom = C(56, 44, 25);
    public Color BtnSelBorder = C(206, 166, 82);     // GOLD
    public Color BtnSelHiLine = C(236, 206, 142);
    public Color BtnSelText = C(238, 222, 188);
    public Color BtnShadow = C(0, 0, 0, 110f / 255f);

    [Header("Слоты и модальные окна (screens_renderer/_base.py, 1:1)")]
    public Color SlotFill = C(32, 28, 22);
    public Color SlotHover = C(58, 49, 35);
    public Color SlotBorderEmpty = C(48, 42, 32);
    public Color ModalDim = C(6, 5, 4, 165f / 255f);
    public Color ModalPanel = C(30, 26, 21, 238f / 255f);
    public Color CloseBtnFill = C(54, 26, 22);
    public Color CloseBtnBorder = C(160, 68, 58);
    public Color CloseBtnX = C(200, 74, 64);

    [Header("Хотбар и панель навыков (game_renderer.py, 1:1)")]
    public Color HotbarContainer = C(22, 20, 17);
    public Color HotbarBorder = C(70, 60, 44);
    public Color HotbarSlot = C(30, 27, 22);
    public Color SkillsContainer = C(24, 21, 34);
    public Color SkillsBorder = C(78, 64, 104);
    public Color SkillsReady = C(52, 42, 78);
    public Color SkillsReadyBorder = C(150, 108, 220);
    public Color SkillsReadyHover = C(190, 150, 255);
    public Color CooldownOverlay = C(0, 0, 0, 150f / 255f);
    public Color CooldownText = C(240, 220, 140);
    public Color ManaCost = C(140, 180, 255);
    public Color ManaCostNo = C(220, 96, 96);

    [Header("Миникарта (game_renderer.py, 1:1)")]
    public Color MiniBg = C(12, 11, 9, 210f / 255f);
    public Color MiniWall = C(82, 74, 62, 210f / 255f);
    public Color MiniFloor = C(56, 52, 45, 210f / 255f);
    public Color MiniExit = C(150, 190, 130);
    public Color MiniFrame = C(52, 45, 34);
    public Color MiniFrameBorder = C(100, 86, 62);

    [Header("Гексагоны дерева навыков (_skills.py, 1:1)")]
    public Color HexOuter = C(10, 9, 8);
    public Color HexLearnedFill = C(30, 38, 27);
    public Color HexLearnedBorder = C(208, 172, 86);
    public Color HexLearnedGlyph = C(238, 216, 152);
    public Color HexAvailFill = C(32, 29, 22);
    public Color HexAvailBorder = C(170, 140, 70);
    public Color HexAvailGlyph = C(224, 202, 148);
    public Color HexLockedFill = C(22, 20, 18);
    public Color HexLockedBorder = C(66, 58, 45);
    public Color HexLockedGlyph = C(108, 98, 82);
    public Color HexSelRing = C(252, 238, 196);
    public Color HexHubFill1 = C(44, 36, 23);
    public Color HexHubFill2 = C(28, 25, 19);
    public Color LinkLearned = C(198, 162, 74);
    public Color LinkDot = C(236, 208, 132);
    public Color LinkLocked = C(92, 79, 56);

    [Header("Выбор класса (menu_renderer/class select, 1:1)")]
    public Color ChipFill = C(34, 29, 23);
    public Color ChipBorder = C(70, 60, 46);
    public Color AttrAtk = C(222, 150, 110);
    public Color AttrDef = C(150, 170, 200);
    public Color AttrHp = C(220, 84, 64);
    public Color AttrMp = C(120, 160, 255);
    public Color AttrDodge = C(150, 166, 178);
    public Color AttrCrit = C(232, 178, 74);
    public Color BarStr = C(220, 120, 100);
    public Color BarAgi = C(140, 200, 130);
    public Color BarVit = C(220, 120, 110);
    public Color BarLuk = C(220, 200, 110);
    public Color BarDef = C(150, 170, 210);
    public Color BarInt = C(130, 170, 240);
    public Color AttrBarBg = C(20, 18, 15);
    public Color CardPreviewFill = C(26, 22, 18, 236f / 255f);
    public Color PortraitFill = C(16, 14, 11);

    [Header("Квесты (screens_renderer/_quests.py, 1:1)")]
    public Color QuestCardFill = C(32, 32, 46);
    public Color QuestCardBorder = C(60, 60, 85);
    public Color QuestCardSelFill = C(60, 60, 80);
    public Color QuestStateTake = C(176, 186, 192);
    public Color QuestStateActive = C(206, 190, 138);
    public Color QuestStateDone = C(134, 190, 118);
    public Color QuestReward = C(240, 210, 90);

    [Header("Legacy COL_* (core/config.py) — лог, урон, плавающие числа")]
    public Color ColText = C(225, 225, 235);
    public Color ColDim = C(110, 110, 130);
    public Color ColHp = C(200, 60, 60);
    public Color ColXp = C(80, 160, 240);
    public Color ColGold = C(240, 210, 90);
    public Color ColDmg = C(255, 100, 100);
    public Color ColCrit = C(255, 200, 50);
    public Color ColHeal = C(120, 240, 120);
    public Color ColDodge = C(180, 180, 255);
    public Color ColWin = C(255, 220, 80);
    public Color ColMerch = C(240, 200, 100);

    [Header("Шрифты (python-final: Consolas)")]
    public string FontName = "Consolas";
    public int H1Size = 40;
    public int H2Size = 22;
    public int BodySize = 18;
    public int SmallSize = 14;
    public int TinySize = 12;

    [Header("Геометрия (python-final)")]
    [Tooltip("Толщина рамки панелей, px")]
    public int FrameThickness = 2;
    [Tooltip("Внутренний отступ окон, px")]
    public int Padding = 20;
    [Tooltip("Высота строки списка, px")]
    public int RowHeight = 46;
    [Tooltip("Высота кнопки главного меню, px")]
    public int ButtonHeight = 56;
    [Tooltip("Ширина кнопки главного меню, px")]
    public int ButtonWidth = 400;
    [Tooltip("Шаг кнопок главного меню, px")]
    public int ButtonStep = 80;
    [Tooltip("Радиусы скруглений R_SM/R_MD/R_LG")]
    public int RadiusSm = 3;
    public int RadiusMd = 6;
    public int RadiusLg = 10;
    [Tooltip("Тень под панелями")]
    public bool PanelShadow = true;

    [Header("Мир: свет и тьма (textures/game_renderer, 1:1)")]
    public Color DarkTint = C(5, 7, 12, 135f / 255f);
    public float TorchGlowK = 88f;

    [Header("Фон меню (menu_background.py)")]
    public Color MenuEmber = C(240, 190, 110);
    public int MenuEmbers = 44;
    public int MenuBackgroundSeed = 20241114;

    // ── Совместимость со старыми именами (используются экранами) ──
    public Color WindowBg => Panel;
    public Color WindowBgTop => PanelHeader;
    public Color Inset => SlotFill;
    public Color Bronze => PanelBorder;
    public Color BronzeHi => Gold;
    public Color GoldSoft => Title;
    public Color Ink => Text;
    public Color InkDim => TextDim;
    public Color InkFaint => TextMuted;
    public Color Life => Hp;
    public Color Mana => ManaPy;
    public Color Experience => Xp;
    public Color Danger => Dmg;
    public Color Success => Heal;
    public int BodySize2 => BodySize;

    /// <summary>Тема: ассет Resources/UITheme (создаётся меню Aethelgard),
    /// иначе — значения по умолчанию.</summary>
    public static UITheme Default
    {
        get
        {
            if (_default == null)
            {
                _default = Resources.Load<UITheme>("UITheme");
                if (_default == null)
                {
                    _default = CreateInstance<UITheme>();
                    _default.name = "UITheme (runtime default)";
                }
            }
            return _default;
        }
    }
    private static UITheme? _default;

    /// <summary>Цвет из 0–255 компонент (как в python).</summary>
    public static Color C(int r, int g, int b, float a = 1f) =>
        new(r / 255f, g / 255f, b / 255f, a);

    /// <summary>Приглушить/осветлить цвет множителем (аналог shade() из textures.py).</summary>
    public static Color Shade(Color c, float k) => new(c.r * k, c.g * k, c.b * k, c.a);

    /// <summary>Кривая холодного света из textures.apply_light:
    /// тени уходят в сине-зелёный, не в серый.</summary>
    public static Color ApplyLight(Color c, float f)
    {
        f = Mathf.Clamp01(f);
        return new Color(c.r * (0.28f + 0.72f * f), c.g * (0.32f + 0.68f * f),
            c.b * (0.40f + 0.60f * f), c.a);
    }
}
