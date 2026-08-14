using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.Game
{
    /// <summary>
    /// Makes and caches the sprite objects every part of the view is built from.
    /// </summary>
    /// <remarks>
    /// Shared plumbing, extracted so that <see cref="DungeonView"/> and <see cref="DungeonScenery"/>
    /// can be separate classes without either one duplicating sprite loading or re-loading the same
    /// texture twice. One cache, one root, one place that knows a missing sprite is worth an error
    /// rather than a silent blank.
    /// </remarks>
    public sealed class SpriteWorkshop
    {
        private readonly Transform _root;
        private readonly Dictionary<string, Sprite> _cache = new();
        private Sprite _solid;

        /// <summary>Creates a workshop that parents everything it makes under one object.</summary>
        /// <param name="root">Transform to build under.</param>
        public SpriteWorkshop(Transform root)
        {
            _root = root;
        }

        /// <summary>The transform everything is built under.</summary>
        public Transform Root => _root;

        /// <summary>Creates one sprite object under the view root.</summary>
        /// <param name="name">Object name, which several tests match on.</param>
        /// <param name="sprite">Resources path of the art.</param>
        /// <param name="position">World position.</param>
        /// <param name="sortingOrder">Draw order.</param>
        /// <returns>The renderer, so the caller can keep hold of it.</returns>
        public SpriteRenderer Make(string name, string sprite, Vector3 position, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Load(sprite);
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        /// <summary>Loads a sprite from Resources, caching it.</summary>
        /// <param name="path">Resources path, without extension.</param>
        /// <returns>The sprite, or null when it is missing.</returns>
        public Sprite Load(string path)
        {
            if (_cache.TryGetValue(path, out Sprite cached))
            {
                return cached;
            }

            Sprite loaded = Resources.Load<Sprite>(path);
            if (loaded == null)
            {
                Debug.LogError($"[Dungeon] missing sprite at Resources/{path}");
            }

            _cache[path] = loaded;
            return loaded;
        }

        /// <summary>A single opaque pixel, stretched to draw bars and tile markers.</summary>
        /// <returns>The shared one-pixel sprite, created on first use.</returns>
        public Sprite Solid()
        {
            if (_solid != null)
            {
                return _solid;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            _solid = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
            return _solid;
        }

        /// <summary>Creates a flat coloured quad, used for every bar in the game.</summary>
        /// <param name="name">Object name.</param>
        /// <param name="sortingOrder">Draw order.</param>
        /// <returns>The renderer.</returns>
        public SpriteRenderer MakeBar(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Solid();
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }
    }
}
