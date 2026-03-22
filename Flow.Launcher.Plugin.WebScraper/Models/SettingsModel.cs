using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Plugin.WebScraper.Models;

public class ScrapeResult
{
    public string Title { get; set; } = "";
    public string SubTitle { get; set; } = "";
    public Dictionary<string, string> VariableBindings {get; set; } = new();
}

public class ScrapeConfig
{
    public string Keyword { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Url { get; set; } = "";
    public ObservableCollection<ScrapeResult> ScrapeResults { get; set; } = new();
}

public class Settings : BaseModel
{
    public ObservableCollection<ScrapeConfig> ScrapeConfigs { get; set; } = new();
    public int Timeout { get; set; } = 15;
}