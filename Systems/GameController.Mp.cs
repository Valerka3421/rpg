using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Screens = Aethelgard.Systems.ScreenMode;
namespace Aethelgard.Systems;

/// <summary>
/// Мультиплеер: подключение, ввод, панели (инвентарь/статы/навыки/
/// квесты/магазин/обмен), попап «Обмен/Дуэль», снапшоты.
/// Выделено из GameController (частичный класс).
/// </summary>
public partial class GameController
{
    public void StartMultiplayer(GameClient client, string classKey)
    {
        MpClient = client;
        IsMultiplayer = true;
        ScreenMode = Screens.MpGame;
        // Через фабрику, как на сервере: раньше локальная копия создавалась
        // мимо CharacterFactory и не имела стартового набора/экипировки
        Player = CharacterFactory.Create(string.IsNullOrEmpty(classKey) ? "warrior" : classKey);
    }

    /// <summary>
    /// Стать хостом: поднять сервер, при продолжении — загрузить мир из
    /// сейва (menu_multiplayer: «Продолжить игру»), иначе — новая игра.
    /// Класс выбирается на class_select (python: хост всегда выбирает класс).
    /// </summary>
    public bool StartMpHost(bool freshStart, string classKey)
    {
        try
        {
            var server = new GameServer();
            server.Start(GameServer.DefaultPort);
            if (!freshStart)
                server.Core.LoadWorld();   // продолжить из сохранения

            var client = new GameClient();
            bool ok = client.Connect("localhost", server.Port, classKey, null);
            if (!ok)
            {
                server.Stop();
                return false;
            }
            MpServer = server;
            StartMultiplayer(client, classKey);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Подключиться к хосту по адресу "host:port" (menu_mp_join).
    /// </summary>
    public bool StartMpJoin(string hostPort)
    {
        try
        {
            var parts = hostPort.Split(':');
            string host = parts[0];
            int port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 9000;
            var client = new GameClient();
            if (!client.Connect(host, port, "warrior", null))
                return false;
            StartMultiplayer(client, "warrior");
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Отключиться от MP-сессии и остановить сервер хоста.</summary>
    public void StopMultiplayer()
    {
        MpClient?.Disconnect();
        MpClient = null;
        MpServer?.Stop();
        MpServer = null;
        IsMultiplayer = false;
        MpSnapshot = null;
        MpPanel = "";
        ChatLog.Clear();
        ChatInput = "";
        ChatActive = false;
        _seenMpChat.Clear();
        _seenMpEvents.Clear();
        // Интерполяция: без сброса повторный вход в MP лерпил от позиций
        // прошлой сессии
        MpPrevPlayerPos.Clear();
        MpSinceSnapshot = 0f;
    }

    /// <summary>Синхронизирует видимые настройки (FPS, мини-карта, звук).</summary>
    public void ApplySettings()
    {
        ShowFps = Settings.ShowFps;
        MinimapVisible = Settings.ShowMinimap;
        if (Audio != null)
            Audio.Volume = Settings.Volume / 10f;
    }


    /// <summary>
    /// Открытая панель MP-клиента: "" | "inventory" | "stats" | "skills" |
    /// "quests" | "shop" — те же экраны, что в синглплеере (I/C/N/L/B).
    /// </summary>
    public string MpPanel = "";
    public int MpPage = 0;          // инвентарь: страница по 8 слотов
    public int MpShopPage = 0;      // магазин: страница по 9
    public bool MpShopSell = false; // магазин: режим продажи
    public bool MpSkillCast = false;// навыки: false — учить (1–7), true — кастовать (1–8)
    public int MpTradeSel = 0;      // обмен: выбранный предмет моего предложения

    // ── Журнал с вкладками (Бой/Предметы/Общение/Опыт/Всё) ──
    public GameChat Chat => GameChat.Instance;
    public int ChatViewTab = 0;     // 0 Всё · 1 Бой · 2 Предметы · 3 Общение · 4 Опыт
    public int ChatScroll = 0;      // 0 — самый низ журнала
    public bool ChatInputOpen = false;
    public string ChatLogInput = "";
    private readonly HashSet<string> _seenMpEvents = new();
    private readonly HashSet<string> _seenMpChat = new();

    // Интерполяция движения: позиции игроков из предыдущего снапшота +
    // время с последнего снапшота. Рендер (MpRenderer) лерпит между ними —
    // раньше координаты брались прямо из снапшота 20 Гц и движение дёргалось.
    public readonly Dictionary<int, (int X, int Y)> MpPrevPlayerPos = new();
    public float MpSinceSnapshot = 0f;

    /// <summary>Символы от Window.TextInput (учитывают раскладку рус/англ);
    /// разбираются вводом чата. SP и MP.</summary>
    public readonly Queue<char> TextInput = new();

    // ── Стек модальных экранов SP (InputMapper-lite): открытие — Push,
    // ── закрытие — Pop, ESC — всегда «шаг вверх» через HandleEscape().
    public readonly ModalStack Modals = new();

    /// <summary>Открыть модальный экран (класть на стек и переключить режим).</summary>
    public void OpenModal(string mode)
    {
        Modals.Push(mode);
        ScreenMode = mode;
    }

    /// <summary>Закрыть модальный экран со стека и вернуться к предыдущему.</summary>
    public void CloseModal()
    {
        Modals.Pop();
        ScreenMode = Modals.Current ?? Screens.Game;
    }

    /// <summary>
    /// Единая обработка ESC для SP (вызывается из World/Game.cs): модал —
    /// шаг вверх; в игре — открыть паузу. Возвращает true, если ESC был
    /// обработан здесь.
    /// </summary>
    public bool HandleEscape()
    {
        if (Modals.IsOpen) { CloseModal(); return true; }
        if (ScreenMode == Screens.Pause) { CloseModal(); return true; }
        if (ScreenMode == Screens.Game) { OpenModal(Screens.Pause); return true; }
        return false;
    }

    // ── MP попап «Обмен / Дуэль» по клику на другом игроке ──
    // (net/view.py:_click_map_player + player_popup)
    public int? MpPopupPid;
    public string MpPopupName = "";
    private int _mpMouseLeft;   // состояние ЛКМ прошлым кадром (edge detection)

    private bool MpPanelOpen() => MpPanel != "";

    /// <summary>
    /// Handle input in multiplayer mode — send to server.
    /// Клавиши повторяют синглплеер: WASD/Space/F/E/T/H/M/P, панели I/C/N/L/B,
    /// хотбар 1–8, дуэль Z, ожидание X, чат Y/Enter, сохранение мира F5.
    /// </summary>
    private void HandleMpInput(KeyboardState keys, KeyboardState prevKeys)
    {
        if (MpClient == null) return;
        var snap = MpSnapshot;
        var my = snap?.Players.FirstOrDefault(p => p.Pid == snap.MyPid);
        bool shift = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);

        // ── Ввод чата (общая логика с SP: EditChatLine) ──
        if (ChatActive)
        {
            bool submit, cancel;
            if (EditChatLine(keys, prevKeys, ref ChatInput, shift, out submit, out cancel))
            {
                if (submit && ChatInput.Length > 0)
                {
                    MpClient.SendChat(ChatInput);
                    ChatLog.Add($"[me] {ChatInput}");
                }
                ChatActive = false;
                ChatInput = "";
            }
            return;
        }

        // Чат: Y или Enter
        if ((keys.IsKeyDown(Keys.Y) && !prevKeys.IsKeyDown(Keys.Y))
            || (keys.IsKeyDown(Keys.Enter) && !prevKeys.IsKeyDown(Keys.Enter)))
        {
            ChatActive = true;
            ChatInput = "";
            return;
        }

        if (snap == null || my == null) return;

        // ── Журнал с вкладками: прокрутка и выбор вкладки мышью ──
        if (keys.IsKeyDown(Keys.PageUp) && !prevKeys.IsKeyDown(Keys.PageUp))
            ChatScroll += 3;
        if (keys.IsKeyDown(Keys.PageDown) && !prevKeys.IsKeyDown(Keys.PageDown))
            ChatScroll = Math.Max(0, ChatScroll - 3);
        if (keys.IsKeyDown(Keys.Home) && !prevKeys.IsKeyDown(Keys.Home))
            // не int.MaxValue: PageUp += 3 вызывал переполнение в отрицательное
            ChatScroll = int.MaxValue / 2;
        if (keys.IsKeyDown(Keys.End) && !prevKeys.IsKeyDown(Keys.End))
            ChatScroll = 0;
        var chatMouse = ScaledMouse();
        if (chatMouse.LeftButton == ButtonState.Pressed)
        {
            for (int i = 0; i < ChatPanel.TabNames.Length; i++)
            {
                if (ChatPanel.TabRect(i).Contains(chatMouse.X, chatMouse.Y))
                {
                    ChatViewTab = i;
                    ChatScroll = 0;
                }
            }
        }

        // Окно обмена открывается и закрывается снапшотом
        // (net/view.py:_apply_trade).
        if (snap.Trade != null && snap.Trade.Players.Contains(snap.MyPid))
        {
            if (MpPanel == "") { MpPanel = Screens.Trade; MpTradeSel = 0; }
        }
        else if (MpPanel == Screens.Trade)
        {
            MpPanel = "";
        }

        // ── Попап «Обмен / Дуэль» по клику на другом игроке ──
        // (net/view.py:_click_map_player + player_popup). Клавиши в попапе:
        // 1 — обмен, 2 — дуэль, Esc — закрыть.
        if (MpPopupPid.HasValue && !MpPanelOpen())
        {
            if (keys.IsKeyDown(Keys.D1) && !prevKeys.IsKeyDown(Keys.D1))
            {
                MpClient!.SendTrade(MpPopupPid.Value, "request");
                MpPopupPid = null;
                return;
            }
            if (keys.IsKeyDown(Keys.D2) && !prevKeys.IsKeyDown(Keys.D2))
            {
                MpClient!.SendDuelRequest(MpPopupPid.Value);
                MpPopupPid = null;
                return;
            }
            if (keys.IsKeyDown(Keys.Escape) && !prevKeys.IsKeyDown(Keys.Escape))
            { MpPopupPid = null; return; }
        }

        // Клик мышью по игроку на поле — открыть попап; мимо — закрыть
        bool mpClickNow = chatMouse.LeftButton == ButtonState.Pressed;
        bool mpClick = mpClickNow && _mpMouseLeft == 0;
        _mpMouseLeft = mpClickNow ? 1 : 0;
        if (mpClick && !MpPanelOpen())
        {
            int worldW = AethelgardGame.ScreenW;
            int worldH = AethelgardGame.ScreenH - Config.HudHeight;
            int camX = Math.Max(0, Math.Min(my.X * Config.TileSize + Config.TileSize / 2 - worldW / 2,
                Math.Max(0, snap.MapW * Config.TileSize - worldW)));
            int camY = Math.Max(0, Math.Min(my.Y * Config.TileSize + Config.TileSize / 2 - worldH / 2,
                Math.Max(0, snap.MapH * Config.TileSize - worldH)));
            int tx = (chatMouse.X + camX) / Config.TileSize;
            int ty = (chatMouse.Y + camY) / Config.TileSize;
            MpPopupPid = null;
            foreach (var p in snap.Players)
            {
                if (p.Pid == snap.MyPid) continue;
                if (p.X == tx && p.Y == ty)
                {
                    MpPopupPid = p.Pid;
                    MpPopupName = p.ClassName;
                    break;
                }
            }
        }

        // ── Панели (экраны MP дублируют SP-экраны) ──
        if (MpPanel != "")
        {
            HandleMpPanelInput(keys, prevKeys, snap, my, shift);
            return;
        }

        // Открытие панелей — те же клавиши, что в сингле
        if (keys.IsKeyDown(Keys.I) && !prevKeys.IsKeyDown(Keys.I))
        { MpPanel = Screens.Inventory; MpPage = 0; return; }
        if (keys.IsKeyDown(Keys.C) && !prevKeys.IsKeyDown(Keys.C))
        { MpPanel = Screens.Stats; return; }
        if (keys.IsKeyDown(Keys.N) && !prevKeys.IsKeyDown(Keys.N))
        { MpPanel = Screens.Skills; MpSkillCast = false; return; }
        if (keys.IsKeyDown(Keys.L) && !prevKeys.IsKeyDown(Keys.L))
        { MpPanel = Screens.Quests; return; }
        if (keys.IsKeyDown(Keys.B) && !prevKeys.IsKeyDown(Keys.B))
        { MpPanel = Screens.Shop; MpShopPage = 0; MpShopSell = false; return; }

        // Движение
        int dx = 0, dy = 0;
        if (keys.IsKeyDown(Keys.W) || keys.IsKeyDown(Keys.Up)) dy = -1;
        else if (keys.IsKeyDown(Keys.S) || keys.IsKeyDown(Keys.Down)) dy = 1;
        else if (keys.IsKeyDown(Keys.A) || keys.IsKeyDown(Keys.Left)) dx = -1;
        else if (keys.IsKeyDown(Keys.D) || keys.IsKeyDown(Keys.Right)) dx = 1;

        if ((dx != 0 || dy != 0) && _moveCooldown <= 0)
        {
            MpClient.SendInput(dx, dy);
            _moveCooldown = Config.PlayerMoveCooldown;
        }

        // Атака (Space)
        if (keys.IsKeyDown(Keys.Space) && !prevKeys.IsKeyDown(Keys.Space))
            MpClient.SendAttack();

        // Лестницы/выход/ворота (F — как в сингле)
        if (keys.IsKeyDown(Keys.F) && !prevKeys.IsKeyDown(Keys.F))
            MpClient.SendUseStairs();

        // Добыча ресурса (E — как в сингле): смежный живой узел из снапшота
        if (keys.IsKeyDown(Keys.E) && !prevKeys.IsKeyDown(Keys.E))
        {
            foreach (var node in snap.Harvests)
            {
                if (Math.Max(Math.Abs(node.X - my.X), Math.Abs(node.Y - my.Y)) <= 1)
                {
                    MpClient.SendHarvest(node.X, node.Y);
                    break;
                }
            }
        }

        // Поговорить с NPC (T — как в сингле): смежный NPC по NpcType
        if (keys.IsKeyDown(Keys.T) && !prevKeys.IsKeyDown(Keys.T))
        {
            foreach (var npc in snap.Npcs)
            {
                if (!string.IsNullOrEmpty(npc.NpcType)
                    && Math.Max(Math.Abs(npc.X - my.X), Math.Abs(npc.Y - my.Y)) <= 1)
                {
                    MpClient.SendQuestTalk(npc.NpcType);
                    break;
                }
            }
        }

        // Быстрое зелье (H — как в сингле)
        if (keys.IsKeyDown(Keys.H) && !prevKeys.IsKeyDown(Keys.H))
            MpClient.SendUsePotion();

        // Ожидание хода (X — net/core.py:wait)
        if (keys.IsKeyDown(Keys.X) && !prevKeys.IsKeyDown(Keys.X))
            MpClient.SendWait();

        // Дуэль (Z — net/core.py:duel_request): ближайший другой игрок
        if (keys.IsKeyDown(Keys.Z) && !prevKeys.IsKeyDown(Keys.Z))
        {
            int bestPid = -1, bestDist = int.MaxValue;
            foreach (var p in snap.Players)
            {
                if (p.Pid == snap.MyPid) continue;
                int d = Math.Max(Math.Abs(p.X - my.X), Math.Abs(p.Y - my.Y));
                if (d < bestDist) { bestDist = d; bestPid = p.Pid; }
            }
            if (bestPid >= 0) MpClient.SendDuelRequest(bestPid);
        }

        // Обмен (G — соседняя с Z клавиша; в python обмен вызывается кликом
        // по игроку): предложение обмена ближайшему игроку в клетке рядом.
        if (keys.IsKeyDown(Keys.G) && !prevKeys.IsKeyDown(Keys.G))
        {
            int bestPid = -1, bestDist = int.MaxValue;
            foreach (var p in snap.Players)
            {
                if (p.Pid == snap.MyPid) continue;
                int d = Math.Max(Math.Abs(p.X - my.X), Math.Abs(p.Y - my.Y));
                if (d < bestDist) { bestDist = d; bestPid = p.Pid; }
            }
            if (bestPid >= 0 && bestDist <= 1) MpClient.SendTrade(bestPid, "request");
        }

        // Повышение класса (P — как в сингле)
        if (keys.IsKeyDown(Keys.P) && !prevKeys.IsKeyDown(Keys.P))
            MpClient.SendPromote();

        // Хотбар 1–8 (как в сингле): использовать назначенный предмет
        for (int i = 0; i < 8 && i < my.Quickbar.Length; i++)
        {
            var key = Keys.D1 + i;
            if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key)
                && !shift && my.Quickbar[i] != null)
            {
                MpClient.SendUseItem(my.Quickbar[i]!);
            }
        }

        // Сохранение мира хостом (F5 — аналог быстрого сохранения)
        if (keys.IsKeyDown(Keys.F5) && !prevKeys.IsKeyDown(Keys.F5))
            MpClient.SendSaveWorld();
    }

    /// <summary>
    /// Ввод на открытой MP-панели: раскладка клавиш повторяет
    /// HandleInventoryInput / HandleStatsInput / HandleSkillsInput /
    /// HandleQuestInput / HandleShopInput синглплеера.
    /// </summary>
    private void HandleMpPanelInput(KeyboardState keys, KeyboardState prevKeys,
        Snapshot snap, Snapshot.PlayerState my, bool shift)
    {
        // Esc / повторная клавиша панели — закрыть
        if (keys.IsKeyDown(Keys.Escape) && !prevKeys.IsKeyDown(Keys.Escape))
        { MpPanel = ""; return; }

        switch (MpPanel)
        {
            case Screens.Inventory:
            {
                if (keys.IsKeyDown(Keys.I) && !prevKeys.IsKeyDown(Keys.I))
                { MpPanel = ""; return; }

                // Q/E — страницы (как в SP)
                if (keys.IsKeyDown(Keys.Q) && !prevKeys.IsKeyDown(Keys.Q))
                    MpPage = Math.Max(0, MpPage - 1);
                if (keys.IsKeyDown(Keys.E) && !prevKeys.IsKeyDown(Keys.E))
                    MpPage++;

                // U/V/H/B/N — снять weapon/armor/head/legs/boots (как в SP)
                var unequips = new (Keys Key, string Slot)[]
                {
                    (Keys.U, "weapon"), (Keys.V, "armor"), (Keys.H, "head"),
                    (Keys.B, "legs"), (Keys.N, "boots"),
                };
                foreach (var (key, slot) in unequips)
                {
                    if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                    {
                        MpClient!.SendUnequip(slot);
                        return;
                    }
                }

                var items = my.Inventory.Keys.ToList();
                // Shift+1–8 — назначить предмет в хотбар (как в SP)
                if (shift)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        var key = Keys.D1 + i;
                        int idx = MpPage * 8 + i;
                        if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key)
                            && idx < items.Count)
                            MpClient!.SendQuickbar(i, items[idx]);
                    }
                    return;
                }

