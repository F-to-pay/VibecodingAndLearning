using Diabloid.Config;
using RoR2;
using UnityEngine;

namespace Diabloid.Elites
{
    /// <summary>
    /// Метка на мастере титана (только сервер). Нужна, чтобы титан призвал копии ровно один раз,
    /// а сами копии не призывали своих копий — иначе получилась бы цепная реакция до краша.
    /// </summary>
    internal sealed class TitanMarker : MonoBehaviour
    {
        public bool IsCopy;

        /// <summary>Копия титана? Копии ничего не роняют и (по умолчанию) не дают золота и опыта.</summary>
        public static bool IsCopyMaster(CharacterMaster master)
        {
            if (!master) return false;
            var marker = master.GetComponent<TitanMarker>();
            return marker && marker.IsCopy;
        }
    }

    /// <summary>
    /// Копилка неисцеляемого урона, который получает кровопийца (только сервер). Проклятие идёт целыми стаками,
    /// а 10% от мелкого удара — это доли стака: дробную часть копим здесь, чтобы она не терялась.
    /// </summary>
    internal sealed class UnhealableCurseBuffer : MonoBehaviour
    {
        public float PendingStacks;
    }

    /// <summary>
    /// Увеличивает модель титана. Работает на КАЖДОЙ машине сама по себе: бафф титана
    /// синхронизирует игра, а размер мы выставляем локально — никакой лишней сети.
    /// Хитбоксы лежат внутри модели, поэтому растут вместе с ней.
    /// </summary>
    internal sealed class TitanScaler : MonoBehaviour
    {
        private CharacterBody body;
        private Transform model;
        private Vector3 originalScale;
        private float appliedMultiplier;

        private void Awake()
        {
            body = GetComponent<CharacterBody>();
        }

        private void LateUpdate()
        {
            if (!body)
            {
                Destroy(this);
                return;
            }

            bool isTitan = DiabloidElites.Is(body, EliteKind.Titan);
            var modelTransform = body.modelLocator ? body.modelLocator.modelTransform : null;

            if (!isTitan)
            {
                // Аспект отобрали (бывает) — возвращаем как было и уходим.
                if (model) model.localScale = originalScale;
                Destroy(this);
                return;
            }

            if (!modelTransform) return; // модель ещё не появилась — подождём кадр

            if (model != modelTransform)
            {
                model = modelTransform;
                originalScale = model.localScale;
                appliedMultiplier = 0f;
            }

            // Размер берём из конфига каждый кадр — можно крутить ползунком прямо в забеге.
            float multiplier = DiabloidConfig.TitanSizeMultiplier.Value;
            if (!Mathf.Approximately(multiplier, appliedMultiplier))
            {
                model.localScale = originalScale * multiplier;
                appliedMultiplier = multiplier;
            }
        }
    }

    /// <summary>
    /// Полупрозрачная сфера ауры вокруг часовщика. Чисто косметика, у каждого своя.
    /// </summary>
    internal sealed class ClockAuraVisual : MonoBehaviour
    {
        private CharacterBody body;
        private GameObject sphere;

        private void Awake()
        {
            body = GetComponent<CharacterBody>();
        }

        private void Update()
        {
            bool wanted = body && DiabloidElites.Is(body, EliteKind.Clockmaker) &&
                          body.healthComponent && body.healthComponent.alive;

            if (!wanted)
            {
                if (sphere) Destroy(sphere);
                if (!body || !DiabloidElites.Is(body, EliteKind.Clockmaker)) Destroy(this);
                return;
            }

            bool show = DiabloidConfig.ClockmakerShowAura.Value && DiabloidElites.AuraSphereMaterial;
            if (!show)
            {
                if (sphere) sphere.SetActive(false);
                return;
            }

            if (!sphere) sphere = CreateSphere();
            sphere.SetActive(true);
            sphere.transform.position = body.corePosition;
            float diameter = DiabloidConfig.ClockmakerRadius.Value * 2f;
            sphere.transform.localScale = new Vector3(diameter, diameter, diameter);
        }

        private GameObject CreateSphere()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "DiabloidClockAura";
            Destroy(go.GetComponent<Collider>()); // иначе сфера толкала бы всех и ловила пули
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = DiabloidElites.AuraSphereMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        private void OnDestroy()
        {
            if (sphere) Destroy(sphere);
        }
    }
}
