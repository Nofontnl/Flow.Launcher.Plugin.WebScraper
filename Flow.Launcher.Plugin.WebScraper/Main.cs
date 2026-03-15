using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Plugin.WebScraper.Models;
using Flow.Launcher.Plugin.WebScraper.Views;
using HtmlAgilityPack;

namespace Flow.Launcher.Plugin.WebScraper
{
    public class WebScraper : IAsyncPlugin, ISettingProvider
    {
        private HttpClient _client;
        private PluginInitContext _context;
        private Settings _settings;

        private static readonly Regex VariableRegex = new(@"\$\{(\w+)\}", RegexOptions.Compiled);

        private ObservableCollection<ScrapeConfig> ValidScrapeConfigs()
        {
            return new ObservableCollection<ScrapeConfig>(
                _settings.ScrapeConfigs.Where(x => !string.IsNullOrWhiteSpace(x.Keyword))
            );
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
            return Task.CompletedTask;
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            // Get all valid configurations
            ObservableCollection<ScrapeConfig> validScrapeConfigs = ValidScrapeConfigs();

            // No config keyword entered, so show options
            if (query.SearchTerms.Length == 0)
            {
                List<Result> options = new();
                options.AddRange(
                    validScrapeConfigs.Select(x => new Result
                    {
                        Title = x.Keyword
                    })
                );
                return options;
            }

            // Find configuration with correct scrape keyword
            string scrapeKeyword = query.SearchTerms[0];
            ScrapeConfig scrapeConfig = validScrapeConfigs.FirstOrDefault(x => x.Keyword == scrapeKeyword);

            // No configuration found, so show options
            if (scrapeConfig == null)
            {
                return new List<Result>
                {
                    new()
                    {
                        Title = $"No configuration found with keyword '{scrapeKeyword}'",
                        SubTitle = $"Available configurations: {string.Join(", ", validScrapeConfigs.Select(x => x.Keyword))}"
                    }
                };
            }

            // Configuration was found, so do the API request
            HttpResponseMessage data;
            try
            {
                data = await _client.GetAsync(new Uri(scrapeConfig.Url), token);
            }
            catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
            {
                return new List<Result>
                {
                    new()
                    {
                        Title = "The HTTP request timed out",
                        SubTitle = "Check whether the URL is reachable"
                    }
                };
            }

            if (data.Content.Headers.ContentType?.MediaType.Contains("text/html") != true)
            {
                return new List<Result>
                {
                    new()
                    {
                        Title = "The URL did not return valid HTML",
                        SubTitle = "Check whether the URL returns valid HTML"
                    }
                };
            }

            string body = await data.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(body);

            var results = new List<Result>();

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
                        var htmlNodes = doc.DocumentNode.SelectNodes(variableBinding.Value);
                        
                        if (htmlNodes != null)
                        {
                            evaluatedXpathDict.Add(variableBinding.Key, new List<string>(htmlNodes.Select(x => WebUtility.HtmlDecode(x.InnerText))));
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
                    return new List<Result>
                    {
                        new()
                        {
                            Title = $"Xpaths returned incompatible element counts: {string.Join(", ", evaluatedXpathDict.Select(x => x.Value.Count).Distinct())}",
                            SubTitle = "Check whether the variable bindings are configured correctly"
                        }
                    };
                }

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
                    }
                    });
                }
            }

            return results;
        }

        public void SaveSettings()
        {
            _context.API.SavePluginSettings();
        }
    }
}