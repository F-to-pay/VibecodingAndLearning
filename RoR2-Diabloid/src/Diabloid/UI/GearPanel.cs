using System;
using System.Globalization;
using Diabloid.Config;
using Diabloid.Gameplay;
using Diabloid.Loot;
using RoR2;
using UnityEngine;

namespace Diabloid.UI
{
    /// <summary>
    /// Простейшая панель экипировки на IMGUI (OnGUI). Некрасиво, зато без ассетов и префабов.
    /// Показывает экипировку ЛОКАЛЬНОГО игрока: у хоста — из серверных данных, у клиента — из присланной копии.
    ///
    /// Чтобы панель не уезжала за нижний край экрана, она сама выбирает раскладку:
    ///   1 колонка  — всё друг под другом (пока влезает);
    ///   2 колонки  — шмотки | итого;
    ///   3 колонки  — 3 шмотки | 3 шмотки | итого;
    ///   и если даже так не влезает — 3 колонки с прокруткой (колесо мыши, когда курсор виден).
    /// Прокрутка — последний вариант: в бою курсор спрятан, крутить нечем, поэтому сначала колонки.
    /// </summary>
    internal sealed class GearPanel : MonoBehaviour
    {
        private const int WindowId = 0x0D1AB1;

        /// <summary>Отступ от краёв экрана, в пикселях.</summary>
        private const float ScreenMargin = 10f;

        /// <summary>Сколько съедают заголовок окна и его рамки (примерно, с запасом).</summary>
        private const float WindowChrome = 32f;

        private bool visible;
        private Rect windowRect = new Rect(20f, 140f, 0f, 0f); // размер GUILayout подгоняет сам каждый кадр
        private Vector2 scroll;
        private GUIStyle textStyle;
        private int textStyleFontSize;
        private GearSet shownGear;
        private readonly float[] totals = new float[Stats.Count];
        private float soulDamage; // перк "души" (0.14.0): копится в скрытом предмете, не в шмотках

        private enum Layout { OneColumn, TwoColumns, ThreeColumns, ThreeColumnsScrolled }

        private Layout layout;
        private float scrollHeight;

        private void Update()
        {
            if (DiabloidConfig.PanelKey.Value.IsDown())
                visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible || !Run.instance) return;

            var master = LocalUserManager.GetFirstLocalUser()?.cachedMaster;
            if (!master) return;

            shownGear = GearRegistry.Find(master);
            soulDamage = PerkManager.KillDamageOf(master.inventory);
            EnsureStyles();

            Array.Clear(totals, 0, totals.Length);
            shownGear?.AccumulateTotals(totals);

            ChooseLayout();

            // На проходе раскладки обнуляем размер, чтобы окно могло и сжаться (иначе GUILayout.Window только растёт).
            // Только на Layout: на Repaint окно рисуется по размеру, который раскладка только что посчитала.
            if (Event.current.type == EventType.Layout)
            {
                windowRect.width = 0f;
                windowRect.height = 0f;
            }
            windowRect = GUILayout.Window(WindowId, windowRect, DrawWindow,
                $"{Loc.T("Equipment", "Экипировка")} [{DiabloidConfig.PanelKey.Value}]");

