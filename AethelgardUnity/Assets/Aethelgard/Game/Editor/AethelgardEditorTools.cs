#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

using Aethelgard.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace Aethelgard.EditorTools
{
    /// <summary>
    /// Инструменты редактора:
    ///  - Create UI Theme Asset — создаёт Assets/Aethelgard/Settings/UITheme.asset
    ///    (правится в инспекторе: цвета, шрифты, размеры — весь UI сразу);
    ///  - Generate UI Prefabs — генерирует префабы виджетов
    ///    (Panel/Button/Slot/Bar/Node), которые можно редактировать
    ///    визуально в Unity Editor и подставлять в экраны;
    ///  - Preview Main Menu — открывает меню игры прямо в редакторе
    ///    (без Play Mode можно смотреть/тюнинговать стили).
    /// </summary>
    public static class AethelgardEditorTools
    {
        private const string PrefabsDir = "Assets/Aethelgard/Prefabs/UI";

        [MenuItem("Aethelgard/Create UI Theme Asset")]
        public static void CreateTheme()
        {
            // Ассет кладём в Resources, чтобы игра подхватывала его в рантайме
            System.IO.Directory.CreateDirectory("Assets/Aethelgard/Resources");
            string path = "Assets/Aethelgard/Resources/UITheme.asset";
            var theme = AssetDatabase.LoadAssetAtPath<Aethelgard.UI.UITheme>(path);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<Aethelgard.UI.UITheme>();
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
                var btn = UiKit.WidgetButton(root, 0, 0, 260, theme.ButtonHeight,
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
            Debug.Log($"Префабы UI сгенерированы в {PrefabsDir} — редактируй их визуально.");
        }

        private static void UiKitPanel(RectTransform root, Aethelgard.UI.UITheme theme,
            int w, int h)
        {
            var go = Aethelgard.UI.UiKit.Node("PanelRoot", null);
            go.transform.SetParent(root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            Aethelgard.UI.UiKit.Panel(rt, 0, 0, w, h, theme);
        }

        private static void MakePrefab(string name, System.Action<RectTransform> build)
        {
            string path = $"{PrefabsDir}/{name}.prefab";
            var go = new GameObject(name, typeof(RectTransform));
            build((RectTransform)go.transform);
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static Aethelgard.UI.UITheme LoadOrCreateTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<Aethelgard.UI.UITheme>(
                "Assets/Aethelgard/Resources/UITheme.asset");
            if (theme != null) return theme;
            CreateTheme();
            return AssetDatabase.LoadAssetAtPath<Aethelgard.UI.UITheme>(
                "Assets/Aethelgard/Resources/UITheme.asset");
        }

        [MenuItem("Aethelgard/Preview Main Menu (Edit Mode)")]
        public static void PreviewMenu()
        {
            // Создаёт временный UI с меню — для правки стилей без запуска игры.
            // После настройки: Delete → Aethelgard UI Preview.
            var go = new GameObject("Aethelgard UI Preview", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();

            var theme = LoadOrCreateTheme();
            var game = new Aethelgard.Systems.GameController();
            var uiGo = new GameObject("PreviewUI", typeof(RectTransform));
            uiGo.transform.SetParent(go.transform, false);
            var ui = uiGo.AddComponent<Aethelgard.UI.UIManager>();
            ui.Init(game, theme);
            game.ScreenMode = Aethelgard.Systems.ScreenMode.Menu;
            ui.Show(game.ScreenMode);
            EditorApplication.MarkSceneDirty();
            Debug.Log("Превью меню создано. Правь UITheme.asset и виджеты; " +
                "удали объект 'Aethelgard UI Preview' когда закончишь.");
        }
    }
}
#endif
