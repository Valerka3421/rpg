// uGUI-порт render/QuestScreen.cs: доска заданий — секции доступные /
// активные (с прогресс-барами) / выполненные, строки с ховером и тултипами.
// Визуал строится один раз в Build(), Refresh() обновляет тексты/цвета,
// сдвигает группы-секции по фактическим количествам квестов (как стек
// секций на desktop) и выполняет хит-тесты через UiInput.
// RefreshRunStats() — порт QuestScreen.DrawRunStats (экран stats_run).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UColor = UnityEngine.Color;
using XnaRect = Microsoft.Xna.Framework.Rectangle;

namespace Aethelgard
{
    public class QuestUi : MonoBehaviour
    {
        private GameController _game = null!;
        private UiKitCanvas _kit = null!;

        // Лимиты строк секций (как на desktop: Math.Min(count, cap))
        private const int AvailCap = 4, ActiveCap = 3, DoneCap = 3;
        private const int RowH = 48, ActiveRowH = 58;
        private const int RowStep = RowH + 4, ActiveStep = ActiveRowH + 4;

        private XnaRect _panel;
        private Text _counts = null!;

        // Секции — группы-контейнеры, чтобы сдвигать стек целиком
        private GameObject _availGroup = null!, _activeGroup = null!, _doneGroup = null!;
        private Vector2 _availBasePos, _activeBasePos, _doneBasePos;
        private int _availBuiltY, _activeBuiltY, _doneBuiltY;

        // Строки доступных/выполненных: bg, имя, описание, награда, подсказка
        private readonly GameObject[] _availRows = new GameObject[AvailCap];
        private readonly Image[] _availBgs = new Image[AvailCap];
        private readonly Text[] _availNames = new Text[AvailCap];
        private readonly Text[] _availDescs = new Text[AvailCap];
        private readonly Text[] _availRewards = new Text[AvailCap];
        private readonly Text[] _availTrail = new Text[AvailCap];
        private readonly XnaRect[] _availRects = new XnaRect[AvailCap];

        private readonly GameObject[] _doneRows = new GameObject[DoneCap];
        private readonly Image[] _doneBgs = new Image[DoneCap];
        private readonly Text[] _doneNames = new Text[DoneCap];
        private readonly Text[] _doneDescs = new Text[DoneCap];
        private readonly Text[] _doneRewards = new Text[DoneCap];
        private readonly Text[] _doneTrail = new Text[DoneCap];
        private readonly XnaRect[] _doneRects = new XnaRect[DoneCap];

        // Строки активных квестов: + прогресс-бар и «Готово»
        private readonly GameObject[] _activeRows = new GameObject[ActiveCap];
        private readonly Image[] _activeBgs = new Image[ActiveCap];
        private readonly Text[] _activeNames = new Text[ActiveCap];
        private readonly Text[] _activeDescs = new Text[ActiveCap];
        private readonly Image[] _activeFills = new Image[ActiveCap];
        private readonly Text[] _activeProg = new Text[ActiveCap];
        private readonly Text[] _activeReady = new Text[ActiveCap];
        private readonly XnaRect[] _activeRects = new XnaRect[ActiveCap];

        // ── Статистика забега (RefreshRunStats) ──
        private GameObject _runGroup = null!;
        private readonly Text[] _runLeftNames = new Text[11];
        private readonly Text[] _runLeftVals = new Text[11];
        private readonly Text[] _runRightNames = new Text[12];
        private readonly Text[] _runRightVals = new Text[12];
        private Text _runEmpty = null!;

        private bool _hoverAny;

        public static QuestUi Create(GameController game)
        {
            var go = new GameObject("QuestUi");
            var ui = go.AddComponent<QuestUi>();
            ui._game = game;
            ui.Build();
            return ui;
        }

