using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Plugin.WebScraper.Models;

namespace Flow.Launcher.Plugin.WebScraper.Views;

public partial class SettingsControl : UserControl
{
    public Settings Settings { get; }
    public string ScrapeConfigsJson { get; set; }
    public string Timeout { get; set;}

    public SettingsControl(Settings settings)
    {
        InitializeComponent();
        Settings = settings;
        DataContext = this;
        ScrapeConfigsJson = JsonSerializer.Serialize(Settings.ScrapeConfigs, new JsonSerializerOptions { WriteIndented = true });
        Timeout = Settings.Timeout.ToString();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var newScrapeConfigs = JsonSerializer.Deserialize<ObservableCollection<ScrapeConfig>>(ScrapeConfigsJson) ?? new ObservableCollection<ScrapeConfig>();
            var newTimeoutValid = int.TryParse(Timeout, out int newTimeout) && newTimeout > 0;
            
            if (!newTimeoutValid)
            {
                throw new InvalidOperationException("The timeout must be a positive integer.");
            }

            Settings.ScrapeConfigs = newScrapeConfigs;
            Settings.Timeout = newTimeout;
        }
        catch (JsonException ex)
        {
            MessageBox.Show("Invalid JSON: " + ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message);
        }
    }
}