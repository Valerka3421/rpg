#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

using Aethelgard.Runtime;
using Aethelgard.Systems;
using Aethelgard.UI;
using Object = UnityEngine.Object;

namespace Aethelgard.EditorTools
{
    /// <summary>
    /// Инструменты редактора для правки интерфейса БЕЗ кода:
    ///
    ///  - Create UI Theme Asset — глобальная тема (цвета, шрифты, размеры);
    ///  - Generate UI Prefabs — префабы виджетов (Panel/Button/Slot/Bar/Node);
    ///  - Generate Screen Prefabs — ГЛАВНЫЙ инструмент: строит все экраны
    ///    игры и сохраняет их как префабы в Resources/Screens/. После этого
    ///    игра накладывает их на свою разметку: раскладка, тексты (узлы с
    ///    префиксом «!»), размеры ячеек правятся в этих префабах;
    ///  - Preview Screen — открывает инстанс экрана в иерархии для правки;
    ///  - Apply Changes To Prefab — сохраняет правки выбранного инстанса
    ///    обратно в ассет префаба экрана.
    /// </summary>
    public static class AethelgardEditorTools
    {
        private const string PrefabsDir = "Assets/Aethelgard/Prefabs/UI";
        private const string ScreensDir = "Assets/Aethelgard/Resources/Screens";

        // Экраны, для которых создаются редактируемые префабы:
        // имя файла → ScreenMode, в котором экран рисуется.
        private static readonly (string Name, string Mode, string? RootName)[] Screens =
        {
            ("Menu", ScreenMode.Menu, "Menu"),
            ("Pause", ScreenMode.Pause, "Menu"),
            ("ClassSelect", ScreenMode.ClassSelect, "ClassSelect"),
            ("Hud", ScreenMode.Game, "Hud"),
            ("Inventory", ScreenMode.Inventory, "Inventory"),
            ("Shop", ScreenMode.Shop, "Shop"),
            ("Skills", ScreenMode.Skills, "Skills"),
            ("Craft", ScreenMode.Craft, "Craft"),
            ("Stats", ScreenMode.Stats, "Stats"),
            ("Quests", ScreenMode.Quests, "Quests"),
            ("RunStats", ScreenMode.StatsRun, "Quests"),
            ("Dialogue", ScreenMode.Dialogue, "Dialogue"),
            ("Bigmap", ScreenMode.Bigmap, "Bigmap"),
            ("Console", ScreenMode.Game, "DevConsole"),
            ("Mp", ScreenMode.MpGame, "Mp"),
            ("Death", ScreenMode.Game, "DeathVictory"),
            ("Message", ScreenMode.Game, "Message"),
        };

        [MenuItem("Aethelgard/Create UI Theme Asset")]
        public static void CreateTheme()
        {
            System.IO.Directory.CreateDirectory("Assets/Aethelgard/Resources");
            string path = "Assets/Aethelgard/Resources/UITheme.asset";
            var theme = AssetDatabase.LoadAssetAtPath<UITheme>(path);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UITheme>();
                AssetDatabase.CreateAsset(theme, path);
            }
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Selection.activeObject = theme;
            Debug.Log("UITheme.asset готов — открой его в инспекторе и правь цвета/размеры UI.");
        }