            // Перетаскивать окно можно, когда курсор виден (открыт чат или меню паузы). Но за экран не пускаем.
            windowRect.x = Mathf.Clamp(windowRect.x, 0f, Mathf.Max(0f, Screen.width - windowRect.width));
            windowRect.y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, Screen.height - windowRect.height));
        }

        /// <summary>Размер шрифта следует за разрешением (14 px при 1080p) и настройкой масштаба.</summary>
        private void EnsureStyles()
        {
            int fontSize = Mathf.Clamp(
                Mathf.RoundToInt(14f * DiabloidConfig.PanelScale.Value * Screen.height / 1080f), 10, 48);

            if (textStyle != null && textStyleFontSize == fontSize) return;
            textStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = fontSize, wordWrap = false };
            textStyleFontSize = fontSize;
        }

        // ------------------------------------------------------------------
        // Раскладка

        /// <summary>
        /// Высота одной строки вместе с отступами. Отступы соседних строк в GUILayout на деле перекрываются,
        /// так что это оценка с запасом — лучше лишний раз разложить в колонки, чем уехать за экран.
        /// </summary>
        private float LineHeight => textStyle.CalcHeight(new GUIContent("Ag"), 1000f) + textStyle.margin.vertical;

        /// <summary>Сколько строк займёт слот: заголовок + главный стат + аффиксы + перки (пустой слот — одна строка).</summary>
        private int SlotLines(int slot)
        {
            var item = shownGear?.Get((GearSlot)slot);
            return item == null ? 1 : 2 + item.Affixes.Count + item.Perks.Count;
        }

        private int GearLines(int from, int to)
        {
            int lines = 0;
            for (int i = from; i < to; i++) lines += SlotLines(i);
            return lines;
        }

        private int TotalsLines()
        {
            int lines = 1; // заголовок "Итого"
            int stats = 0;
            foreach (float value in totals)
                if (Math.Abs(value) >= 0.0001f) stats++;
            if (shownGear != null && shownGear.BookPerks.Count > 0) lines += 1 + shownGear.BookPerks.Count; // "Книга:" + перки
            if (soulDamage > 0f) lines += 1;
            lines += 1; // сияние (0.19.0)
            return lines + Math.Max(1, stats);
        }

        private void ChooseLayout()
        {
            float available = Screen.height - 2f * ScreenMargin - WindowChrome;
            float line = LineHeight;
            int half = Slots.Count / 2;

            int gear = GearLines(0, Slots.Count);
            int totalsLines = TotalsLines();

            float oneColumn = (gear + totalsLines) * line + 8f;
            float twoColumns = Math.Max(gear, totalsLines) * line;
            float threeColumns = Math.Max(Math.Max(GearLines(0, half), GearLines(half, Slots.Count)), totalsLines) * line;

            if (oneColumn <= available) layout = Layout.OneColumn;
            else if (twoColumns <= available) layout = Layout.TwoColumns;
            else if (threeColumns <= available) layout = Layout.ThreeColumns;
            else
            {
                layout = Layout.ThreeColumnsScrolled;
                scrollHeight = Math.Max(line * 4f, available);
            }
        }

        // ------------------------------------------------------------------
        // Рисование

        private void DrawWindow(int id)
        {
            int half = Slots.Count / 2;

            switch (layout)
            {
                case Layout.OneColumn:
                    DrawGear(0, Slots.Count);
                    GUILayout.Space(8f);
                    DrawTotals();
                    break;

                case Layout.TwoColumns:
                    GUILayout.BeginHorizontal();
                    Column(() => DrawGear(0, Slots.Count));
                    Column(DrawTotals);
                    GUILayout.EndHorizontal();
                    break;

                case Layout.ThreeColumns:
                    DrawThreeColumns(half);
                    break;

                default:
                    scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(scrollHeight));
                    DrawThreeColumns(half);
                    GUILayout.EndScrollView();
                    break;
            }

            GUI.DragWindow();
        }

        private void DrawThreeColumns(int half)
        {
            GUILayout.BeginHorizontal();
            Column(() => DrawGear(0, half));
            Column(() => DrawGear(half, Slots.Count));
            Column(DrawTotals);
            GUILayout.EndHorizontal();
        }

        private void Column(Action draw)
        {
            GUILayout.BeginVertical();
            draw();
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.Space(16f);
        }

        private void DrawGear(int from, int to)
        {
            for (int i = from; i < to; i++)
            {
                var slot = (GearSlot)i;
                var item = shownGear?.Get(slot)?.Current(); // у пустотных — цифры под текущий уровень
                string header = $"<b>{Slots.Get(slot).DisplayName}:</b> "; // Words сам выберет язык

                if (item == null)
                {
                    GUILayout.Label(header + "<color=#666666>" + Loc.T("empty", "пусто") + "</color>", textStyle);
                    continue;
                }

                string score = item.Score.ToString("0.0", CultureInfo.InvariantCulture);
                string level = item.ScalesWithMonsterLevel ? $"{item.Level}↑" : item.Level.ToString();
                int radiance = Qualities.Get(item.Quality).Radiance;
                string glow = radiance > 0 ? $", <color=#FFE680>{Loc.T("radiance", "сияние")} {radiance}</color>" : "";
                GUILayout.Label($"{header}{item.ColoredName} <color=#888888>[{Loc.T("lvl", "ур.")} {level}, {Loc.T("power", "сила")} {score}{glow}]</color>", textStyle);

                // Главный стат — белым, остальные аффиксы — голубым, проклятия — красным, как в Диабло.
                GUILayout.Label("      <color=#FFFFFF>" + item.Implicit.Describe() + "</color>", textStyle);
                foreach (var affix in item.Affixes)
                    GUILayout.Label((affix.IsCurse ? "      <color=#FF5555>" : "      <color=#8FB8FF>") + affix.Describe() + "</color>", textStyle);
                // Перки — золотом, со звёздочкой.
                foreach (var perk in item.Perks)
                    GUILayout.Label("      <color=#FFCC55>★ " + perk.Describe(item.Level) + "</color>", textStyle);
            }
        }

        /// <summary>«Сияние: 17 → враги +34% здоровья, урона, брони и регена, спавн +51%». В коопе — ещё своё.</summary>
        private void DrawRadiance()
        {
            float current = RadianceManager.Current;
            int mine = shownGear != null ? shownGear.Radiance() : 0;
            string value = current.ToString("0.#", CultureInfo.InvariantCulture);
            if (Math.Abs(current - mine) > 0.05f)
                value += $" ({Loc.T("yours", "твоё")} {mine}, {Loc.T("averaged over players", "среднее по игрокам")})";
            string stats = (RadianceManager.EnemyStatBonus * 100f).ToString("0", CultureInfo.InvariantCulture);
            string spawn = (RadianceManager.SpawnBonus * 100f).ToString("0", CultureInfo.InvariantCulture);
            GUILayout.Label("      <color=#FFE680>" + Loc.T("Radiance", "Сияние") + ": " + value + " → " +
                            Loc.T($"enemies +{stats}% health, damage, armor and regen, spawns +{spawn}%",
                                  $"враги +{stats}% здоровья, урона, брони и регена, спавн +{spawn}%") + "</color>", textStyle);
        }

        private void DrawTotals()
        {
            GUILayout.Label("<b>" + Loc.T("Total from gear:", "Итого со шмоток:") + "</b>", textStyle);

            bool any = false;
            for (int s = 0; s < totals.Length; s++)
            {
                if (Math.Abs(totals[s]) < 0.0001f) continue;
                any = true;
                var stat = (StatType)s;
                string line = Stats.Get(stat).Describe(totals[s]);
                if (totals[s] < 0f) line = "<color=#FF5555>" + line + "</color>"; // проклятия перевесили
                if (stat == StatType.CooldownReduction && totals[s] > DiabloidConfig.CooldownReductionCap.Value)
                    line += $" <color=#888888>({Loc.T("cap", "потолок")} {DiabloidConfig.CooldownReductionCap.Value}%)</color>";
                GUILayout.Label("      " + line, textStyle);
            }

            if (!any)
                GUILayout.Label("      <color=#666666>" + Loc.T("nothing yet. Go kill something.", "ничего. Иди убивай.") + "</color>", textStyle);

            DrawRadiance();

            if (soulDamage > 0f)
                GUILayout.Label("      <color=#B48CFF>" + Loc.T("Souls", "Души") + ": +" +
                                soulDamage.ToString("0.##", CultureInfo.InvariantCulture) + " " +
                                Loc.T("base damage", "к базовому урону") + "</color>", textStyle);

            // Перк книги (пока книга в слоте снаряжения).
            if (shownGear != null && shownGear.BookPerks.Count > 0)
            {
                bool scroll = shownGear.BookPerks.Count > 1;
                GUILayout.Label("<b>" + (scroll ? Loc.T("Scroll:", "Свиток:") : Loc.T("Book:", "Книга:")) + "</b>", textStyle);
                foreach (var perk in shownGear.BookPerks)
                    GUILayout.Label("      <color=#FFCC55>★ " + perk.Describe(shownGear.BookPerkLevel) + "</color>", textStyle);
            }
        }
    }
}
