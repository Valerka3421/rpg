// Мостик движкового слоя Unity: то, что MonoGame-версия берёт из
// Desktop/Game.cs (AethelgardGame), здесь живёт по-другому, но с тем же
// именем — GameController перенесён дословно и ссылается на этот тип.
// Сопутствующие мосты вынесены рядом: AudioSystem.cs (процедурный звук)
// и ChatPanel.cs (геометрия журнала чата).
namespace Aethelgard.Systems
{
    /// <summary>Статика окна: масштаб мыши (letterbox). GameApp обновляет
    /// каждый кадр из RectTransform/камеры.</summary>
    public static class AethelgardGame
    {
        public static float MouseScaleX = 1f;
        public static float MouseScaleY = 1f;
        public const int ScreenW = 1920;
        public const int ScreenH = 1080;
    }
}
