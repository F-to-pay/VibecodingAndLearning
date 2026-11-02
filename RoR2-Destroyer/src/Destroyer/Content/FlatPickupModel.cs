using RoR2;
using UnityEngine;

namespace Destroyer.Content
{
    /// <summary>
    /// 2D-моделька предмета на земле: наша иконка на плоском квадрате, всегда лицом к камере (0.14.0).
    ///
    /// Как игра показывает подбираемое (IL PickupDisplay 1.5): берёт PickupDef.displayPrefab (если
    /// displayPrefabReference пустой — проверка на null есть), инстансит, собирает рендереры и ПОДГОНЯЕТ РАЗМЕР ПО ОБЪЁМУ
    /// bounds первого MeshRenderer/SkinnedMeshRenderer (idealVolume / объём). Поэтому:
    ///  * рисуем MeshRenderer, а не SpriteRenderer: подсветка подбора (Highlight) берёт только Mesh/SkinnedMesh;
    ///  * у плоского квадрата объём 0 → ошибка в логе и бесконечный масштаб. В меш добавлены две вершины без
    ///    треугольников (z = ±0.5): невидимы, но bounds становятся кубом 1×1×1 — размер как у ванильных моделек;
    ///  * материал — копия встроенного Sprites-Default (его отдаёт любой новый SpriteRenderer, он есть в любой сборке
    ///    Unity; свой шейдер без AssetBundle не притащить). Без освещения, с прозрачностью, двусторонний.
    /// Префаб живёт под выключенным объектом-держателем (DontDestroyOnLoad): сам он activeSelf = true, поэтому копии,
    /// которые делает игра, сразу активны. Сеть не нужна — модельку каждый клиент создаёт сам.
    /// </summary>
    internal static class FlatPickupModel
    {
        private static GameObject holder;
        private static Material baseMaterial;
        private static Mesh mesh;
        private static bool failed;

        /// <summary>Префаб для иконки или null (тогда остаётся ванильная/одолженная моделька).</summary>
        public static GameObject Create(string name, Sprite icon)
        {
            if (failed || !icon || !icon.texture) return null;
            if (!Init()) return null;

            var root = new GameObject(name + "_FlatPickup");
            root.transform.SetParent(holder.transform, false);

            var card = new GameObject("Card");
            card.transform.SetParent(root.transform, false);
            card.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = card.AddComponent<MeshRenderer>();
            var material = new Material(baseMaterial) { name = name + "_FlatPickupMat", mainTexture = icon.texture };
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            card.AddComponent<FaceCamera>();
            return root;
        }

        private static bool Init()
        {
            if (holder) return true;
            try
            {
                holder = new GameObject("DestroyerFlatPickupPrefabs");
                holder.SetActive(false);
                Object.DontDestroyOnLoad(holder);

                var probe = new GameObject("DestroyerSpriteMaterialProbe");
                var sprite = probe.AddComponent<SpriteRenderer>();
                baseMaterial = sprite.sharedMaterial;
                Object.Destroy(probe);
                if (!baseMaterial)
                {
                    var shader = Shader.Find("Sprites/Default");
                    if (shader) baseMaterial = new Material(shader);
                }
                if (!baseMaterial)
                {
                    DestroyerPlugin.Log.LogWarning("Flat pickup models: Sprites-Default material not found, borrowed 3D models stay.");
                    failed = true;
                    return false;
                }

                mesh = new Mesh { name = "DestroyerFlatPickupQuad" };
                mesh.vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
                    new Vector3(0f, 0f, -0.5f), new Vector3(0f, 0f, 0.5f), // только для bounds (объём 1), в треугольниках нет
                };
                mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f) };
                mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white, Color.white, Color.white };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
                return true;
            }
            catch (System.Exception e)
            {
                DestroyerPlugin.Log.LogError($"Flat pickup models failed to initialize: {e}");
                failed = true;
                return false;
            }
        }
    }

    /// <summary>Поворачивает квадрат лицом к камере игрока (каждый кадр, после вращения подбираемого предмета).</summary>
    internal sealed class FaceCamera : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera cam = null;
            var rigs = CameraRigController.readOnlyInstancesList;
            for (int i = 0; i < rigs.Count && !cam; i++)
                if (rigs[i] && rigs[i].sceneCam) cam = rigs[i].sceneCam;
            if (!cam) cam = Camera.main;
            if (!cam) return;
            Vector3 away = transform.position - cam.transform.position;
            if (away.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(away, cam.transform.up);
        }
    }
}
