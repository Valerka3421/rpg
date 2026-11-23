namespace Aethelgard.Structs;

/// <summary>
/// Сборный ресурс (дерево/руда/куст) на карте. Перенесён из HarvestSystem.
/// </summary>
public class HarvestRecord
{
    public string Kind = "";
    public int Hp;
    public int MaxHp;
    public bool Has;
    public double ReadyAt;
    public double SpawnedAt;
}