        // ── Построение визуала один раз ──
        private void Build()
        {
            _kit = new UiKitCanvas("QuestCanvas");

            int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;
            _panel = new XnaRect((sw - 1000) / 2, (sh - 640) / 2, 1000, 640);
            _kit.Panel(_panel);

            // Шапка
            _kit.Label(new XnaRect(_panel.X + 20, _panel.Y + 14, 500, 36),
                "ДОСКА ЗАДАНИЙ", 2, UiKitCanvas.Theme.GoldBright);
            _kit.Label(new XnaRect(_panel.X + 20, _panel.Y + 48, 500, 20),
                "принимай, выполняй, сдавай", 4, UiKitCanvas.Theme.TextMuted);
            _counts = _kit.Label(new XnaRect(_panel.X + _panel.Width - 620, _panel.Y + 24, 600, 20),
                "", 4, UiKitCanvas.Theme.TextDim, TextAnchor.UpperRight);
            _kit.Label(new XnaRect(_panel.X, _panel.Y + _panel.Height - 28, _panel.Width, 22),
                "↑/↓: выбор   Enter: принять/сдать   Esc: закрыть", 4,
                UiKitCanvas.Theme.TextMuted, TextAnchor.UpperCenter);

            // Построенные позиции секций — все три с лимитами строк;
            // фактические позиции/видимость обновляет Refresh.
            int y = _panel.Y + 74;
            _availBuiltY = y;
            y += 26 + AvailCap * RowStep + 6 + UiKitCanvas.Gap;
            _activeBuiltY = y;
            y += 26 + ActiveCap * ActiveStep + 6 + UiKitCanvas.Gap;
            _doneBuiltY = y;

            BuildStaticSection(_panel.X + 20, _availBuiltY, _panel.Width - 40,
                "Доступные", out _availGroup, out _availBasePos,
                _availRows, _availBgs, _availNames, _availDescs, _availRewards,
                _availTrail, _availRects);
            BuildActiveSection(_panel.X + 20, _activeBuiltY, _panel.Width - 40,
                out _activeGroup, out _activeBasePos);
            BuildStaticSection(_panel.X + 20, _doneBuiltY, _panel.Width - 40,
                "Выполненные — сдайте для награды", out _doneGroup, out _doneBasePos,
                _doneRows, _doneBgs, _doneNames, _doneDescs, _doneRewards,
                _doneTrail, _doneRects);

            BuildRunStats();
        }

        /// <summary>Секция с однотипными строками (доступные/выполненные).</summary>
        private void BuildStaticSection(int x, int y, int w, string title,
            out GameObject group, out Vector2 basePos,
            GameObject[] rows, Image[] bgs, Text[] names, Text[] descs,
            Text[] rewards, Text[] trails, XnaRect[] rects)
        {
            group = NewGroup(out basePos);
            int cap = rows.Length;
            var section = new XnaRect(x, y, w, 26 + cap * RowStep + 6);
            var bg = _kit.Section(section, title);
            bg.rectTransform.SetParent(group.transform, true);

            int rx = section.X + 8, rw = section.Width - 16;
            for (int i = 0; i < cap; i++)
            {
                int ry = y + 30 + i * RowStep;
                var row = new XnaRect(rx, ry, rw, RowH);
                rects[i] = row;
                rows[i] = NewGroup(out _);
                rows[i].transform.SetParent(group.transform, true);

                bgs[i] = _kit.RectImage(row, UiKitCanvas.Theme.BgSlot);
                bgs[i].rectTransform.SetParent(rows[i].transform, true);
                names[i] = _kit.Label(new XnaRect(row.X + 10, row.Y + 5, rw - 260, 22),
                    "", 3, UiKitCanvas.Theme.Text);
                names[i].rectTransform.SetParent(rows[i].transform, true);
                descs[i] = _kit.Label(new XnaRect(row.X + 10, row.Y + 26, rw - 260, 16),
                    "", 4, UiKitCanvas.Theme.TextDim);
                descs[i].rectTransform.SetParent(rows[i].transform, true);
                rewards[i] = _kit.Label(new XnaRect(row.X + row.Width - 240, row.Y + 5, 230, 16),
                    "", 4, UiKitCanvas.Theme.Gold, TextAnchor.UpperRight);
                rewards[i].rectTransform.SetParent(rows[i].transform, true);
                trails[i] = _kit.Label(new XnaRect(row.X + row.Width - 240, row.Y + 26, 230, 16),
                    "", 4, UiKitCanvas.Theme.GoldBright, TextAnchor.UpperRight);
                trails[i].rectTransform.SetParent(rows[i].transform, true);
            }
        }

