using System;
using System.Linq;
using System.Configuration;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;

using Dapper;

using MySql.Data.MySqlClient;
using Microsoft.Bot.Connector;

namespace Microsoft.Bot.Sample.LuisBot
{
    // For more information about this template visit http://aka.ms/azurebots-csharp-luis
    [Serializable]
    public class BasicLuisDialog : LuisDialog<object>
    {
        public BasicLuisDialog() : base(new LuisService(new LuisModelAttribute(
            ConfigurationManager.AppSettings["LuisAppId"], 
            ConfigurationManager.AppSettings["LuisAPIKey"], 
            domain: ConfigurationManager.AppSettings["LuisAPIHostName"])))
        {
        }

        private class Player
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Stat { get; set; }
            public int Val { get; set; }
        }

        [LuisIntent("None")]
        public async Task NoneIntent(IDialogContext context, LuisResult result)
        {
            await this.ShowLuisResult(context, result);
        }

        // Go to https://luis.ai and create a new intent, then train/publish your luis app.
        // Finally replace "Greeting" with the name of your newly created intent in the following handler
        [LuisIntent("Greeting")]
        public async Task GreetingIntent(IDialogContext context, LuisResult result)
        {
            await this.ShowLuisResult(context, result);
        }

        [LuisIntent("Cancel")]
        public async Task CancelIntent(IDialogContext context, LuisResult result)
        {
            await this.ShowLuisResult(context, result);
        }

        [LuisIntent("Help")]
        public async Task HelpIntent(IDialogContext context, LuisResult result)
        {
            await this.ShowLuisResult(context, result);
        }

