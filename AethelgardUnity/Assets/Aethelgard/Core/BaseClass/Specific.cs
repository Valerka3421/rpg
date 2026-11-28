using Microsoft.Xna.Framework;

namespace Aethelgard.BaseClass;

/// <summary>
/// Базовый класс любого существа мира (в стиле проекта «Пример»):
/// приватные защищённые поля + публичные свойства-доступа.
/// От него наследуются Entity (и далее Player / Monster / Npc).
/// </summary>
public class Specific
{
    // ── Приватные защищённые поля ──
    private protected int _x;
    private protected int _y;
    private protected int _hp;
    private protected int _maxHp;
    private protected int _atk;
    private protected int _df;
    private protected string _name = "";
    private protected char _ch = '?';
    private protected Color _color = Color.White;
    private protected int _xpValue;
    private protected bool _boss;
    private protected bool _immortal;
    private protected string? _ability;
    private protected (int Dx, int Dy) _facing = (1, 0);

    // Статус-эффекты: { "burning": turns_left, "slow": turns_left, ... }
    private protected Dictionary<string, int> _statuses = new();

    // Таймеры действий/анимации
    private protected float _actTimer;
    private protected float _breathTimer;
    private protected float _animTime;
    private protected float _attackAnim;
    private protected float _moveBob;
    private protected float _statusAccum;

    // ── Публичные свойства ──
    public int X { get => _x; set => _x = value; }
    public int Y { get => _y; set => _y = value; }
    public int Hp { get => _hp; set => _hp = value; }
    public int MaxHp { get => _maxHp; set => _maxHp = value; }
    public int Atk { get => _atk; set => _atk = value; }
    public int Df { get => _df; set => _df = value; }
    public string Name { get => _name; set => _name = value; }
    public char Ch { get => _ch; set => _ch = value; }
    public Color Color { get => _color; set => _color = value; }
    public int XpValue { get => _xpValue; set => _xpValue = value; }
    public bool Boss { get => _boss; set => _boss = value; }
    public bool Immortal { get => _immortal; set => _immortal = value; }
    public string? Ability { get => _ability; set => _ability = value; }
    public (int Dx, int Dy) Facing { get => _facing; set => _facing = value; }

    public Dictionary<string, int> Statuses
    {
        get => _statuses;
        set
        {
            _statuses = value ?? new Dictionary<string, int>();
            StatusVersion++;
        }
    }

    /// <summary>Растёт при любом изменении статусов — для инвалидации кэшей.</summary>
    public int StatusVersion { get; private set; }

    public void MarkStatusesChanged() => StatusVersion++;

    // Таймер хода ИИ. В оригинале поле называлось ActTimer, а код GameController
    // и Net/GameCore обращался к несуществующему ActionTimer — теперь оба имени работают.
    public float ActionTimer { get => _actTimer; set => _actTimer = value; }
    public float ActTimer { get => _actTimer; set => _actTimer = value; }
    public float BreathTimer { get => _breathTimer; set => _breathTimer = value; }
    public float AnimTime { get => _animTime; set => _animTime = value; }
    public float AttackAnim { get => _attackAnim; set => _attackAnim = value; }
    public float MoveBob { get => _moveBob; set => _moveBob = value; }

    public bool Alive => _hp > 0;

    // ── Работа со статусами ──
    public bool HasStatus(string key) => _statuses.ContainsKey(key);

    public void AddStatus(string key, int? duration = null)
    {
        var def = StatusData.Get(key);
        if (def == null) return;
        int d = duration ?? def.Duration;
        _statuses[key] = Math.Max(_statuses.GetValueOrDefault(key), d);
        StatusVersion++;
    }

    public void RemoveStatus(string key)
    {
        if (_statuses.Remove(key)) StatusVersion++;
    }

    public void ClearStatuses()
    {
        _statuses.Clear();
        StatusVersion++;
    }

    /// <summary>
    /// Один тик всех статус-эффектов (урон/лечение + уменьшение длительности).
    /// Вызывается GameController каждые Config.StatusTickInterval секунд.
    /// В оригинале TickStatuses() вызывался, но нигде не был объявлен.
    /// </summary>
    public void TickStatuses(Action<string, int, Color>? onTick = null)
    {
        StatusData.Tick(this, onTick);
    }
}
