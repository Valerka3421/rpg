# Screenshots

Папка для геймплейных скриншотов. В README они встроены через
`![](docs/screenshots/01-village.png)`.

Чтобы сделать скриншот:
1. Запусти игру: `python main.py`
2. В dev console (F12) введи `fps on` — увидишь счётчик FPS
3. Сделай скриншот окна (Win+Shift+S на Windows, `import` на Linux)
4. Сохрани как `01-village.png`, `02-dungeon.png`, `03-boss.png`, `04-craft.png`

Минимум 3-4 кадра для Steam-страницы и README.

## Рекомендуемые скриншоты

| Имя | Сцена | Описание |
|---|---|---|
| `01-village.png` | Деревня Этельгард, день | Старт игры, вид на площади и дома |
| `02-dungeon.png` | Подземелье, этаж 3 | Факелы, мобы, прогресс игрока |
| `03-boss.png` | Босс-арена | Король гоблинов / Лич в бою |
| `04-craft.png` | Окно крафта | Дерево навыков и мини-игра качества |
| `05-inventory.png` | Инвентарь 10×10 | Полный инвентарь с разными предметами |
| `06-bigmap.png` | Большая карта на «M» | Все 8 биомов + деревня + руины |
| `07-mp.png` | Кооператив | 2-4 игрока в одном кадре |

## Разрешение

- **Полный кадр:** 1920×1080 PNG (для Steam-страницы)
- **Превью:** 800×450 JPEG (для README)
- **GIF:** 800×450, 5-10 сек, оптимизированный через ezgif.com

## CI

В будущем скриншоты можно снимать автоматически в CI через
PyVirtualDisplay + xvfb:
```yaml
- run: xdotool key F12  # открыть dev console
- run: xdotool type "screenshot /tmp/shot.png"
- uses: actions/upload-artifact@v4
  with: { name: screenshots, path: /tmp/shot.png }
```
