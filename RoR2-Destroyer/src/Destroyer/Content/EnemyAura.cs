using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Destroyer.Content
{
    /// <summary>
    /// Аура-дебафф на врагов вокруг носителей (голова льва, корона). Только сервер.
    ///
    /// Каждый вызов Apply: врагу в радиусе хоть одного источника — стаки баффа = максимум стаков среди накрывших его
    /// источников (обычно это число копий предмета у носителя; сильнейший побеждает, не складываются). Ушёл из всех аур —
    /// стаки в ноль. Снимаем только то, что ставили сами (словарь applied), чужие стаки того же баффа не трогаем.
    /// Смена стаков (SetBuffCount) сама помечает статы грязными и уходит клиентам, сами статы — RecalculateStatsAPI предмета.
    /// </summary>
    internal sealed class EnemyAura
    {
        public struct Source
        {
            public CharacterBody Holder;
            public float Radius;
            public int Stacks;
        }

        private readonly BuffDef buff;
        private readonly Dictionary<CharacterBody, int> applied = new Dictionary<CharacterBody, int>();
        private readonly Dictionary<CharacterBody, int> wanted = new Dictionary<CharacterBody, int>();
        private readonly List<CharacterBody> scratch = new List<CharacterBody>();

        public EnemyAura(BuffDef buff) { this.buff = buff; }

        public void Apply(List<Source> sources)
        {
            if (!NetworkServer.active || buff == null) return;
            wanted.Clear();
            foreach (var source in sources)
            {
                if (!source.Holder || source.Stacks <= 0) continue;
                Vector3 center = source.Holder.corePosition;
                float r = source.Radius;
                foreach (var enemy in CharacterBody.readOnlyInstancesList)
                {
                    if (!Geometry.AreEnemies(source.Holder, enemy) || !enemy.healthComponent || !enemy.healthComponent.alive) continue;
                    // До поверхности тела, а не до центра: босса задеваешь краем ауры.
                    if ((enemy.corePosition - center).magnitude - enemy.bestFitRadius > r) continue;
                    if (!wanted.TryGetValue(enemy, out int have) || have < source.Stacks) wanted[enemy] = source.Stacks;
                }
            }

            // Ставим нужное.
            foreach (var pair in wanted)
            {
                if (applied.TryGetValue(pair.Key, out int had) && had == pair.Value) continue;
                pair.Key.SetBuffCount(buff.buffIndex, pair.Value);
                applied[pair.Key] = pair.Value;
            }

            // Снимаем ушедшим.
            scratch.Clear();
            foreach (var pair in applied)
                if (!wanted.ContainsKey(pair.Key)) scratch.Add(pair.Key);
            foreach (var body in scratch)
            {
                if (body) body.SetBuffCount(buff.buffIndex, 0);
                applied.Remove(body);
            }
        }

        public void Clear()
        {
            foreach (var pair in applied)
                if (pair.Key && NetworkServer.active) pair.Key.SetBuffCount(buff.buffIndex, 0);
            applied.Clear();
        }
    }
}
