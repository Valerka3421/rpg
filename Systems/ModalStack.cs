using System.Collections.Generic;
namespace Aethelgard.Systems;

/// <summary>
/// Стек модальных экранов одиночной игры (InputMapper-lite): открытие окна
/// кладёт его на вершину, закрытие снимает, ESC всегда означает «шаг вверх»
/// и обрабатывается в одном месте — <see cref="GameController.HandleEscape"/>.
/// Раньше закрытие модалей было размазано по трём слоям (World/Game.cs,
/// GameController, обработчики внутри экранов) и ESC-контракт держался
/// вручную — часть нажатий терялась.
/// </summary>
public class ModalStack
{
    private readonly Stack<string> _stack = new();

    public bool IsOpen => _stack.Count > 0;

    /// <summary>Открытый модал на вершине (null — стек пуст, мы в игре).</summary>
    public string? Current => _stack.Count > 0 ? _stack.Peek() : null;

    public void Push(string mode)
    {
        if (_stack.Count == 0 || _stack.Peek() != mode) _stack.Push(mode);
    }

    public string? Pop()
    {
        if (_stack.Count == 0) return null;
        return _stack.Pop();
    }

    /// <summary>Полный сброс (новая игра, выход в меню, загрузка).</summary>
    public void Clear() => _stack.Clear();
}