                // 1–8 — использовать/экипировать предмет (как в SP)
                for (int i = 0; i < 8; i++)
                {
                    var key = Keys.D1 + i;
                    int idx = MpPage * 8 + i;
                    if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key)
                        && idx < items.Count)
                        MpClient!.SendUseItem(items[idx]);
                }
                break;
            }

            case Screens.Stats:
            {
                if (keys.IsKeyDown(Keys.C) && !prevKeys.IsKeyDown(Keys.C))
                { MpPanel = ""; return; }
                // 1–6 — вложить очко (str/agi/vit/luk/df/int, как в SP)
                string[] stats = { "str", "agi", "vit", "luk", "df_stat", "int" };
                for (int i = 0; i < 6; i++)
                {
                    var key = Keys.D1 + i;
                    if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                        MpClient!.SendAllocateStat(stats[i]);
                }
                break;
            }

            case Screens.Skills:
            {
                if (keys.IsKeyDown(Keys.N) && !prevKeys.IsKeyDown(Keys.N))
                { MpPanel = ""; return; }

                // Q/E — переключение «учить / кастовать» (как табы в SP)
                if ((keys.IsKeyDown(Keys.Q) && !prevKeys.IsKeyDown(Keys.Q))
                    || (keys.IsKeyDown(Keys.E) && !prevKeys.IsKeyDown(Keys.E)))
                    MpSkillCast = !MpSkillCast;

                // P — повышение класса (как в SP)
                if (keys.IsKeyDown(Keys.P) && !prevKeys.IsKeyDown(Keys.P))
                {
                    MpClient!.SendPromote();
                    return;
                }

                if (!MpSkillCast)
                {
                    // 1–7 — изучить узел (как HandleSkillsInput в SP)
                    for (int i = 0; i < 7; i++)
                    {
                        var key = Keys.D1 + i;
                        if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                            MpClient!.SendLearnNode(i);
                    }
                }
                else
                {
                    // 1–8 — применить активный навык (как F1–F8 в SP)
                    for (int i = 0; i < 8; i++)
                    {
                        var key = Keys.D1 + i;
                        if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                            MpClient!.SendSkill(i);
                    }
                }
                break;
            }

            case Screens.Quests:
            {
                if (keys.IsKeyDown(Keys.L) && !prevKeys.IsKeyDown(Keys.L))
                { MpPanel = ""; return; }
                // 1–9: принять доступный / сдать выполненный (как в SP)
                var available = snap.QuestBoard.Where(q => q.State == "available").ToList();
                var completed = snap.QuestBoard.Where(q => q.State == "completed").ToList();
                for (int i = 0; i < 9; i++)
                {
                    var key = Keys.D1 + i;
                    if (!keys.IsKeyDown(key) || prevKeys.IsKeyDown(key)) continue;
                    if (i < available.Count)
                        MpClient!.SendQuest(available[i].Key, true);
                    else
                    {
                        int cIdx = i - available.Count;
                        if (cIdx < completed.Count)
                            MpClient!.SendQuest(completed[cIdx].Key, false);
                    }
                }
                break;
            }

            case Screens.Shop:
            {
                if (keys.IsKeyDown(Keys.B) && !prevKeys.IsKeyDown(Keys.B))
                { MpPanel = ""; return; }

                // S — купить/продать (как в SP)
                if (keys.IsKeyDown(Keys.S) && !prevKeys.IsKeyDown(Keys.S))
                { MpShopSell = !MpShopSell; MpShopPage = 0; }

                // Left/Right — страницы (как в SP)
                if (keys.IsKeyDown(Keys.Left) && !prevKeys.IsKeyDown(Keys.Left))
                    MpShopPage = Math.Max(0, MpShopPage - 1);
                if (keys.IsKeyDown(Keys.Right) && !prevKeys.IsKeyDown(Keys.Right))
                    MpShopPage++;

                // R — реролл стока торговца (как в SP)
                if (keys.IsKeyDown(Keys.R) && !prevKeys.IsKeyDown(Keys.R))
                    MpClient!.SendReroll();

                // 1–9 — купить/продать
                for (int i = 0; i < 9; i++)
                {
                    var key = Keys.D1 + i;
                    if (!keys.IsKeyDown(key) || prevKeys.IsKeyDown(key)) continue;
                    int idx = MpShopPage * Config.ShopPageSize + i;
                    if (MpShopSell) MpClient!.SendSell(idx);
                    else MpClient!.SendBuy(idx);
                }
                break;
            }

            case Screens.Trade:
            {
                var trade = snap.Trade;
                if (trade == null) { MpPanel = ""; return; }
                int myPid = snap.MyPid;
                int other = trade.Players.FirstOrDefault(p => p != myPid);
                if (other <= 0) { MpPanel = ""; return; }
                bool accepted = trade.Accepted.Contains(myPid);

                // Esc — отмена (decline до подтверждения, cancel после)
                if (keys.IsKeyDown(Keys.Escape) && !prevKeys.IsKeyDown(Keys.Escape))
                {
                    MpClient!.SendTrade(other, accepted ? "cancel" : "decline");
                    MpPanel = "";
                    return;
                }

                // Y — принять заявку
                if (!accepted && keys.IsKeyDown(Keys.Y) && !prevKeys.IsKeyDown(Keys.Y))
                { MpClient!.SendTrade(other, "accept"); return; }

                if (!accepted) return;

                var myOffer = trade.Offers.GetValueOrDefault(myPid.ToString())
                    ?? new Dictionary<string, int>();
                var myKeys = myOffer.Keys.OrderBy(k => k).ToList();
                if (MpTradeSel >= myKeys.Count) MpTradeSel = Math.Max(0, myKeys.Count - 1);

                // R — подтвердить (net/core.py:trade_ready)
                if (keys.IsKeyDown(Keys.R) && !prevKeys.IsKeyDown(Keys.R))
                { MpClient!.SendTrade(other, "ready"); return; }

                // Q/E — выбор предмета моего предложения
                if (keys.IsKeyDown(Keys.Q) && !prevKeys.IsKeyDown(Keys.Q))
                    MpTradeSel = Math.Max(0, MpTradeSel - 1);
                if (keys.IsKeyDown(Keys.E) && !prevKeys.IsKeyDown(Keys.E))
                    MpTradeSel = Math.Min(myKeys.Count - 1, MpTradeSel + 1);

                // X — убрать выбранный предмет из предложения
                if (keys.IsKeyDown(Keys.X) && !prevKeys.IsKeyDown(Keys.X)
                    && myKeys.Count > 0 && MpTradeSel < myKeys.Count)
                { MpClient!.SendTrade(other, "remove", myKeys[MpTradeSel]); return; }

                // Золото: C +10 · V −10 · B +100 (в python — кнопки в окне)
                int myGold = trade.Gold.GetValueOrDefault(myPid.ToString());
                if (keys.IsKeyDown(Keys.C) && !prevKeys.IsKeyDown(Keys.C)
                    && myGold + 10 <= my.Gold)
                    MpClient!.SendTrade(other, "gold", null, 1, myGold + 10);
                if (keys.IsKeyDown(Keys.V) && !prevKeys.IsKeyDown(Keys.V)
                    && myGold - 10 >= 0)
                    MpClient!.SendTrade(other, "gold", null, 1, myGold - 10);
                if (keys.IsKeyDown(Keys.B) && !prevKeys.IsKeyDown(Keys.B)
                    && myGold + 100 <= my.Gold)
                    MpClient!.SendTrade(other, "gold", null, 1, myGold + 100);

                // 1–9 — добавить предмет из инвентаря (×1)
                var inv = my.Inventory.Keys.OrderBy(k => k).ToList();
                for (int i = 0; i < 9 && i < inv.Count; i++)
                {
                    var key = Keys.D1 + i;
                    if (keys.IsKeyDown(key) && !prevKeys.IsKeyDown(key))
                    { MpClient!.SendTrade(other, "add", inv[i]); return; }
                }
                break;
            }
        }
    }

    /// <summary>
    /// Update multiplayer state — fetch latest snapshot.
    /// </summary>
    public void UpdateMp(float dt)
    {
        if (MpClient == null) return;

        // Обрыв соединения: раньше игра вечно рисовала замороженный снапшот
        // без экрана «соединение потеряно» — теперь выходим в меню с сообщением.
        if (!MpClient.Connected)
        {
            string reason = MpClient.ErrorMessage ?? "Соединение с сервером потеряно";
            StopMultiplayer();
            ScreenMode = Screens.Menu;
            ShowMessage(reason);
            Audio?.Play("menu_move");
            return;
        }

        var snap = MpClient.GetSnapshot();
        if (snap != null && !ReferenceEquals(snap, MpSnapshot))
        {
            // Новый снапшот: запоминаем прошлые позиции игроков для интерполяции.
            // Дальние скачки (телепорт/смена этажа/респавн) не лерпятся —
            // иначе спрайт «проносится дугой» через всю карту за 50 мс.
            MpPrevPlayerPos.Clear();
            if (MpSnapshot != null)
                foreach (var p in MpSnapshot.Players)
                {
                    var cur = snap.Players.FirstOrDefault(q => q.Pid == p.Pid);
                    if (cur != null && Math.Abs(cur.X - p.X) + Math.Abs(cur.Y - p.Y) <= 3)
                        MpPrevPlayerPos[p.Pid] = (p.X, p.Y);
                }
            MpSinceSnapshot = 0f;
            MpSnapshot = snap;
        }
        MpSinceSnapshot = Math.Min(MpSinceSnapshot + dt, 0.5f);

        if (MpSnapshot != null)
        {
            // Update chat log from snapshot
            foreach (var chat in MpSnapshot.Chat)
            {
                string entry = $"[P{chat.Pid}] {chat.Text}";
                // Хеш-множество вместо ChatLog.Contains: раньше два одинаковых
                // сообщения от разных игроков показывались один раз, и поиск был O(n)
                if (_seenMpChat.Add(entry))
                {
                    ChatLog.Add(entry);
                    // Журнал «Общение» работает и в мультиплеере
                    GameChat.Instance.Social(entry);
                }
            }
            if (_seenMpChat.Count > 400) _seenMpChat.Clear();
            if (ChatLog.Count > 20)
                ChatLog.RemoveRange(0, ChatLog.Count - 20);

            // События сервера (киллы/обмены/квесты) — вкладка «Бой»
            foreach (var ev in MpSnapshot.Events)
            {
                if (_seenMpEvents.Add(ev))
                    GameChat.Instance.Combat(ev);
            }
            if (_seenMpEvents.Count > 400) _seenMpEvents.Clear();
        }

        // Message timer
        if (MessageTimer > 0)
            MessageTimer -= dt;

        // Move cooldown
        _moveCooldown -= dt;
    }

    // ── City NPCs ──

}