        /// <summary>Секция активных квестов: строка + прогресс-бар справа.</summary>
        private void BuildActiveSection(int x, int y, int w,
            out GameObject group, out Vector2 basePos)
        {
            group = NewGroup(out basePos);
            var section = new XnaRect(x, y, w, 26 + ActiveCap * ActiveStep + 6);
            var bg = _kit.Section(section, "Активные");
            bg.rectTransform.SetParent(group.transform, true);

            int rx = section.X + 8, rw = section.Width - 16;
            const int barW = 220;
            for (int i = 0; i < ActiveCap; i++)
            {
                int ry = y + 30 + i * ActiveStep;
                var row = new XnaRect(rx, ry, rw, ActiveRowH);
                _activeRects[i] = row;
                _activeRows[i] = NewGroup(out _);
                _activeRows[i].transform.SetParent(group.transform, true);

                _activeBgs[i] = _kit.RectImage(row, UiKitCanvas.Theme.BgSlot);
                _activeBgs[i].rectTransform.SetParent(_activeRows[i].transform, true);
                _activeNames[i] = _kit.Label(new XnaRect(row.X + 10, row.Y + 5, rw - 260, 22),
                    "", 3, UiKitCanvas.Theme.Text);
                _activeNames[i].rectTransform.SetParent(_activeRows[i].transform, true);
                _activeDescs[i] = _kit.Label(new XnaRect(row.X + 10, row.Y + 26, rw - 260, 16),
                    "", 4, UiKitCanvas.Theme.TextDim);
                _activeDescs[i].rectTransform.SetParent(_activeRows[i].transform, true);

                var bar = _kit.Bar(new XnaRect(row.X + row.Width - barW - 12,
                    row.Y + 8, barW, 14), UiKitCanvas.Theme.Xp);
                // Внутренняя подложка бара живёт в корне канваса — переносим
                // её в группу строки, чтобы бар двигался вместе с секцией.
                ((RectTransform)bar.fill.transform.parent)
                    .SetParent(_activeRows[i].transform, true);
                bar.bg.rectTransform.SetParent(_activeRows[i].transform, true);
                _activeFills[i] = bar.fill;
                _activeProg[i] = _kit.Label(
                    new XnaRect(row.X + row.Width - barW - 12, row.Y + 8, barW, 16),
                    "", 4, UiKitCanvas.Theme.GoldBright, TextAnchor.UpperCenter);
                _activeProg[i].rectTransform.SetParent(_activeRows[i].transform, true);
                _activeReady[i] = _kit.Label(
                    new XnaRect(row.X + row.Width - 200, row.Y + 34, 190, 16),
                    "Готово — сдай!", 4, UiKitCanvas.Theme.Xp, TextAnchor.UpperRight);
                _activeReady[i].rectTransform.SetParent(_activeRows[i].transform, true);
            }
        }

        /// <summary>Группа-контейнер в координатах дизайна (для сдвига секций).</summary>
        private GameObject NewGroup(out Vector2 basePos)
        {
            var go = new GameObject("group");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(_kit.Root, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(AethelgardGame.ScreenW, AethelgardGame.ScreenH);
            basePos = Vector2.zero;
            return go;
        }

        // ── Обновление каждый кадр (вызывается извне) ──
        public void Refresh()
        {
            if (_kit == null) return;
            _hoverAny = false;

            var avail = _game.Quests.Available.ToList();
            var active = _game.Quests.Active.ToList();
            var done = _game.Quests.Completed.ToList();
            _counts.text = $"Доступно: {avail.Count}   Активно: {active.Count}   Сдать: {done.Count}";

            int y = _panel.Y + 74;

            // ── Доступные квесты ──
            int availRows = Math.Min(avail.Count, AvailCap);
            PlaceSection(_availGroup, _availBasePos, _availBuiltY, y,
                avail.Count > 0, availRows, _availRows);
            if (avail.Count > 0)
            {
                UpdateStaticRows(_availRows, _availBgs, _availNames, _availDescs,
                    _availRewards, _availTrail, _availRects, avail,
                    selectedIndexOffset: 0, trailing: "Enter — принять",
                    trailingColor: UiKitCanvas.Theme.GoldBright);
                y += 26 + availRows * RowStep + 6 + UiKitCanvas.Gap;
            }

            // ── Активные квесты ──
            int activeRows = Math.Min(active.Count, ActiveCap);
            PlaceSection(_activeGroup, _activeBasePos, _activeBuiltY, y,
                active.Count > 0, activeRows, _activeRows);
            if (active.Count > 0)
            {
                UpdateActiveRows(active, activeRows);
                y += 26 + activeRows * ActiveStep + 6 + UiKitCanvas.Gap;
            }

            // ── Выполненные ──
            int doneRows = Math.Min(done.Count, DoneCap);
            PlaceSection(_doneGroup, _doneBasePos, _doneBuiltY, y,
                done.Count > 0, doneRows, _doneRows);
            if (done.Count > 0)
                UpdateStaticRows(_doneRows, _doneBgs, _doneNames, _doneDescs,
                    _doneRewards, _doneTrail, _doneRects, done,
                    selectedIndexOffset: avail.Count, trailing: "Enter — сдать!",
                    trailingColor: UiKitCanvas.Theme.Xp);

            if (!_hoverAny) _kit.HideTooltip();
        }

        /// <summary>Показ/скрытие секции и сдвиг группы к фактической позиции.</summary>
        private void PlaceSection(GameObject group, Vector2 basePos, int builtY,
            int wantY, bool visible, int rows, GameObject[] rowGos)
        {
            group.SetActive(visible);
            if (!visible) return;
            int shift = wantY - builtY; // вниз в дизайне = минус по anchored Y
            group.GetComponent<RectTransform>().anchoredPosition =
                basePos + new Vector2(0f, -shift);
            for (int i = 0; i < rowGos.Length; i++)
                rowGos[i].SetActive(i < rows);
        }

        /// <summary>Строки доступных/выполненных квестов (имя, описание, награда).</summary>
        private void UpdateStaticRows(GameObject[] rows, Image[] bgs, Text[] names,
            Text[] descs, Text[] rewards, Text[] trails, XnaRect[] rects,
            List<string> keys, int selectedIndexOffset, string trailing,
            UColor trailingColor)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                if (!rows[i].activeSelf) continue;
                var def = QuestData.Get(keys[i]);
                if (def == null) { rows[i].SetActive(false); continue; }

                bool selected = selectedIndexOffset + i == _game.QuestSelected;
                bool hot = UiInput.Hot(rects[i]);
                bgs[i].color = selected ? UiKitCanvas.Theme.BgSlotSel
                    : hot ? UiKitCanvas.Theme.BgSlotHot : UiKitCanvas.Theme.BgSlot;
                names[i].text = def.Name;
                names[i].color = selected ? UiKitCanvas.Theme.GoldBright : UiKitCanvas.Theme.Text;
                descs[i].text = def.Desc;
                rewards[i].text = $"+{def.RewardGold} з. +{def.RewardXp} XP";
                trails[i].text = trailing;
                trails[i].color = trailingColor;

                if (hot)
                {
                    _hoverAny = true;
                    var lines = new List<string> { def.Desc,
                        $"+{def.RewardGold} з.  +{def.RewardXp} XP" };
                    foreach (var it in def.RewardItems)
                        lines.Add($"  + {ItemData.DisplayName(it.Key)}×{it.Value}");
                    _kit.ShowTooltip(rects[i], def.Name, lines);
                }
            }
        }

