﻿using System;
using System.Collections.Generic;
using CedMod.INIT;
using GameCore;
using MEC;
using UnityEngine;
using Random = System.Random;

namespace CedMod.Handlers
{
    using Exiled.API.Features;
    using Exiled.Events.EventArgs;

    /// <summary>
    /// Handles server-related events.
    /// </summary>
    public class Player
    {
        public void OnJoin(JoinedEventArgs ev)
        {
            Timing.RunCoroutine(BanSystem.HandleJoin(ev));
            foreach (string b in ConfigFile.ServerConfig.GetStringList("cm_nicknamefilter"))
            {
                if (ev.Player.Nickname.ToUpper().Contains(b.ToUpper()))
                {
                    ev.Player.ReferenceHub.nicknameSync.DisplayName = "Filtered name";
                }
            }
            if (!RoundSummary.RoundInProgress() && ConfigFile.ServerConfig.GetBool("cm_customloadingscreen", true))
                Timing.RunCoroutine(Playerjoinhandle(ev));
        }
        public static ItemType GetRandomItem()
        {
            Random random = new Random();
            int index = UnityEngine.Random.Range(0, CedModMain.items.Count);
            return CedModMain.items[index];
        }
        public IEnumerator<float> Playerjoinhandle(JoinedEventArgs ev)
        {
            ReferenceHub Player = ev.Player.ReferenceHub;
            yield return Timing.WaitForSeconds(0.5f);
            if (!RoundSummary.RoundInProgress())
            {
                Player.characterClassManager.SetPlayersClass(RoleType.Tutorial, Player.gameObject);
                ev.Player.IsGodModeEnabled = false;
                ItemType item = GetRandomItem();
                ev.Player.Inventory.AddNewItem(item);
                yield return Timing.WaitForSeconds(0.2f);
                ev.Player.Position = (new Vector3(-20f, 1020, -43));
            }
            yield return 1f;
        }
        public void OnLeave(LeftEventArgs ev)
        {
        }

        public void OnDying(DyingEventArgs ev)
        {
            FriendlyFireAutoban.HandleKill(ev);
            Timing.RunCoroutine(PlayerStatsDieEv(ev));
        }

        IEnumerator<float> PlayerStatsDieEv(DyingEventArgs ev)
        {
            if (!RoundSummary.RoundInProgress())
                yield return 0f;
            if (FriendlyFireAutoban.IsTeakill(ev))
                API.APIRequest("playerstats/addstat.php",
                    $"?rounds=0&kills=0&deaths=0&teamkills=1&alias={API.GetAlias()}&id={ev.Killer.UserId}&dnt={Convert.ToInt32(ev.Target.ReferenceHub.serverRoles.DoNotTrack)}&ip={ev.Killer.IPAddress}&username={ev.Killer.Nickname}");
            else
                API.APIRequest("playerstats/addstat.php",
                    $"?rounds=0&kills=1&deaths=0&teamkills=0&alias={API.GetAlias()}&id={ev.Killer.UserId}&dnt={Convert.ToInt32(ev.Target.ReferenceHub.serverRoles.DoNotTrack)}&ip={ev.Killer.IPAddress}&username={ev.Killer.Nickname}");
            API.APIRequest("playerstats/addstat.php",
                $"?rounds=0&kills=0&deaths=1&teamkills=0&alias={API.GetAlias()}&id={ev.Target.UserId}&dnt={Convert.ToInt32(ev.Target.ReferenceHub.serverRoles.DoNotTrack)}&ip={ev.Target.IPAddress}&username={ev.Target.Nickname}");
            yield return 0f;
        }
    }
}