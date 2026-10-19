using Diabloid.Network;
using RoR2;
using UnityEngine.Networking;

namespace Diabloid.Gameplay
{
    /// <summary>Сброс экипировки между забегами и пересинхронизация на новых этапах.</summary>
    internal static class GearLifecycle
    {
        public static void Init()
        {
            Run.onRunStartGlobal += _ => GearRegistry.Clear();
            Run.onRunDestroyGlobal += _ => GearRegistry.Clear();
            CharacterBody.onBodyStartGlobal += OnBodyStart;
        }

        /// <summary>
        /// Тело игрока появилось (новый этап, респавн, кто-то зашёл посреди забега) —
        /// на всякий случай рассылаем всем экипировку всех. Это пара сотен байт, не жалко.
        /// </summary>
        private static void OnBodyStart(CharacterBody body)
        {
            if (!NetworkServer.active || !body) return;
            var master = body.master;
            if (!master || !master.playerCharacterMasterController) return;
            GearNetwork.BroadcastAll();
        }
    }
}
