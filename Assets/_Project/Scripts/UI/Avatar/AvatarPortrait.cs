using System.Collections.Generic;
using LostPetNetwork.Services.Avatar;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>
    /// Renders a head-and-shoulders picture of an avatar for lists and the profile header, using a hidden
    /// 3D stage. Results are cached by avatar settings, so each look is rendered once.
    /// </summary>
    public static class AvatarPortrait
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static async Awaitable<Sprite> RenderAsync(AvatarConfig config)
        {
            string key = config.ToJson();
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var host = new GameObject("AvatarPortrait", typeof(RectTransform), typeof(RawImage)) { hideFlags = HideFlags.DontSave };
            try
            {
                var preview = host.AddComponent<Preview3D>();
                preview.SetPitch(0f);
                preview.SetContent(AvatarFactory.Build(config), AvatarFactory.PortraitCenter, AvatarFactory.PortraitRadius);
                await Awaitable.NextFrameAsync();
                await Awaitable.NextFrameAsync();

                var texture = preview.Capture();
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                Cache[key] = sprite;
                return sprite;
            }
            finally
            {
                Object.Destroy(host);
            }
        }
    }
}
