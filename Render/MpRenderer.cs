using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
namespace Aethelgard.Render;

/// <summary>
/// Multiplayer renderer — draws the game from server snapshots.
/// Ported from Python net/view.py.
///
/// Вместо упрощённого «пол везде» рисует настоящую карту из снапшота
/// (стены/лестницы/ворота — те же процедурные тайлы, что в сингле),
/// а также панели инвентаря/статов/навыков/квестов/магазина, дублирующие
/// SP-экраны (паритет контента MP и SP).
/// </summary>
public class MpRenderer
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sb;
    private readonly TileRenderer _tiles;
    private SpriteFont? _font;
    private Texture2D? _blank;

    private int _camX, _camY;
    private Systems.GameController _game = null!;

    public MpRenderer(GraphicsDevice device, SpriteBatch sb)
    {
        _device = device;
        _sb = sb;
        _tiles = new TileRenderer(device);
    }

    public void LoadFont(SpriteFont font) => _font = font;

    /// <summary>
    /// Draw multiplayer game from snapshot.
    /// </summary>
    public void Draw(GameController game)
    {
        _game = game;
        _tiles.DrainDisposals();   // dispose выброшенных текстур до Begin
        var snap = game.MpSnapshot;
        if (snap == null || _font == null)
        {
            // Waiting for first snapshot
            _sb.Begin();
            _sb.Draw(Blank, new Rectangle(0, 0, AethelgardGame.ScreenW, AethelgardGame.ScreenH),
                new Color(12, 12, 18));
            string msg = "Подключение к серверу...";
            var size = _font?.MeasureString(msg) ?? new Vector2(200, 20);
            if (_font != null)
                _sb.DrawString(_font, msg,
                    new Vector2((AethelgardGame.ScreenW - size.X) / 2,
                                (AethelgardGame.ScreenH - size.Y) / 2),
                    Config.ColDim);
            _sb.End();
            return;
        }

        // Find my player
        var myState = snap.Players.FirstOrDefault(p => p.Pid == snap.MyPid);
        if (myState == null)
        {
            _sb.Begin();
            _sb.Draw(Blank, new Rectangle(0, 0, AethelgardGame.ScreenW, AethelgardGame.ScreenH),
                new Color(12, 12, 18));
            _sb.DrawString(_font, "Ожидание данных игрока...",
                new Vector2(AethelgardGame.ScreenW / 2 - 100, AethelgardGame.ScreenH / 2),
                Config.ColDim);
            _sb.End();
            return;
        }

        // Camera: по интерполированной позиции своего игрока — иначе камера
        // прыгает по снапшоту, а спрайт доезжает лерпом (визуальный разъезд)
        int worldW = AethelgardGame.ScreenW;
        int worldH = AethelgardGame.ScreenH - Config.HudHeight;
        float alpha = Math.Clamp(_game.MpSinceSnapshot / Net.GameServer.SnapshotInterval, 0f, 1f);
        float myXi = myState.X, myYi = myState.Y;
        if (_game.MpPrevPlayerPos.TryGetValue(myState.Pid, out var myPrev))
        {
            myXi = MathHelper.Lerp(myPrev.X, myState.X, alpha);
            myYi = MathHelper.Lerp(myPrev.Y, myState.Y, alpha);
        }
        _camX = (int)(myXi * Config.TileSize + Config.TileSize / 2 - worldW / 2);
        _camY = (int)(myYi * Config.TileSize + Config.TileSize / 2 - worldH / 2);
        _camX = Math.Max(0, Math.Min(_camX, Math.Max(0, snap.MapW * Config.TileSize - worldW)));
        _camY = Math.Max(0, Math.Min(_camY, Math.Max(0, snap.MapH * Config.TileSize - worldH)));

        // World
        _sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        DrawTiles(snap, myState);
        DrawHarvests(snap, myState);
        DrawItems(snap);
        DrawMonsters(snap);
        DrawProjectiles(snap);
        DrawPlayers(snap, myState);
        _sb.End();

        // HUD
        DrawHud(snap, myState, game);

        // Chat + события
        DrawChat(game, snap);

        // Панель (инвентарь/статы/навыки/квесты/магазин)
        DrawPanel(game, snap, myState);

        // Попап «Обмен / Дуэль» (net/view.py player_popup)
        if (game.MpPopupPid.HasValue && _font != null && game.MpPanel == "")
        {
            string label = $"Игрок {game.MpPopupName} (PID {game.MpPopupPid.Value})";
            string hint = "1 — Обмен · 2 — Дуэль · Esc — отмена";
            var ls = _font.MeasureString(label);
            var hs = _font.MeasureString(hint);
            int w = (int)MathF.Max(ls.X, hs.X) + 28;
            var rect = new Rectangle(
                (AethelgardGame.ScreenW - w) / 2,
                AethelgardGame.ScreenH - Config.HudHeight - 96, w, 58);
            _sb.Draw(Blank, rect, new Color(20, 17, 14, 240));
            _sb.Draw(Blank, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Color(206, 166, 82));
            _sb.Draw(Blank, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Color(206, 166, 82));
            _sb.DrawString(_font, label,
                new Vector2(rect.X + (rect.Width - ls.X) / 2, rect.Y + 8),
                new Color(238, 222, 188));
            _sb.DrawString(_font, hint,
                new Vector2(rect.X + (rect.Width - hs.X) / 2, rect.Y + 30),
                new Color(200, 190, 170));
        }
    }

    /// <summary>
    /// Вид тайла — та же логика, что TileRenderer.TileKindAt в сингле:
    /// стены по биомам Overworld, лестницы/выход/ворота — спец-тайлы.
    /// </summary>
    private static string KindFor(Snapshot snap, int x, int y)
    {
        int t = x < 0 || x >= snap.MapW || y < 0 || snap.Tiles == null || y >= snap.Tiles.Count
            ? (int)Tile.Wall
            : (snap.Tiles[y].Length > x ? snap.Tiles[y][x] - '0' : (int)Tile.Wall);
        switch ((Tile)t)
        {
            case Tile.Stairs: return "stairs";
            case Tile.Exit: return "exit";
            case Tile.Gate: return "gate";
            case Tile.Wall:
                return snap.WorldMode == "dungeon" ? "wall" : Overworld.WallKind(x, y);
            default:
                return snap.WorldMode == "dungeon" ? "floor" : Overworld.GroundKind(x, y);
        }
    }

    private void DrawTiles(Snapshot snap, Snapshot.PlayerState myState)
    {
        if (snap.Tiles == null || snap.Tiles.Count == 0)
        {
            // Старый сервер без тайлов — fallback: пол
            DrawTilesFallback(snap, myState);
            return;
        }

        int startX = Math.Max(0, _camX / Config.TileSize);
        int startY = Math.Max(0, _camY / Config.TileSize);
        int endX = Math.Min(snap.MapW, startX + Config.ViewW + 2);
        int endY = Math.Min(snap.MapH, startY + Config.ViewH + 2);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                string kind = KindFor(snap, x, y);
                int variant = Config.HashInt(x, y, 13, 8);

                float dist = MathF.Sqrt((x - myState.X) * (x - myState.X)
                    + (y - myState.Y) * (y - myState.Y));
                float light = Math.Max(0.35f, 1.0f - dist / Config.FovRadius);

                var tex = _tiles.GetTile(kind, variant, light);
                var pos = new Vector2(x * Config.TileSize - _camX,
                                      y * Config.TileSize - _camY);
                _sb.Draw(tex, pos, Color.White);

                // Замок/сундук — цветная метка поверх земли (как в миникарте SP).
                // Проверка длины строки: «короткий» снапшот (старая/битая
                // версия сервера) раньше ронял клиента IndexOutOfRange
                if (x >= snap.Tiles[y].Length) continue;
                int tile = snap.Tiles[y][x] - '0';
                Color? mark = ((Tile)tile) switch
                {
                    Tile.LockedDoor => new Color(200, 80, 60),
                    Tile.Chest => new Color(230, 190, 80),
                    Tile.UpStairs => new Color(200, 160, 90),
                    _ => null,
                };
                if (mark != null)
                    _sb.Draw(Blank, new Rectangle((int)pos.X + 12, (int)pos.Y + 12,
                        Config.TileSize - 24, Config.TileSize - 24), mark.Value);
            }
        }
    }

    private void DrawTilesFallback(Snapshot snap, Snapshot.PlayerState myState)
    {
        int startX = Math.Max(0, _camX / Config.TileSize);
        int startY = Math.Max(0, _camY / Config.TileSize);
        int endX = Math.Min(snap.MapW, startX + Config.ViewW + 2);
        int endY = Math.Min(snap.MapH, startY + Config.ViewH + 2);
        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                int variant = Config.HashInt(x, y, 13, 8);
                float dist = MathF.Sqrt((x - myState.X) * (x - myState.X)
                    + (y - myState.Y) * (y - myState.Y));
                float light = Math.Max(0.35f, 1.0f - dist / Config.FovRadius);
                var tex = _tiles.GetTile("floor", variant, light);
                _sb.Draw(tex, new Vector2(x * Config.TileSize - _camX,
                                          y * Config.TileSize - _camY), Color.White);
            }
        }
    }

    /// <summary>Маркеры живых ресурсов для добычи (E).</summary>
    private void DrawHarvests(Snapshot snap, Snapshot.PlayerState myState)
    {
        foreach (var node in snap.Harvests)
        {
            float dist = MathF.Sqrt((node.X - myState.X) * (node.X - myState.X)
                + (node.Y - myState.Y) * (node.Y - myState.Y));
            if (dist > Config.FovRadius) continue;
            var pos = new Vector2(node.X * Config.TileSize - _camX + Config.TileSize / 2,
                                  node.Y * Config.TileSize - _camY + Config.TileSize / 2);
            DrawCircle(pos, 10, new Color(110, 160, 80));
        }
    }

    private void DrawItems(Snapshot snap)
    {
        foreach (var item in snap.Items)
        {
            var def = ItemData.Get(item.Key);
            if (def == null) continue;
            var pos = new Vector2(item.X * Config.TileSize - _camX + Config.TileSize / 2,
                                   item.Y * Config.TileSize - _camY + Config.TileSize / 2);
            DrawCircle(pos, 8, def.Color);
        }
    }

    private void DrawMonsters(Snapshot snap)
    {
        foreach (var m in snap.Monsters)
        {
            var pos = new Vector2(m.X * Config.TileSize - _camX + Config.TileSize / 2,
                                   m.Y * Config.TileSize - _camY + Config.TileSize / 2);
            int radius = m.Boss ? 24 : 16;
            DrawCircle(pos, radius, m.Color);
            if (m.Boss)
                DrawCircleOutline(pos, radius + 2, new Color(220, 50, 50));

            if (_font != null)
                _sb.DrawString(_font, m.Ch.ToString(),
                    pos - _font.MeasureString(m.Ch.ToString()) / 2, Color.White);

            // HP bar
            if (m.Hp < m.MaxHp)
            {
                int barW = m.Boss ? 40 : 24;
                int bx = (int)pos.X - barW / 2;
                int by = (int)pos.Y - radius - 8;
                _sb.Draw(Blank, new Rectangle(bx, by, barW, 3), new Color(40, 20, 20));
                int fillW = m.MaxHp > 0 ? barW * m.Hp / m.MaxHp : 0;
                _sb.Draw(Blank, new Rectangle(bx, by, fillW, 3), Config.ColHp);
            }
        }
    }

    /// <summary>Летящие снаряды из снапшота (визуальный эффект дальних атак).</summary>
    private void DrawProjectiles(Snapshot snap)
    {
        foreach (var p in snap.Projectiles)
        {
            var pos = new Vector2(p.X * Config.TileSize - _camX + Config.TileSize / 2,
                                   p.Y * Config.TileSize - _camY + Config.TileSize / 2);
            DrawCircle(pos, 5, p.Color);
        }
    }

    private void DrawPlayers(Snapshot snap, Snapshot.PlayerState myState)
    {
        // Интерполяция между предыдущим и текущим снапшотом (20 Гц сервер):
        // без неё координаты «прыгали» на клетку при каждом обновлении.
        float alpha = Math.Clamp(_game.MpSinceSnapshot / Net.GameServer.SnapshotInterval, 0f, 1f);
        foreach (var p in snap.Players)
        {
            Vector2 pos;
            if (_game.MpPrevPlayerPos.TryGetValue(p.Pid, out var prev)
                && (prev.X != p.X || prev.Y != p.Y))
            {
                pos = new Vector2(
                    MathHelper.Lerp(prev.X, p.X, alpha) * Config.TileSize - _camX + Config.TileSize / 2,
                    MathHelper.Lerp(prev.Y, p.Y, alpha) * Config.TileSize - _camY + Config.TileSize / 2);
            }
            else
            {
                pos = new Vector2(p.X * Config.TileSize - _camX + Config.TileSize / 2,
                                   p.Y * Config.TileSize - _camY + Config.TileSize / 2);
            }
            bool isMe = p.Pid == snap.MyPid;
            int radius = isMe ? 20 : 18;
            DrawCircle(pos, radius, p.Color);

            if (isMe)
                DrawCircleOutline(pos, radius + 2, Config.ColGold);

            if (_font != null)
            {
                _sb.DrawString(_font, p.Ch.ToString(),
                    pos - _font.MeasureString(p.Ch.ToString()) / 2, Color.White);

                string nameText = isMe ? "[ВЫ]" : $"И{p.Pid}";
                var nameSize = _font.MeasureString(nameText);
                _sb.DrawString(_font, nameText,
                    new Vector2(pos.X - nameSize.X / 2, pos.Y - radius - 16),
                    isMe ? Config.ColGold : Config.ColText);

                if (p.Hp < p.MaxHp)
                {
                    int barW = 30;
                    int bx = (int)pos.X - barW / 2;
                    int by = (int)pos.Y + radius + 4;
                    _sb.Draw(Blank, new Rectangle(bx, by, barW, 3), new Color(40, 20, 20));
                    int fillW = p.MaxHp > 0 ? barW * p.Hp / p.MaxHp : 0;
                    _sb.Draw(Blank, new Rectangle(bx, by, fillW, 3), Config.ColHp);
                }
            }
        }
    }

    private void DrawHud(Snapshot snap, Snapshot.PlayerState myState, GameController game)
    {
        // Журнал с вкладками над HUD (Всё/Бой/Предметы/Общение/Опыт)
        if (_font != null)
        {
            var raw = Mouse.GetState();
            var scaled = new MouseState(
                (int)(raw.X * AethelgardGame.MouseScaleX),
                (int)(raw.Y * AethelgardGame.MouseScaleY),
                raw.ScrollWheelValue, raw.LeftButton, raw.MiddleButton,
                raw.RightButton, raw.XButton1, raw.XButton2);
            ChatPanel.Draw(_sb, _font, game, Blank, scaled);
        }

        _sb.Draw(Blank,
            new Rectangle(0, AethelgardGame.ScreenH - Config.HudHeight, AethelgardGame.ScreenW, Config.HudHeight),
            new Color(18, 16, 14, 220));

        if (_font == null) return;

        int y = AethelgardGame.ScreenH - Config.HudHeight + 10;
        int x = 16;

        string loc = snap.WorldMode == "dungeon"
            ? $"Подземелье {snap.Depth}" : "Открытый мир";
        _sb.DrawString(_font, $"{myState.ClassName}  Lv.{myState.Level}  [{loc}]  (MP)",
            new Vector2(x, y), Config.ColText);
        y += 20;

        _sb.DrawString(_font, $"HP: {myState.Hp}/{myState.MaxHp}", new Vector2(x, y), Config.ColHp);
        DrawBar(x + 120, y + 4, 200, 12, myState.Hp, myState.MaxHp, Config.ColHp);
        y += 20;

        _sb.DrawString(_font, $"MP: {myState.Mana}/{myState.MaxMana}",
            new Vector2(x, y), new Color(100, 150, 220));
        DrawBar(x + 120, y + 4, 200, 12, myState.Mana, myState.MaxMana, new Color(100, 150, 220));
        y += 20;

        _sb.DrawString(_font, $"XP: {myState.Xp}/{myState.XpNext}", new Vector2(x, y), Config.ColXp);
        DrawBar(x + 120, y + 4, 200, 12, myState.Xp, myState.XpNext, Config.ColXp);
        y += 20;

        _sb.DrawString(_font, $"Золото: {myState.Gold}", new Vector2(x, y), Config.ColGold);

        // Статусы (как в SP HUD)
        if (myState.Statuses.Count > 0)
        {
            string statuses = "Статусы: " + string.Join(", ",
                myState.Statuses.Keys);
            _sb.DrawString(_font, statuses,
                new Vector2(x + 250, y), new Color(200, 120, 200));
        }

        _sb.DrawString(_font, $"Игроков: {snap.Players.Count}",
            new Vector2(AethelgardGame.ScreenW - 120, y), Config.ColText);

        // Подсказки по клавишам (паритет с SP: F/E/T/I/C/N/L/B, Z/X/P, F5)
        _sb.DrawString(_font,
            "WASD:шаг  Пробел:атака  F:лестница  E:добыча  T:говорить  I:инвентарь  C:статы  N:навыки  L:квесты  B:магазин  H:зелье  X:ждать  Z:дуэль  P:повышение  Y/Enter:чат  F5:сохранить мир  Esc:панель",
            new Vector2(16, 8), Config.ColDim);

        // Хотбар (1–8) — как в SP
        DrawHotbar(myState);
    }

    private void DrawHotbar(Snapshot.PlayerState myState)
    {
        if (_font == null) return;
        int hx = 16;
        int hy = AethelgardGame.ScreenH - Config.HudHeight + 66;

        for (int i = 0; i < 8 && i < myState.Quickbar.Length; i++)
        {
            var rect = new Rectangle(hx + i * 40, hy, 36, 22);
            _sb.Draw(Blank, rect, new Color(30, 26, 22));
            _sb.Draw(Blank, new Rectangle(rect.X, rect.Y, rect.Width, 1), Config.ColDim);
            _sb.Draw(Blank, new Rectangle(rect.X, rect.Y + rect.Height - 1, rect.Width, 1), Config.ColDim);
            _sb.DrawString(_font, $"{i + 1}", new Vector2(rect.X + 3, rect.Y + 2), Config.ColDim);

            var key = myState.Quickbar[i];
            if (key != null)
            {
                var def = ItemData.Get(key);
                Color c = def?.Color ?? Config.ColText;
                _sb.Draw(Blank, new Rectangle(rect.X + 22, rect.Y + 5, 12, 12), c);
                if (def != null && _font != null)
                    _sb.DrawString(_font, def.Ch.ToString(),
                        new Vector2(rect.X + 14, rect.Y + 3), Color.White);
            }
        }
    }

    private void DrawChat(GameController game, Snapshot snap)
    {
        if (_font == null) return;

        int chatX = 16;
        int chatY = AethelgardGame.ScreenH - Config.HudHeight - 200;
        int chatW = 400;

        // События мира (боевой лог) — 3 последние над чатом.
        // Индексный обход вместо TakeLast().ToList(): без аллокаций в кадре
        int evCount = Math.Min(3, snap.Events.Count);
        for (int i = 0; i < evCount; i++)
            _sb.DrawString(_font, snap.Events[snap.Events.Count - evCount + i],
                new Vector2(chatX, chatY - (evCount - i) * 18), Config.ColDim);

        // Chat log (last 8 messages)
        int chatCount = Math.Min(8, game.ChatLog.Count);
        for (int i = 0; i < chatCount; i++)
        {
            _sb.DrawString(_font, game.ChatLog[game.ChatLog.Count - chatCount + i],
                new Vector2(chatX, chatY + i * 18), Config.ColText);
        }

        if (game.ChatActive)
        {
            _sb.Draw(Blank, new Rectangle(chatX - 4, chatY + 8 * 18 + 4, chatW, 24),
                new Color(20, 18, 14, 220));
            _sb.Draw(Blank, new Rectangle(chatX - 4, chatY + 8 * 18 + 4, chatW, 1),
                Config.ColGold);
            _sb.DrawString(_font, $"{game.ChatInput}_",
                new Vector2(chatX, chatY + 8 * 18 + 8), Config.ColText);
        }
    }

    // ── Панели (инвентарь/статы/навыки/квесты/магазин) ──

    private void DrawPanel(GameController game, Snapshot snap, Snapshot.PlayerState my)
    {
        if (_font == null || game.MpPanel == "") return;

        List<string> lines = new();
        string title;
        string hints;
        List<string>? numbered = null;

        switch (game.MpPanel)
        {
            case "inventory":
            {
                title = "ИНВЕНТАРЬ (MP)";
                string eq(string label, string? slot) => $"{label}: {(slot != null ? ItemData.DisplayName(slot) : "—")}";
                lines.Add(eq("Оружие", my.Weapon));
                lines.Add(eq("Броня", my.Armor));
                lines.Add(eq("Шлем", my.Head));
                lines.Add(eq("Поножи", my.Legs));
                lines.Add(eq("Обувь", my.Boots));
                lines.Add("");
                var items = my.Inventory.Keys.ToList();
                numbered = new List<string>();
                int pageStart = game.MpPage * 8;
                for (int i = 0; i < 8; i++)
                {
                    int idx = pageStart + i;
                    if (idx >= items.Count) break;
                    var key = items[idx];
                    var def = ItemData.Get(key);
                    numbered.Add($"{i + 1}. {def?.Name ?? key} ×{my.Inventory[key]}");
                }
                hints = "1–8:использовать/надеть  Shift+1–8:быстрая панель  Q/E:страница  U/V/H/B/N:снять  I/Esc:закрыть";
                break;
            }

            case "stats":
            {
                title = "ХАРАКТЕРИСТИКИ (MP)";
                lines.Add($"Свободных очков: {my.FreePoints}");
                lines.Add("");
                numbered = new List<string>
                {
                    $"1. STR {my.Str}", $"2. AGI {my.Agi}", $"3. VIT {my.Vit}",
                    $"4. LUK {my.Luk}", $"5. DEF {my.DfStat}", $"6. INT {my.IntStat}",
                };
                hints = "1–6:вложить очко  C/Esc:закрыть";
                break;
            }

            case "skills":
            {
                title = game.MpSkillCast ? "НАВЫКИ — применение (MP)" : "НАВЫКИ — изучение (MP)";
                if (!game.MpSkillCast)
                {
                    numbered = snap.Skills.Select((s, i) =>
                        $"{i + 1}. {s.Name} (Ур.{s.Level}){(s.Learned ? " [изучен]" : "")}")
                        .ToList();
                    if (snap.Promotions.Count > 0)
                    {
                        lines.Add($"Повышение: {string.Join(", ", snap.Promotions.Select(k => ClassData.Get(k)?.Name ?? k))}");
                    }
                }
                else
                {
                    numbered = snap.ActiveSkills.Select((s, i) =>
                        $"{i + 1}. {s.Name} ({s.Mana} MP)").ToList();
                }
                hints = "Q/E:изучение/применение  1–7/1–8:действие  P:повышение  N/Esc:закрыть";
                break;
            }

            case "quests":
            {
                title = "КВЕСТЫ (MP)";
                numbered = new List<string>();
                int n = 1;
                foreach (var q in snap.QuestBoard.Where(q => q.State == "available"))
                    numbered.Add($"{n++}. [доступен] {q.Name} (0/{q.Count})");
                foreach (var q in snap.QuestBoard.Where(q => q.State == "active"))
                    numbered.Add($"    [активен] {q.Name} ({q.Progress}/{q.Count})");
                foreach (var q in snap.QuestBoard.Where(q => q.State == "completed"))
                    numbered.Add($"{n++}. [выполнен] {q.Name} — сдать");
                hints = "1–9:взять/сдать (рядом со Старейшиной)  L/Esc:закрыть";
                break;
            }

            case "shop":
            {
                title = game.MpShopSell ? "МАГАЗИН — продажа (MP)" : "МАГАЗИН — покупка (MP)";
                // Сток: подземелье — из снапшота; город — по смежному NPC
                var stock = ShopStockFor(snap, my);
                int pageStart = game.MpShopPage * Config.ShopPageSize;
                if (!game.MpShopSell)
                {
                    numbered = new List<string>();
                    for (int i = pageStart; i < stock.Count && i < pageStart + Config.ShopPageSize; i++)
                        numbered.Add($"{i - pageStart + 1}. {ItemData.DisplayName(stock[i].Key)} — {stock[i].Price} золота");
                }
                else
                {
                    var inv = my.Inventory.Keys.ToList();
                    numbered = new List<string>();
                    for (int i = pageStart; i < inv.Count && i < pageStart + Config.ShopPageSize; i++)
                        numbered.Add($"{i - pageStart + 1}. {ItemData.DisplayName(inv[i])} ×{my.Inventory[inv[i]]} (продажа: {ItemData.SellPrice(inv[i])} золота)");
                }
                hints = "S:покупка/продажа  1–9:действие  ←/→:страница  R:обновить  B/Esc:закрыть";
                break;
            }

            case "trade":
            {
                // Окно обмена (parity render/screens_renderer/_trade.py)
                var trade = snap.Trade;
                if (trade == null) return;
                title = "ОБМЕН (MP)";
                int myPid = snap.MyPid;
                int other = trade.Players.FirstOrDefault(p => p != myPid);
                string otherName = snap.Players
                    .FirstOrDefault(p => p.Pid == other)?.ClassName ?? $"Игрок {other}";
                bool accepted = trade.Accepted.Contains(myPid);

                if (!accepted)
                {
                    lines.Add($"{otherName} предлагает обмен");
                    hints = "Y:принять  Esc:отказаться";
                    break;
                }

                var myOffer = trade.Offers.GetValueOrDefault(myPid.ToString())
                    ?? new Dictionary<string, int>();
                var myKeys = myOffer.Keys.OrderBy(k => k).ToList();
                if (game.MpTradeSel >= myKeys.Count)
                    game.MpTradeSel = Math.Max(0, myKeys.Count - 1);
                bool myReady = trade.Ready.Contains(myPid);
                int myGold = trade.Gold.GetValueOrDefault(myPid.ToString());
                lines.Add(myReady
                    ? $"Вы выкладываете  [ГОТОВ]   Золото: {myGold}"
                    : $"Вы выкладываете   Золото: {myGold}");
                if (myKeys.Count == 0) lines.Add("  (пусто)");
                for (int i = 0; i < myKeys.Count; i++)
                    lines.Add($"  {(i == game.MpTradeSel ? ">" : " ")} "
                              + $"{ItemData.DisplayName(myKeys[i])} ×{myOffer[myKeys[i]]}");

                var otherOffer = trade.Offers.GetValueOrDefault(other.ToString())
                    ?? new Dictionary<string, int>();
                int otherGold = trade.Gold.GetValueOrDefault(other.ToString());
                lines.Add("");
                lines.Add(trade.Ready.Contains(other)
                    ? $"{otherName}  [ГОТОВ]   Золото: {otherGold}"
                    : $"{otherName}   Золото: {otherGold}");
                if (otherOffer.Count == 0) lines.Add("  (пусто)");
                foreach (var kv in otherOffer.OrderBy(kv => kv.Key))
                    lines.Add($"  {ItemData.DisplayName(kv.Key)} ×{kv.Value}");

                var inv = my.Inventory.Keys.OrderBy(k => k).ToList();
                numbered = new List<string>();
                for (int i = 0; i < 9 && i < inv.Count; i++)
                    numbered.Add($"{i + 1}. {ItemData.DisplayName(inv[i])} ×{my.Inventory[inv[i]]}");
                hints = "1–9:добавить  Q/E:выбор  X:убрать  C/V/B:золото +10/−10/+100  R:готов  Esc:отмена";
                break;
            }

            default:
                return;
        }

        DrawPanelBox(title, lines, numbered, hints);
    }

    private List<Snapshot.ShopEntry> ShopStockFor(Snapshot snap, Snapshot.PlayerState my)
    {
        if (snap.WorldMode == "dungeon")
            return snap.MerchantStock;
        // Город: находим смежного лавочника и берём его фиксированный сток
        foreach (var npc in snap.Npcs)
        {
            if (npc.NpcType is "blacksmith" or "alchemist" or "innkeeper"
                && CityData.Npcs.TryGetValue(npc.NpcType, out var def))
                return def.ShopStock
                    .Select(e => new Snapshot.ShopEntry { Key = e.Key, Price = e.Price })
                    .ToList();
        }
        return new List<Snapshot.ShopEntry>();
    }

    private void DrawPanelBox(string title, List<string> lines, List<string>? numbered, string hints)
    {
        var all = lines.Concat(numbered ?? new List<string>()).ToList();
        int w = 460, lineH = 18;
        int h = 40 + all.Count * lineH + 40;
        int x = 60, y = 60;

        _sb.Draw(Blank, new Rectangle(x, y, w, h), new Color(16, 14, 12, 235));
        _sb.Draw(Blank, new Rectangle(x, y, w, 2), Config.ColGold);
        _sb.Draw(Blank, new Rectangle(x, y + h - 2, w, 2), Config.ColGold);

        _sb.DrawString(_font!, title, new Vector2(x + 12, y + 8), Config.ColGold);
        int cy = y + 34;
        foreach (var line in all)
        {
            // Мёртвый тернарник (обе ветки ColText) и O(n²) Contains убраны
            _sb.DrawString(_font!, line, new Vector2(x + 12, cy), Config.ColText);
            cy += lineH;
        }
        _sb.DrawString(_font!, hints, new Vector2(x + 12, y + h - 26), Config.ColDim);
    }

    private void DrawCircle(Vector2 center, int radius, Color color)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            int w = (int)Math.Sqrt(radius * radius - dy * dy);
            _sb.Draw(Blank, new Rectangle((int)center.X - w, (int)center.Y + dy, w * 2, 1), color);
        }
    }

    private void DrawCircleOutline(Vector2 center, int radius, Color color)
    {
        for (int angle = 0; angle < 360; angle += 5)
        {
            double rad = angle * Math.PI / 180.0;
            int x = (int)(center.X + Math.Cos(rad) * radius);
            int y = (int)(center.Y + Math.Sin(rad) * radius);
            _sb.Draw(Blank, new Rectangle(x, y, 2, 2), color);
        }
    }

    private void DrawBar(int x, int y, int w, int h, int value, int max, Color color)
    {
        _sb.Draw(Blank, new Rectangle(x, y, w, h), new Color(30, 25, 20));
        int fillW = max > 0 ? w * value / max : 0;
        _sb.Draw(Blank, new Rectangle(x, y, fillW, h), color);
    }

    private Texture2D Blank
    {
        get
        {
            if (_blank == null)
            {
                _blank = new Texture2D(_device, 1, 1);
                _blank.SetData(new[] { Color.White });
                GpuReset.Register(_device, () =>
                {
                    try { _blank?.Dispose(); } catch { }
                    _blank = null;
                });
            }
            return _blank;
        }
    }
}
