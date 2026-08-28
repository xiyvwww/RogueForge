using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using BepInEx;
using BepInEx.Bootstrap;

#nullable enable
namespace RogueForge;

/// <summary>
/// CustomBuildings 的主要 API 类。提供创建自定义建筑（ObjectReal）的工厂方法，
/// 并维护所有已注册建筑的注册表（供各 patch 遍历使用）。
/// 格式仿照 RogueLibsCore 的 <see cref="RogueLibsCore.RogueLibs"/>。
/// </summary>
public static class CustomObjects
{
    /// <summary>CustomBuildings 的 BepInEx 插件 GUID。</summary>
    public const string GUID = "xiyuw.sor.custombuildings";

    /// <summary>插件内部名称。</summary>
    internal const string Name = "CustomBuildings";

    /// <summary>已注册的自定义建筑注册表（名称 → 元数据）。</summary>
    public static readonly Dictionary<string, CustomObjectMetadata> Registry = new Dictionary<string, CustomObjectMetadata>();

    /// <summary>创建自定义建筑并返回建筑构建器。</summary>
    /// <typeparam name="TCustomObject">自定义建筑类型，必须继承 <see cref="CustomObjectReal"/>。</typeparam>
    /// <returns>建筑构建器。</returns>
    public static ObjectBuilder CreateCustomObject<TCustomObject>() where TCustomObject : CustomObjectReal, new()
    {
        CustomObjectMetadata metadata = CustomObjectMetadata.Get<TCustomObject>();
        Registry[metadata.Name] = metadata;
        if (CustomBuildingsPlugin.Logger != null)
            CustomBuildingsPlugin.LogInfo($"[CustomBuildings] CreateCustomObject<{typeof(TCustomObject).Name}> 已注册，名称={metadata.Name}");
        return new ObjectBuilder(metadata);
    }

    /// <summary>按名称获取已注册的建筑元数据。</summary>
    /// <param name="name">建筑名称。</param>
    /// <returns>建筑元数据，未找到返回 null。</returns>
    public static CustomObjectMetadata? GetObject(string name)
    {
        return Registry.TryGetValue(name, out CustomObjectMetadata? metadata) ? metadata : null;
    }

    /// <summary>按类型获取已注册的建筑元数据。</summary>
    /// <typeparam name="TCustomObject">自定义建筑类型。</typeparam>
    /// <returns>建筑元数据。</returns>
    public static CustomObjectMetadata GetObject<TCustomObject>() where TCustomObject : CustomObjectReal
        => CustomObjectMetadata.Get<TCustomObject>();

    /// <summary>注册表中是否存在指定名称的建筑。</summary>
    /// <param name="name">建筑名称。</param>
    public static bool IsRegistered(string name) => Registry.ContainsKey(name);

    /// <summary>获取所有已注册建筑的名称。</summary>
    public static IReadOnlyCollection<string> Names => Registry.Keys;

    /// <summary>
    /// 从调用方程序集加载并注册所有自定义建筑内容。
    /// 调用所有标记了 [RLSetup] 属性的方法（与 <see cref="RogueLibsCore.RogueLibs.LoadFromAssembly"/> 一致）。
    /// 注意：若 mod 已调用过 <see cref="RogueLibsCore.RogueLibs.LoadFromAssembly"/>（它同样会触发 [RLSetup]），
    /// 则**不要**再调用本方法，否则 Setup 二次执行会导致名称/精灵重复注册异常。
    /// 多 dll 场景请使用 <see cref="LoadAllPluginLibraries"/>（扫描整个 BepInEx/plugins 目录）。
    /// </summary>
    public static void LoadFromAssembly()
    {
        Assembly? callingAssembly = Assembly.GetCallingAssembly();
        foreach (Type type in callingAssembly.GetTypes())
        {
            InvokeSetupMethods(type);
        }
    }

