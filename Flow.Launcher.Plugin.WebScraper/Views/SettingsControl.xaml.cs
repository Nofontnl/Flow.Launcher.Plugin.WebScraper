using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Plugin.WebScraper.Models;

namespace Flow.Launcher.Plugin.WebScraper.Views;

public partial class SettingsControl : UserControl
{
    public Settings Settings { get; }
    public string SettingsJson { get; set; }

    public SettingsControl(Settings settings)
    {
        InitializeComponent();
        Settings = settings;
        DataContext = this;
        SettingsJson = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var updated = JsonSerializer.Deserialize<Settings>(SettingsJson);
            if (updated != null)
            {
                Settings.ScrapeConfigs = updated.ScrapeConfigs;
            }
        }
        catch (JsonException ex)
        {
            MessageBox.Show("Invalid JSON: " + ex.Message);
        }
    }
}