using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godot;

namespace MahjongRising.code.Mod;

/// <summary>
/// Mod 加载器。
/// 从指定目录扫描 DLL，反射查找 IMahjongMod 实现并注册。
///
/// 加载顺序：
///   1. 扫描 Mod 目录中的 .dll 文件
///   2. 加载程序集，查找实现 IMahjongMod 的类
///   3. 实例化并调用 Register（上下文中自动设置 CurrentModId）
///
/// 约定：
///   - Mod DLL 放在 user://mods/ 目录下
///   - 每个 DLL 可包含多个 IMahjongMod 实现
///   - Mod 的资源文件放在 user://mods/{modId}/ 目录下
/// </summary>
public sealed class ModLoader
{
    private readonly List<IMahjongMod> _loadedMods = new();
    private ModRegistrationContext _context = null!;

    public IReadOnlyList<IMahjongMod> LoadedMods => _loadedMods;

    /// <summary>
    /// 初始化 Mod 加载器并加载所有 Mod。
    /// </summary>
    public void LoadAll(ModRegistrationContext context, string? modDirectory = null)
    {
        _context = context;
        modDirectory ??= GetDefaultModDirectory();

        if (!Directory.Exists(modDirectory))
        {
            GD.Print($"[ModLoader] Mod 目录不存在，跳过加载: {modDirectory}");
            return;
        }

        var dllFiles = Directory.GetFiles(modDirectory, "*.dll", SearchOption.AllDirectories);
        GD.Print($"[ModLoader] 发现 {dllFiles.Length} 个 Mod DLL");

        foreach (var dllPath in dllFiles)
        {
            try
            {
                LoadModDll(dllPath);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[ModLoader] 加载 Mod 失败 ({dllPath}): {ex.Message}");
            }
        }

        GD.Print($"[ModLoader] 成功加载 {_loadedMods.Count} 个 Mod");
    }

    /// <summary>卸载所有 Mod。</summary>
    public void UnloadAll()
    {
        foreach (var mod in _loadedMods)
        {
            try
            {
                _context.CurrentModId = mod.ModId;
                mod.Unregister(_context);
                GD.Print($"[ModLoader] 已卸载 Mod: {mod.ModId}");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[ModLoader] 卸载 Mod 失败 ({mod.ModId}): {ex.Message}");
            }
        }
        _loadedMods.Clear();
        _context.CurrentModId = "";
    }

    private void LoadModDll(string dllPath)
    {
        var assembly = Assembly.LoadFrom(dllPath);
        var modTypes = assembly.GetTypes()
            .Where(t => typeof(IMahjongMod).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        foreach (var type in modTypes)
        {
            var mod = (IMahjongMod)Activator.CreateInstance(type)!;

            if (_loadedMods.Any(m => m.ModId == mod.ModId))
            {
                GD.PrintErr($"[ModLoader] Mod ID 重复，跳过: {mod.ModId}");
                continue;
            }

            // 设置当前 Mod ID，使 Mod 内可通过 context.ModResourceRoot 访问资源路径
            _context.CurrentModId = mod.ModId;
            mod.Register(_context);
            _loadedMods.Add(mod);
            GD.Print($"[ModLoader] 已加载 Mod: {mod.DisplayName} v{mod.Version} ({mod.ModId})");
        }

        _context.CurrentModId = "";
    }

    private static string GetDefaultModDirectory()
    {
        return ProjectSettings.GlobalizePath("user://mods");
    }
}