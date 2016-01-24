using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace DestinyWeeklyUpdateSentiment
{
    public class BungieClient : IDisposable
    {
        private const string UriBase = "https://www.bungie.net";
        private readonly HttpClient _httpClient;

        public BungieClient(string apiKey)
        {
            _httpClient = new HttpClient(new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            });
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }

        public Task<NewsItems> GetNews(string category, string language, int? itemsPerPage = null, int? currentPage = null)
        {
            var url = $"{UriBase}/Platform/Content/Site/News/{category}/{language}/";

            if (itemsPerPage != null || currentPage != null)
            {
                url += "?";

                if (itemsPerPage != null)
                {
                    url += "itemsPerPage=" + itemsPerPage;
                }

                if (currentPage != null)
                {
                    if (itemsPerPage != null)
                    {
                        url += "&";
                    }

                    url += "currentPage=" + currentPage;
                }
            }

            return Get<NewsItems>(url);
        }

        public async Task<NewsItem[]> GetAllNews(string category, string language)
        {
            var newsItems = new List<NewsItem>();
            int page = 1;
            NewsItems response;

            do
            {
                response = await GetNews(category, language, currentPage: page++);

                newsItems.AddRange(response.results);
            }
            while (response.hasMore);

            return newsItems.ToArray();
        }

        private async Task<T> Get<T>(string url)
        {
            int attempts = 0;

            while (true)
            {
                attempts += 1;

                BungieResponse<T> response;
                var delay = TimeSpan.FromSeconds(2 * attempts);

                try
                {
                    using (var res = await _httpClient.GetAsync(url))
                    {
                        res.EnsureSuccessStatusCode();

                        response = await res.Content.ReadAsAsync<BungieResponse<T>>();
                    }

                    if (response.ThrottleSeconds > 0)
                    {
                        delay = TimeSpan.FromSeconds(response.ThrottleSeconds);

                        throw new BungieClientException(response);
                    }

                    if (response.ErrorCode != BungieError.Success)
                    {
                        throw new BungieClientException(response);
                    }
                }
                catch (Exception) when (attempts <= 3)
                {
                    await Task.Delay(delay);

                    continue;
                }

                return response.Response;
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }

    [Serializable]
    public class BungieClientException : Exception
    {
        public BungieResponse Response { get; }

        public BungieClientException() { }
        public BungieClientException(string message) : base(message) { }
        public BungieClientException(string message, Exception inner) : base(message, inner) { }
        protected BungieClientException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context)
        { }

        public BungieClientException(BungieResponse response)
            : this(response.Message)
        {
            Response = response;
        }
    }

    public class BungieResponse
    {
        public BungieError ErrorCode { get; set; }
        public int ThrottleSeconds { get; set; }
        public string ErrorStatus { get; set; }
        public string Message { get; set; }
        public MessageData MessageData { get; set; }
    }

    public class BungieResponse<T> : BungieResponse
    {
        public T Response { get; set; }
    }

    public class NewsItems
    {
        public NewsItem[] results { get; set; }
        public string totalResults { get; set; }
        public bool hasMore { get; set; }
        public Query query { get; set; }
        public bool useTotalResults { get; set; }
    }

    public class Query
    {
        public string[] tags { get; set; }
        public string contentType { get; set; }
        public int itemsPerPage { get; set; }
        public int currentPage { get; set; }
    }

    public class NewsItem
    {
        public string contentId { get; set; }
        public string cType { get; set; }
        public string cmsPath { get; set; }
        public DateTime creationDate { get; set; }
        public DateTime modifyDate { get; set; }
        public bool allowComments { get; set; }
        public bool hasAgeGate { get; set; }
        public int minimumAge { get; set; }
        public string ratingImagePath { get; set; }
        public Author author { get; set; }
        public bool autoEnglishPropertyFallback { get; set; }
        public Properties properties { get; set; }
        public object[] representations { get; set; }
        public string[] tags { get; set; }
        public CommentCummary commentSummary { get; set; }
    }

    public class Author
    {
        public string membershipId { get; set; }
        public string uniqueName { get; set; }
        public string displayName { get; set; }
        public int profilePicture { get; set; }
        public int profileTheme { get; set; }
        public int userTitle { get; set; }
        public string successMessageFlags { get; set; }
        public bool isDeleted { get; set; }
        public string about { get; set; }
        public DateTime firstAccess { get; set; }
        public DateTime lastUpdate { get; set; }
        public string psnDisplayName { get; set; }
        public string xboxDisplayName { get; set; }
        public bool showActivity { get; set; }
        public int followerCount { get; set; }
        public int followingUserCount { get; set; }
        public string locale { get; set; }
        public bool localeInheritDefault { get; set; }
        public bool showGroupMessaging { get; set; }
        public string profilePicturePath { get; set; }
        public string profileThemeName { get; set; }
        public string userTitleDisplay { get; set; }
        public string statusText { get; set; }
        public DateTime statusDate { get; set; }
    }

    public class Properties
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Summary { get; set; }
        public string Content { get; set; }
        public string Thumbnail { get; set; }
        public string FrontPageBanner { get; set; }
        public string ArticleBanner { get; set; }
        public string Video { get; set; }
        public string MobileTitle { get; set; }
        public string MobileBanner { get; set; }
        public string FrontPageBannerVideoWebM { get; set; }
        public string FrontPageBannerVideoMp4 { get; set; }
    }

    public class CommentCummary
    {
        public string topicId { get; set; }
        public int commentCount { get; set; }
    }

    public class MessageData
    {
    }

    public enum BungieError
    {
        None = 0,
        Success = 1,
        NotFound = 21
    }
}
