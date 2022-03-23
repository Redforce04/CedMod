﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CedMod.Addons.QuerySystem.Patches;
using CedMod.Addons.QuerySystem.WS;
using Exiled.API.Features;
using Exiled.Events.EventArgs;
using MEC;
using Newtonsoft.Json;

namespace CedMod.Addons.QuerySystem
{
    public class QueryServerEvents
    {
        public IEnumerator<float> SyncStart()
        {
            yield return Timing.WaitForSeconds(3);
            if (CedModMain.Singleton.Config.QuerySystem.SecurityKey != "None")
            {
                Log.Debug("Checking configs", CedModMain.Singleton.Config.QuerySystem.Debug);
                if (CedModMain.Singleton.Config.QuerySystem.EnableExternalLookup)
                {
                    Log.Debug("Setting lookup mode", CedModMain.Singleton.Config.QuerySystem.Debug);
                    ServerConfigSynchronizer.Singleton.NetworkRemoteAdminExternalPlayerLookupMode = "fullauth";
                    ServerConfigSynchronizer.Singleton.NetworkRemoteAdminExternalPlayerLookupURL =
                        $"https://{QuerySystem.PanelUrl}/Api/Lookup/";
                    ServerConfigSynchronizer.Singleton.RemoteAdminExternalPlayerLookupToken =
                        CedModMain.Singleton.Config.QuerySystem.SecurityKey;
                }

                Task.Factory.StartNew(() =>
                {
                    Log.Debug("Checking configs", CedModMain.Singleton.Config.QuerySystem.Debug);
                    if (CedModMain.Singleton.Config.QuerySystem.EnableBanreasonSync)
                    {
                        Log.Debug("Enabling ban reasons", CedModMain.Singleton.Config.QuerySystem.Debug);
                        ServerConfigSynchronizer.Singleton.NetworkEnableRemoteAdminPredefinedBanTemplates = true;
                        Log.Debug("Clearing ban reasons", CedModMain.Singleton.Config.QuerySystem.Debug);
                        ServerConfigSynchronizer.Singleton.RemoteAdminPredefinedBanTemplates.Clear();
                        HttpClient client = new HttpClient();
                        Log.Debug("Downloading ban reasons", CedModMain.Singleton.Config.QuerySystem.Debug);
                        var response =
                            client.GetAsync(
                                $"https://{QuerySystem.PanelUrl}/Api/BanReasons/{CedModMain.Singleton.Config.QuerySystem.SecurityKey}");
                        Log.Debug("Addding ban reasons", CedModMain.Singleton.Config.QuerySystem.Debug);
                        foreach (var dict in JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(response
                            .Result
                            .Content.ReadAsStringAsync().Result))
                        {
                            Log.Debug($"Addding ban reason {JsonConvert.SerializeObject(dict)}",
                                CedModMain.Singleton.Config.QuerySystem.Debug);

                            var durationNice = "";
                            TimeSpan timeSpan = TimeSpan.FromMinutes(double.Parse(dict["Dur"]));
                            int num2 = timeSpan.Days / 365;
                            if (num2 > 0)
                            {
                                durationNice = string.Format("{0}y", num2);
                            }
                            else if (timeSpan.Days > 0)
                            {
                                durationNice = string.Format("{0}d", timeSpan.Days);
                            }
                            else if (timeSpan.Hours > 0)
                            {
                                durationNice = string.Format("{0}h", timeSpan.Hours);
                            }
                            else if (timeSpan.Minutes > 0)
                            {
                                durationNice = string.Format("{0}m", timeSpan.Minutes);
                            }
                            else
                            {
                                durationNice = string.Format("{0}s", timeSpan.Seconds);
                            }

                            ServerConfigSynchronizer.Singleton.RemoteAdminPredefinedBanTemplates.Add(
                                new ServerConfigSynchronizer.PredefinedBanTemplate()
                                {
                                    Duration = Convert.ToInt32(dict["Dur"]),
                                    DurationNice = durationNice,
                                    Reason = dict["Reason"]
                                });
                        }
                    }

                    HttpClient client1 = new HttpClient();
                    Log.Debug("Downloading syncs", CedModMain.Singleton.Config.QuerySystem.Debug);
                    var response1 =
                        client1.GetAsync(
                            $"https://{QuerySystem.PanelUrl}/Api/ReservedSlotUsers/{CedModMain.Singleton.Config.QuerySystem.SecurityKey}");
                    Log.Debug($"Downloaded Reserved slots: {response1.Result.Content.ReadAsStringAsync().Result}",
                        CedModMain.Singleton.Config.QuerySystem.Debug);
                    QuerySystem.ReservedSlotUserids =
                        JsonConvert.DeserializeObject<List<string>>(response1.Result.Content.ReadAsStringAsync()
                            .Result);
                });
            }
        }

