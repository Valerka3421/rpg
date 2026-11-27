# Архитектура Aethelgard 0.3.0 (переработанная)

Переработка проекта [Valerka3421/rpg](https://github.com/Valerka3421/rpg.git)
(Aethelgard, MonoGame, .NET 8) по образцу архитектуры проекта «Пример»:
иерархия базовых классов с приватными полями и свойствами, фабрики Spawn
для монстров, данные в структурах и перечислениях, аккуратные namespace.

## 1. Слои и каталоги

Ядро игры (BaseClass/Entities/Structs/Data/World/Systems) живёт в
Unity-проекте (`Unity/Assets/Scripts/Core/`) и линкуется в MonoGame-сборку
через `Compile Include` в `Aethelgard.csproj` — двойная компиляция: один и
тот же код работает и на desktop (MonoGame), и в Unity (через shim
`Core/Compat/`).

```
Aethelgard/
├── Program.cs                  — точка входа desktop (namespace Aethelgard)
├── GlobalUsings.cs             — глобальные using всех слоёв desktop
├── Aethelgard.csproj/.sln      — MonoGame-сборка (ядро линкуется из Unity)
├── Desktop/                    — MonoGame-хост (namespace Aethelgard.Desktop)
│   └── Game.cs                 — AethelgardGame : XnaGame (цикл, режимы экранов)
├── Systems/                    — desktop-частъ GameController (partial-файлы
│   │                             GameController.*.cs — SP/MP/город/инвентарь/
│   │                             магазин/крафт/навыки) + DevConsole
├── Render/                     — экраны и рендеры MonoGame (UiKit, SpriteStore,
│   │                             RuntimeFont, MpRenderer, ChatPanel…)
│   ├── icons/, tiles/          — процедурные иконки/тайлы
├── Audio/                      — процедурный синтез звука (AudioSystem.cs)
├── Content/fonts/              — шрифты DejaVu (свободная лицензия)
├── assets/                     — иконки навыков, спрайты, иконка приложения
├── tools/                      — gen_glyph_atlas.py (атлас глифов для Unity)
├── tests/
│   ├── Aethelgard.Tests/       — xUnit-тесты (сохранения, сеть, чат, данные)
│   └── HangProbe/              — диагностический хост (проверка зависаний)
└── Unity/                      — Unity-проект (6000.x)
    ├── Assets/Scripts/Core/    — ЯДРО (двойная компиляция, см. выше):
    │   ├── BaseClass/          — Specific: приватные поля + свойства, статусы
    │   ├── Entities/           — Entity : Specific, Player, Npc, Monsters/ (24)
    │   ├── Structs/            — Tile, Room, FloatText, HarvestRecord, ItemInstance
    │   ├── World/              — Dungeon/DungeonGen/WorldGen/Overworld/
    │   │                          Locations/Projectiles
    │   ├── Systems/            — GameController (ядро логики), CombatSystem,
    │   │                          Rules, MonsterAI, PlayerActions, SkillTree,
    │   │                          QuestLog, SaveSystem, Settings, Modes, …
    │   ├── Data/               — таблицы в C#: *Defs.cs + логика *Data.cs
    │   ├── Net/                — кооп: Client/Server/GameCore/Snapshot/
    │   │                          Protocol/EcdhInterop
    │   ├── Render/             — PixelCanvas/TileArt (процедурные текстуры)
    │   └── Compat/             — shim Xna/ввода (только Unity-сборка)
    └── Assets/Scripts/Game/    — движковый слой Unity (asmdef Aethelgard.Game):
        ├── GameApp.cs          — драйвер (ввод, камера) + WorldView (спрайты,
        │                          HP-бары, MP-снапшоты) + Hud
        ├── GameController*.cs  — partial-продолжения (дословный порт Systems/)
        ├── UiKitCanvas.cs      — uGUI-порт UiKit + UiInput (хит-тесты)
        ├── MenuUi/MpUi/ChatUi  — меню, MP-меню, чат (uGUI)
        ├── InventoryUi …       — экраны: инвентарь, магазин, навыки, крафт,
        │                          квесты, характеристики, диалог, пауза
        ├── AudioSystem.cs      — процедурный звук (AudioClip + AudioSource-пул)
        ├── ChatPanel.cs        — геометрия журнала чата (мост для GameController)
        ├── EngineBridge.cs     — AethelgardGame (статика окна)
        └── DevConsole.cs       — dev-консоль Unity
```

Направление зависимостей: `BaseClass → (Entities, Data) → (World, Systems) →
(Render, Net, Audio) → Desktop / Unity Game`. Циклы namespace между Entities
и Data допустимы в одной сборке и остаются из-за API оригинала
(MonsterData.CreateEntity ↔ Entity).

## 2. Что сделано в стиле «Пример»

| Приём из «Пример» | Реализация здесь |
|---|---|
| Базовый класс `Specific` с `_полями` и свойствами | `BaseClass/Specific.cs` — статы, статусы, таймеры любого существа |
| Наследники с собственными файлами | `Entities/Player.cs`, `Entities/Monsters/*.cs` (24 файла), `Entities/Npc.cs` |
| Фабрика `Spawn(lvl, список)` у каждого моба | `Monster.Spawn(depth, list)` + `InitFromDef` + `MonsterFactory.Create` |
| `Structs/` со структурами и перечислениями | `Structs/Tile.cs`, `Room.cs`, `HarvestRecord.cs`, `ItemInstance.cs` |
| `World/` с миром и циклом | мир — `Unity/Assets/Scripts/Core/World/` (Dungeon/WorldGen/…), игровой цикл — `Desktop/Game.cs` |
| Единые namespace по папкам | `Aethelgard.BaseClass / .Entities / .Structs / .World / .Systems / .Data` |

## 3. Исправленные проблемы оригинала

Снапшот оригинала **не компилировался** (20+ ошибок) и содержал дрейф API.
Все перечисленное исправлено с сохранением поведения:

1. **Дубли типов**: `Projectile` (Entities.cs и Projectiles.cs), `StatusData`
   (Core/StatusData.cs и Core/Data/StatusData.cs), `Room` (Entities.cs и
   вложенный в DungeonGen) — оставлен один экземпляр каждого.
2. **Несуществующие члены, на которые ссылался код** (реализованы):
   - `Player.UseItem(key)` — использование зелий/еды/книг/свитков;
   - `Player.TickStatuses()` и `Entity.ActionTimer` — тик статусов и таймер ИИ;
   - `Player.CraftLevels / CraftXp` — крафт-ветки (сохранения и UI);
   - `ClassData.Promote / AvailablePromotions` и `Def.StrBonus…DfStatBonus` —
     повышения класса на 5/15 уровнях;
   - `MonsterData.PoolForDepth(depth)` — пул монстров этажа;
   - `ItemData.ChestLoot(depth)` — лут сундуков;
   - `ItemData.DropTable` — таблица дропа (строится из MonsterDrops);
   - `ItemData.Def.Price` — цена для магазина;
   - `QuestLog.PopulateBoard()` — доска заданий;
   - `SkillTree.Aggregate(player)` — пассивные эффекты узлов;
   - `SkillTree` ссылался на `Def.BaseClass` вместо `Def.Base`.
3. **Python-артефакты генератора таблиц**: `None` и `new[] { }` в
   ClassTable.g.cs — таблицы переведены в JSON, проблема исчезла.
4. **Несоответствия типов**: `Dungeon.Rooms` получал кортежи вместо `Room`;
   `SaveSystem` писал `Dictionary<string,int>` в `Dictionary<string,QuestState>`;
   `Harvestables` заполнялся строками вместо `HarvestRecord` — исправлено.
5. **Отсутствующие using**: Render-файлы не видели `GameController`/`DevConsole`
   (CS0246) — решено `GlobalUsings.cs`.
6. **Инверсия слоя**: `SaveSystem` (Core) зависел от `GameController` (Game) —
   оба теперь в одном слое `Systems`.
7. **Магазин**: `ShopStock`/`Pos` у `NpcDef`, `BuyItem` через записи склада с
   ценой лавки, а не базовой ценой предмета.
8. **Рассинхрон дропа**: `Rules.AwardKill` сопоставлял имя монстра ключу через
   switch — теперь используется `MonsterKey` от фабрики.
9. **Game1 / Game.ScreenW** — пережитки шаблона MonoGame, заменены на
   `AethelgardGame`.

## 4. Данные: C#-таблицы → JSON

7 сгенерированных таблиц (`*Table.g.cs`, ~2000 строк) конвертированы в JSON
(`data/*.json`) и загружаются через `DataLoader` (System.Text.Json,
`IncludeFields`, цвет `R,G,B`). Каталог данных переопределяется переменной
`ETHELGARD_DATA_DIR`. Таблицы копируются в вывод сборки тегом `Content`.

## 5. Проверка

- `dotnet build Aethelgard.sln -c Release -warnaserror` — **0 ошибок,
  0 предупреждений** (оригинал: 20+ ошибок).
- `dotnet test Aethelgard.sln -c Release` — **38 тестов xUnit**: round-trip
  сохранений и всех полей `FloorData`, версия сейва, JSON-протокол и HMAC,
  PBKDF2-пароли, эффекты классов/узлов, детерминизм оверворлда, подземелья
  и локаций, **валидатор ссылочной целостности данных** (WType классов ⊆
  предметы, стартовое снаряжение, материалы рецептов, чертежи, награды и
  цели квестов).
- GitHub Actions (`.github/workflows/ci.yml`) повторяет restore → build
  с `-warnaserror` → test на `windows-latest`.

## 6. Ограничения

- Проверены компиляция и логика ядра xUnit-тестами; полноценный запуск окна
  MonoGame в headless-песочнице невозможен.
- `RuntimeFont` использует System.Drawing: сборка кроссплатформенная, но
  рендер шрифта рассчитан на Windows (как в оригинале).
- Функциональность сохранена полностью: кооператив (Net), процедурный звук
  (Audio), dev-консоль, 42 класса, 24 вида монстров, 4 босса.

## 12. Паритет с веткой python-final

Сверка с pygame-версией (ветка `python-final`, ~27 400 строк) выполнена
21.11.2026. Совпадают: 135 предметов, 42 класса (дерево T1→T2→T3,
уровни повышения 5/15), 20 мобов + 4 босса (этажи 5/10/15/20), 6 квестов +
доска поручений, 10 статусов, городские NPC и лавки, 3 ремесленные ветки
(кривая 45·lvl^1.35), сбор ресурсов (tree/ore/bush с регроу), 13 локаций
120×70 + оверворлд 140×70, подземелье до 20 этажа, формат сейва v8,
настройки, сеть (TCP:9000, 20 Гц, HMAC, максимум 4 игрока), 12 событий
аудио, 21 команда дев-консоли, хоткеи.

Устранённые расхождения:

| Область | Было | Стало (как в python-final) |
|---|---|---|
| Уровни узлов ветки | 1/5/9/13/17/21/25 | 1,3,5,7,9,11,13,15,17,19,21,25 |
| Эффекты узлов по тиру | без масштаба | T2 ×1.4, T3 ×1.8 (`_TIER_SCALE`) |
| Повышение класса | только смена ключа | бонусы статов + 2 SP + сохранение ветки |
| Дев-консоль goto/depth/fps | заглушки | полная реализация |
| MP wait/quest_talk/duel/save_world | отсутствовали | реализованы + файлы `savegame_mp_*.json` |
| Snapshot | без ClassTier/Inventory | добавлены поля |
| Инвентарь U/V/H/B/N | только U/V (head/legs) | все 5 слотов |
| FPS | всегда виден | только при show_fps |

Документированное отличие: у Python каждая специализация имеет собственное
перетасованное дерево узлов (детерминированный хеш-порядок, пулы имён);
в C# ветка общая для архетипа, а уникальность специализаций достигается
масштабом эффектов T2/T3. Функциональный результат эквивалентен: узлы не
сгорают при повышении, бонусы специализации сильнее базовых.

Ограничение среды: headless-песочница не открывает окно MonoGame, поэтому
рендер проверен только компиляцией; логика покрыта xUnit-тестами (17).

## 13. Данные в классах (перенос из JSON)

Все игровые таблицы перенесены из `data/*.json` в C#-классы; каталог
`data/` и загрузчик `DataLoader` удалены, проект больше не читает
JSON-таблицы. `ETHELGARD_DATA_DIR` остался только для каталога сейвов
и настроек.

| Таблица | Где живёт | Объём |
|---|---|---|
| Монстры | по классу на вид: `Entities/Monsters/*.cs` (Rat … AncientDragon), каждый вид держит свои Hp/Atk/Df/Xp/Ch/Color/Ability/босс-параметры | 24 |
| Реестр монстров и дропы | `Data/MonsterData.cs`: All строится из классов через `MonsterFactory.AllKeys`; BossByDepth, BossDrops, MonsterDrops, ManaCrystal* в коде | — |
| Предметы | `Data/ItemDefs.cs` → `ItemData.All` | 135 |
| Классы персонажей | `Data/ClassDefs.cs` → `ClassData.All` | 42 |
| Статусы | `Data/StatusDefs.cs` → `StatusData.All/Mods/AbilityToStatus` | 10 |
| Город | `Data/CityDefs.cs` → `CityData.*` | 4 NPC |
| Квесты | `Data/QuestDefs.cs` → `QuestData.Quests` | 6 |
| Ремёсла | `Data/RecipeDefs.cs` → `RecipeData.Skills/SkillOrder/CraftTabs` | 3 |

Файлы `Data/*Defs.cs` были сгенерированы из прежних JSON-таблиц; сейчас они
редактируются напрямую как обычный C#-код. Монстр-классы
(`Entities/Monsters/*.cs`) также поддерживаются вручную.

Попутный фикс: `ItemData.Def.Kind` теперь вычисляется из `Type`
(`KindOf`) при построении таблицы — в JSON-схеме поле Kind было
артефактом дампа, и при переносе в код тип предмета нужно задавать явно.

## 14. Паритет контента мультиплеера и сингла

Аудит показал, что MP-клиент получал только движение/атаку/лестницы/чат.
Теперь MP повторяет контент сингла:

| Функция | Сингл | Мультиплеер |
|---|---|---|
| Инвентарь (исп./экипировка) | I, 1–8, Shift+1–8 хотбар | I (панель), те же клавиши через UseItemKey/QuickbarSlot |
| Снятие экипировки | U/V/H/B/N | U/V/H/B/N (UnequipSlot) |
| Характеристики | C, 1–6 | C (панель), AllocateStat |
| Дерево навыков | N, 1–7 учить | N (панель), LearnNodeIdx |
| Активные навыки | F1–F4/F6–F8/F10 | панель N → Q/E → 1–8 (SkillIdx) |
| Повышение класса | P | P (Promote) |
| Квесты (принять/сдать) | L, Enter | L (панель, доска из снапшота), 1–9 |
| Магазин | B у торговца, S/R/1–9 | B (панель у торговца), S/R/1–9, BuyIdx/SellIdx/Reroll |
| Добыча ресурсов | E | E → HarvestX/Y (живые узлы в снапшоте) |
| Разговор с NPC | T | T → QuestTalk (NpcType в снапшоте) |
| Пропуск хода | — | X (wait) |
| Дуэль | — | Z (duel_request, ближайший игрок) |
| Сохранение | F5 (слот 1) | F5 → save_world (хост) |
| Карта | тайлы Dungeon | тайлы в снапшоте (строки '0'+Tile), те же процедурные текстуры |

Сервер валидирует MP-действия так же, как SP: покупка/продажа/реролл —
только рядом с торговцем, квесты — только рядом со старейшиной, обмен —
на дистанции ≤ 1 и не с самим собой.

Исправленные баги: уничтожение шмоток в Player.UseItem (снятие предмета
до switch), неработающий ввод текста MP-чата (был заглушкой), цепочка
зелий H с несуществующими potion_hp_s/potion_hp_m (теперь
potion_s → potion_m → potion_l), покупка из любой точки подземелья.

Крафт-ветки SkillTree.CraftBranches теперь содержат только реально
существующие предметы таблицы ItemDefs (раньше упоминались
несуществующие potion_hp_s / potion_mana_s / chainmail / cap / helm /
platemail) — все ключи сверены с ItemDefs.

## 15. Главное меню и русификация (v2.4.0)

Игра полностью русскоязычная. Точка входа — главное меню
(`Render/MainMenuScreen.cs`, паритет `game/menu_controller.py` +
`render/menu_renderer.py`):

```
Главное:    Начать игру · Загрузить игру · Мультиплеер · Настройки · Создатели · Выйти
Пауза:      Вернуться в игру · Сохранить игру · Загрузить игру · Настройки · Выйти в главное меню
Сейвы:      3 слота (Enter/клик, N — новая игра в пустой слот, D — удалить)
Настройки:  Разрешение · Полный экран · Громкость · Показ FPS · Мини-карта · Управление
Мультиплеер: Продолжить игру / Начать новую игру / Присоединиться (если есть MP-сейв),
             иначе Создать игру / Присоединиться к игре → ввод IP хоста
```

Навигация: ↑/↓/W/S, Enter/Space, Esc (возврат на уровень вверх), мышь.
Настройки хранятся в `settings.json` (Systems/Settings.cs), окно меняет
разрешение/полный экран через GraphicsDeviceManager; отрисовка всегда идёт
в render target 1920×1080 и масштабируется под размер окна.

Мультиплеер из меню: «Продолжить игру» поднимает сервер и восстанавливает
мир из `savegame_mp_world.json` + игроков из `savegame_mp_players.json`
(`GameCore.LoadWorld` / `RestoreSavedPlayer` — parity net/core.py);
«Создать/Начать новую» идёт через выбор класса с флагом PendingMpHost;
«Присоединиться» подключается к `host:port`.

Кириллица: RuntimeFont генерирует атлас ASCII + А–я + Ё/ё + типографику
(«» — – … · № • ↑↓←→ × §) в двух кеглях (14 интерфейс, 22 меню).
Раньше атлас был ASCII-only, из-за чего русский текст рисовался «?» —
поэтому интерфейс был переведён на английский. Все строки интерфейса
(HUD, экраны, меню, консоль, MP-панели, диалоги, названия локаций/NPC/
узлов навыков/веток крафта) приведены к русским названиям python-final.

## 16. Надёжность, безопасность и тесты

| Область | Решение |
|---|---|
| Сохранения | DTO только со свойствами (System.Text.Json не видит public-поля), версия сейва проверяется, запись атомарная (`.tmp` + `File.Replace`), `LastError` вместо молчаливого сбоя |
| Полнота мира | `FloorData` хранит тайлы, комнаты, монстров, предметы, ресурсы, сундуки, ассортимент торговца, ворота (`Transitions`), барьеры и разведанные клетки; MP-хост сохраняет все посещённые этажи и локации |
| Сеть | `Protocol.Version` отклоняет несовпадающие клиенты, общие `JsonOptions`, UTF-8 stateful decoder, лимит размера сообщения, rate limit, HMAC-SHA256 по канонической нагрузке |
| Пароли | PBKDF2-HMAC-SHA256, 210 000 итераций, случайная соль, `CryptographicOperations.FixedTimeEquals`; `ETHELGARD_DEV_PASSWORD_HASH` (готовый хеш) или legacy `ETHELGARD_DEV_PASSWORD` (хешируется при старте) |
| Детерминизм | `Config.WorldSeed` + локальные `Random(seed)` в `WorldGen`, `DungeonGen`, `Locations`, `RecipeData.MakeShopStock`; статические генераторы удалены |
| Эффекты | Проценты хранятся долями (0.15), статы пересчитываются через модификаторы, базовые значения не мутируются; кэш сбрасывается по `StatusVersion` |
| Рендер | Процедурные текстуры строятся на CPU через `PixelCanvas`, `RenderTarget2D` не переключается внутри активного `SpriteBatch` |
| CI | `dotnet build -warnaserror` + `dotnet test` на `windows-latest` (шрифты используют Windows-only System.Drawing) |

Осознанные ограничения: `GameController` и `Net/GameCore` остаются
контроллерами состояния и ввода (SP и авторитетный MP-сервер) и намеренно
не сливаются в один класс — у них разные источники ввода и разные модели
синхронизации. Общие правила при этом вынесены в `Systems`: бой и награды
(`CombatSystem`, `Rules`), создание персонажа (`CharacterFactory`), ИИ
монстров (`MonsterAI`), навыки/магазин/статы/прогресс (`PlayerActions`).
`World/Projectiles.cs` (ядро, `Unity/Assets/Scripts/Core/World/`) подключён к активному циклу и в SP, и в MP
(визуальные снаряды передаются в снапшоте и рисуются `MpRenderer`), а
`Systems/Modes.cs` — единый источник констант экранов для `Desktop/Game.cs`
и `GameController`.

## 17. Общая симуляция синглплеера и MP

| Модуль | Что внутри | Кто использует |
|---|---|---|
| `Systems/CharacterFactory.cs` | создание `Player`, бонусы класса, стартовый набор и автоэкипировка | `GameController.NewGame`, `GameCore.AddPlayer` |
| `Systems/CombatSystem.cs` | поиск ближайшей цели/цели по взгляду, первый и двойной удар, выдача награды и удаление монстра | SP-атаки и навыки, MP `PlayerAttack`/`TryMovePlayer`/`KillMonster` |
| `Systems/MonsterAI.cs` | таймеры действий, flee/healer/ranged, дыхание босса, шаг к цели | `GameController.UpdateMonsters`, `GameCore.Update` |
| `Systems/PlayerActions.cs` | `SkillSystem`, `ShopSystem`, `StatSystem`, `ProgressionSystem`, `ItemSystem` | панели SP, MP-сообщения и серверные обработчики |
| `Systems/Rules.cs` | урон, крит/уклонение, лут, XP/золото, крафт, магия | оба контроллера и `MonsterAI` |

`GameController` и `GameCore` теперь отвечают за состояние, ввод и
синхронизацию, а не за копии правил. Это позволяет менять баланс или
исправлять бой в одном месте без расхождения между синглом и кооперативом.

## 18. Журнал, торговля, надёжность (0.3.0)

**Журнал с вкладками** (`Systems/GameChat.cs` + `Render/ChatPanel.cs`):
вкладки «Всё · Бой · Предметы · Общение · Опыт», каждая хранит последние
200 сообщений, одно событие попадает в несколько вкладок (лут с моба —
в «Бой» и «Предметы»). Ввод по Y работает и в одиночной игре, текст —
через `Window.TextInput` (рус/англ раскладка), прокрутка PgUp/PgDn/Home/End.

**Окно торговли MP** (`Net/Snapshot.cs:TradeSnapshot`, `Net/GameCore.cs`,
`Systems/GameController.Mp.cs`, `Render/MpRenderer.cs`): фаза заявки
(Y — принять), два столбца предметов и золото, «Готов» по каждому игроку
отдельно, попап «Обмен / Дуэль» по клику на игроке (1/2/Esc) — паритет
`render/screens_renderer/_trade.py`.

**Экран «Статистика забега»** (Tab, `Render/QuestScreen.DrawRunStats`):
время, киллы/боссы/зелья/крафт/квесты, убийства по типам — паритет
`run_summary()` python-final.

**Надёжность и безопасность (P0-аудит)**:
- сейвы: `SanitizePlayerData` (клампы, whitelist предметов/класса), позиция
  клампится в карту, `.bak` перед `File.Replace`, ошибка загрузки показывается
  игроку, а не «Слот пуст»;
- сеть (протокол v3): HMAC-подпись обязательна после hello, монотонный `Seq`
  (anti-replay), таймауты чтения сокета, flood реально рвёт соединение,
  `save_world` — только хост и запись на диск вне блокировки симуляции,
  серверный кулдаун атаки, кумулятивная проверка trade-офферов;
- шрифты DejaVu Sans в комплекте (`Content/fonts/`), при их отсутствии —
  понятная ошибка вместо падения;
- `GameController` разрезан на частичные классы: `GameController.Mp.cs`
  (мультиплеер), `.City.cs` (NPC/стражники/лавки), `.Craft.cs` (мини-игра
  крафта с выбором варианта рецепта [ / ]), `.Shop.cs` (магазин/квесты);
- кэш тайлов очищается при `GraphicsDevice.DeviceResetting` (полный экран,
  смена разрешения) и ограничен 2000 текстур.

## 19. Раунд «остаток аудита» (0.3.x)

- **ECDH-рукопожатие (протокол v4)**: сессионный ключ HMAC больше не
  передаётся plaintext'ом в welcome — сервер и клиент обмениваются
  публичными ключами ECDH P-256 (`ServerMsg.DhPublic` / `ClientMsg.DhPublic`)
  и выводят общий секрет `DeriveKeyFromHash(SHA256)`. Защита от пассивного
  прослушивания; активный MITM по-прежнему возможен — сервер не
  аутентифицируется (предел для авторитетного LAN-сервера).
- **FOV-фильтр снапшота** (`Config.SnapshotRadius = 32`, чевров):
  монстры/предметы/ресурсы/NPC/снаряды отсылаются только в радиусе вокруг
  запрашивающего игрока; игроки — целиком (интерполяция, торговля, чат).
- **Шрифты при DeviceReset**: создание атласов вынесено в
  `AethelgardGame.CreateFonts()`, подписка `DeviceResetting` пересоздаёт
  шрифты (старые текстуры после сброса устройства недействительны).
- **Декомпозиция GameController завершена**: main 1079 строк + частичные
  файлы Mp (740), World (470), City (345), Inventory (163), Craft (147),
  Skills (109), Shop (126) — ввод, мир, город, крафт и сеть разделены.

## 20. Unity-порт (двойная компиляция)

Ядро (BaseClass/Entities/Structs/Data/World/Systems/Data/Net, см. раздел 1)
компилируется дважды: в MonoGame-сборку (линком из `Aethelgard.csproj`,
без `Core/Compat/`) и в Unity-asmdef `Aethelgard.Core` (с shim
`Core/Compat/XnaShim.cs` + `InputShim.cs`, подменяющими Microsoft.Xna.Framework).

Движковый слой Unity — asmdef `Aethelgard.Game` (`Unity/Assets/Scripts/Game/`):

- **Драйвер**: `GameApp.cs` (перевод Unity-ввода в shim KeyboardState/MouseState,
  камера, фокус InputField), `WorldView` (спрайтовый рендер тайлов/сущностей,
  HP-бары монстров, кольца боссов, игроки из MP-снапшота), `Hud` (HP/мана/XP).
- **UI**: порт immediate-mode UiKit на retained-mode uGUI — `UiKitCanvas.cs`
  (палитра/панели/бары/вкладки/тултипы) + `UiInput` (хит-тесты по shim-мыши в
  координатах дизайна 1920×1080). Экраны: InventoryUi, ShopUi, SkillsUi,
  CraftUi, QuestUi, StatsUi, DialogueUi, PauseUi, MenuUi, MpUi, ChatUi.
  Логика действий — в GameController (дословный порт), экраны только
  отображают состояние и вызывают его публичные методы (как desktop
  `Desktop/Game.cs`).
- **Сейвы**: `SaveSystem.SaveDir` на Unity — `Application.persistentDataPath/saves`,
  на desktop — AppData (условная компиляция `UNITY_5_3_OR_NEWER`).
- **Звук**: `AudioSystem.cs` — те же процедурные генераторы, что и на desktop,
  но byte[] → `AudioClip.Create` (лениво, с кешем) + пул из 6 AudioSource.
- **MP**: Net-стек (`Core/Net/`) портирован целиком, включая ручной экспорт
  ASN.1 DER SPKI для ECDH в Unity-профиле; `GameApp` тикает `UpdateMp(dt)`.
- **Шрифт UI**: глиф-атлас `Resources/GlyphAtlas` (генерируется
  `tools/gen_glyph_atlas.py`), uGUI-текст — LegacyRuntime.ttf.

## 21. Гигиена репозитория

- Папки desktop-слоя в PascalCase: `Desktop/`, `Render/`, `Audio/`,
  `Systems/`; ядро живёт в `Unity/Assets/Scripts/Core/` (BaseClass,
  Entities, Structs, Data, World, Systems, Net, Render, Compat).
- Локальные артефакты (settings.json, сейвы, кэши pytest, логи Unity CLI)
  не коммитятся — см. `.gitignore`.
- `unity_create.log` и пустые python-каталоги (core/, game/) удалены.
