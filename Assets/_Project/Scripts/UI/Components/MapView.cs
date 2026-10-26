using System;
using System.Collections;
using System.Collections.Generic;
using LostPetNetwork.Services.Map;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>Downloads and keeps Mapbox tiles in memory (small least-recently-used cache, limited parallel downloads).</summary>
    public static class MapTileLoader
    {
        const int MaxTextures = 64;
        const int MaxParallel = 6;

        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        static readonly LinkedList<string> Recent = new LinkedList<string>();
        static int _active;

        public static int Active => _active;

        public static bool TryGet(string key, out Texture2D texture)
        {
            if (Cache.TryGetValue(key, out texture) && texture != null)
            {
                Recent.Remove(key);
                Recent.AddFirst(key);
                return true;
            }
            texture = null;
            return false;
        }

        public static IEnumerator Load(string key, string url, Action<Texture2D> done)
        {
            while (_active >= MaxParallel) yield return null;
            _active++;
            Texture2D texture = null;
            try
            {
                using (var request = UnityWebRequestTexture.GetTexture(url, true))
                {
                    request.timeout = 12;
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        texture = DownloadHandlerTexture.GetContent(request);
                        texture.wrapMode = TextureWrapMode.Clamp;
                    }
                }
            }
            finally
            {
                _active--;
            }
            if (texture != null) Store(key, texture);
            done?.Invoke(texture);
        }

        static void Store(string key, Texture2D texture)
        {
            if (Cache.TryGetValue(key, out var old) && old != null) UnityEngine.Object.Destroy(old);
            Cache[key] = texture;
            Recent.Remove(key);
            Recent.AddFirst(key);
            while (Recent.Count > MaxTextures)
            {
                string oldest = Recent.Last.Value;
                Recent.RemoveLast();
                if (Cache.TryGetValue(oldest, out var tex) && tex != null) UnityEngine.Object.Destroy(tex);
                Cache.Remove(oldest);
            }
        }
    }

    /// <summary>
    /// A pannable, zoomable street map made of Mapbox raster tiles. Pins are not drawn here: other components
    /// ask for <see cref="WorldToLocal"/> and place themselves, and listen to <see cref="ViewChanged"/>.
    /// </summary>
    public class MapView : MonoBehaviour, IDragHandler, IScrollHandler
    {
        public const int MinZoom = 3;
        public const int MaxZoom = 18;

        [SerializeField] RectTransform tileLayer;
        [SerializeField] int startZoom = 13;
        [SerializeField] double startLatitude = 36.8065;
        [SerializeField] double startLongitude = 10.1815;

        readonly Dictionary<long, RawImage> _tiles = new Dictionary<long, RawImage>();
        readonly Stack<RawImage> _pool = new Stack<RawImage>();
        readonly List<long> _scratch = new List<long>();

        double _centerX, _centerY;
        Vector2 _lastSize;
        bool _dirty = true;
        float _lastPinch;
        float _pinchBase;
        float _nextScrollZoom;

        public int Zoom { get; private set; }
        public double CenterLatitude => WebMercator.YToLat(_centerY, Zoom);
        public double CenterLongitude => WebMercator.XToLon(_centerX, Zoom);
        public int LoadedTileCount => _tiles.Count;
        public event Action ViewChanged;

        void Awake()
        {
            Zoom = Mathf.Clamp(startZoom, MinZoom, MaxZoom);
            SetView(startLatitude, startLongitude, Zoom);
        }

        void OnEnable() => _dirty = true;

        void Update()
        {
            var size = ((RectTransform)transform).rect.size;
            if (size != _lastSize) { _lastSize = size; _dirty = true; }
            HandlePinch();
            if (!_dirty) return;
            _dirty = false;
            Refresh();
            ViewChanged?.Invoke();
        }

        public void SetView(double lat, double lon, int zoom)
        {
            Zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            _centerX = WebMercator.LonToX(lon, Zoom);
            _centerY = WebMercator.LatToY(lat, Zoom);
            _dirty = true;
        }

        public void ZoomBy(int delta)
        {
            int next = Mathf.Clamp(Zoom + delta, MinZoom, MaxZoom);
            if (next == Zoom) return;
            SetView(CenterLatitude, CenterLongitude, next);
        }

        /// <summary>Centres and zooms so every given point is visible.</summary>
        public void FitTo(IList<(double lat, double lon)> points)
        {
            if (points == null || points.Count == 0) return;
            double minLat = 90, maxLat = -90, minLon = 180, maxLon = -180;
            foreach (var p in points)
            {
                minLat = Math.Min(minLat, p.lat); maxLat = Math.Max(maxLat, p.lat);
                minLon = Math.Min(minLon, p.lon); maxLon = Math.Max(maxLon, p.lon);
            }
            var size = ((RectTransform)transform).rect.size;
            if (size.x < 10) size = new Vector2(390, 700);
            int zoom = points.Count == 1 ? 15 : WebMercator.ZoomToFit(minLat, maxLat, minLon, maxLon, size.x, size.y, 90, MinZoom, 16);
            SetView((minLat + maxLat) / 2, (minLon + maxLon) / 2, zoom);
        }

        /// <summary>Position of a place relative to the middle of the map view, in UI units.</summary>
        public Vector2 WorldToLocal(double lat, double lon)
        {
            double x = WebMercator.LonToX(lon, Zoom) - _centerX;
            double y = WebMercator.LatToY(lat, Zoom) - _centerY;
            return new Vector2((float)x, (float)-y);
        }

        public void OnDrag(PointerEventData eventData)
        {
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position - eventData.delta, eventData.pressEventCamera, out var from)) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out var to)) return;
            var move = to - from;
            _centerX -= move.x;
            _centerY += move.y;
            _dirty = true;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (Time.unscaledTime < _nextScrollZoom || Mathf.Approximately(eventData.scrollDelta.y, 0f)) return;
            _nextScrollZoom = Time.unscaledTime + 0.25f;
            ZoomBy(eventData.scrollDelta.y > 0 ? 1 : -1);
        }

        void HandlePinch()
        {
            var screen = Touchscreen.current;
            if (screen == null) return;
            var a = screen.touches[0];
            var b = screen.touches[1];
            if (!a.isInProgress || !b.isInProgress) { _lastPinch = 0f; return; }
            float distance = Vector2.Distance(a.position.ReadValue(), b.position.ReadValue());
            if (_lastPinch <= 0f) { _pinchBase = distance; _lastPinch = distance; return; }
            _lastPinch = distance;
            if (distance > _pinchBase * 1.5f) { ZoomBy(1); _pinchBase = distance; }
            else if (distance < _pinchBase / 1.5f) { ZoomBy(-1); _pinchBase = distance; }
        }

        static long Key(int z, int x, int y) => ((long)z << 44) | ((long)x << 22) | (uint)y;

        void Refresh()
        {
            if (tileLayer == null) return;
            var size = ((RectTransform)transform).rect.size;
            int tiles = 1 << Zoom;
            int x0 = Mathf.FloorToInt((float)((_centerX - size.x / 2) / WebMercator.TileSize)) - 1;
            int x1 = Mathf.FloorToInt((float)((_centerX + size.x / 2) / WebMercator.TileSize)) + 1;
            int y0 = Mathf.Max(0, Mathf.FloorToInt((float)((_centerY - size.y / 2) / WebMercator.TileSize)) - 1);
            int y1 = Mathf.Min(tiles - 1, Mathf.FloorToInt((float)((_centerY + size.y / 2) / WebMercator.TileSize)) + 1);

            _scratch.Clear();
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int wrappedX = ((x % tiles) + tiles) % tiles;
                long key = Key(Zoom, x, y);
                _scratch.Add(key);
                if (!_tiles.TryGetValue(key, out var image))
                {
                    image = Rent();
                    _tiles[key] = image;
                    image.texture = null;
                    image.color = new Color(0, 0, 0, 0);
                    LoadTile(key, image, Zoom, wrappedX, y);
                }
                var rt = image.rectTransform;
                rt.anchoredPosition = new Vector2((float)(x * WebMercator.TileSize - _centerX), (float)-(y * WebMercator.TileSize - _centerY));
            }

            var stale = new List<long>();
            foreach (var key in _tiles.Keys)
                if (!_scratch.Contains(key)) stale.Add(key);
            foreach (var key in stale)
            {
                Return(_tiles[key]);
                _tiles.Remove(key);
            }
        }

        void LoadTile(long key, RawImage image, int z, int x, int y)
        {
            string cacheKey = z + "/" + x + "/" + y;
            if (MapTileLoader.TryGet(cacheKey, out var cached))
            {
                Apply(image, cached);
                return;
            }
            if (!MapConfig.IsConfigured) return;
            // Downloads run on a persistent runner so closing the map never leaves the shared counter stuck.
            DeviceSensorRunner.Instance.StartCoroutine(MapTileLoader.Load(cacheKey, MapboxApi.TileUrl(MapConfig.MapboxToken, z, x, y), texture =>
            {
                // The tile may have scrolled away (or this view been disabled) while it downloaded.
                if (this == null || texture == null) return;
                if (_tiles.TryGetValue(key, out var current) && current == image) Apply(image, texture);
            }));
        }

        static void Apply(RawImage image, Texture2D texture)
        {
            image.texture = texture;
            image.color = Color.white;
        }

        RawImage Rent()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }
            var go = new GameObject("Tile", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(tileLayer, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(WebMercator.TileSize, WebMercator.TileSize);
            var image = go.GetComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        void Return(RawImage image)
        {
            image.texture = null;
            image.gameObject.SetActive(false);
            _pool.Push(image);
        }
    }
}
