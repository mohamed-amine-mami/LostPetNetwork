using System.Collections.Generic;
using LostPetNetwork.Services.Pets;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>Renders a small picture of a 3D pet for lists, using a hidden 3D stage. Cached by the pet's settings.</summary>
    public static class PetPortrait
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static async Awaitable<Sprite> RenderAsync(PetConfig config)
        {
            string key = config.ToJson();
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var host = new GameObject("PetPortrait", typeof(RectTransform), typeof(RawImage)) { hideFlags = HideFlags.DontSave };
            try
            {
                var preview = host.AddComponent<Preview3D>();
                preview.DefaultYaw = PetFactory.DefaultYaw;
                preview.ResetView();
                preview.SetContent(PetFactory.Build(config), PetFactory.FrameCenter, PetFactory.FrameRadius);
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
