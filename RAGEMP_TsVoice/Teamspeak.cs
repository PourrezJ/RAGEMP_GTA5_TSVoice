using GTANetworkAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeamSpeak3QueryApi.Net;
using TeamSpeak3QueryApi.Net.Specialized;
using TeamSpeak3QueryApi.Net.Specialized.Responses;

namespace RAGEMP_TsVoice
{
    public class Teamspeak : Script
    {
        private TeamSpeakClient tsQuery;

        private static string TeamspeakQueryAddress { get; set; }
        private static short TeamspeakQueryPort { get; set; }
        private static string TeamspeakPort { get; set; }
        private static string TeamspeakLogin { get; set; }
        private static string TeamspeakPassword { get; set; }
        private static string TeamspeakChannel { get; set; }

        public Teamspeak()
        {
            Console.WriteLine("Teamspeak Wrapper Initialization...");
        }

        [ServerEvent(Event.PlayerConnected)]
        public void OnPlayerConnected(Client client)
        {
            Teamspeak.Connect(client, client.SocialClubName); 
        }

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart()
        {
            Task.Run(async () =>
            {
                try
                {
                    TeamspeakQueryAddress = NAPI.Resource.GetSetting<string>(this, "teamspeak_query_address");
                    TeamspeakQueryPort = NAPI.Resource.GetSetting<short>(this, "teamspeak_query_port");
                    TeamspeakPort = NAPI.Resource.GetSetting<string>(this, "teamspeak_port");
                    TeamspeakLogin = NAPI.Resource.GetSetting<string>(this, "teamspeak_login");
                    TeamspeakPassword = NAPI.Resource.GetSetting<string>(this, "teamspeak_password");
                    TeamspeakChannel = NAPI.Resource.GetSetting<string>(this, "teamspeak_channel");

                    await InitTSQuery();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

                
                Console.WriteLine("Teamspeak Wrapper Initialised!");
            });          
        }

        [ServerEvent(Event.ResourceStop)]
        public static void OnResourceStop()
        {
            NAPI.ClientEvent.TriggerClientEventForAll("DisconnectTeamspeak");
        }

        [RemoteEvent("ChangeVoiceRange")]
        public void ChangeVoiceRange(Client client)
        {
            string voiceRange = "Normal";
            if (client.HasSharedData("VOICE_RANGE"))
                voiceRange = client.GetSharedData("VOICE_RANGE");


            switch (voiceRange)
            {
                case "Normal":
                    voiceRange = "Weit";
                    break;
                case "Weit":
                    voiceRange = "Kurz";
                    break;
                case "Kurz":
                    voiceRange = "Normal";
                    break;
            }
            client.SetSharedData("VOICE_RANGE", voiceRange);
            client.SendNotification("Voice Range: " + voiceRange);
        }

        public static void Connect(Client client, string characterName)
        {
            client.SetSharedData("VOICE_RANGE", "Normal");
            client.SetSharedData("TsName", characterName);
            client.TriggerEvent("ConnectTeamspeak", characterName);
        }

        private async Task InitTSQuery()
        {
            tsQuery = new TeamSpeakClient(TeamspeakQueryAddress, TeamspeakQueryPort); // Create rich client instance

            try
            {              
                await tsQuery.Connect(); // connect to the server
                await tsQuery.Login(TeamspeakLogin, TeamspeakPassword); // login to do some stuff that requires permission
                await tsQuery.UseServer(1); // Use the server with id '1'
                var me = await tsQuery.WhoAmI(); // Get information about yourself!

                var channel = (await tsQuery.FindChannel(TeamspeakChannel)).FirstOrDefault();

                Utils.Delay(100, false, async () =>
                    await UpdateTeamspeak(channel)
                );
            }
            catch(QueryException ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private async Task UpdateTeamspeak(FoundChannel channel)
        {
            for (int i = 0; i < NAPI.Pools.GetAllPlayers().Count; i++)
            {
                if (NAPI.Pools.GetAllPlayers()[i] != null && NAPI.Pools.GetAllPlayers()[i].Exists)
                {
                    await RefreshSpeaker(channel, NAPI.Pools.GetAllPlayers()[i], NAPI.Pools.GetAllPlayers());
                }
            }
        }

        public async Task RefreshSpeaker(FoundChannel channel, Client player, List<Client> players)
        {
            if (!tsQuery.Client.IsConnected)
                return;

            try
            {
                var clients = await tsQuery.GetClients(GetClientOptions.Voice);
                var clientschannel = clients.ToList().FindAll(c => c.ChannelId == channel.Id);

                var name = player.GetSharedData("TsName");
                var tsplayer = clientschannel.Find(p => p.NickName == name);

                if (tsplayer != null)
                {
                    if (tsplayer.Talk && !player.HasData("IS_SPEAKING"))
                    {
                        players.FindAll(p => p.Exists && p.Position.DistanceTo2D(player.Position) < 5f)
                            .ForEach((client) => client.TriggerEvent("Teamspeak_LipSync", player.Handle.Value, true));

                        player.SetData("IS_SPEAKING", true);
                    }
                    else if (!tsplayer.Talk && player.HasData("IS_SPEAKING"))
                    {
                        players.FindAll(p => p.Exists && p.Position.DistanceTo2D(player.Position) < 5f)
                            .ForEach((client) => client.TriggerEvent("Teamspeak_LipSync", player.Handle.Value, false));

                        player.ResetData("IS_SPEAKING");
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("TS RefreshSpeaker Error: " + ex.ToString());
            }  
        }
    }
}
