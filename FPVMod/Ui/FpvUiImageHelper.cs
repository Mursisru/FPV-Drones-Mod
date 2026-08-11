using UnityEngine;
using UnityEngine.UI;

namespace FPVMod.Ui
{
    /// <summary>Solid white sprite for UI Image fills (ported from MC UiImageHelper).</summary>
    internal static class FpvUiImageHelper
    {
        private static Sprite? _white;

        internal static void ApplySolid(Image image, Color color)
        {
            if (image == null)
                return;
            image.sprite = WhiteSprite();
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
        }

        private static Sprite WhiteSprite()
        {
            if (_white != null)
                return _white;
            Texture2D tex = Texture2D.whiteTexture;
            _white = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return _white;
        }
    }
}
