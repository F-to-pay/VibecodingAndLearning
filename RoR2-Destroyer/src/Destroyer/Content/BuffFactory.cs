using System;
using Destroyer.Art;
using R2API;
using RoR2;
using UnityEngine;

namespace Destroyer.Content
{
    /// <summary>
    /// Свои баффы/дебаффы. Создавать в Awake (в Hook предмета), до сборки каталогов.
    /// Иконка — белый силуэт из IconBook.Buffs: игра сама красит её в buffColor.
    /// </summary>
    internal static class BuffFactory
    {
        /// <param name="id">Как Id предмета: "TASTY" → BuffDef.name "bdDESTROYER_TASTY".</param>
        public static BuffDef Create(string id, Color color, bool isDebuff, bool canStack)
        {
            var def = ScriptableObject.CreateInstance<BuffDef>();
            def.name = "bd" + ItemBase.TokenPrefix + id;
            def.buffColor = color;
            def.isDebuff = isDebuff;
            def.canStack = canStack;
            def.isHidden = false;
            def.isCooldown = false;
            def.iconSprite = IconFactory.Create(def.name, IconBook.GetBuff(id));

            if (!ContentAddition.AddBuffDef(def))
                DestroyerPlugin.Log.LogError($"Buff {def.name} was not added (duplicate name?).");
            return def;
        }

        /// <summary>Цвет из "#rrggbb".</summary>
        public static Color Hex(string hex)
        {
            var c = Rgba.Hex(hex);
            return new Color(c.R, c.G, c.B, c.A);
        }
    }
}