    /// <summary>扫描指定类型中所有标记了 [RLSetup] 属性的方法并调用（与 RogueLibsCore 内部逻辑一致）。
    /// 返回实际调用成功的 [RLSetup] 方法数量。</summary>
    /// <param name="type">要扫描的类型。</param>
    private static int InvokeSetupMethods(Type type)
    {
        int count = 0;
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (method.GetCustomAttribute<RogueLibsCore.RLSetupAttribute>() != null)
            {
                if (method.GetParameters().Length != 0)
                {
                    CustomBuildingsPlugin.LogError($"{type.FullName}: Methods marked with [RLSetup] cannot have any parameters!");
                    continue;
                }
                try
                {
                    if (!method.IsStatic)
                    {
                        CustomBuildingsPlugin.LogError($"{type.FullName}: Methods marked with [RLSetup] must be static!");
                        continue;
                    }
                    method.Invoke(null, null);
                    count++;
                }
                catch (Exception ex)
                {
                    // Logger 可能尚未初始化（Initialize 未调用时），判空避免掩盖真实异常
                    CustomBuildingsPlugin.LogError(ex.ToString());
                }
            }
        }
        return count;
    }

    /// <summary>获取所有注册建筑中实现了指定接口的类型。</summary>
    /// <typeparam name="TInterface">要查找的接口类型。</typeparam>
    /// <returns>实现该接口的建筑元数据列表。</returns>
    public static IEnumerable<CustomObjectMetadata> GetObjectsWithInterface<TInterface>()
        => Registry.Values.Where(m => typeof(TInterface).IsAssignableFrom(m.Type));

    // ==================== 多 dll 插件库加载（扫描 BepInEx/plugins 目录） ====================

    /// <summary>本次会话已扫描过的插件目录程序集（按简单名），防止重复加载/注册。</summary>
    private static readonly HashSet<string> scannedPluginAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 扫描 BepInEx/plugins 目录下所有 dll，统一加载其中的 [RLSetup] 注册（多 dll 支持）。
    /// 由 <see cref="CustomBuildingsPlugin.Initialize"/> 自动调用，也可手动调用。
    ///
    /// 判定规则：
    /// - <b>已被 BepInEx 作为插件激活的 dll</b>（<see cref="Chainloader.PluginInfos"/> 中存在
    ///   Location 指向该文件的条目）→ 跳过：BepInEx 会调用其 Awake，按模板它会在 Awake 里调用
    ///   RogueLibs.LoadFromAssembly / CustomObjects.LoadFromAssembly 自行注册（避免 [RLSetup] 二次执行
    ///   导致名称/精灵重复注册异常）。
    /// - <b>纯类库 dll</b>（没有插件入口，如用户新建的 TrashCan.dll）→ BepInEx 不会加载，
    ///   本方法统一 Assembly.LoadFrom 加载并调用其 [RLSetup] 注册，让建筑在游戏中出现。
    /// - <b>插件 GUID 重复被 BepInEx 跳过的 dll</b>（重复 GUID 时 BepInEx 只激活其中一个插件，
    ///   其余 dll 即使被加载也不会创建插件实例、Awake 不会执行）→ 由本方法兜底加载注册。
    ///
    /// 幂等保护：调用前检查该类型对应的建筑名是否已在 <see cref="Registry"/> 中，已注册则跳过
    /// （防止与本 dll 自身 Awake 的注册路径重复执行）。
    /// </summary>
    public static void LoadAllPluginLibraries()
    {
        try
        {
            string pluginDir = Paths.PluginPath;
            if (string.IsNullOrEmpty(pluginDir) || !Directory.Exists(pluginDir))
            {
                CustomBuildingsPlugin.LogInfo("[CustomBuildings] 未找到 BepInEx/plugins 目录，跳过插件库扫描");
                return;
            }

            string[] dllFiles = Directory.GetFiles(pluginDir, "*.dll", SearchOption.AllDirectories);
            string selfName = typeof(CustomObjects).Assembly.GetName().Name ?? "RogueForge";
            int loaded = 0, skipped = 0, failed = 0;
            foreach (string dll in dllFiles)
            {
                string simpleName = Path.GetFileNameWithoutExtension(dll);
                if (string.IsNullOrEmpty(simpleName)) continue;

                if (!scannedPluginAssemblies.Add(simpleName)) { skipped++; continue; }   // 本会话已处理过
                if (string.Equals(simpleName, selfName, StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }   // 本库自身（无 [RLSetup]）

                try
                {
                    // 已被 BepInEx 激活的插件（Location 匹配该 dll 路径）→ 由它自己的 Awake 注册，跳过
                    if (IsActiveBepInExPlugin(dll)) { skipped++; continue; }

                    // 解析程序集：已加载则复用（避免重复实例导致类型不一致），否则 LoadFrom 加载
                    Assembly asm = FindLoadedAssembly(simpleName) ?? Assembly.LoadFrom(dll);
                    PatchResourceManager(asm);   // 与 RogueLibs 一致：给 Resources 类换缓存版资源管理器

                    int setups = 0;
                    foreach (Type type in GetLoadableTypes(asm))
                    {
                        // 幂等：该类型对应的建筑名已在 Registry（已被本 dll 或其他路径注册过）→ 跳过
                        string buildingName = type.GetCustomAttribute<ObjectNameAttribute>()?.Name ?? type.Name;
                        if (Registry.ContainsKey(buildingName)) continue;
                        setups += InvokeSetupMethods(type);
                    }
                    loaded++;
                    CustomBuildingsPlugin.LogInfo($"[CustomBuildings] 已加载插件库: {Path.GetFileName(dll)}（[RLSetup] x{setups}）");
                }
                catch (Exception ex)
                {
                    failed++;
                    CustomBuildingsPlugin.LogWarning($"[CustomBuildings] 加载插件库失败: {dll} - {ex.Message}");
                }
            }
            CustomBuildingsPlugin.LogInfo($"[CustomBuildings] 插件库扫描完成: 新加载 {loaded} 个, 跳过 {skipped} 个, 失败 {failed} 个");
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.LogError($"[CustomBuildings] 扫描插件库异常: {e}");
        }
    }

    /// <summary>该 dll 是否已被 BepInEx 作为插件激活（Chainloader.PluginInfos 中存在 Location 指向该文件的条目）。
    /// 被激活的插件由 BepInEx 调用其 Awake 自行注册，这里不应重复执行 [RLSetup]。</summary>
    private static bool IsActiveBepInExPlugin(string dllPath)
    {
        try
        {
            foreach (KeyValuePair<string, PluginInfo> kv in Chainloader.PluginInfos)
            {
                if (kv.Value != null && string.Equals(kv.Value.Location, dllPath, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }
        return false;
    }

    /// <summary>按简单名查找已加载的程序集（找不到返回 null）。</summary>
    private static Assembly? FindLoadedAssembly(string simpleName)
    {
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (string.Equals(a.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase)) return a;
            }
            catch { }
        }
        return null;
    }

    /// <summary>获取程序集可加载的类型（个别类型因依赖缺失无法加载时返回能加载的部分，不让整个 dll 注册失败）。</summary>
    private static IEnumerable<Type> GetLoadableTypes(Assembly asm)
    {
        try
        {
            return asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }

    /// <summary>
    /// 替换指定程序集中生成的 Resources 类的 ResourceManager 为缓存版（与 RogueLibs.LoadFromAssembly 的
    /// PatchResourceManager 一致），保证 [RLSetup] 里读取内嵌精灵资源（如 Mod.Properties.Resources.XXX）正常。
    /// </summary>
    private static void PatchResourceManager(Assembly asm)
    {
        try
        {
            foreach (Type type in GetLoadableTypes(asm))
            {
                if (!type.IsNotPublic || type.Name != "Resources") continue;
                FieldInfo? field = type.GetField("resourceMan", BindingFlags.Static | BindingFlags.NonPublic);
                PropertyInfo? property = type.GetProperty("ResourceManager", BindingFlags.Static | BindingFlags.Public);
                if (field == null || property == null || !property.CanRead) continue;
                ResourceManager? rm = property.GetValue(null) as ResourceManager;
                if (rm is CachedResourceManager) continue;
                field.SetValue(null, new CachedResourceManager(rm?.BaseName ?? (type.Assembly.GetName().Name + ".Properties.Resources"), type.Assembly));
            }
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.LogWarning($"[CustomBuildings] 替换资源管理器失败: {e.Message}");
        }
    }
}

/// <summary>
/// 增强的资源管理器（与 RogueLibsCore.BetterResourceManager 相同逻辑：字典缓存提升资源加载性能）。
/// 用于被 <see cref="CustomObjects.LoadAllPluginLibraries"/> 扫描的插件库程序集。
/// </summary>
internal sealed class CachedResourceManager : ResourceManager
{
    private readonly Dictionary<string, object> cache = new Dictionary<string, object>();

    public CachedResourceManager(string baseName, Assembly assembly) : base(baseName, assembly) { }

    public override object? GetObject(string name) => this.GetObject(name, CultureInfo.CurrentUICulture);

    public override object? GetObject(string name, CultureInfo culture)
    {
        object? cached;
        if (this.cache.TryGetValue(name, out cached)) return cached;
        cached = base.GetObject(name, culture);
        if (cached != null) this.cache.Add(name, cached);
        return cached;
    }

    public override void ReleaseAllResources()
    {
        this.cache.Clear();
        base.ReleaseAllResources();
    }
}
