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
                    val = await conn.ExecuteScalarAsync<int>(@"select b.HR
                                                                from master m
    	                                                            INNER JOIN batting b ON m.playerID = b.playerID
                                                                where b.yearID = @year
    	                                                            AND m.nameLast = @last
                                                                    AND m.nameFirst = @first", new { @year = year, @first = first, @last = last });

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
    }
}