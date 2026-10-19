using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>
    /// Shows a sprite scaled to cover its (masked) parent without distortion, like CSS object-fit: cover.
    /// Requires an AspectRatioFitter in EnvelopeParent mode on the same object.
    /// </summary>
    [RequireComponent(typeof(Image), typeof(AspectRatioFitter))]
    public class CoverImage : MonoBehaviour
    {
        Image _image;
        AspectRatioFitter _fitter;

        public bool HasSprite => Image.sprite != null;

        Image Image => _image != null ? _image : (_image = GetComponent<Image>());
        AspectRatioFitter Fitter => _fitter != null ? _fitter : (_fitter = GetComponent<AspectRatioFitter>());

        public void Set(Sprite sprite)
        {
            Image.sprite = sprite;
            Image.enabled = sprite != null;
            if (sprite != null && sprite.rect.height > 0)
                Fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
        }
    }
}
