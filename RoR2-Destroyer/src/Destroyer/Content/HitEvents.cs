using System;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Destroyer.Content
{
    /// <summary>
    /// "При попадании" как у ванильных предметов: один On-хук на GlobalEventManager.OnHitEnemy на весь мод,
    /// предметы подписываются на событие OnHit.
    ///
    /// Почему OnHitEnemy, а не onServerDamageDealt: ванильные on-hit предметы живут именно там. Атака, которая
    /// не должна ничего прокать (DoT, урон предметов с proc 0), его не зовёт или зовёт с procCoefficient 0.
    /// damageInfo.damage тут — "TOTAL damage" ванильных описаний (до брони, без множителя крита; крит — флаг crit).
    ///
    /// Фильтр как у ванили (ProcessHitEnemy, IL 1.5): только сервер, procCoefficient &gt; 0, урон не отклонён.
    /// Плюс есть тело атакующего и тело жертвы.
    /// </summary>
    internal static class HitEvents
    {
        public delegate void HitHandler(DamageInfo info, CharacterBody attacker, CharacterBody victim);

        private static HitHandler handlers;
        private static bool installed;

        public static event HitHandler OnHit
        {
            add
            {
                Install();
                handlers += value;
            }
            remove => handlers -= value;
        }

        private static void Install()
        {
            if (installed) return;
            installed = true;
            On.RoR2.GlobalEventManager.OnHitEnemy += OnHitEnemy;
        }

        private static void OnHitEnemy(On.RoR2.GlobalEventManager.orig_OnHitEnemy orig, GlobalEventManager self,
                                       DamageInfo info, GameObject victim)
        {
            orig(self, info, victim); // сначала ванильные проки

            if (handlers == null || !NetworkServer.active) return;
            if (info == null || info.rejected || info.procCoefficient <= 0f || !info.attacker || !victim) return;

            var attackerBody = info.attacker.GetComponent<CharacterBody>();
            var victimBody = victim.GetComponent<CharacterBody>();
            if (!attackerBody || !victimBody) return;

            // Каждый предмет отдельно: исключение в одном не должно глушить остальные.
            foreach (HitHandler handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(info, attackerBody, victimBody);
                }
                catch (Exception e)
                {
                    DestroyerPlugin.Log.LogError($"On-hit handler {handler.Method.DeclaringType?.Name}.{handler.Method.Name} failed: {e}");
                }
            }
        }
    }
}