        public void OnWaitingForPlayers()
        {
            Timing.RunCoroutine(SyncStart());

            Task.Factory.StartNew(delegate
            {
                if (!WebSocketSystem.Socket.IsAlive)
                {
                    WebSocketSystem.Stop();
                    WebSocketSystem.Start();
                }
            });

            WebSocketSystem.SendQueue.Enqueue(new QueryCommand()
            {
                Recipient = "ALL",
                Data = new Dictionary<string, string>()
                {
                    {"Type", nameof(OnWaitingForPlayers)},
                    {"Message", "Server is waiting for players."}
                }
            });
        }

        public void OnRoundStart()
        {
            WebSocketSystem.SendQueue.Enqueue(new QueryCommand()
            {
                Recipient = "ALL",
                Data = new Dictionary<string, string>()
                {
                    {"Type", nameof(OnRoundStart)},
                    {"Message", "Round is restarting."}
                }
            });
            LevelerStore.TrackingEnabled = WebSocketSystem.HelloMessage.ExpEnabled;

            if (LevelerStore.TrackingEnabled)
            {
                Timing.CallDelayed(2, () =>
                {
                    LevelerStore.InitialPlayerRoles.Clear();
                    foreach (var plr in Player.List)
                    {
                        LevelerStore.InitialPlayerRoles.Add(plr, plr.Role);
                    }
                });
            }
        }

        public void OnRoundEnd(RoundEndedEventArgs ev)
        {
            BulletHolePatch.HoleCreators.Clear();
            WebSocketSystem.SendQueue.Enqueue(new QueryCommand()
            {
                Recipient = "ALL",
                Data = new Dictionary<string, string>()
                {
                    {"Type", nameof(OnRoundEnd)},
                    {"Message", "Round is restarting."}
                }
            });

            if (LevelerStore.TrackingEnabled)
            {
                foreach (var plr in LevelerStore.InitialPlayerRoles)
                {
                    if (plr.Key == null || !plr.Key.IsConnected)
                        continue;
                    WebSocketSystem.SendQueue.Enqueue(new QueryCommand()
                    {
                        Recipient = "PANEL",
                        Data = new Dictionary<string, string>()
                        {
                            {"Message", "GRANTEXP"},
                            {"GrantType", "SurvivalEndRound"},
                            {"UserId", plr.Key.UserId},
                            {"RoleType", plr.Value.ToString()}
                        }
                    });
                }
                LevelerStore.InitialPlayerRoles.Clear();
            }
        }

