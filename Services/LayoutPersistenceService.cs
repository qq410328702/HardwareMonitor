using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HardwareMonitor.Services;

public class CardLayoutItem
{
    public string CardId { get; set; } = "";
    public int Order { get; set; }
    public bool IsVisible { get; set; } = true;
}

public class LayoutConfig
{
    public List<CardLayoutItem> Cards { get; set; } = new();
}

public interface ILayoutPersistenceService
{
    LayoutConfig Load();
    void Save(LayoutConfig config);
}

public static class LayoutSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static string Serialize(LayoutConfig config)
    {
        return JsonSerializer.Serialize(config, Options);
    }

    public static LayoutConfig Deserialize(string json)
    {
        return JsonSerializer.Deserialize<LayoutConfig>(json, Options) ?? new LayoutConfig();
    }
}

public class LayoutPersistenceService : ILayoutPersistenceService
{
    private readonly string _filePath;
    private readonly ILogger? _logger;

    public LayoutPersistenceService(string? filePath = null, ILogger? logger = null)
    {
        _logger = logger;
        if (filePath != null)
        {
            _filePath = filePath;
        }
        else
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HardwareMonitor");
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "layout.json");
        }
    }

    public LayoutConfig Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return CreateDefaultLayout();

            var json = File.ReadAllText(_filePath);
            return LayoutSerializer.Deserialize(json);
        }
        catch (JsonException ex)
        {
            _logger?.Warn($"Invalid layout JSON, using default layout: {ex.Message}");
            return CreateDefaultLayout();
        }
    }

    public void Save(LayoutConfig config)
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (dir != null)
                Directory.CreateDirectory(dir);

            var json = LayoutSerializer.Serialize(config);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            _logger?.Error("Failed to save layout config", ex);
        }
    }

    public static LayoutConfig CreateDefaultLayout()
    {
        var defaultCards = new[] { "cpu", "gpu", "memory", "disk", "network", "process", "charts", "history", "alert", "layout" };
        var config = new LayoutConfig();
        for (int i = 0; i < defaultCards.Length; i++)
        {
            config.Cards.Add(new CardLayoutItem
            {
                CardId = defaultCards[i],
                Order = i,
                IsVisible = true
            });
        }
        return config;
    }
}
