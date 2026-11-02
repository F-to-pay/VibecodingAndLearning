using System.Collections.Generic;
using CharacterTest.Config;
using EntityStates;
using UnityEngine;

namespace CharacterTest.Content
{
    /// <summary>
    /// Основа состояний навыков мода: BaseSkillState + лог входа (Debug → VerboseLog) + безопасные анимации.
    /// Напоминание, где что выполняется:
    ///   OnEnter/FixedUpdate/OnExit идут на ВСЕХ машинах, где живёт тело (машина состояний синхронизируется);
    ///   isAuthority — машина, которая управляет телом (игрок у себя / сервер для ИИ): атаки, движение, смена состояний;
    ///   NetworkServer.active — сервер: баффы, спавн, всё "настоящее".
    /// </summary>
    public abstract class ModSkillState : BaseSkillState
    {
        // Чего нет в аниматоре — пишем в лог один раз на пару "слой/состояние", дальше молча пропускаем.
        private static readonly HashSet<string> reportedMissing = new HashSet<string>();

        public override void OnEnter()
        {
            base.OnEnter();
            if (CharacterTestConfig.VerboseLog != null && CharacterTestConfig.VerboseLog.Value)
                CharacterTestPlugin.Log.LogInfo($"[state] {GetType().Name} enter on {(characterBody ? characterBody.name : "?")} (authority={isAuthority})");
        }

        /// <summary>
        /// Первая пара (слой, состояние), которая нашлась в аниматоре донора. Если слоя с таким именем нет, но состояние
        /// есть на другом слое — играем там (ванила так и живёт: у Коммандо 1.5 нет слоя "Gesture Additive, Left",
        /// FirePistol2 отдаёт Unity индекс −1, и Unity сама ищет состояние по всем слоям). Что нашлось где —
        /// один раз в лог ([anim]), вместе со списком настоящих слоёв. Ничего не нашлось — false и молчим.
        /// Скорость: playbackRateParam (float в аниматоре) = длина клипа / duration, как в ванильном PlayAnimation.
        /// </summary>
        protected bool PlayAnimationSafe(string playbackRateParam, float duration, params (string layer, string state)[] candidates)
        {
            var animator = GetModelAnimator();
            if (!animator) return false;

            foreach (var (layer, state) in candidates)
            {
                int stateHash = Animator.StringToHash(state);
                int asked = animator.GetLayerIndex(layer);
                int found = asked >= 0 && animator.HasState(asked, stateHash) ? asked : -1;
                if (found < 0)
                    for (int i = 0; i < animator.layerCount; i++)
                        if (animator.HasState(i, stateHash)) { found = i; break; }

                string key = $"{animator.name}|{layer}|{state}";
                if (found != asked && reportedMissing.Add(key))
                {
                    var names = new List<string>();
                    for (int i = 0; i < animator.layerCount; i++) names.Add(animator.GetLayerName(i));
                    CharacterTestPlugin.Log.LogWarning(
                        $"[anim] {GetType().Name}: '{state}' asked on '{layer}' (index {asked}), " +
                        (found >= 0 ? $"found on '{animator.GetLayerName(found)}' (index {found})" : "not found on any layer") +
                        $". Layers of {animator.name}: {string.Join(" | ", names)}");
                }

                if (found < 0) continue;
                Play(animator, found, stateHash, playbackRateParam, duration);
                return true;
            }
            return false;
        }

        private static void Play(Animator animator, int layer, int stateHash, string rateParam, float duration)
        {
            bool hasRate = !string.IsNullOrEmpty(rateParam) && duration > 0f;
            animator.speed = 1f;
            animator.Update(0f);
            if (hasRate) animator.SetFloat(rateParam, 1f);
            animator.PlayInFixedTime(stateHash, layer, 0f);
            animator.Update(0f);
            if (hasRate)
            {
                float length = animator.GetCurrentAnimatorStateInfo(layer).length;
                animator.SetFloat(rateParam, length / duration);
            }
        }

        protected bool PlayAnimationSafe(string layer, string state) => PlayAnimationSafe(null, 0f, (layer, state));
    }
}
