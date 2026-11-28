using System.Collections.Generic;
    using Aethelgard.Systems;
    using Aethelgard.Render;
    using Aethelgard.UI;
    using UnityEngine;
    using XnaColor = Microsoft.Xna.Framework.Color;

    namespace Aethelgard.Runtime
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
    /// Отрисовка мира: тайлы видимого окна + сущности (монстры, NPC, игрок).
    /// Мир рисуется сеткой SpriteRenderer'ов (пул), спрайты — из SpriteStore
    /// (процедурные 64×64, parity GameRenderer MonoGame-версии).
    /// Камера центрируется на игроке и работает в дизайн-координатах.
    /// </summary>
    public class WorldRenderer
    {
        private const int Tile = Config.TileSize;

        private readonly GameObject _root;
        private readonly Camera _camera;
        private readonly TileRenderer _tiles = new();
        private readonly Dictionary<(int, int), SpriteRenderer> _tilePool = new();
        private readonly Dictionary<Entity, SpriteRenderer> _entityPool = new();
        private SpriteRenderer? _playerSr;
        private Transform? _playerT;
        private SpriteRenderer? _darkness;

        public WorldRenderer(Transform parent, Camera camera)
        {
            _camera = camera;
            _camera.orthographic = true;
            // Видимое окно 20×8 тайлов, как Config.ViewW/H; центр камеры на игроке
            _camera.orthographicSize = Config.ViewH * Tile / 2f;
            _root = new GameObject("World");
            _root.transform.SetParent(parent, false);
        }

        /// <summary>Мировая позиция тайла (x,y) — тайлы идут сверху-вниз.</summary>
        public static Vector3 TileWorld(int x, int y, int mapH) =>
            new(x * Tile + Tile / 2f, (mapH - 1 - y) * Tile + Tile / 2f, 0f);

        public void Draw(GameController game)
        {
            var map = game.CurrentMap;
            if (map == null || game.Player == null) return;
            int mapH = Config.MapH;

            // Камера на игроке
            var ppos = TileWorld(game.Player.X, game.Player.Y, mapH);
            _playerT = _root.transform.Find("Player");
            if (_playerT != null)
            {
                var pt = _playerT.position;
                _camera.transform.position = new Vector3(pt.x, pt.y, -10f);
            }

            // ── Тайлы видимого окна ──
            int x0 = Mathf.Clamp(game.Player.X - Config.ViewW / 2 - 1, 0, Config.MapW - 1);
            int x1 = Mathf.Min(Config.MapW, x0 + Config.ViewW + 2);
            int y0 = Mathf.Clamp(game.Player.Y - Config.ViewH / 2 - 1, 0, mapH - 1);
            int y1 = Mathf.Min(mapH, y0 + Config.ViewH + 2);

            var used = new HashSet<(int, int)>();
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    used.Add((x, y));
                    if (!_tilePool.TryGetValue((x, y), out var sr))
                    {
                        var go = new GameObject($"tile_{x}_{y}");
                        go.transform.SetParent(_root.transform, false);
                        go.transform.position = TileWorld(x, y, mapH);
                        sr = go.AddComponent<SpriteRenderer>();
                        _tilePool[(x, y)] = sr;
                    }
                    string kind = TileRenderer.TileKindAt(map, x, y);
                    int variant = (x * 7 + y * 13) % 4;
                    sr.sprite = SpriteFromTile(_tiles.GetTile(kind, variant, 1f));
                    sr.sortingOrder = 0;
                }
            }

            // Скрываем тайлы вне окна (пул)
            foreach (var kv in _tilePool)
                kv.Value.gameObject.SetActive(used.Contains(kv.Key));

            // ── Предметы на полу ──
            foreach (var kv in map.Items)
            {
                if (!_tilePool.TryGetValue(kv.Key, out var tsr) || tsr == null) continue;
                // Маркеры предметов рисуются поверх тайла маленьким квадратом
                var child = tsr.transform.Find("item");
                if (child == null)
                {
                    var go = new GameObject("item");
                    go.transform.SetParent(tsr.transform, false);
                    var r = go.AddComponent<SpriteRenderer>();
                    r.sortingOrder = 2;
                    r.sprite = MakeRectSprite(MapPalette.Item, 14);
                    child = go.transform;
                }
                child.gameObject.SetActive(true);
            }

            // ── Сущности ──
            DrawEntities(map.Monsters, game, mapH);

            // ── Игрок ──
            if (_playerSr == null)
            {
                var go = new GameObject("Player");
                go.transform.SetParent(_root.transform, false);
                _playerSr = go.AddComponent<SpriteRenderer>();
                _playerSr.sortingOrder = 5;
            }
            var cls = game.Player.ClassKey is { Length: > 0 } ck
                ? ClassData.Get(ck) : null;
            var pcolor = cls?.Color ?? new XnaColor(120, 230, 130);
            _playerSr.sprite = SpriteStore.Get(
                "player_" + (cls?.Key ?? "warrior"), pcolor);
            _playerSr.transform.position = ppos;
            _playerSr.gameObject.SetActive(true);

            // ── Тьма (game_renderer._draw_darkness): оверлей (5,7,12,α135) с
            //    радиальной «дырой» r=(FOV+1) тайлов; градиент α=α·(r/half)^1.7 ──
            if (_darkness == null)
            {
                var dgo = new GameObject("Darkness");
                dgo.transform.SetParent(_root.transform, false);
                _darkness = dgo.AddComponent<SpriteRenderer>();
                _darkness.sortingOrder = 6;
                var tex = UiKit.RadialGradient(256, 1.7f);
                _darkness.sprite = tex;
                _darkness.color = new Color(5f / 255f, 7f / 255f, 12f / 255f, 135f / 255f);
                float diameter = 2f * (Config.FovRadius + 1) * Tile;
                dgo.transform.localScale = new Vector3(
                    diameter / tex.bounds.size.x, diameter / tex.bounds.size.y, 1f);
            }
            _darkness.transform.position = ppos;
        }

        private void DrawEntities(List<Entity> entities, GameController game, int mapH)
        {
            var alive = new HashSet<Entity>();
            if (entities != null)
            {
                foreach (var e in entities)
                {
                    if (e == null || !e.Alive) continue;
                    alive.Add(e);
                    if (!_entityPool.TryGetValue(e, out var sr) || sr == null)
                    {
                        var go = new GameObject("ent_" + e.Name);
                        go.transform.SetParent(_root.transform, false);
                        sr = go.AddComponent<SpriteRenderer>();
                        sr.sortingOrder = 4;
                        _entityPool[e] = sr;
                    }
                    bool boss = e.Boss;
                    string key = !string.IsNullOrEmpty(e.MonsterKey)
                        ? e.MonsterKey : e.Name.ToLowerInvariant();
                    sr.sprite = SpriteStore.Get(key, e.Color, boss);
                    // Idle-покачивание (ANIM_IDLE_AMP 2.5px, sin(t·4)) — python
                    float bob = Mathf.Sin(Time.time * 4f + (e.X + e.Y) * 0.7f) * 2.5f;
                    sr.transform.position = TileWorld(e.X, e.Y, mapH) +
                        new Vector3(0, bob, 0);
                    sr.gameObject.SetActive(true);
                }
            }
            // Убираем мёртвых/ушедших
            var dead = new List<Entity>();
            foreach (var kv in _entityPool)
                if (!alive.Contains(kv.Key) || !kv.Key.Alive) dead.Add(kv.Key);
            foreach (var e in dead)
            {
                if (_entityPool.TryGetValue(e, out var sr) && sr != null)
                    UnityEngine.Object.Destroy(sr.gameObject);
                _entityPool.Remove(e);
            }
        }

        private static readonly Dictionary<Color32, Sprite> _rectSprites = new();
        public static Sprite MakeRectSprite(Color32 c, int px)
        {
            // Спрайт на каждый цвет (раньше кэш игнорировал цвет — баг)
            if (_rectSprites.TryGetValue(c, out var sp)) return sp;
            var canvas = new PixelCanvas(px, px);
            canvas.Clear(c);
            sp = Sprite.Create(canvas.ToTexture(),
                new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), 1f);
            _rectSprites[c] = sp;
            return sp;
        }

        private static readonly Dictionary<Texture2D, Sprite> _tileSprites = new();
        private static Sprite SpriteFromTile(Texture2D tex)
        {
            if (_tileSprites.TryGetValue(tex, out var sp)) return sp;
            sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 1f); // 1px = 1 юнит
            _tileSprites[tex] = sp;
            return sp;
        }
    }
}
