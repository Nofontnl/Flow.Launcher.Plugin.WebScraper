using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Plugin.WebScraper.Models;
using Flow.Launcher.Plugin.WebScraper.Views;
using AngleSharp;
using AngleSharp.Dom;

namespace Flow.Launcher.Plugin.WebScraper
{
    public class WebScraper : IAsyncPlugin, ISettingProvider
    {
        private HttpClient _client;
        private PluginInitContext _context;
        private Settings _settings;
        private static string _faviconCacheDirectory;
        private const string WebScraperIcoPath = "Images\\webscraper.png";
        private const string ScrapeErrorIcoPath = "Images\\error.png";

        private static readonly Regex VariableRegex = new(@"\$\{(\w+)\}", RegexOptions.Compiled);

        private ObservableCollection<ScrapeConfig> ValidScrapeConfigs()
        {
            return new ObservableCollection<ScrapeConfig>(
                _settings.ScrapeConfigs.Where(x => !string.IsNullOrWhiteSpace(x.Keyword))
            );
        }

        private List<Result> SingleResult(string title, string subtitle, Func<ActionContext, bool> action = null, string icoPath = WebScraperIcoPath)
        {
            return new List<Result>
            {
                new()
                {
                    Title = title,
                    SubTitle = subtitle,
                    Action = action,
                    IcoPath = icoPath
                }
            };
        }

        private string GetCachedIconPath(string url)
        {
            string filename = Convert.ToBase64String(Encoding.UTF8.GetBytes(url))
                            .Replace("=", "") + ".png";
            return Path.Combine(_faviconCacheDirectory, filename);
        }

        private async Task<string> GetIconAsync(string url)
        {
            string localPath = GetCachedIconPath(url);

            if (!File.Exists(localPath))
            {
                using var response = await _client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var bytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(localPath, bytes);
            }

            return localPath;
        }