        [MenuItem("Aethelgard/Generate UI Prefabs")]
        public static void GeneratePrefabs()
        {
            System.IO.Directory.CreateDirectory(PrefabsDir);
            var theme = LoadOrCreateTheme();

            MakePrefab("UIPanel", root => { UiKitPanel(root, theme, 400, 300); });
            MakePrefab("UIButton", root =>
            {
                UiKit.WidgetButton(root, 0, 0, 260, theme.ButtonHeight,
                    "Кнопка", theme.BodySize, theme);
            });
            MakePrefab("UISlot", root => { UiKit.Slot(root, 0, 0, 92, theme); });
            MakePrefab("UIBar", root => { UiKit.Bar(root, 0, 0, 200, 20, theme, theme.Life); });
            MakePrefab("UINode", root =>
            {
                var slot = UiKit.Slot(root, 0, 0, 96, theme, "SkillNode");
                UiKit.Frame((RectTransform)slot.transform, theme.GoldSoft, 2);
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Префабы виджетов сгенерированы в {PrefabsDir}.");
        }

        /// <summary>
        /// Строит каждый экран игры (с текущей темой и тестовым игроком)
        /// и сохраняет как редактируемый префаб в Resources/Screens/.
        /// Именно эти префабы игра накладывает на свою разметку
        /// (ScreenPrefabs.Apply) — правки в Editor видны в игре.
        /// </summary>
        [MenuItem("Aethelgard/Generate Screen Prefabs")]
        public static void GenerateScreenPrefabs()
        {
            System.IO.Directory.CreateDirectory(ScreensDir);
            var theme = LoadOrCreateTheme();

            // Временный canvas + игровые экраны (edit mode, без Play)
            var canvasGo = new GameObject("Aethelgard PrefabGen", typeof(RectTransform));
            canvasGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var game = new GameController();
            game.Audio = new Aethelgard.Audio.AudioSystem();
            game.NewGame("warrior"); // тестовый игрок — чтобы экраны с состоянием построились

            var uiGo = new GameObject("PreviewUI", typeof(RectTransform));
            uiGo.transform.SetParent(canvasGo.transform, false);
            var ui = uiGo.AddComponent<UIManager>();
            ui.Init(game, theme);

            var mouse = new Microsoft.Xna.Framework.Input.MouseState();
            foreach (var (name, mode, rootName) in Screens)
            {
                SetupScreenState(game, name);
                game.ScreenMode = mode;
                ui.Show(mode);
                ui.Tick(mouse, mouse, 0.016f);
                ui.Tick(mouse, mouse, 0.016f);

                // Корень экрана → его панель (префаб = панель, Apply кладёт
                // её прямоугольник на _panel при пересборке)
                GameObject? root = FindActiveChild(uiGo.transform, rootName);
                if (root == null)
                {
                    Debug.LogWarning($"[Aethelgard] Экран '{name}' не построился");
                    continue;
                }
                GameObject? panel = FindPanel(root);
                if (panel == null) panel = root;
                string path = $"{ScreensDir}/{name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(panel, path);
                Debug.Log($"[Aethelgard] Префаб экрана сохранён: {path}");
            }

            Object.DestroyImmediate(canvasGo);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Готово: экраны в Resources/Screens — правь их в Editor, " +
                "игра применит раскладку и тексты (узлы с «!») автоматически.");
        }

        private static GameObject? FindActiveChild(Transform parent, string name)
        {
            Transform? best = null;
            foreach (Transform c in parent)
            {
                if (c.name == name && c.gameObject.activeSelf) best = c;
            }
            return best?.gameObject;
        }

        /// <summary>Панель экрана = ребёнок с именем на «Panel», иначе корень.</summary>
        private static GameObject? FindPanel(GameObject root)
        {
            foreach (Transform c in root.transform)
                if (c.name.EndsWith("Panel") && c.GetComponent<RectTransform>() != null)
                    return c.gameObject;
            return root;
        }

        /// <summary>Состояние игры, чтобы зависимые экраны построились.</summary>
        private static void SetupScreenState(GameController game, string name)
        {
            game.Victory = name == "Death";
            game.Console.Open = name == "Console";
            game.LastMessage = name == "Message" ? "Тестовое сообщение" : null;
            game.MessageTimer = name == "Message" ? 5f : 0f;
            game.DialogueText = name == "Dialogue"
                ? "Приветствую, путник. Чем могу помочь?" : null;
            game.DialogueNpc = name == "Dialogue" ? "Стражник" : null;
            if (name == "Death") game.KillsTotal = 5;
        }

        /// <summary>Открыть инстанс экрана в иерархии для правки без Play Mode.</summary>
        [MenuItem("Aethelgard/Preview Screen")]

        [MenuItem("Aethelgard/Preview Screen/Pause")]
        public static void PreviewPause() => PreviewScreen("Pause", ScreenMode.Pause);
        [MenuItem("Aethelgard/Preview Screen/ClassSelect")]
        public static void PreviewClass() => PreviewScreen("ClassSelect", ScreenMode.ClassSelect);
        [MenuItem("Aethelgard/Preview Screen/Hud")]
        public static void PreviewHud() => PreviewScreen("Hud", ScreenMode.Game);
        [MenuItem("Aethelgard/Preview Screen/Inventory")]
        public static void PreviewInventory() => PreviewScreen("Inventory", ScreenMode.Inventory);
        [MenuItem("Aethelgard/Preview Screen/Shop")]
        public static void PreviewShop() => PreviewScreen("Shop", ScreenMode.Shop);
        [MenuItem("Aethelgard/Preview Screen/Skills")]
        public static void PreviewSkills() => PreviewScreen("Skills", ScreenMode.Skills);
        [MenuItem("Aethelgard/Preview Screen/Craft")]
        public static void PreviewCraft() => PreviewScreen("Craft", ScreenMode.Craft);
        [MenuItem("Aethelgard/Preview Screen/Stats")]
        public static void PreviewStats() => PreviewScreen("Stats", ScreenMode.Stats);
        [MenuItem("Aethelgard/Preview Screen/Quests")]
        public static void PreviewQuests() => PreviewScreen("Quests", ScreenMode.Quests);
        [MenuItem("Aethelgard/Preview Screen/Dialogue")]
        public static void PreviewDialogue() => PreviewScreen("Dialogue", ScreenMode.Dialogue);
        [MenuItem("Aethelgard/Preview Screen/Death")]
        public static void PreviewDeath() => PreviewScreen("Death", ScreenMode.Game);

        private static void PreviewScreen(string screenName, string mode)
        {
            if (GameObject.Find("Aethelgard UI Preview") != null)
                Object.DestroyImmediate(GameObject.Find("Aethelgard UI Preview"));

            var canvasGo = new GameObject("Aethelgard UI Preview", typeof(RectTransform));
            canvasGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            var theme = LoadOrCreateTheme();
            var game = new GameController();
            game.Audio = new Aethelgard.Audio.AudioSystem();
            game.NewGame("warrior");

            var uiGo = new GameObject(screenName, typeof(RectTransform));
            uiGo.transform.SetParent(canvasGo.transform, false);
            var ui = uiGo.AddComponent<UIManager>();
            ui.Init(game, theme);
            game.ScreenMode = mode;
            ui.Show(mode);
            var mouse = new Microsoft.Xna.Framework.Input.MouseState();
            ui.Tick(mouse, mouse, 0.016f);
            ui.Tick(mouse, mouse, 0.016f);

            Selection.activeObject = uiGo;
            EditorApplication.MarkSceneDirty();
            Debug.Log($"Превью '{screenName}' создано в иерархии. Правь узлы, " +
                "затем Aethelgard → Apply Changes To Prefab, чтобы сохранить в ассет.");
        }

        /// <summary>Сохранить правки выбранного инстанса экрана обратно в префаб.</summary>
        [MenuItem("Aethelgard/Apply Changes To Prefab")]
        public static void ApplyChangesToPrefab()
        {
            var sel = Selection.activeGameObject;
            if (sel == null)
            {
                Debug.LogWarning("Выбери в иерархии корень экрана (например 'Menu').");
                return;
            }
            string path = $"{ScreensDir}/{sel.name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning($"Префаб '{path}' не найден — сначала Generate Screen Prefabs.");
                return;
            }
            PrefabUtility.SaveAsPrefabAsset(sel, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"Правки сохранены в {path} — игра применит их автоматически.");
        }

        private static void UiKitPanel(RectTransform root, UITheme theme, int w, int h)
        {
            var go = UiKit.Node("PanelRoot", null);
            go.transform.SetParent(root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            UiKit.Panel(rt, 0, 0, w, h, theme);
        }

        private static void MakePrefab(string name, System.Action<RectTransform> build)
        {
            string path = $"{PrefabsDir}/{name}.prefab";
            var go = new GameObject(name, typeof(RectTransform));
            build((RectTransform)go.transform);
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static UITheme LoadOrCreateTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UITheme>(
                "Assets/Aethelgard/Resources/UITheme.asset");
            if (theme != null) return theme;
            CreateTheme();
            return AssetDatabase.LoadAssetAtPath<UITheme>(
                "Assets/Aethelgard/Resources/UITheme.asset");
        }
    }
}
#endif
