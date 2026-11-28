# Этельгард — Unity-версия

Порт игры с MonoGame (а изначально с Python/pygame, ветка `python-final`)
на Unity 6000.3.29f1. Старый MonoGame-проект остаётся в корне репозитория
для сравнения; Unity-проект полностью самодостаточен.

## Запуск

1. Открыть `AethelgardUnity` в Unity Hub (Unity 6000.3.29f1).
2. Открыть сцену по умолчанию и нажать **Play** — вся игра собирается сама
   (`GameLoop.Bootstrap`, сцена не нужна).

Или через CLI: `Unity.exe -projectPath <путь> -batchmode ...`

## Структура

```
Assets/Aethelgard/
  Core/                 # чистая логика БЕЗ UnityEngine (asmdef Aethelgard.Core)
    BaseClass/ Entities/ Structs/ World/ Systems/ Data/ Net/
    Shim/               # шимы XNA (Color, KeyboardState) — логика переехала без правок
  Game/                 # Unity-слой (asmdef Aethelgard.Game)
    Runtime/            # GameLoop, WorldRenderer, UnityInput, TextureGen (PixelCanvas/TileRenderer/SpriteStore/MapPalette)
    UI/                 # uGUI: UIManager + все экраны
    Audio/              # AudioSystem (процедурный синтез → AudioClip)
    Editor/             # меню Aethelgard в редакторе
  Prefabs/UI/           # префабы виджетов (Generate UI Prefabs)
  Resources/            # UITheme.asset, иконки навыков
  Tests/                # порт CoreTests (NUnit, EditMode)
```

## Редактирование интерфейса из Unity Editor

- **Aethelgard → Create UI Theme Asset** — создаёт
  `Assets/Aethelgard/Resources/UITheme.asset`. Открой его в инспекторе:
  все цвета (окна, бронза, золото, текст, полосы HP/MP/XP), размеры
  шрифтов, отступы, высоту кнопок. Правка ассета меняет весь UI игры.
- **Aethelgard → Generate UI Prefabs** — генерирует префабы
  `UIPanel / UIButton / UISlot / UIBar / UINode` в `Prefabs/UI`.
  Их можно открывать в Editor и менять визуально (кнопка с hover-цветами,
  слот с рамкой, бар с заполнением и overlay-текстом).
- **Aethelgard → Preview Main Menu (Edit Mode)** — показывает главное меню
  прямо в редакторе без Play Mode: удобно тюнить стили по кругу.

## Паритет с python-final

Перенесено 1:1: 7 меню + пауза, выбор класса (42 класса, 3 ступени,
переход на 5/15), инвентарь (страницы, экипировка, тултипы), магазин
(покупка/продажа/реролл), крафт с минигеймом молота, доска заданий,
диалоги NPC, большая карта, дев-консоль (F12), мультиплеер (TCP-сервер,
пароль, чат, трейд), статистика забега, **экраны смерти/победы**
(добавлены — их не было даже в MonoGame-версии), 4 босса, harvest,
локации и 8 биомов, процедурные тайлы/спрайты/звуки.

## Дерево навыков

Улучшено относительно MonoGame-версии:
- узлы с PNG-иконками (`Resources/SkillIcons/Class/<класс>_<n>.png`);
- три состояния: изучен (рамка цвета класса) / доступен (золотая рамка) /
  заблокирован (затемнение), подписи «ур. N»;
- линии-связи между узлами, светящиеся при изучении пути;
- тултипы с эффектами и подсказкой действия;
- бейдж доступных очков, панель перехода на новый класс;
- ремесленные ветки — 25 пипсов уровней, XP-бар, разблокировка рецептов.

## Тесты

Window → General → Test Runner → Edit Mode: 22 теста
(сохранения, протокол MP + HMAC, эффекты навыков, генерация мира,
боевая система и т.д. — порт `tests/Aethelgard.Tests/CoreTests.cs`).
