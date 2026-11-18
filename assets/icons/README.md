# Иконки древа навыков

PNG с прозрачным фоном, желательно 128×128 или 256×256 — игра сама
масштабирует под шестиугольник. Файлы необязательны: без них рисуется
процедурный значок.

## Приоритет

1. `assets/icons/skills/<ветка>_<номер>.png` — иконка конкретного узла,
   например `skills/warrior_0.png` (узел `warrior:0`).
2. `assets/icons/effects/<эффект>.png` — общая иконка эффекта
   (используется всеми узлами с этим эффектом).
3. `assets/icons/classes/<класс>.png` — центральный узел класса.
4. `assets/icons/craft/<ветка>.png` — центральный узел ремесла.

Номер узла — его индекс в ветке (0…6 для классов, 0…7 для ремёсел).

## Обязательный минимум: эффекты (`assets/icons/effects/`)

| Файл | Что означает |
| --- | --- |
| `atk.png` | сила атаки |
| `crit.png` | критический удар |
| `hp.png` | здоровье |
| `df.png` | защита |
| `dodge.png` | уклонение |
| `mana.png` | мана |
| `lifesteal.png` | вампиризм |
| `attack_speed.png` | скорость атаки |
| `move_speed.png` | скорость движения |
| `gold_pct.png` | золото |
| `xp_pct.png` | опыт |
| `quest_gold.png` | награда за задания (золото) |
| `quest_xp.png` | награда за задания (опыт) |
| `craft_xp.png` | опыт ремесла |
| `craft_quality.png` | качество крафта |
| `smithing_xp.png` | опыт кузнечного дела |
| `alchemy_xp.png` | опыт алхимии |
| `engineering_xp.png` | опыт магической инженерии |
| `summon.png` | призыв |
| `rage.png` | ярость |
| `frenzy.png` | бешенство |
| `slow_hit.png` | замедляющий удар |
| `fov.png` | обзор |
| `extra_stats.png` | дополнительные характеристики |
| `plain_weapons.png` | владение простым оружием |

## Центры классов (`assets/icons/classes/`)

`default`, `warrior`, `squire`, `knight`, `swordmaster`, `barbarian`,
`berserker`, `fighter`, `mercenary`, `merc_captain`, `rogue`, `thief`,
`pickpocket`, `killer`, `assassin`, `ninja`, `tracker`, `bounty_hunter`,
`hunter`, `ranger`, `archer`, `mage`, `apprentice`, `elements`,
`fire_mage`, `water_mage`, `air_mage`, `earth_mage`, `lightning_mage`,
`light`, `dark`, `necromancer`, `cleric`, `druid`, `nature`,
`blacksmith`, `magic_engineer`, `alchemist`, `potionmaker`, `herbalist`,
`artisan`.

## Центры ремёсел (`assets/icons/craft/`)

`blacksmith.png`, `alchemy.png`, `magic_engineering.png`.

## Необязательные иконки конкретных узлов

Любой узел можно переопределить файлом
`assets/icons/skills/<ветка>_<номер>.png` — например
`skills/alchemy_2.png`, `skills/mage_5.png`. Достаточно перечислить
только те узлы, для которых нужна своя картинка.
