using System;
using Diabloid.Books;
using Diabloid.Config;
using Diabloid.Content;
using Diabloid.Loot;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace Diabloid.UI
{
    /// <summary>
    /// Отладочное меню (IMGUI): выбрать слот и качество — шмотка падает перед игроком; плюс любая книга.
    /// Включается настройкой Debug/DebugMenu, открывается Debug/DebugKey (F3). Пока открыто — виден курсор
    /// (через MPEventSystem.cursorOpenerCount, как у ванильных окон), ввод персонажа игра сама глушит.
    /// Создаёт пикапы только сервер, поэтому работает у хоста (и в одиночке); у клиента — просто надпись.
    /// Статы роллятся при подборе, как у обычного дропа: уровень — текущий уровень монстров.
    /// </summary>
    internal sealed class DebugMenu : MonoBehaviour
    {
        private const int WindowId = 0x0D1AB2;

        /// <summary>Epic вырезан — в меню его нет.</summary>
        private static readonly GearQuality[] MenuQualities =
        {
            GearQuality.Common, GearQuality.Uncommon, GearQuality.Rare,
            GearQuality.Legendary, GearQuality.Lunar, GearQuality.Void,
        };

        private bool visible;
        private Rect windowRect = new Rect(420f, 140f, 0f, 0f);
        private GearSlot slot;
        private GearQuality quality = GearQuality.Legendary;
        private int count = 1;
        private MPEventSystem cursorOwner;
        private GUIStyle buttonStyle;

        private void Update()
        {
            bool allowed = DiabloidConfig.DebugMenuEnabled.Value && Run.instance;
            if (allowed && DiabloidConfig.DebugKey.Value.IsDown()) visible = !visible;
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
            if (buttonStyle == null) buttonStyle = new GUIStyle(GUI.skin.button) { richText = true };

            if (Event.current.type == EventType.Layout)
            {
                windowRect.width = 0f;
                windowRect.height = 0f;
            }
            windowRect = GUILayout.Window(WindowId, windowRect, DrawWindow,
                $"Diabloid debug [{DiabloidConfig.DebugKey.Value}]");
            windowRect.x = Mathf.Clamp(windowRect.x, 0f, Mathf.Max(0f, Screen.width - windowRect.width));
            windowRect.y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, Screen.height - windowRect.height));
        }

        private void DrawWindow(int id)
        {
            var lang = Loc.Current;

            if (!NetworkServer.active)
            {
                GUILayout.Label(Loc.T("Only the host can spawn items.", "Создавать предметы может только хост."));
                GUI.DragWindow();
                return;
            }
            if (!LootCatalog.Ready)
            {
                GUILayout.Label(Loc.T("Catalog is not ready yet.", "Каталог ещё не готов."));
                GUI.DragWindow();
                return;
            }

            GUILayout.Label(Loc.T("Slot", "Слот"));
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Slots.Count; i++)
            {
                var s = (GearSlot)i;
                if (GUILayout.Toggle(slot == s, Slots.Get(s).DisplayName.Get(lang), buttonStyle)) slot = s;
            }
            GUILayout.EndHorizontal();

            GUILayout.Label(Loc.T("Quality", "Качество"));
            GUILayout.BeginHorizontal();
            foreach (var q in MenuQualities)
            {
                var info = Qualities.Get(q);
                string text = $"<color=#{info.ColorHex}>{info.DisplayName(lang)}</color>";
                if (GUILayout.Toggle(quality == q, text, buttonStyle)) quality = q;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("Count", "Сколько") + $": {count}", GUILayout.ExpandWidth(false));
            if (GUILayout.Button("-", GUILayout.Width(30f))) count = Math.Max(1, count - 1);
            if (GUILayout.Button("+", GUILayout.Width(30f))) count = Math.Min(10, count + 1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Loc.T("Spawn", "Создать")))
                for (int i = 0; i < count; i++) Spawn(LootCatalog.GetPickupIndex(slot, quality), i, count);
            if (GUILayout.Button(Loc.T("All 6 slots", "Все 6 слотов")))
                for (int i = 0; i < Slots.Count; i++) Spawn(LootCatalog.GetPickupIndex((GearSlot)i, quality), i, Slots.Count);
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label(Loc.T("Books", "Книги"));
            int column = 0;
            GUILayout.BeginHorizontal();
            foreach (var book in GearBooks.DebugList())
            {
                if (column == 4)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    column = 0;
                }
                if (GUILayout.Button(book.Key.Get(lang))) Spawn(book.Value, 0, 1);
                column++;
            }
            GUILayout.EndHorizontal();

            GUILayout.Label(Loc.T("<size=11>Stats are rolled on pickup, item level = monster level.</size>",
                                  "<size=11>Статы роллятся при подборе, уровень = уровень монстров.</size>"));
            GUI.DragWindow();
        }

        /// <summary>Пикап веером перед игроком: index из total — номер в веере.</summary>
        private static void Spawn(PickupIndex pickup, int index, int total)
        {
            if (!NetworkServer.active || !pickup.isValid) return;
            var user = LocalUserManager.GetFirstLocalUser();
            var master = user != null ? user.cachedMaster : null;
            var body = master ? master.GetBody() : null;
            if (!body) return;

            Vector3 forward = body.inputBank ? body.inputBank.aimDirection : body.gameObject.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            float angle = (index - (total - 1) * 0.5f) * 15f;
            var direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;

            var info = new GenericPickupController.CreatePickupInfo { rotation = Quaternion.identity };
            info.pickup = new UniquePickup(pickup) { ignoreArtifactBasedPickupReplacements = true };
            PickupDropletController.CreatePickupDroplet(info, body.corePosition + Vector3.up, direction * 8f + Vector3.up * 15f);
        }
    }
}
