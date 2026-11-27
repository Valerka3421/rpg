using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Screens = Aethelgard.Systems.ScreenMode;
namespace Aethelgard.Systems;

/// <summary>
/// Древо навыков: изучение узлов, повышение класса, клики по панели.
/// Выделено из GameController (частичный класс).
/// </summary>
public partial class GameController
{
    private void HandleSkillsInput(KeyboardState keys, KeyboardState prevKeys)
    {
        // Tab switching: Q/E
        if (keys.IsKeyDown(Keys.Q) && !prevKeys.IsKeyDown(Keys.Q))
            SkillTab = Math.Max(0, SkillTab - 1);
        if (keys.IsKeyDown(Keys.E) && !prevKeys.IsKeyDown(Keys.E))
            SkillTab = Math.Min(3, SkillTab + 1);

        // Number keys 1-7: learn class skill node
        if (SkillTab == 0)
        {
            for (int i = 0; i < 7; i++)
            {
                var key = Keys.D1 + i;
                if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                {
                    TryLearnNode(i);
                }
            }
        }

        // Class promotion: P key (when available)
        if (keys.IsKeyDown(Keys.P) && !prevKeys.IsKeyDown(Keys.P))
        {
            TryPromoteClass();
        }
    }

    /// <summary>
    /// Handle input in shop mode.
    /// </summary>
    private void TryLearnNode(int index)
    {
        if (ProgressionSystem.LearnNode(Player, index, out string message))
        {
            ShowMessage(message);
            GameChat.Instance.Xp(message);   // изучение навыка — вкладка «Опыт»
        }
        else if (message.Length > 0)
            ShowMessage(message);
    }

    /// <summary>Клик мышью по узлу древа навыков (из SkillScreen.Draw).
    /// SkillScreen возвращает команду только на «свежий» клик (один раз
    /// на нажатие), здесь остаётся только исполнить её.</summary>
    public void HandleSkillClick(string? command)
    {
        if (string.IsNullOrEmpty(command)) return;

        // Кнопка эволюции класса
        if (command.StartsWith("promote:"))
        {
            string newKey = command["promote:".Length..];
            if (ClassData.Promote(Player, newKey))
            {
                ShowMessage($"Новый класс: {ClassData.Get(newKey)?.Name}!");
                GameChat.Instance.Level($"Класс сменён: {ClassData.Get(newKey)?.Name}!");
            }
            else
                ShowMessage($"Смена класса доступна с уровня " +
                    $"{ClassData.ClassChangeLevels[Math.Min(Player.ClassChanges, ClassData.ClassChangeLevels.Length - 1)]}.");
            return;
        }

        // Изучение узла
        if (command.StartsWith("learn:"))
        {
            string nodeKey = command["learn:".Length..];
            var nodes = SkillTree.GetNodes(Player);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].Key == nodeKey)
                {
                    TryLearnNode(i);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Try to promote to the first available class.
    /// </summary>
    private void TryPromoteClass()
    {
        if (ProgressionSystem.Promote(Player, out string message))
        {
            ShowMessage(message);
            GameChat.Instance.Level(message);   // смена класса — вкладка «Опыт»
        }
        else if (message.Length > 0)
            ShowMessage(message);
    }

    /// <summary>
    /// Use or equip item at given index on current inventory page (сетка 10×10).
    /// </summary>
}