        /// <summary>Активные квесты: имя, прогресс-бар reached/kill-счётчика.</summary>
        private void UpdateActiveRows(List<KeyValuePair<string, QuestState>> active, int rows)
        {
            for (int i = 0; i < rows; i++)
            {
                var (key, state) = (active[i].Key, active[i].Value);
                var def = QuestData.Get(key);
                if (def == null) { _activeRows[i].SetActive(false); continue; }

                _activeNames[i].text = def.Name;
                _activeDescs[i].text = def.Desc;

                double frac = state.Count > 0 ? (double)state.Progress / state.Count : 0;
                UiKitCanvas.SetBar(_activeFills[i], frac);
                _activeProg[i].text = $"{state.Progress}/{state.Count}";
                _activeReady[i].gameObject.SetActive(frac >= 1.0);

                bool hot = UiInput.Hot(_activeRects[i]);
                _activeBgs[i].color = hot ? UiKitCanvas.Theme.BgSlotHot : UiKitCanvas.Theme.BgSlot;
                if (hot)
                {
                    _hoverAny = true;
                    var lines = new List<string>
                    {
                        def.Type == "reach" ? "Достигни нужной глубины"
                            : def.Type == "craft" ? "Скрафти предметы"
                            : def.Type == "collect" ? "Собери предметы"
                            : "Убей врагов",
                        $"Прогресс: {state.Progress}/{state.Count}",
                        $"Награда: +{def.RewardGold} з.  +{def.RewardXp} XP",
                    };
                    _kit.ShowTooltip(_activeRects[i], def.Name, lines);
                }
            }
        }

        // ── Статистика забега (Tab) — порт DrawRunStats ──

