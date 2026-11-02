using System;
using Destroyer.Config;
using Destroyer.Content;
using Destroyer.Core;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace Destroyer.UI
{
    /// <summary>
    /// Отладочное меню (IMGUI): все предметы мода — выдать себе, забрать, бросить на землю.
    /// Включается настройкой Debug/DebugMenu, открывается Debug/DebugKey (F3). Пока открыто — виден курсор.
    /// Менять инвентарь может только сервер, поэтому работает у хоста и в одиночке.
    /// (Каркас взят из отладочного меню Diabloid, проверенного в игре.)
    /// </summary>
    internal sealed class DebugMenu : MonoBehaviour
    {
        private const int WindowId = 0xDE57;

        private bool visible;
        private Rect windowRect = new Rect(420f, 140f, 0f, 0f);
        private Vector2 scroll;
        private MPEventSystem cursorOwner;
        private GUIStyle labelStyle;

        private void Update()
        {
            bool allowed = DestroyerConfig.DebugMenuEnabled.Value && Run.instance;
            if (allowed && DestroyerConfig.DebugKey.Value.IsDown()) visible = !visible;
            if (!allowed) visible = false;
            SetCursor(visible);
        }

        private void OnDisable() => SetCursor(false);

        private void SetCursor(bool open)
        {
            if (open && !cursorOwner)
            {
                var user = LocalUserManager.GetFirstLocalUser();
                var eventSystem = user != null ? user.eventSystem : null;
                if (!eventSystem) return;
                eventSystem.cursorOpenerCount++;
                cursorOwner = eventSystem;
            }
            else if (!open && cursorOwner)
            {
                cursorOwner.cursorOpenerCount = Math.Max(0, cursorOwner.cursorOpenerCount - 1);
                cursorOwner = null;
            }
        }

        private void OnGUI()
        {
            if (!visible || !Run.instance) return;
            if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label) { richText = true };

            if (Event.current.type == EventType.Layout)
            {
                windowRect.width = 0f;
                windowRect.height = 0f;
            }
            windowRect = GUILayout.Window(WindowId, windowRect, DrawWindow,
                $"{DestroyerPlugin.PluginName} debug [{DestroyerConfig.DebugKey.Value}]");
            windowRect.x = Mathf.Clamp(windowRect.x, 0f, Mathf.Max(0f, Screen.width - windowRect.width));
            windowRect.y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, Screen.height - windowRect.height));
        }

        private void DrawWindow(int id)
        {
            if (!NetworkServer.active)
            {
                GUILayout.Label(Loc.T("Only the host can change inventories.", "Менять инвентарь может только хост."));
                GUI.DragWindow();
                return;
            }
            if (!ItemRegistry.Ready)
            {
                GUILayout.Label(Loc.T("Catalog is not ready yet.", "Каталог ещё не готов."));
                GUI.DragWindow();
                return;
            }

            var master = LocalMaster();
            var inventory = master ? master.inventory : null;
            if (!inventory)
            {
                GUILayout.Label(Loc.T("No local player.", "Нет локального игрока."));
                GUI.DragWindow();
                return;
            }

            if (ItemRegistry.All.Count == 0)
            {
                GUILayout.Label(Loc.T("The mod has no items yet (or all are disabled).",
                                      "Предметов в моде пока нет (или все выключены)."));
                GUI.DragWindow();
                return;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Loc.T("All +1", "Все +1"))) foreach (var item in ItemRegistry.All) inventory.GiveItemPermanent(item.Def, 1);
            if (GUILayout.Button(Loc.T("Remove all", "Забрать все")))
                foreach (var item in ItemRegistry.All)
                {
                    int have = inventory.GetItemCountPermanent(item.Def);
                    if (have > 0) inventory.RemoveItemPermanent(item.Def, have);
                }
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            float height = Mathf.Min(ItemRegistry.All.Count * 28f + 8f, Screen.height * 0.6f);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(height), GUILayout.MinWidth(460f));
            foreach (var item in ItemRegistry.All)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<color=#{ColorHex(item)}>{Language.GetString(item.Def.nameToken)}</color>  ×{item.Count(inventory)}",
                                labelStyle, GUILayout.Width(250f));
                if (GUILayout.Button("+1", GUILayout.Width(36f))) inventory.GiveItemPermanent(item.Def, 1);
                if (GUILayout.Button("+10", GUILayout.Width(40f))) inventory.GiveItemPermanent(item.Def, 10);
                if (GUILayout.Button("-1", GUILayout.Width(36f)) && inventory.GetItemCountPermanent(item.Def) > 0)
                    inventory.RemoveItemPermanent(item.Def, 1);
                if (GUILayout.Button(Loc.T("Drop", "Бросить"), GUILayout.Width(70f))) Drop(master, item.PickupIndex);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.Label(Loc.T("<size=11>Effective count (permanent + temporary). −1 takes a permanent copy.</size>",
                                  "<size=11>Число — действующие копии (постоянные + временные). −1 забирает постоянную.</size>"), labelStyle);
            GUI.DragWindow();
        }

        private static CharacterMaster LocalMaster()
        {
            var user = LocalUserManager.GetFirstLocalUser();
            return user != null ? user.cachedMaster : null;
        }

        private static string ColorHex(ItemBase item)
        {
            var pickup = item.PickupIndex.isValid ? PickupCatalog.GetPickupDef(item.PickupIndex) : null;
            return pickup != null ? ColorUtility.ToHtmlStringRGB(pickup.baseColor) : "ffffff";
        }

        /// <summary>Капля предмета перед игроком — проверить модельку на земле и подбор.</summary>
        private static void Drop(CharacterMaster master, PickupIndex pickup)
        {
            if (!NetworkServer.active || !pickup.isValid) return;
            var body = master ? master.GetBody() : null;
            if (!body) return;

            Vector3 forward = body.inputBank ? body.inputBank.aimDirection : body.gameObject.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            var info = new GenericPickupController.CreatePickupInfo { rotation = Quaternion.identity };
            info.pickup = new UniquePickup(pickup);
            PickupDropletController.CreatePickupDroplet(info, body.corePosition + Vector3.up, forward * 8f + Vector3.up * 15f);
        }
    }
}
