using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TweetSharp;
using System.Collections.Generic;

namespace man.twitterfollowermonitor
{
    public static class HttpTriggerCSharp
    {
        [FunctionName("HttpTriggerCSharp")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            if (name != null) {
                var twitterService = new TwitterService(
                    consumerKey: Environment.GetEnvironmentVariable("twitterconsumerkey"),
                    consumerSecret: Environment.GetEnvironmentVariable("twitterconsumersecret"));

                twitterService.AuthenticateWith(
                    token: Environment.GetEnvironmentVariable("twitteraccesstoken"), 
                    tokenSecret: Environment.GetEnvironmentVariable("twitteraccesstokensecret"));

                var followerList = new List<string>();

                var options = new ListFollowersOptions();
                options.ScreenName = name;
                options.IncludeUserEntities = false;
                options.SkipStatus = true;
                options.Count = 200;
                options.Cursor = -1;

                TwitterCursorList<TwitterUser> cursorList = null;
                do {
                    var cursorRequest = await twitterService.ListFollowersAsync(options);
                    if (cursorRequest.Response.Error != null) {
                        return new BadRequestObjectResult(cursorRequest.Response.Error.Message);
                    }

                    cursorList = cursorRequest.Value;
                    foreach (TwitterUser user in cursorList)
                    {
                        followerList.Add(user.ScreenName);
                    }

                    if (options.Cursor != -1) break;//only load two pages (2*200 followers) because of rate limiting :(

                    options.Cursor = cursorList.NextCursor;
                } while(cursorList.NextCursor != null);

                return new OkObjectResult($"Followers of {name} (continue with cursor {cursorList.NextCursor}):" 
                    + Environment.NewLine + string.Join(Environment.NewLine, followerList));
            } else {
                return new BadRequestObjectResult($"Please pass a name on the query string or in the request body.");
            }
        }
    }
}
