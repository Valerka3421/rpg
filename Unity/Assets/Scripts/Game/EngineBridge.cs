// Мостики движкового слоя Unity: то, что MonoGame-версия берёт из
// World/Game.cs (AethelgardGame) и Audio/AudioSystem.cs, здесь живёт
// по-другому, но с теми же именами — GameController перенесён дословно
// и ссылается на эти типы.
using UnityEngine;

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

namespace Aethelgard.Audio
{
    /// <summary>Заглушка звука: имя эффекта логируется. Настоящий
    /// AudioSource-пул подключается позже (клипы в Resources/Sounds).</summary>
    public class AudioSystem
    {
        public void Play(string name)
        {
            // TODO: UnityEngine.AudioSource + клипы из Resources/Sounds
            Debug.Log($"[audio] {name}");
        }

        /// <summary>Громкость 0..1 (Settings.ShowFps-аналог: читается настройками).</summary>
        public float Volume { get; set; } = 0.8f;

        public void Dispose() { }
    }
}

namespace Aethelgard.Render
{
    using Microsoft.Xna.Framework;

    /// <summary>Геометрия журнала чата в координатах дизайна 1920×1080 —
    /// GameController использует её для кликов по вкладкам. Настоящая
    /// uGUI-панель чата будет рисовать по этим же координатам.</summary>
    public static class ChatPanel
    {
        public static readonly string[] TabNames =
            { "Всё", "Бой", "Предметы", "Общение", "Опыт" };

        public static Rectangle TabRect(int i) => new(8 + i * 220, 1080 - 92, 210, 34);
    }
}
