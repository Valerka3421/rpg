#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Aethelgard.EditorTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Aethelgard.BaseClass;
    using Aethelgard.Entities;
    using Aethelgard.Entities.Monsters;
    using Aethelgard.Structs;
    using Aethelgard.World;
    using Aethelgard.Systems;
    using Aethelgard.Data;
    using Aethelgard.Net;
    using Aethelgard.Audio;
    /// <summary>
    /// ВИЗУАЛЬНЫЙ РЕДАКТОР ИНТЕРФЕЙСА (без кода!).
    /// Окно: Aethelgard → Edit Interface.
    ///
    /// Слева — все экраны и их элементы (кнопки, тексты, ячейки).
    /// Справа — поля выбранного элемента: позиция X/Y, размер W/H,
    /// текст, размер шрифта, чекбокс «Текст из макета».
    /// Кнопка «Сохранить» записывает правки в префаб экрана — игра
    /// применяет их автоматически (даже в запущенном exe).
    ///
    /// Для серий одинаковых элементов (ячейки инвентаря Slot0…31,
    /// строки квестов, рецепты) есть «сетка»: задаёшь шаг X/Y и
    /// расставляешь всю серию одной кнопкой.
    /// </summary>
    public class UIScreenEditor : EditorWindow
    {
        private const string ScreensDir = "Assets/Aethelgard/Resources/Screens";

        private string[] _screenFiles = System.Array.Empty<string>();
        private string[] _screenNames = System.Array.Empty<string>();
        private int _screenIdx;
        private GameObject _prefab;          // загруженный ассет
        private string _loadedName = "";

        private Vector2 _scroll;
        private List<Transform> _flat = new();  // плоский список узлов
        private int _sel = -1;

        // Поля редактирования
        private Vector2 _pos, _size;
        private string _text = "";
        private int _fontSize;
        private bool _textFromLayout;
        private string _gridBase = "";
        private float _gridStepX, _gridStepY;
        private int _gridCount;

        [MenuItem("Aethelgard/Edit Interface")]
        public static void Open()
        {
            var w = GetWindow<UIScreenEditor>("Интерфейс Этельгарда");
            w.minSize = new Vector2(720, 480);
            w.ReloadScreens();
        }

        private void ReloadScreens()
        {
            try
            {
                _screenFiles = System.IO.Directory.GetFiles(ScreensDir, "*.prefab");
            }
            catch { _screenFiles = System.Array.Empty<string>(); }
            _screenNames = _screenFiles
                .Select(System.IO.Path.GetFileNameWithoutExtension)
                .OrderBy(n => n).ToArray();
            _screenIdx = Mathf.Clamp(_screenIdx, 0, Mathf.Max(0, _screenNames.Length - 1));
            LoadSelected();
        }

        private string CurrentPath() =>
            _screenNames.Length == 0 ? "" : $"{ScreensDir}/{_screenNames[_screenIdx]}.prefab";

        private void LoadSelected()
        {
            string path = CurrentPath();
            if (path == _loadedName) return;
            _prefab = path.Length > 0
                ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
            _loadedName = path;
            _sel = -1;
            Flatten();
        }

        /// <summary>Плоский список узлов с отступами по глубине.</summary>
        private void Flatten()
        {
            _flat.Clear();
            if (_prefab == null) return;
            Walk(_prefab.transform, 0);
        }

        private void Walk(Transform t, int depth)
        {
            if (depth > 0) _flat.Add(t);
            for (int i = 0; i < t.childCount; i++)
                Walk(t.GetChild(i), depth + 1);
        }

        private void OnGUI()
        {
            if (GUILayout.Button("⟳ Обновить список экранов"))
                ReloadScreens();

            if (_screenNames.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "Префабы экранов не найдены. Сначала: Aethelgard → Generate Screen Prefabs.",
                    MessageType.Warning);
                return;
            }

            int idx = EditorGUILayout.Popup("Экран", _screenIdx, _screenNames);
            if (idx != _screenIdx) { _screenIdx = idx; _loadedName = ""; LoadSelected(); }

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Выбери элемент слева, правь поля справа, затем «Сохранить». " +
                "Игра подхватит правки автоматически.", MessageType.None);

            EditorGUILayout.BeginHorizontal();

            // ── Левая колонка: список элементов ──
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Width(330));
            for (int i = 0; i < _flat.Count; i++)
            {
                var t = _flat[i];
                if (t == null) continue;
                string indent = new string(' ', Mathf.Min(6, DepthOf(t) * 2));
                bool hasText = t.GetComponent<Text>() != null || t.Find("Text") != null;
                string mark = t.name.StartsWith("!") ? " [текст]" : "";
                string label = $"{indent}{CleanName(t.name)}{mark}";
                var style = i == _sel ? EditorStyles.linkLabel : EditorStyles.label;
                Rect r = GUILayoutUtility.GetRect(new GUIContent(label), style,
                    GUILayout.Height(18));
                r.xMin += DepthOf(t) * 14f;
                if (GUI.Button(r, label, i == _sel ? EditorStyles.linkLabel : EditorStyles.label))
                {
                    _sel = i;
                    LoadFields(t);
                }
            }
            EditorGUILayout.EndScrollView();

            // ── Правая колонка: редактор выбранного элемента ──
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            if (_sel >= 0 && _sel < _flat.Count && _flat[_sel] != null)
                DrawElementEditor(_flat[_sel]);
            else
                EditorGUILayout.HelpBox("Выбери элемент слева.", MessageType.None);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            if (GUILayout.Button("💾 Сохранить в префаб", GUILayout.Height(34)))
                Save();
            EditorGUILayout.HelpBox(
                "Сохранение записывает префаб в Resources/Screens — " +
                "запущенная игра применит правки сама (или перезапусти exe).",
                MessageType.None);
        }

        private int DepthOf(Transform t)
        {
            int d = 0;
            while (t.parent != null && t.parent != _prefab.transform) { d++; t = t.parent; }
            return d + 1;
        }

        private static string CleanName(string n) =>
            n.StartsWith("!") ? n[1..] : n;

        // ── Редактор выбранного элемента ──

        private void LoadFields(Transform t)
        {
            var rt = (RectTransform)t;
            _pos = rt.anchoredPosition;
            _size = rt.sizeDelta;

            Text txt = t.GetComponent<Text>() ?? t.GetComponentInChildren<Text>();
            if (txt != null)
            {
                _text = txt.text;
                _fontSize = txt.fontSize;
                _textFromLayout = t.name.StartsWith("!");
            }
            else
            {
                _text = "";
                _fontSize = 0;
                _textFromLayout = false;
            }

            // Серия (Slot0, Goods3, QuestRow12 …)?
            var m = Regex.Match(CleanName(t.name), @"^([A-Za-z_А-Яа-я]+)(\d+)$");
            if (m.Success)
            {
                _gridBase = m.Groups[1].Value;
                CountSeries(t, _gridBase, out _gridStepX, out _gridStepY, out _gridCount);
            }
            else _gridBase = "";
        }

        private void CountSeries(Transform node, string baseName,
            out float stepX, out float stepY, out int count)
        {
            var parent = node.parent;
            var members = new List<Transform>();
            foreach (Transform c in parent)
                if (c.name == baseName + Regex.Match(CleanName(node.name), @"\d+$").Value
                    || Regex.IsMatch(CleanName(c.name), $"^{baseName}\\d+$"))
                    members.Add(c);
            count = members.Count;
            stepX = 0; stepY = 0;
            if (members.Count >= 2)
            {
                members.Sort((a, b) => Idx(a, baseName).CompareTo(Idx(b, baseName)));
                var p0 = ((RectTransform)members[0]).anchoredPosition;
                var p1 = ((RectTransform)members[1]).anchoredPosition;
                stepX = p1.x - p0.x;
                stepY = p1.y - p0.y;
            }
        }

        private static int Idx(Transform t, string baseName)
        {
            var m = Regex.Match(CleanName(t.name), @"\d+$");
            return m.Success ? int.Parse(m.Value) : 0;
        }

        private void DrawElementEditor(Transform t)
        {
            EditorGUILayout.LabelField("Элемент: " + CleanName(t.name), EditorStyles.boldLabel);
            string kind = t.GetComponent<Button>() != null ? "Кнопка"
                : t.GetComponent<Text>() != null || t.Find("Text") != null ? "Текст"
                : t.GetComponent<Image>() != null ? "Панель/Слот" : "Контейнер";
            EditorGUILayout.LabelField("Тип: " + kind);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Положение и размер (px, дизайн 1920×1080):");
            _pos = EditorGUILayout.Vector2Field("  Позиция (X, Y)", _pos);
            _size = EditorGUILayout.Vector2Field("  Размер (W, H)", _size);

            EditorGUILayout.Space(6);
            Text txt = t.GetComponent<Text>() ?? t.GetComponentInChildren<Text>();
            if (txt != null)
            {
                EditorGUILayout.LabelField("Содержание:");
                _text = EditorGUILayout.TextField("  Текст", _text);
                _fontSize = EditorGUILayout.IntField("  Размер шрифта", _fontSize);
                _textFromLayout = EditorGUILayout.Toggle(
                    new GUIContent("Текст из макета (!)",
                        "Включи, чтобы текст брался из макета, а не из кода " +
                        "(для статических кнопок и подписей)"),
                    _textFromLayout);
            }

            if (_gridBase.Length > 0)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField(
                    $"Серия «{_gridBase}N» ({_gridCount} шт.) — сетка:", EditorStyles.boldLabel);
                _gridStepX = EditorGUILayout.FloatField("  Шаг X", _gridStepX);
                _gridStepY = EditorGUILayout.FloatField("  Шаг Y", _gridStepY);
                if (GUILayout.Button("Расставить всю серию от выбранного"))
                    ApplyGrid(t);
                EditorGUILayout.HelpBox(
                    "Например для ячеек инвентаря: шаг X=100, Y=0 — по горизонтали; " +
                    "шаг Y=100, X=0 — по вертикали (позиции — центры).",
                    MessageType.None);
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Применить к элементу", GUILayout.Height(28)))
                ApplyFields(t);
        }

        private void ApplyFields(Transform t)
        {
            var rt = (RectTransform)t;
            Undo.RecordObject(rt, "Aethelgard UI Edit");
            rt.anchoredPosition = _pos;
            rt.sizeDelta = _size;

            Text txt = t.GetComponent<Text>() ?? t.GetComponentInChildren<Text>();
            if (txt != null)
            {
                Undo.RecordObject(txt, "Aethelgard UI Edit");
                txt.text = _text;
                if (_fontSize > 0) txt.fontSize = _fontSize;
                EditorUtility.SetDirty(txt);

                // Чекбокс «Текст из макета» = префикс «!» у имени узла
                string want = _textFromLayout ? "!" + CleanName(t.name) : CleanName(t.name);
                if (t.name != want)
                {
                    Undo.RecordObject(t, "Aethelgard UI Edit");
                    t.name = want;
                    EditorUtility.SetDirty(t.gameObject);
                }
            }
            EditorUtility.SetDirty(rt);
        }

        private void ApplyGrid(Transform selected)
        {
            int start = Idx(selected, _gridBase);
            var startPos = ((RectTransform)selected).anchoredPosition;
            foreach (Transform c in selected.parent)
            {
                if (!Regex.IsMatch(CleanName(c.name), $"^{_gridBase}\\d+$")) continue;
                int i = Idx(c, _gridBase);
                var rt = (RectTransform)c;
                Undo.RecordObject(rt, "Aethelgard UI Grid");
                rt.anchoredPosition = startPos + new Vector2(_gridStepX, _gridStepY) * (i - start);
                EditorUtility.SetDirty(rt);
            }
        }

        private void Save()
        {
            if (_prefab == null) return;
            PrefabUtility.SaveAsPrefabAsset(_prefab, CurrentPath());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Aethelgard] Интерфейс сохранён: {CurrentPath()} — игра применит правки.");
        }
    }
}
#endif
