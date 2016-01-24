using RedditSharp;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace DestinyWeeklyUpdateSentiment
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string[] contentIds;
            
            var config = Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));

            using (var cli = new BungieClient(config.BungieApiKey))
            {
                contentIds = cli.GetAllNews("Destiny", "en").Result
                    .Where(x => x.properties.Title.StartsWith("Bungie Weekly Update"))
                    .Select(x => x.contentId)
                    .ToArray();
            }

            var reddit = new Reddit();
            var me = reddit.LogIn(config.RedditUser, config.RedditPassword);

            using (var outStream = File.OpenWrite("results.csv"))
            {
                using (var writer = new StreamWriter(outStream))
                {
                    foreach (var aid in contentIds)
                    {
                        var misses = 0;
                        
                        foreach (var post in reddit.Search<Post>($"selftext:aid={aid} self:yes subreddit:DestinyTheGame", sortE: Sorting.Top))
                        {
                            Console.Write(".");
                            
                            if (++misses == 10)
                                return;

                            try
                            {
                                if (!post.IsSelfPost || (post.Title.IndexOf("Bungie Weekly Update", StringComparison.OrdinalIgnoreCase) == -1 && post.Title.IndexOf("Destiny Update", StringComparison.OrdinalIgnoreCase) == -1))
                                    continue;
                            }
                            catch (WebException ex) when ((ex.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
                            {
                                continue;
                            }

                            var scores = new List<double>();

                            foreach (var comment in post.GetCommentsRecursive())
                            {
                                if (string.IsNullOrEmpty(comment.Body))
                                    continue;
                                
                                scores.Add(i4Ds.LanguageToolkit.SentimentAnalyzer.GetSentiment("en", comment.Body));
                            }

                            var line = string.Format("{0:yyyy/MM/dd},{1}", post.CreatedUTC, scores.Average());

                            writer.WriteLine(line);

                            Console.WriteLine();
                            Console.WriteLine(line);

                            break;
                        }
                    }
                }
            }
        }
    }
    
    public static class RedditExtensions 
    {
        public static IEnumerable<Comment> GetCommentsRecursive(this Post post)
        {
            foreach (var comment in post.Comments)
            {
                yield return comment;
                
                foreach (var child in comment.GetCommentsRecursive())
                {
                    yield return child;
                }
            }
        }

        public static IEnumerable<Comment> GetCommentsRecursive(this Comment post)
        {
            foreach (var comment in post.Comments)
            {
                yield return comment;
                
                foreach (var child in comment.GetCommentsRecursive())
                {
                    yield return child;
                }
            }
        }
    }
    
    public class Config 
    {
        public string RedditUser { get; set; }
        public string RedditPassword { get; set; }
        public string BungieApiKey { get; set; }
    }
}