        async Task<string> TryGetIconAsync(string path)
        {
            try
            {
                return await GetIconAsync(path);
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        public Control CreateSettingPanel()
        {
            return new SettingsControl(_settings);
        }

        public Task InitAsync(PluginInitContext context)
        {
            _context = context;
            _settings = _context.API.LoadSettingJsonStorage<Settings>() ?? new Settings();
            _client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _faviconCacheDirectory = Path.Combine(_context.CurrentPluginMetadata.PluginDirectory, "IconCache");
            Directory.CreateDirectory(_faviconCacheDirectory);
            return Task.CompletedTask;
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            // Get all valid configurations
            ObservableCollection<ScrapeConfig> validScrapeConfigs = ValidScrapeConfigs();

            // No config keyword entered, so show options
            if (query.SearchTerms.Length == 0)
            {
                // Show 'no configurations found' when the user has not added any configurations
                if (validScrapeConfigs.Count == 0)
                {
                    return SingleResult(
                        "No configurations found",
                        "Please add a scrape configuration in the plugin settings. Select this result to open the usage guide",
                        _ =>
                        {
                            _context.API.OpenUrl("https://github.com/Nofontnl/Flow.Launcher.Plugin.WebScraper");
                            return true;
                        }
                    );
                }
                
                // Merge configurations with the same keyword
                List<Result> options = new();
                var mergedScrapeConfigs = new Dictionary<string, List<string>>();
                var scores = new Dictionary<string, int>();

                for (int i = 0; i < validScrapeConfigs.Count; i++)
                {
                    var scrapeConfig = validScrapeConfigs[i];

                    // Add description to list, create list if key doesn't exist
                    if (!mergedScrapeConfigs.TryGetValue(scrapeConfig.Keyword, out var list))
                    {
                        list = new List<string>();
                        mergedScrapeConfigs[scrapeConfig.Keyword] = list;

                        // Store the priority calculated by first occurrence's index
                        scores[scrapeConfig.Keyword] = validScrapeConfigs.Count - i;
                    }

                    list.Add(scrapeConfig.Tag);
                }

                // Rename empty tags only if there are multiple tags to be displayed
                foreach (var mergedScrapeConfig in mergedScrapeConfigs)
                {
                    if (mergedScrapeConfig.Value.Count == 1) continue;
                    var newVal = mergedScrapeConfig.Value.Select(x =>
                    {
                        return x == "" ? "No tag" : x;
                    }).ToList();
                    mergedScrapeConfigs[mergedScrapeConfig.Key] = newVal;
                }

                options.AddRange(
                    mergedScrapeConfigs.Select(kvp => new Result
                    {
                        Title = kvp.Key,
                        SubTitle = string.Join(", ", kvp.Value),
                        Action = _ =>
                        {
                            _context.API.ChangeQuery(query + " " + kvp.Key);
                            return false;
                        },
                        Score = scores[kvp.Key],
                        IcoPath = WebScraperIcoPath
                    })
                );
                return options;
            }

            // Find configuration with correct scrape keyword
            string scrapeKeyword = query.SearchTerms[0];
            List<ScrapeConfig> scrapeConfigs = validScrapeConfigs.Where(x => x.Keyword == scrapeKeyword).ToList();

            // No configuration found, so show options
            if (scrapeConfigs.Count == 0)
            {
                return SingleResult(
                    $"No configuration found with keyword '{scrapeKeyword}'",
                    $"Available configurations: {string.Join(", ", validScrapeConfigs.Select(x => x.Keyword).Distinct())}"
                );
            }

            // Configuration was found, so do the API request
            var results = new List<Result>();
            foreach (ScrapeConfig scrapeConfig in scrapeConfigs)
            {
                string body;
                try
                {
                    using HttpResponseMessage data = await _client.GetAsync(new Uri(scrapeConfig.Url), token);
                    if (data.Content.Headers.ContentType?.MediaType.Contains("text/html") != true)
                    {
                        return SingleResult(
                            "The URL did not return a valid HTML response",
                            "Please check whether the configured URL returns an HTML response",
                            icoPath: ScrapeErrorIcoPath
                        );
                    }
                    body = await data.Content.ReadAsStringAsync(token);
                }
                catch (HttpRequestException)
                {
                    return SingleResult(
                        "No internet connection / cannot reach host",
                        "Please check your internet connection",
                        icoPath: ScrapeErrorIcoPath
                    );
                }
                catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
                {
                    return SingleResult(
                        "The HTTP request timed out",
                        "The website may be slow or unreachable. Try again",
                        icoPath: ScrapeErrorIcoPath
                    );
                }

                var document = await ParseDocument(body, token);

                foreach (ScrapeResult scrapeResult in scrapeConfig.ScrapeResults)
                {
                    var evaluatedXpathDict = new Dictionary<string, List<string>>();
                    foreach (KeyValuePair<string, string> variableBinding in scrapeResult.VariableBindings)
                    {
                        // Extract variable keys used in Title and SubTitle
                        var usedKeys = new HashSet<string>(
                            VariableRegex.Matches(scrapeResult.Title).Select(m => m.Groups[1].Value)
                            .Concat(VariableRegex.Matches(scrapeResult.SubTitle).Select(m => m.Groups[1].Value))
                        );

                        if (usedKeys.Contains(variableBinding.Key))
                        {
                            IHtmlCollection<IElement> htmlNodes = document.QuerySelectorAll(variableBinding.Value);
                            
                            if (htmlNodes.Length != 0)
                            {
                                evaluatedXpathDict.Add(variableBinding.Key, new List<string>(htmlNodes.Select(x => WebUtility.HtmlDecode(x.InnerHtml))));
                            }
                        }
                    }

                    // Broadcast singular values
                    Dictionary<string, List<string>> processedEvaluatedXpathDict = evaluatedXpathDict;
                    int itemCount = 0;

                    if (evaluatedXpathDict.Count != 0)
                    {
                        itemCount = evaluatedXpathDict.Max(x => x.Value.Count);
                        processedEvaluatedXpathDict = evaluatedXpathDict.ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Count == 1 ? Enumerable.Repeat(kvp.Value.ElementAt(0), itemCount).ToList() : kvp.Value
                        );
                    }

                    // Check if counts are compatible
                    if (processedEvaluatedXpathDict.Select(x => x.Value.Count).Distinct().Count() > 1)
                    {
                        return SingleResult(
                            $"Cannot generate combinations: variables have incompatible numbers of values",
                            $"{string.Join(", ", evaluatedXpathDict.Select(x => $"${{{x.Key}}}: {x.Value.Count} matches"))}. Please check whether the variable bindings are configured correctly",
                            icoPath: ScrapeErrorIcoPath
                        );
                    }

                    // Extract icon
                    var iconNode = document.QuerySelector("link[rel=\"icon\"]");
                    var icoPath = new Uri(new Uri(scrapeConfig.Url), "/favicon.ico").AbsoluteUri;
                    if (iconNode != null)
                    {
                        var href = iconNode.GetAttribute("href");
                        icoPath = new Uri(new Uri(scrapeConfig.Url), href).AbsoluteUri;
                    }
                    
                    string icoPathLocal = await TryGetIconAsync(icoPath);

                    for (var i = 0; i < itemCount; i++)
                    {
                        var fullTitle = VariableRegex.Replace(scrapeResult.Title, match =>
                        {
                            string key = match.Groups[1].Value;
                            return processedEvaluatedXpathDict.TryGetValue(key, out var value) ? value.ElementAt(i) : match.Value;
                        });
                        var fullSubTitle = VariableRegex.Replace(scrapeResult.SubTitle, match =>
                        {
                            string key = match.Groups[1].Value;
                            return processedEvaluatedXpathDict.TryGetValue(key, out var value) ? value.ElementAt(i) : match.Value;
                        });
                        results.Add(new()
                        {
                            Title = fullTitle,
                            SubTitle = fullSubTitle,
                            Action = _ =>
                            {
                                _context.API.OpenUrl(scrapeConfig.Url);
                                return true;
                            },
                            IcoPath = icoPathLocal
                        });
                    }
                }
            }

            return results;
        }

        private static async Task<IDocument> ParseDocument(string body, CancellationToken token)
        {
            IConfiguration angleConfig = Configuration.Default;
            IBrowsingContext angleContext = BrowsingContext.New(angleConfig);
            return await angleContext.OpenAsync(req => req.Content(body), cancel: token);
        }

        public void SaveSettings()
        {
            _context.API.SavePluginSettings();
        }
    }
}