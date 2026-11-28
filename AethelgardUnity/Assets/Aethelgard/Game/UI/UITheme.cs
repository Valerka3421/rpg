using UnityEngine;

namespace Aethelgard.UI;

/// <summary>
/// Тема интерфейса «Этельгард» (стиль Stoneshards): тёмное дерево, бронза,
/// приглушённое золото, пергаментный текст.
///
/// ЭТО ГЛАВНАЯ ТОЧКА НАСТРОЙКИ ВНЕШНЕГО ВИДА UI:
/// актив «Assets/Aethelgard/Settings/UITheme.asset» открывается в
/// инспекторе — правка любого поля сразу меняет все меню и окна игры
/// (при следующем открытии экрана или в Play Mode на лету).
/// Размеры шрифтов, отступы и радиусы тоже здесь.
/// </summary>
[CreateAssetMenu(fileName = "UITheme", menuName = "Aethelgard/UI Theme")]
public class UITheme : ScriptableObject
{
    [Header("Окна и панели")]
    public Color WindowBg = new(0.066f, 0.055f, 0.039f);   // фон окна
    public Color WindowBgTop = new(0.11f, 0.09f, 0.063f);  // верх градиента
    public Color Inset = new(0.094f, 0.078f, 0.059f);      // углубления
    public Color RowHover = new(0.204f, 0.169f, 0.118f);
    public Color RowSelect = new(0.259f, 0.208f, 0.118f);

    [Header("Рамки и акценты")]
    public Color Bronze = new(0.376f, 0.306f, 0.18f);
    public Color BronzeHi = new(0.62f, 0.502f, 0.282f);
    public Color Gold = new(0.878f, 0.761f, 0.471f);
    public Color GoldSoft = new(0.808f, 0.651f, 0.322f);

    [Header("Текст")]
    public Color Ink = new(0.878f, 0.839f, 0.761f);
    public Color InkDim = new(0.588f, 0.549f, 0.471f);
    public Color InkFaint = new(0.408f, 0.373f, 0.306f);

    [Header("Полосы состояния")]
    public Color Life = new(0.745f, 0.243f, 0.227f);
    public Color Mana = new(0.376f, 0.573f, 0.847f);
    public Color Experience = new(0.329f, 0.612f, 0.863f);
    public Color Danger = new(0.816f, 0.361f, 0.282f);
    public Color Success = new(0.549f, 0.745f, 0.471f);

    [Header("Шрифты (названия системных шрифтов Windows)")]
    public string FontName = "Segoe UI";
    public int H1Size = 40;
    public int H2Size = 22;
    public int BodySize = 18;
    public int SmallSize = 14;
    public int TinySize = 12;

    [Header("Геометрия")]
    [Tooltip("Толщина рамки панелей, px")]
    public int FrameThickness = 2;
    [Tooltip("Внутренний отступ окон, px")]
    public int Padding = 20;
    [Tooltip("Высота строки списка, px")]
    public int RowHeight = 46;
    [Tooltip("Высота кнопки, px")]
    public int ButtonHeight = 52;
    [Tooltip("Тень под панелями")]
    public bool PanelShadow = true;

    [Header("Фон меню")]
    public Color MenuSkyTop = new(0.10f, 0.09f, 0.14f);
    public Color MenuSkyBottom = new(0.24f, 0.14f, 0.10f);
    public int MenuEmbers = 40;

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
}
