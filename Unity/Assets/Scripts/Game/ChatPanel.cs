using Microsoft.Xna.Framework;

namespace Aethelgard.Render
{
    /// <summary>Геометрия журнала чата в координатах дизайна 1920×1080 —
    /// GameController использует её для кликов по вкладкам. Настоящая
    /// uGUI-панель чата рисует по этим же координатам (ChatUi.cs).</summary>
    public static class ChatPanel
    {
        public static readonly string[] TabNames =
            { "Всё", "Бой", "Предметы", "Общение", "Опыт" };

        public static Rectangle TabRect(int i) => new(8 + i * 220, 1080 - 92, 210, 34);
    }
}
