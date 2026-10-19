using System.Text;
using Diabloid.Config;
using Diabloid.Loot;
using RoR2;

namespace Diabloid.Gameplay
{
    /// <summary>
    /// Сообщение "кто что надел" в ЛОКАЛЬНЫЙ чат, на языке ЭТОЙ машины.
    ///
    /// Почему не обычный сетевой чат: Chat.SendBroadcastChat собирает текст на сервере,
    /// и у клиента-англичанина в чате был бы русский текст хоста. Поэтому сервер шлёт только
    /// цифры (GearSyncMessage с пометкой "вот этот слот только что сменился"), а текст каждая
    /// машина собирает сама: хост — сразу в GearPickup.Grant, клиенты — при получении сообщения.
    /// </summary>
    internal static class GearAnnouncer
    {
        public static void Announce(CharacterMaster master, GearItem item, GearItem previous)
        {
            if (item == null || !DiabloidConfig.AnnounceInChat.Value) return;
            item = item.Current(); // у пустотных — цифры под текущий уровень

            string who = master ? Util.GetBestMasterName(master) : "?";

            var sb = new StringBuilder();
            sb.Append("<color=#AAAAAA>").Append(who).Append(' ').Append(Loc.T("equips", "надевает")).Append("</color> ")
              .Append(item.ColoredName)
              .Append(" <color=#888888>[").Append(Loc.T("lvl", "ур.")).Append(' ').Append(item.Level).Append("]</color>");

            foreach (var affix in item.AllAffixes)
            {
                sb.Append("\n   ");
                if (affix.IsCurse) sb.Append("<color=#FF5555>").Append(affix.Describe()).Append("</color>");
                else sb.Append(affix.Describe());
            }
            foreach (var perk in item.Perks)
                sb.Append("\n   <color=#FFCC55>★ ").Append(perk.Describe(item.Level)).Append("</color>");

            if (previous != null)
                sb.Append("\n<color=#888888>").Append(Loc.T("Dropped:", "Брошено на землю:")).Append("</color> ").Append(previous.ColoredName);

            // AddMessage кладёт строку в чат как есть, без string.Format — фигурные скобки в нике не страшны.
            Chat.AddMessage(sb.ToString());
        }
    }
}