        [LuisIntent("LeaderStat")]
        public async Task GetLeaderStatIntent(IDialogContext context, LuisResult result)
        {
            string stat = "HR";
            Player player = new Player { Id = "dsh", Name = "Dave", Stat = "HR", Val = 0 };
            int year = 2001;
            var q = result.Query;

            foreach (var ent in result.Entities)
            {
                switch (ent.Type)
                {
                    case "stat":
                        if (ent.Resolution.Values.Count > 0)
                        {
                            stat = ((List<object>)ent.Resolution["values"]).First().ToString();
                        }
                        else
                        {
                            stat = ent.Entity;
                        }
                        break;
                    case "year":
                        year = Convert.ToInt32(ent.Entity);
                        break;
                    default:
                        break;
                }
            }

            #region hide this
            var connStr = ConfigurationManager.ConnectionStrings["baseballData"].ConnectionString;
            #endregion

            try
            {
                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    var results = await conn.QueryAsync<Player>(GetLeaderQueryForStat(stat), new { @year = year });
                    var topPlayers = results.Take(5);

                    var players = new List<CardAction>();
                    foreach (var p in topPlayers)
                    {
                        players.Add(new CardAction()
                        {
                            Value = $"https://www.baseball-reference.com/players/{p.Id.First().ToString()}/{p.Id}.shtml",
                            Type = "openUrl",
                            Title = $"{p.Name} hit {p.Val} {p.Stat} in {year}"
                        });
                    }
                    var card = new HeroCard()
                    {
                        Title = $"Top 5 Players for {stat} in {year}",
                        Buttons = players
                    };
                    var connector = new ConnectorClient(new Uri(context.Activity.ServiceUrl));

                    var reply = ((Activity)context.Activity).CreateReply("");
                    reply.Attachments = new List<Attachment>();
                    reply.Attachments.Add(card.ToAttachment());

                    await connector.Conversations.SendToConversationAsync(reply);
                }
            }
            catch (Exception exc)
            {
                await context.PostAsync($"Error occurred {exc.Message}");
                context.Wait(MessageReceived);
            }

        }

        [LuisIntent("PlayerStat")]
        public async Task GetPlayerStatIntent(IDialogContext context, LuisResult result)
        {
            int year = 2001;
            string first = "barry", last = "bonds", stat = "HR";
            int val;

            var q = result.Query;
            foreach (var ent in result.Entities)
            {
                switch (ent.Type)
                {
                    case "stat":
                        if (ent.Resolution.Values.Count > 0)
                        {
                            stat = ((List<object>)ent.Resolution["values"]).First().ToString();
                        }
                        else
                        {
                            stat = ent.Entity;
                        }
                        break;
                    case "year":
                        year = Convert.ToInt32(ent.Entity);
                        break;
                    case "player":
                        first = ent.Entity.Substring(0, ent.Entity.IndexOf(" "));
                        last = ent.Entity.Substring(ent.Entity.IndexOf(" ") + 1);
                        break;
                    default:
                        break;
                }
            }


            #region hide this
            var connStr = ConfigurationManager.ConnectionStrings["baseballData"].ConnectionString;
            #endregion

            try
            {
                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    //HR value being hard-coded here...but can be parameterized
                    val = await conn.ExecuteScalarAsync<int>(GetQueryForStat(stat), new { @year = year, @first = first, @last = last });

                }

                await context.PostAsync($"{first} {last} hit {val} {stat} in {year}");
                context.Wait(MessageReceived);
            }
            catch (Exception exc)
            {
                await context.PostAsync($"Error occurred {exc.Message}");
                context.Wait(MessageReceived);
            }
            
        }


        private async Task ShowLuisResult(IDialogContext context, LuisResult result) 
        {
            await context.PostAsync($"You have reached {result.Intents[0].Intent}. You said: {result.Query}");
            context.Wait(MessageReceived);
        }

        private static string GetQueryForStat(string stat)
        {
            string q;
            switch (stat)
            {
                case "hrs":
                    q = "select b.HR from master m INNER JOIN batting b ON m.playerID = b.playerID where b.yearID = @year AND m.nameLast = @last AND m.nameFirst = @first";
                    break;
                case "2b":
                    q = "select b.2B from master m INNER JOIN batting b ON m.playerID = b.playerID where b.yearID = @year AND m.nameLast = @last AND m.nameFirst = @first";
                    break;
                case "3b":
                    q = "select b.3B from master m INNER JOIN batting b ON m.playerID = b.playerID where b.yearID = @year AND m.nameLast = @last AND m.nameFirst = @first";
                    break;
                case "rbi":
                    q = "select b.RBI from master m INNER JOIN batting b ON m.playerID = b.playerID where b.yearID = @year AND m.nameLast = @last AND m.nameFirst = @first";
                    break;
                case "h":
                    q = "select b.H from master m INNER JOIN batting b ON m.playerID = b.playerID where b.yearID = @year AND m.nameLast = @last AND m.nameFirst = @first";
                    break;
                case "ab":
                    q = "select b.AB from master m INNER JOIN batting b ON m.playerID = b.playerID where b.yearID = @year AND m.nameLast = @last AND m.nameFirst = @first";
                    break;
                case "k":
                    q = "select b.SO from master m INNER JOIN batting b ON m.playerID = b.playerID where b.yearID = @year AND m.nameLast = @last AND m.nameFirst = @first";
                    break;
                default:
                    q = "select b.HR from master m INNER JOIN batting b ON m.playerID = b.playerID where b.yearID = @year AND m.nameLast = @last AND m.nameFirst = @first";
                    break;
            }
            return q;
        }

        private static string GetLeaderQueryForStat(string stat)
        {
            string q;
            switch (stat)
            {
                case "hrs":
                    q = "select m.bbrefID as id, CONCAT_WS(' ',m.nameFirst, m.nameLast) as name, 'HR' as stat, b.HR as val from master m INNER JOIN batting b ON m.playerID = b.playerID where b.yearID = @year ORDER BY b.HR desc";
                    break;
                case "2b":
                    q = "select m.bbrefID as id, CONCAT_WS(' ',m.nameFirst, m.nameLast) as name, '2B' as stat, b.2B as val from master m INNER JOIN batting b ON m.playerID = b.playerID where b.yearID = @year ORDER BY b.2B desc";
                    break;
                case "3b":
                    q = "select m.bbrefID as id, CONCAT_WS(' ',m.nameFirst, m.nameLast) as name, '3B' as stat, b.3B as val from master m INNER JOIN batting b ON m.playerID = b.playerID where b.yearID = @year ORDER BY b.3B desc";
                    break;
                case "rbi":
                    q = "select m.bbrefID as id, CONCAT_WS(' ',m.nameFirst, m.nameLast) as name, 'RBI' as stat, b.RBI as val from master m INNER JOIN batting b ON m.playerID = b.playerID where b.yearID = @year ORDER BY b.RBI desc";
                    break;
                case "ab":
                    q = "select m.bbrefID as id, CONCAT_WS(' ',m.nameFirst, m.nameLast) as name, 'AB' as stat, b.AB as val from master m INNER JOIN batting b ON m.playerID = b.playerID where b.yearID = @year ORDER BY b.AB desc";
                    break;
                case "k":
                    q = "select m.bbrefID as id, CONCAT_WS(' ',m.nameFirst, m.nameLast) as name, 'K' as stat, b.SO as val from master m INNER JOIN batting b ON m.playerID = b.playerID where b.yearID = @year ORDER BY b.SO desc";
                    break;
                case "h":
                    q = "select m.bbrefID as id, CONCAT_WS(' ',m.nameFirst, m.nameLast) as name, 'Hits' as stat, b.H as val from master m INNER JOIN batting b ON m.playerID = b.playerID where b.yearID = @year ORDER BY b.H desc";
                    break;
                default:
                    q = "select m.bbrefID as id, CONCAT_WS(' ',m.nameFirst, m.nameLast) as name, 'HR' as stat, b.HR as val from master m INNER JOIN batting b ON m.playerID = b.playerID where b.yearID = @year ORDER BY b.HR desc";
                    break;
            }
            return q;
        }
    }
}