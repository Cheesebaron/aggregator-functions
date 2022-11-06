using Microsoft.Extensions.Logging;
using PlanetXamarin.Extensions;
using PlanetXamarinAuthors.Models;
using Polly;
using Polly.Retry;
using System.Net;
using System.Net.Http.Headers;
using System.ServiceModel.Syndication;
using System.Xml;

namespace PlanetXamarin.Infrastructure
{
    public class CombinedFeedSource
    {
        private readonly HttpClient _httpClient;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly IList<Author> _authors;
        private readonly ILogger _logger;
        private readonly string _rssFeedTitle;
        private readonly string _rssFeedDescription;
        private readonly string _rssFeedUrl;
        private readonly string _rssFeedImageUrl;

        public CombinedFeedSource(
            IList<Author> authors,
            ILogger logger,
            string rssFeedTitle,
            string rssFeedDescription,
            string rssFeedUrl,
            string rssFeedImageUrl)
        {
            _httpClient = EnsureHttpClient();

            // retry policy with max 2 retries, delay by x*x^1.2 where x is retry attempt
            // this will ensure we don't retry too quickly
            _retryPolicy = Policy.Handle<FeedReadFailedException>()
                .WaitAndRetryAsync(2, retry => TimeSpan.FromSeconds(retry * Math.Pow(1.2, retry)));

            _authors = authors;
            _logger = logger;
            _rssFeedTitle = rssFeedTitle;
            _rssFeedDescription = rssFeedDescription;
            _rssFeedUrl = rssFeedUrl;
            _rssFeedImageUrl = rssFeedImageUrl;
        }

        private HttpClient EnsureHttpClient()
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("PlanetXamarin", $"{GetType().Assembly.GetName().Version}"));
            httpClient.Timeout = TimeSpan.FromSeconds(15);

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13;

            return httpClient;
        }

        public async Task<SyndicationFeed> LoadFeed(int? numberOfItems, string languageCode = "mixed")
        {
            IList<Author> tamarins;
            if (languageCode is "mixed") // use all tamarins
            {
                tamarins = _authors;
            }
            else
            {
                tamarins = _authors.Where(t => t.FeedLanguageCode == languageCode).ToList();
            }

            var feedTasks = tamarins.SelectMany(TryReadFeeds).ToArray();

            _logger.LogInformation($"Loading feed for language: {languageCode} for {feedTasks.Length} authors");

            var syndicationItems = await Task.WhenAll(feedTasks).ConfigureAwait(false);
            var combinedFeed = GetCombinedFeed(syndicationItems.SelectMany(f => f), languageCode, tamarins, numberOfItems);
            return combinedFeed;
        }

        private IEnumerable<Task<IEnumerable<SyndicationItem>>> TryReadFeeds(Author tamarin)
        {
            return tamarin.FeedUris.Select(uri => TryReadFeed(tamarin, uri.AbsoluteUri));
        }

        private async Task<IEnumerable<SyndicationItem>> TryReadFeed(Author tamarin, string feedUri)
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(_ => ReadFeed(feedUri), new Context(feedUri)).ConfigureAwait(false);
            }
            catch (FeedReadFailedException ex)
            {
                _logger.LogError(ex, "{FirstName} {LastName}'s feed of {FeedUri} failed to load.", tamarin.FirstName,
                    tamarin.LastName, ex.Data["FeedUri"]);
            }

            return Array.Empty<SyndicationItem>();
        }

        private async Task<IEnumerable<SyndicationItem>> ReadFeed(string feedUri)
        {
            HttpResponseMessage response;
            try
            {
                _logger.LogInformation("Loading feed {FeedUri}", feedUri);
                response = await _httpClient.GetAsync(feedUri).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    await using var feedStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    using var reader = XmlReader.Create(feedStream);
                    var feed = SyndicationFeed.Load(reader);
                    var filteredItems = feed.Items
                        .Where(item => item.ApplyDefaultFilter());

                    return filteredItems;
                }
            }
            catch (HttpRequestException hex)
            {
                throw new FeedReadFailedException("Loading remote syndication feed failed", hex)
                    .WithData("FeedUri", feedUri);
            }
            catch (WebException ex)
            {
                throw new FeedReadFailedException("Loading remote syndication feed timed out", ex)
                    .WithData("FeedUri", feedUri);
            }
            catch (XmlException ex)
            {
                throw new FeedReadFailedException("Failed parsing remote syndication feed", ex)
                    .WithData("FeedUri", feedUri);
            }
            catch (TaskCanceledException ex)
            {
                throw new FeedReadFailedException("Reading feed timed out", ex)
                    .WithData("FeedUri", feedUri);
            }
            catch (OperationCanceledException opcex)
            {
                throw new FeedReadFailedException("Reading feed timed out", opcex)
                    .WithData("FeedUri", feedUri);
            }

            throw new FeedReadFailedException("Loading remote syndication feed failed.")
                .WithData("FeedUri", feedUri)
                .WithData("HttpStatusCode", (int)response.StatusCode);
        }

        private SyndicationFeed GetCombinedFeed(IEnumerable<SyndicationItem> items, string languageCode, 
            IEnumerable<Author> tamarins, int? numberOfItems)
        {
            DateTimeOffset GetMaxTime(SyndicationItem item)
            {
                return new[] { item.PublishDate.UtcDateTime, item.LastUpdatedTime.UtcDateTime }.Max();
            }

            var orderedItems = items
                .Where(item =>
                    GetMaxTime(item) <= DateTimeOffset.UtcNow)
                .OrderByDescending(GetMaxTime);

            var feed = new SyndicationFeed(
                _rssFeedTitle,
                _rssFeedDescription,
                new Uri(_rssFeedUrl),
                numberOfItems.HasValue ? orderedItems.Take(numberOfItems.Value) : orderedItems)
            {
                ImageUrl = new Uri(_rssFeedImageUrl),
                Copyright = new TextSyndicationContent("The copyright for each post is retained by its author."),
                Language = languageCode,
                LastUpdatedTime = DateTimeOffset.UtcNow
            };

            foreach(var tamarin in tamarins)
            {
                feed.Contributors.Add(new SyndicationPerson(
                    tamarin.EmailAddress, $"{tamarin.FirstName} {tamarin.LastName}", tamarin.WebSite.ToString()));
            }

            return feed;
        }
    }

    public class FeedReadFailedException : Exception
    {
        public FeedReadFailedException(string message) 
            : base(message)
        {
        }

        public FeedReadFailedException(string message, Exception inner) 
            : base(message, inner)
        {
        }
    }

    internal static class ExceptionExtensions
    {
        public static TException WithData<TException>(this TException exception, string key, object value) where TException : Exception
        {
            exception.Data[key] = value;
            return exception;
        }
    }
}