using System;
using Destroyer.Art;
using UnityEngine;

namespace Destroyer.Content
{
    /// <summary>
    /// Иконки предметов, нарисованные кодом: IconCanvas (Art/) → Texture2D → Sprite.
    /// Без AssetBundle и картинок в моде. Рисуется один раз при загрузке, ~десятки мс на иконку.
    /// </summary>
    internal static class IconFactory
    {
        /// <summary>Размер иконки. Workbench рисует PNG того же размера.</summary>
        public const int Size = 256;

        public static Sprite Create(string name, Action<IconCanvas> painter)
        {
            var canvas = IconBook.Render(painter, Size);
            var bytes = canvas.ToRgba32(bottomUp: true); // Unity: первая строка текстуры — нижняя

            var pixels = new Color32[Size * Size];
            for (int k = 0; k < pixels.Length; k++)
                pixels[k] = new Color32(bytes[k * 4], bytes[k * 4 + 1], bytes[k * 4 + 2], bytes[k * 4 + 3]);

            // С мипмапами: в HUD иконка мелкая, без них уменьшенная 256→48 рябит.
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = "tex" + name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            return sprite;
        }
    }
}