        private void BuildRunStats()
        {
            _runGroup = NewGroup(out _);

            int sw = AethelgardGame.ScreenW, sh = AethelgardGame.ScreenH;
            var panel = new XnaRect((sw - 1100) / 2, (sh - 640) / 2, 1100, 640);
            var bg = _kit.Panel(panel);
            bg.rectTransform.SetParent(_runGroup.transform, true);

            _kit.Label(new XnaRect(panel.X + 20, panel.Y + 14, 500, 36),
                "СТАТИСТИКА ЗАБЕГА", 2, UiKitCanvas.Theme.GoldBright);
            _kit.Label(new XnaRect(panel.X, panel.Y + panel.Height - 28, panel.Width, 22),
                "Tab/Esc — закрыть", 4, UiKitCanvas.Theme.TextMuted, TextAnchor.UpperCenter);

            int colW = panel.Width / 2 - 30;
            var leftRect = new XnaRect(panel.X + 20, panel.Y + 60, colW, 26 + 11 * 28 + 8);
            var lb = _kit.Section(leftRect, "Общее");
            lb.rectTransform.SetParent(_runGroup.transform, true);
            int ly = leftRect.Y + 32;
            for (int i = 0; i < _runLeftNames.Length; i++)
            {
                _runLeftNames[i] = _kit.Label(new XnaRect(leftRect.X + 12, ly, colW / 2, 18),
                    "", 4, UiKitCanvas.Theme.TextDim);
                _runLeftNames[i].rectTransform.SetParent(_runGroup.transform, true);
                _runLeftVals[i] = _kit.Label(new XnaRect(leftRect.X + 12, ly, colW - 24, 18),
                    "", 4, UiKitCanvas.Theme.GoldBright, TextAnchor.UpperRight);
                _runLeftVals[i].rectTransform.SetParent(_runGroup.transform, true);
                ly += 28;
            }

            var rightRect = new XnaRect(leftRect.X + leftRect.Width + UiKitCanvas.Gap, leftRect.Y,
                panel.Width - 40 - colW - UiKitCanvas.Gap, leftRect.Height);
            var rb = _kit.Section(rightRect, "Убито по типам");
            rb.rectTransform.SetParent(_runGroup.transform, true);
            int ry = rightRect.Y + 32;
            int rowW = rightRect.Width - 24;
            for (int i = 0; i < _runRightNames.Length; i++)
            {
                _runRightNames[i] = _kit.Label(new XnaRect(rightRect.X + 12, ry, rowW / 2, 18),
                    "", 4, UiKitCanvas.Theme.Text);
                _runRightNames[i].rectTransform.SetParent(_runGroup.transform, true);
                _runRightVals[i] = _kit.Label(new XnaRect(rightRect.X + 12, ry, rowW, 18),
                    "", 4, UiKitCanvas.Theme.GoldBright, TextAnchor.UpperRight);
                _runRightVals[i].rectTransform.SetParent(_runGroup.transform, true);
                ry += 28;
            }
            _runEmpty = _kit.Label(new XnaRect(rightRect.X + 12, rightRect.Y + 32, rowW, 18),
                "(пока никого)", 4, UiKitCanvas.Theme.TextMuted);
            _runEmpty.rectTransform.SetParent(_runGroup.transform, true);
        }

        /// <summary>Обновить статистику забега (экран stats_run, Tab).</summary>
        public void RefreshRunStats()
        {
            if (_kit == null) return;

            var ts = DateTime.Now - _game.RunStart;
            int mins = (int)ts.TotalSeconds / 60, secs = (int)ts.TotalSeconds % 60;
            (string, string)[] left =
            {
                ("Время", $"{mins}:{secs:D2}"),
                ("Убито мобов", _game.KillsTotal.ToString()),
                ("Боссов", _game.BossKills.ToString()),
                ("Уровень", _game.Player.Level.ToString()),
                ("Глубина", _game.Depth.ToString()),
                ("Золото сейчас", _game.Player.Gold.ToString()),
                ("Золото добыто", _game.GoldEarned.ToString()),
                ("Зелий", _game.PotionsUsed.ToString()),
                ("Крафтов", _game.ItemsCrafted.ToString()),
                ("Квестов сдано", _game.QuestsDone.ToString()),
                ("Квестов активно", _game.Quests.Active.Count.ToString()),
            };
            for (int i = 0; i < _runLeftNames.Length; i++)
            {
                _runLeftNames[i].text = left[i].Item1;
                _runLeftVals[i].text = left[i].Item2;
            }

            // Правая колонка — убито по типам, по убыванию
            int row = 0;
            bool has = false;
            foreach (var kv in _game.KillsByCh.OrderByDescending(kv => kv.Value))
            {
                if (kv.Value == 0) continue;
                has = true;
                if (row >= _runRightNames.Length) break;
                _runRightNames[row].text = kv.Key;
                _runRightVals[row].text = kv.Value.ToString();
                row++;
            }
            for (int i = row; i < _runRightNames.Length; i++)
            {
                _runRightNames[i].text = "";
                _runRightVals[i].text = "";
            }
            _runEmpty.gameObject.SetActive(!has);
        }

        public void SetVisible(bool on)
        {
            _kit.SetActive(on);
            if (!on) _kit.HideTooltip();
        }
    }
}