        public void OnCheaterReport(ReportingCheaterEventArgs ev)
        {
            Log.Debug("sending report WR", CedModMain.Singleton.Config.QuerySystem.Debug);
            Task.Factory.StartNew(() =>
            {
                Log.Debug("Thread report send", CedModMain.Singleton.Config.QuerySystem.Debug);
                if (CedModMain.Singleton.Config.QuerySystem.SecurityKey == "None")
                    return;
                Log.Debug("sending report WR", CedModMain.Singleton.Config.QuerySystem.Debug);
                HttpClient client = new HttpClient();
                try
                {
                    var response = client
                        .PostAsync(
                            $"https://{QuerySystem.PanelUrl}/Api/Reports/{CedModMain.Singleton.Config.QuerySystem.SecurityKey}?identity={CedModMain.Singleton.Config.QuerySystem.Identifier}",
                            new StringContent(JsonConvert.SerializeObject(new Dictionary<string, string>()
                                {
                                    {"reporter", ev.Issuer.UserId},
                                    {"reported", ev.Target.UserId},
                                    {"reason", ev.Reason},
                                }), Encoding.Default,
                                "application/json")).Result;
                    Log.Debug(response.Content.ReadAsStringAsync().Result, CedModMain.Singleton.Config.QuerySystem.Debug);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            });
            WebSocketSystem.SendQueue.Enqueue(new QueryCommand()
            {
                Recipient = "ALL",
                Data = new Dictionary<string, string>()
                {
                    {"Type", nameof(OnCheaterReport)},
                    {"UserId", ev.Issuer.UserId},
                    {"UserName", ev.Issuer.Nickname},
                    {
                        "Message", string.Concat(new string[]
                        {
                            "ingame: ",
                            ev.Issuer.UserId,
                            " report ",
                            ev.Target.UserId,
                            " for ",
                            ev.Reason,
                            "."
                        })
                    }
                }
            });
        }

        public void OnRespawn(RespawningTeamEventArgs ev)
        {
            WebSocketSystem.SendQueue.Enqueue(new QueryCommand()
            {
                Recipient = "ALL",
                Data = new Dictionary<string, string>()
                {
                    {"Type", nameof(OnRespawn)},
                    {"Message", string.Format("Respawn: {0} as {1}.", ev.Players.Count, ev.NextKnownTeam)}
                }
            });
        }

        public void OnReport(LocalReportingEventArgs ev)
        {
            Log.Debug("sending report WR", CedModMain.Singleton.Config.QuerySystem.Debug);
            Task.Factory.StartNew(() =>
            {
                Log.Debug("Thread report send", CedModMain.Singleton.Config.QuerySystem.Debug);
                if (CedModMain.Singleton.Config.QuerySystem.SecurityKey == "None")
                    return;
                Log.Debug("sending report WR", CedModMain.Singleton.Config.QuerySystem.Debug);
                HttpClient client = new HttpClient();
                try
                {
                    var response = client
                        .PostAsync(
                            $"https://{QuerySystem.PanelUrl}/Api/Reports/{CedModMain.Singleton.Config.QuerySystem.SecurityKey}?identity={CedModMain.Singleton.Config.QuerySystem.Identifier}",
                            new StringContent(JsonConvert.SerializeObject(new Dictionary<string, string>()
                                {
                                    {"reporter", ev.Issuer.UserId},
                                    {"reported", ev.Target.UserId},
                                    {"reason", ev.Reason},
                                }), Encoding.Default,
                                "application/json")).Result;
                    Log.Debug(response.Content.ReadAsStringAsync().Result, CedModMain.Singleton.Config.QuerySystem.Debug);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            });
            WebSocketSystem.SendQueue.Enqueue(new QueryCommand()
            {
                Recipient = "ALL",
                Data = new Dictionary<string, string>()
                {
                    {"Type", nameof(OnReport)},
                    {"UserId", ev.Target.UserId},
                    {"UserName", ev.Target.Nickname},
                    {
                        "Message", string.Concat(new string[]
                        {
                            "ingame: ",
                            ev.Issuer.UserId,
                            " report ",
                            ev.Target.UserId,
                            " for ",
                            ev.Reason,
                            "."
                        })
                    }
                }
            });
        }
    }

    public class MiniMapClass
    {
        public List<MiniMapElement> MapElements = new List<MiniMapElement>();
        public List<MiniMapPlayerElement> PlayerElements = new List<MiniMapPlayerElement>();
    }

    public class MiniMapElement
    {
        public string Position;
        public string Name;
        public string ZoneType;
        public string Rotation;
    }

    public class MiniMapPlayerElement
    {
        public string Position;
        public string Name;
        public string Zone;
        public string TeamColor;
    }
}