﻿using System.Text.RegularExpressions;
using EmmyLua.CodeAnalysis.Document;
using EmmyLua.CodeAnalysis.Document.Version;
using EmmyLua.CodeAnalysis.Workspace;
using Newtonsoft.Json;


namespace EmmyLua.Configuration;

public class SettingManager
{
    public static readonly string ConfigName = ".emmyrc.json";

    private string Workspace { get; set; } = string.Empty;

    private string SettingPath => Path.Combine(Workspace, ConfigName);

    public Setting? Setting { get; private set; }

    public delegate void SettingChanged(SettingManager settingManager);

    public event SettingChanged? OnSettingChanged;

    private bool _firstLoad = true;

    private JsonSerializerSettings SerializerSettings { get; } = new()
    {
        Formatting = Formatting.Indented
    };

    private FileSystemWatcher? Watcher { get; set; }

    private System.Timers.Timer? _timer;

    public HashSet<string> WorkspaceExtensions { get; set; } = new();

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType is WatcherChangeTypes.Changed or WatcherChangeTypes.Created)
        {
            _timer?.Stop();
            _timer = new System.Timers.Timer(500); // 设置延迟时间为500毫秒
            _timer.Elapsed += (s, ee) => LoadSetting(SettingPath);
            _timer.AutoReset = false;
            _timer.Start();
        }
    }

    public void Watch(string workspace)
    {
        if (!Directory.Exists(workspace))
        {
            return;
        }

        Workspace = workspace;
        if (Watcher is null)
        {
            Watcher = new FileSystemWatcher();
            Watcher.Created += OnChanged;
            Watcher.Changed += OnChanged;
            Watcher.Path = Workspace;
            Watcher.Filter = ConfigName;
            Watcher.EnableRaisingEvents = true;
        }

        LoadSetting(SettingPath);
    }

    public void LoadSetting(string settingPath)
    {
        try
        {
            if (!File.Exists(settingPath))
            {
                return;
            }

            var fileText = File.ReadAllText(settingPath);
            // ReSharper disable once IdentifierTypo
            var setting = JsonConvert.DeserializeObject<Setting>(fileText, SerializerSettings);
            if (setting is not null)
            {
                Setting = setting;
                ProcessSetting(Setting);
            }

            if (!_firstLoad)
            {
                OnSettingChanged?.Invoke(this);
            }

            _firstLoad = false;
        }
        catch (Exception e)
        {
            // ignore
        }
    }

    private void ProcessSetting(Setting setting)
    {
        setting.Workspace.WorkspaceRoots = setting.Workspace.WorkspaceRoots.Select(PreProcessPath).ToList();
        setting.Workspace.Library = setting.Workspace.Library.Select(PreProcessPath).ToList();
        setting.Resource.Paths = setting.Resource.Paths.Select(PreProcessPath).ToList();
        if (setting.Resource.Paths.Count == 0)
        {
            setting.Resource.Paths.Add(Workspace);
        }
    }

    private string PreProcessPath(string path)
    {
        if (path.StartsWith('~'))
        {
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..]);
        }
        else if (path.StartsWith("./"))
        {
            path = Path.Combine(Workspace, path[2..]);
        }
        else if (path.StartsWith("/"))
        {
            path = Path.Combine(Workspace, path.TrimStart('/'));
        }

        path = path.Replace("${workspaceFolder}", Workspace);
        return Path.GetFullPath(path);
    }

    public LuaFeatures GetLuaFeatures()
    {
        var features = new LuaFeatures();
        if (Setting is null)
        {
            return features;
        }

        var setting = Setting;
        features.ExcludeFolders.UnionWith(setting.Workspace.IgnoreDir);
        features.DontIndexMaxFileSize = setting.Workspace.PreloadFileSize;
        features.ThirdPartyRoots.AddRange(setting.Workspace.Library);
        features.WorkspaceRoots.AddRange(setting.Workspace.WorkspaceRoots);
        features.Language = new LuaLanguage(setting.Runtime.Version switch
        {
            LuaVersion.Lua51 => LuaLanguageLevel.Lua51,
            LuaVersion.LuaJIT => LuaLanguageLevel.LuaJIT,
            LuaVersion.Lua52 => LuaLanguageLevel.Lua52,
            LuaVersion.Lua53 => LuaLanguageLevel.Lua53,
            LuaVersion.Lua54 => LuaLanguageLevel.Lua54,
            LuaVersion.LuaLatest => LuaLanguageLevel.LuaLatest,
            _ => LuaLanguageLevel.Lua54
        });
        features.DiagnosticConfig.Globals.UnionWith(setting.Diagnostics.Globals);
        features.DiagnosticConfig.WorkspaceDisabledCodes.UnionWith(setting.Diagnostics.Disable);
        features.DiagnosticConfig.WorkspaceEnabledCodes.UnionWith(setting.Diagnostics.Enables);
        foreach (var globalRegexString in setting.Diagnostics.GlobalsRegex)
        {
            try
            {
                var regex = new Regex(globalRegexString);
                features.DiagnosticConfig.GlobalRegexes.Add(regex);
            }
            catch
            {
                // ignore
            }
        }

        foreach (var (code, severity) in setting.Diagnostics.Severity)
        {
            features.DiagnosticConfig.SeverityOverrides[code] = severity;
        }

        features.RequireLikeFunction.UnionWith(setting.Runtime.RequireLikeFunction);
        foreach (var framework in setting.Runtime.FrameworkVersions)
        {
            if (FrameworkVersion.TryParse(framework, out var version))
            {
                features.FrameworkVersions.Add(version);
            }
        }

        foreach (var extension in setting.Runtime.Extensions.Concat(WorkspaceExtensions))
        {
            if (extension.StartsWith('.'))
            {
                features.Extensions.Add($"*{extension}");
            }
            else if (extension.StartsWith("*."))
            {
                features.Extensions.Add(extension);
            }
            else
            {
                features.Extensions.Add($"*.{extension}");
            }
        }

        if (setting.Runtime.RequirePattern.Count > 0)
        {
            features.RequirePattern.Clear();
            features.RequirePattern.AddRange(setting.Runtime.RequirePattern);
        }

        foreach (var extension in features.Extensions)
        {
            var hashSet = features.RequirePattern.ToHashSet();
            var newPattern = extension.Replace("*", "?");
            if (!hashSet.Contains(newPattern))
            {
                features.RequirePattern.Add(newPattern);
            }
        }

        features.RequirePathStrict = setting.Strict.RequirePath;
        features.TypeCallStrict = setting.Strict.TypeCall;

        return features;
    }

    public void Save(Setting setting)
    {
        if (setting.Schema is null)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "schema.json");
            setting.Schema = (new Uri(path)).AbsoluteUri;
        }

        var json = JsonConvert.SerializeObject(setting, SerializerSettings);
        File.WriteAllText(SettingPath, json);
    }
}
