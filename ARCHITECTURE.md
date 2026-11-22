# Архитектура Aethelgard 0.2.0 (переработанная)

Переработка проекта [Valerka3421/rpg](https://github.com/Valerka3421/rpg.git)
(Aethelgard, MonoGame, .NET 8) по образцу архитектуры проекта «Пример»:
иерархия базовых классов с приватными полями и свойствами, фабрики Spawn
для монстров, данные в структурах и перечислениях, аккуратные namespace.

## 1. Слои и каталоги

```
Aethelgard/
├── Program.cs                  — точка входа (namespace Aethelgard)
├── GlobalUsings.cs             — глобальные using всех слоёв
├── BaseClass/                  — БАЗОВЫЙ КЛАСС существа (в стиле «Пример»)
│   └── Specific.cs             — private protected поля (_hp, _name…) + свойства
│                                  (Hp, Name…), статусы, таймеры, TickStatuses()
├── Entities/                   — сущности
│   ├── Entity.cs               — Entity : Specific (MonsterKey, флаги NPC, таймеры)
│   ├── Player.cs               — игрок: классы, статы, инвентарь, опыт, UseItem()
│   ├── Npc.cs                  — фабрика NPC города из CityData.NpcDef
│   └── Monsters/               — 24 вида монстров, по файлу на вид (как в «Пример»)
│       ├── Monster.cs          — Monster : Entity, InitFromDef(def, depth)
│       ├── MonsterFactory.cs   — Create(key) → экземпляр нужного вида
│       ├── Rat.cs … Turtle.cs  — обычные и overworld-монстры
│       └── GoblinKing.cs …     — 4 босса (глубины 5/10/15/20)
├── Structs/                    — структуры и перечисления (как Structs/ в «Пример»)
│   ├── Tile.cs                 — enum Tile (раньше жил внутри Config.cs)
│   ├── Room.cs                 — единый класс комнаты (был объявлен дважды)
│   ├── FloatText.cs            — FloatText + Message
│   ├── HarvestRecord.cs        — сборный ресурс карты
│   └── ItemInstance.cs         — типизированный ключ предмета
├── World/                      — мир и игровой цикл
│   ├── Game.cs                 — AethelgardGame : XnaGame (цикл MonoGame)
│   ├── Dungeon.cs              — карта: тайлы, сущности, предметы
│   ├── WorldGen.cs             — генерация открытого мира (класс World → WorldGen)
│   ├── DungeonGen.cs           — генерация этажей, боссовые арены
│   ├── Overworld.cs            — константы раскладки мира
│   ├── Locations.cs            — 13 именованных карт
│   └── Projectiles.cs          — Projectile + ProjectileSystem
├── Systems/                    — правила и контроллер (бывший Core/Game)
│   ├── GameController.cs       — состояние + ввод + апдейт
│   ├── DevConsole.cs           — dev-консоль
│   ├── Rules.cs                — бой, лут, награды
│   ├── Config.cs               — константы, палитра, XP-кривая
│   ├── SkillTree.cs            — древо навыков + Aggregate()
│   ├── QuestLog.cs             — квесты + PopulateBoard()
│   ├── HarvestSystem.cs        — сбор ресурсов
│   ├── SaveSystem.cs           — сохранения в JSON-слоты
│   ├── Settings.cs, Modes.cs
├── Data/                       — данные игры (замена Core/Data/*.g.cs)
│   ├── ItemData.cs … StatusData.cs — логика поверх таблиц (API сохранён)
│   └── *Defs.cs                — 7 таблиц в C#: items, classes, monsters,
│                                  recipes, city, quests, statuses
├── Render/                     — экраны и рендеры MonoGame (без изменений логики)
├── Net/                        — кооператив: Client/Server/GameCore/Snapshot/Protocol
├── Audio/                      — процедурный синтез звука
├── tests/Aethelgard.Tests/     — xUnit-тесты (сохранения, сеть, эффекты, мир)
└── .github/workflows/ci.yml    — сборка и тесты на windows-latest
```

Направление зависимостей: `BaseClass → (Entities, Data) → (World, Systems) →
(Render, Net, Audio)`. Циклы namespace между Entities и Data допустимы в одном
сборке и остаются из-за API оригинала (MonsterData.CreateEntity ↔ Entity).

## 2. Что сделано в стиле «Пример»

| Приём из «Пример» | Реализация здесь |
|---|---|
| Базовый класс `Specific` с `_полями` и свойствами | `BaseClass/Specific.cs` — статы, статусы, таймеры любого существа |
| Наследники с собственными файлами | `Entities/Player.cs`, `Entities/Monsters/*.cs` (24 файла), `Entities/Npc.cs` |
| Фабрика `Spawn(lvl, список)` у каждого моба | `Monster.Spawn(depth, list)` + `InitFromDef` + `MonsterFactory.Create` |
| `Structs/` со структурами и перечислениями | `Structs/Tile.cs`, `Room.cs`, `HarvestRecord.cs`, `ItemInstance.cs` |
| `World/` с точкой входа и меню | `World/Game.cs` (игровой цикл), `World/DungeonGen.cs` и др. |
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
- `dotnet test Aethelgard.sln -c Release` — **17 тестов xUnit**: round-trip
  сохранений и всех полей `FloorData`, версия сейва, JSON-протокол и HMAC,
  PBKDF2-пароли, эффекты классов/узлов, детерминизм оверворлда, подземелья
  и локаций.
- GitHub Actions (`.github/workflows/ci.yml`) повторяет restore → build
  с `-warnaserror` → test на `windows-latest`.

## 6. Ограничения

- Проверены компиляция и логика ядра xUnit-тестами; полноценный запуск окна
  MonoGame в headless-песочнице невозможен.
- `RuntimeFont` использует System.Drawing: сборка кроссплатформенная, но
  рендер шрифта рассчитан на Windows (как в оригинале).
- Функциональность сохранена полностью: кооператив (Net), процедурный звук
  (Audio), dev-консоль, 41 класс, 24 вида монстров, 4 босса.

## 12. Паритет с веткой python-final

Сверка с pygame-версией (ветка `python-final`, ~27 400 строк) выполнена
21.11.2026. Совпадают: 128 предметов, 42 класса (дерево T1→T2→T3,
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
| Предметы | `Data/ItemDefs.cs` → `ItemData.All` | 128 |
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
в render target 1280×720 и масштабируется под размер окна.

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

Осознанные ограничения: `GameController` и `Net/GameCore` остаются крупными
и частично дублируют правила — вынос общей симуляции в отдельный слой
запланирован, но не входит в текущий релиз. `World/Projectiles.cs` и
`Systems/Modes.cs` сохранены как задел: снаряды и константы экранов пока не
подключены к активному циклу.
