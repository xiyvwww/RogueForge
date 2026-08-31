using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Logging;
using RogueLibsCore;
using UnityEngine;
using UnityEngine.UI;

#nullable enable
namespace RogueForge;

/// <summary>
/// CustomBuildings 的初始化与 Harmony patch 宿主。
/// 注意：本类**不是**独立 BepInEx 插件（BepInEx 5 不会加载 mod 子目录里的 dll 为插件）。
/// 由 mod 在 <see cref="BaseUnityPlugin.Awake"/> 中显式调用 <see cref="Initialize"/> 注册全部 patch，
/// patch 方法体位于本类，但通过宿主插件的 RoguePatcher 注册。
/// </summary>
public static class CustomBuildingsPlugin
{
    /// <summary>库的日志源（Initialize 时从宿主插件获取）。</summary>
    public static ManualLogSource Logger = null!;

    /// <summary>
    /// 输出调试级别日志信息（仅在 DEBUG 编译模式下生效），统一以 [RogueForge] 开头。
    /// 消息内容可自带模块前缀（如 "[KMap] ..."），输出形如 "[RogueForge] [KMap] ..."。
    /// </summary>
    /// <param name="message">要记录的日志消息。</param>
    [Conditional("DEBUG")]
    internal static void LogInfo(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (Logger == null) return;
        Logger.LogInfo("[RogueForge] " + message);
    }

    /// <summary>
    /// 输出警告级别日志信息（所有编译模式均生效），统一以 [RogueForge] 开头。
    /// </summary>
    /// <param name="message">要记录的日志消息。</param>
    internal static void LogWarning(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (Logger == null) return;
        Logger.LogWarning("[RogueForge] " + message);
    }

    /// <summary>
    /// 输出错误级别日志信息（所有编译模式均生效），统一以 [RogueForge] 开头。
    /// </summary>
    /// <param name="message">要记录的日志消息。</param>
    internal static void LogError(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (Logger == null) return;
        Logger.LogError("[RogueForge] " + message);
    }

    /// <summary>是否已初始化（防止重复注册 patch）。</summary>
    private static bool initialized;

    /// <summary>
    /// 左下角版本签名文本（显示在游戏版本号右下侧，追加在 RogueLibs 签名之后，不遮挡）。
    /// 参考 RogueLibsCore：GameController.SetVersionText Postfix 往 versionText2.text 追加 "RL v4.0.0-rc.2"。
    /// 修改此字符串即可自定义显示内容（例如改成你自己的 Mod 名和版本号）。
    /// </summary>
    public static string VersionSignature = "RF 1.3";



    /// <summary>
    /// 初始化 CustomBuildings：注册全部 Harmony patch（prefab 注册、编辑器注入、网格重画、生成重建），
    /// 并自动扫描 BepInEx/plugins 目录加载所有插件库 dll（多 dll 支持）。
    /// 应在 mod 插件 Awake 中调用，且在 <see cref="CustomObjects.LoadFromAssembly"/> 之后。
    /// </summary>
    /// <param name="host">宿主 BepInEx 插件实例（mod 的插件）。</param>
    public static void Initialize(BaseUnityPlugin host)
    {
        if (host == null) throw new ArgumentNullException(nameof(host));
        if (initialized) return;
        initialized = true;

        // host.Logger 受保护，用反射读取（与 RoguePatcher 内部一致）
        Logger = (ManualLogSource)typeof(BaseUnityPlugin).GetProperty("Logger",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            .GetValue(host);

        // 多 dll 支持：扫描 BepInEx/plugins 目录，统一加载所有尚未被 CLR 加载的插件库 dll
        // （如用户新建的 TrashCan.dll / 纯类库 dll）中的 [RLSetup] 注册，让其中的自定义建筑在游戏中出现。
        // 已被 BepInEx 作为插件加载的 dll（如 MyAwesomeMod.dll）由它们自己的 Awake 完成注册，这里跳过避免二次注册。
        CustomObjects.LoadAllPluginLibraries();

        // 关键：指定 patch 方法所在类型为本类（而非 host.GetType()），
        // 这样 patch 方法保留在库中，通过宿主插件的 Harmony 实例注册。
        RoguePatcher patcher = new RoguePatcher(host, typeof(CustomBuildingsPlugin));

        LogInfo($"[CustomBuildings] Initialize: Registry 当前内容 = {CustomObjects.Names.Count} 个: [{string.Join(", ", CustomObjects.Names)}]");

        // prefab 注册（GameResources.SetupDics 后把所有注册建筑注册进字典）
        bool r1 = patcher.Postfix(typeof(GameResources), nameof(GameResources.SetupDics), "GameResources_SetupDics");

        // objectVarDic 注册（BasicObject.Spawn 会查 objectVars.objectVarDic[name]，缺条目会 KeyNotFound 卡加载）
        bool r2 = patcher.Postfix(typeof(ObjectVars), "Awake", "ObjectVars_Awake");

        // 编辑器物件放置面板注入（2 参重载；3 参会 Sort 打乱，需 Postfix 重排）
        bool r3 = patcher.Prefix(typeof(LevelEditor), nameof(LevelEditor.OpenObjectLoad), "LevelEditor_OpenObjectLoad", new Type[] { typeof(List<string>), typeof(List<string>) });
       
        bool r4 = patcher.Postfix(typeof(LevelEditor), nameof(LevelEditor.OpenObjectLoad), "LevelEditor_OpenObjectLoadPostfix", new Type[] { typeof(List<string>), typeof(List<string>) });

        // 编辑器网格重画 + materialInsts 修复
        bool r5 = patcher.Postfix(typeof(LevelEditor), nameof(LevelEditor.SetTileImage), "LevelEditor_SetTileImage");

        // 物件生成入口（prefab 失效重建兜底）
        bool r6 = patcher.Prefix(typeof(BasicObject), nameof(BasicObject.Spawn), "BasicObject_Spawn");
        // 标记"编辑器/瓦片放置"生成的建筑（IsEditorPlaced）——关卡加载清理时保留它们
        bool r6b = patcher.Postfix(typeof(BasicObject), nameof(BasicObject.Spawn), "BasicObject_SpawnPostfix",
            new Type[] { typeof(SpawnerBasic), typeof(string), typeof(Vector2), typeof(Vector2), typeof(Chunk) });

        // 诊断钩子：Bed.Interact（验证"近距离才交互"机制——记录触发时玩家与床的距离）
        bool r7 = patcher.Prefix(typeof(Bed), nameof(Bed.Interact), "Bed_Interact", new Type[] { typeof(Agent) });

        // 名称显示修复：NameDB.GetName 找不到条目时返回 "E_"+名称（错误标记），
        // Postfix 去掉错误前缀 "E_"（只去第一个），避免按钮/界面文本显示 E_#sym:xxx。
        bool r8 = patcher.Postfix(typeof(NameDB), "GetName", "NameDB_GetName");

        // 关卡加载开始前：先销毁所有旧的自定义建筑实例，避免跨层残留导致小地图标记不刷新/建筑越积越多
        bool r8b = patcher.Prefix(typeof(LoadLevel), "SetupMore4", "LoadLevel_SetupMore4_DestroyOldBuildings");

        // 关卡加载完成（LoadLevel.SetupMore4 每关 100% 时调用）：
        // 重置所有存活自定义建筑实例的容器填充状态并重新填充。
        // 解决"退出回主菜单再重新进入显示空空如也"——重新进关卡会重新填充。
        bool r9 = patcher.Postfix(typeof(LoadLevel), "SetupMore4", "LoadLevel_SetupMore4");

        // 购买价格显示：原版 InvSlot.UpdateInvSlot 的价格显示分支硬编码了
        // LoadoutMachine/ATMMachine 的 objectName，自定义建筑（如 RecycleBin）不在分支内 →
        // 购买界面物品价格不显示（左上角显示 0/数量）。
        // Postfix 检测自定义建筑购买界面，手动修正价格文本（$ + determineMoneyCost）。
        // 注意：InvSlot 未被 RogueLibsPatcher DMD 重写（RogueLibs 用 Harmony patch 它），钩子有效。
        bool r10 = patcher.Postfix(typeof(InvSlot), "UpdateInvSlot", "InvSlot_UpdateInvSlot");

        // 购买回调：拦截 InvSlot.BuyItem（玩家右键点击商店物品触发的原版自动购买），
        // 自定义建筑商店改为触发 IStore.OnItemBought(item, buyer) 用户回调——由用户端判断是否购买。
        // 用户回调中调用 IStoreExtensions.PurchaseItem 执行购买（扣钱+移货）。
        bool r11 = patcher.Prefix(typeof(InvSlot), "BuyItem", "InvSlot_BuyItem");

        // 普通关卡建筑刷新：LoadLevel.SetupMore4 在每关 100%（物体环境生成完毕后）调用，
        // 此时区块、玩家、StartingPoint/ExitPoint 全部就绪，最适合生成自定义建筑。
        // 遍历所有实现 IBuildingSpawner 的注册建筑，调用 OnLevelSpawn 让自定义建筑像原版建筑一样在普通关卡自动出现。
        bool r12 = patcher.Postfix(typeof(LoadLevel), "SetupMore4", "LoadLevel_SetupMore4_SpawnBuildings");

        // 左下角版本签名：参考 RogueLibsCore，在 GameController.SetVersionText 后把 VersionSignature 追加到
        // versionText2.text（左下角版本号文本）。追加在 RogueLibs 签名之后靠右显示，互不遮挡。
        bool r13 = patcher.Postfix(typeof(GameController), "SetVersionText", "GameController_SetVersionText");

        // 存档兜底：原版在存档读不出来时（Unlocks.CopyToCorrupted）会把 CloudData/BackupData 里的原档
        // 移动/替换到 Corrupted 目录（File.Replace），导致玩家存档文件消失；改成"只复制不移动"，
        // 无论任何原因读档失败，原档都保留在原地，玩家重装 mod 后仍可恢复。
        bool r14 = patcher.Prefix(typeof(Unlocks), "CopyToCorrupted", "Unlocks_CopyToCorrupted",
            new Type[] { typeof(string), typeof(string), typeof(string) });

        // 官方交互系统：注册自定义建筑交互提供者（RogueLibsPatcher hook 驱动，绕开 DMD）
        // 它同时接管了高亮：RogueLibs 拦截 PlayfieldObject.interactable getter → IsInteractable()
        // → 检测到我们的按钮 → 返回 true → 游戏原生高亮（无需强制高亮 hack）。
        CustomObjectReal.RegisterInteractions();

        // 入侵门禁：默认所有自定义建筑【不可被入侵】——原版 HackObject 不检查 hackable，
        // 任何 functional 的 ObjectReal 都能被黑客工具/笔记本入侵。以下三个入口全部拦截：
        //  ① HackObject（远程按 E → InteractFarHook 的汇聚点，弹进度条/弹按钮）
        //  ② LaptopHack（用笔记本电脑点击建筑 → ItemFunctions.TargetObject 的路径）
        //  ③ HackingToolHack（用黑客工具点击建筑 → ItemFunctions.TargetObject 的路径）
        // 未 override CanBeHacked=true 且未 override OnHackingComplete 的自定义建筑，入侵无任何效果。
        // 另在 CustomObjectReal.FinishedOperating 里还有一道兜底门禁（进度条即使启动也不执行效果）。
        bool r15 = patcher.Prefix(typeof(ObjectReal), nameof(ObjectReal.HackObject), "ObjectReal_HackObject", new Type[] { typeof(Agent) });
        bool r16 = patcher.Prefix(typeof(ObjectReal), nameof(ObjectReal.LaptopHack), "ObjectReal_LaptopHack", new Type[] { typeof(Agent) });
        bool r17 = patcher.Prefix(typeof(ObjectReal), nameof(ObjectReal.HackingToolHack), "ObjectReal_HackingToolHack", new Type[] { typeof(Agent) });

        // 交互结束后恢复小地图标记：自定义建筑交互（打开/关闭容器等）会使标记被隐藏/销毁，
        // 原版建筑无此问题（它们没有 NonQuestObject 标记或不受框架标记创建路径影响）。
        // 注意：必须用 Prefix 而非 Postfix——RogueLibs 的 PlayfieldObject_StopInteraction Prefix
        // 在 useModelStopInteraction=true 时会 return false 跳过原方法，Postfix 因此不触发。
        // Prefix 在所有前缀之前执行，即使 RL 后面跳过原方法，恢复也已生效。
        bool r18 = patcher.Prefix(typeof(PlayfieldObject), nameof(PlayfieldObject.StopInteraction), "PlayfieldObject_StopInteraction_MarkerRestore");

        // 空箱销毁标记拦截（根因修复）：MakeChestNonInteractable 是原版"空箱销毁任务标记"的唯一汇聚点
        // （HideChest / ShowChest空箱 / InvDatabase / ObjectMult RPC 客户端全都调它）。
        // 原版在容器变空时 Object.Destroy(nonQuestObjectMarker)；框架所有 CustomObjectReal 实例
        // chestReal=true（TryFillContainer 统一设置）→ 任意交互后标记被销毁、图标消失。
        // Prefix 暂存+置空标记引用防销毁，Postfix 恢复引用并强制可见（见方法注释的根因说明）。
        bool r19 = patcher.Prefix(typeof(PlayfieldObject), nameof(PlayfieldObject.MakeChestNonInteractable), "PlayfieldObject_MakeChestNonInteractable_Prefix");
        bool r19b = patcher.Postfix(typeof(PlayfieldObject), nameof(PlayfieldObject.MakeChestNonInteractable), "PlayfieldObject_MakeChestNonInteractable_Postfix");

        // 注意：不再用 Harmony 钩子 patch PlayfieldObject/ObjectReal 的交互方法——
        // 它们被 RogueLibsPatcher 的 DMD 技术重写，钩子打空不触发。
        // 交互与高亮都改用 RogueLibs 官方 RogueInteractions.CreateProvider 机制。
        // （HackObject 未被 DMD 重写，Harmony Prefix 有效。）

        LogInfo($"[RogueForge] 初始化完成，20 个 patch 注册结果: {r1}{r2}{r3}{r4}{r5}{r6}{r6b}{r7}{r8}{r8b}{r9}{r10}{r11}{r12}{r13}{r14}{r15}{r16}{r17}{r18}{r19}{r19b}");
        LogInfo($"[RogueForge] 如果遇到有关于RogueForge的报错，请先自行翻阅RogueForge错误手册。");
    }

    // ==================== 玩家小地图图标永远最上层 ====================
    // 原版：玩家标记是 minimap 子物体里最后加入的 → Unity UI 后加入的在上层 → 玩家图标永远盖住其他标记。
    // mod 创建大量自定义建筑标记（150+ 水晶等）后，它们排在玩家标记之后 → 玩家图标被盖住。
    // 这里把玩家标记 SetAsLastSibling 移回最上层（小图 minimap + 大地图 minimapBig）。

    /// <summary>保证玩家小地图图标在所有标记之上（小图 + 大地图）。幂等：已在最上层则跳过。</summary>
    public static void EnsurePlayerMarkerOnTop()
    {
        try
        {
            GameController gc = GameController.gameController;
            if (gc == null) return;
            if (gc.minimap != null) MovePlayerMarkerToTop(gc.minimap.transform);
            if (gc.minimapBig != null) MovePlayerMarkerToTop(gc.minimapBig.transform);
        }
        catch { }
    }

    private static void MovePlayerMarkerToTop(Transform minimapRoot)
    {
        Transform pm = minimapRoot.Find("PlayerMarker");
        if (pm != null && pm.GetSiblingIndex() != minimapRoot.childCount - 1)
            pm.SetAsLastSibling();
    }

    /// <summary>[Prefix] PlayfieldObject.StopInteraction — 自定义建筑交互结束后恢复小地图标记。
    /// 交互（打开/关闭容器等）会使框架创建的 NonQuestObject 标记被隐藏（colorInvis）或销毁；
    /// 原版有标记的建筑不受影响。用 Prefix 保证在 RogueLibs 的同名前缀拦截原方法前先执行恢复。</summary>
    public static bool PlayfieldObject_StopInteraction_MarkerRestore(PlayfieldObject __instance)
    {
        if (__instance is CustomObjectReal custom)
        {
            custom.ReensureMinimapMarker();
        }
        return true;
    }

    // ==================== Patch: 容器清空时保留自定义建筑小地图标记（根因修复） ====================
    // 根因（原版任务标记系统）：PlayfieldObject.HideChest（StopInteraction 流程内）在容器变空时调用
    // MakeChestNonInteractable()，其条件为 objectInvDatabase 非空 && chestReal && isEmpty() && hasInteracted。
    // 框架 TryFillContainer 对所有 CustomObjectReal 实例统一设 chestReal=true + 挂 objectInvDatabase
    // （即使垃圾桶这种只走 ShowUseOn 的建筑也一样）→ 玩家与任意自定义建筑交互结束后，
    // 原版都会 Object.Destroy(nonQuestObjectMarker)（帧末生效）→ 图标永久消失。
    // 之前 r18 在 StopInteraction Prefix 里恢复标记，但恢复发生在销毁之前，随后被销毁动作覆盖 → 无效。
    // 正确修法：在唯一的销毁汇聚点 MakeChestNonInteractable 拦截——
    // ① Prefix 把标记引用暂存到实例字段、置空字段 → 原版跳过销毁（其余逻辑照常：变"已空"、
    //   关灯、不可高亮、RpcMakeChestNonInteractable 同步客户端）；
    // ② Postfix 恢复引用并强制可见 + 玩家图标最上层。
    // 原版建筑不受影响（非 CustomObjectReal 不拦截），原版"空箱后图标消失"行为保持不变。

    /// <summary>[Prefix] PlayfieldObject.MakeChestNonInteractable — 自定义建筑暂存标记并阻止原版销毁。</summary>
    public static bool PlayfieldObject_MakeChestNonInteractable_Prefix(PlayfieldObject __instance)
    {
        if (__instance is CustomObjectReal custom)
        {
            custom._chestMarkerBackup = __instance.nonQuestObjectMarker;
            __instance.nonQuestObjectMarker = null;
        }
        return true;
    }

    /// <summary>[Postfix] PlayfieldObject.MakeChestNonInteractable — 自定义建筑恢复标记引用并强制可见。</summary>
    public static void PlayfieldObject_MakeChestNonInteractable_Postfix(PlayfieldObject __instance)
    {
        if (__instance is CustomObjectReal custom)
        {
            QuestMarker? m = custom._chestMarkerBackup;
            custom._chestMarkerBackup = null;
            if (m != null && m.gameObject != null)
            {
                __instance.nonQuestObjectMarker = m;
                custom.ReensureMinimapMarker();
            }
        }
    }

    // ==================== Patch: 自定义建筑入侵门禁（HackObject / LaptopHack / HackingToolHack） ====================
    // 原版 HackObject 不检查 hackable：任何 functional 的 ObjectReal 都能被黑客工具/笔记本远程入侵
    // （Hacker 职业直接弹按钮，其余职业弹 2 秒进度条）→ 导致"任意自定义建筑都能被入侵"。
    // 三个 Prefix 全部拦截：未启用入侵的自定义建筑（未 override CanBeHacked=true 且未 override
    // OnHackingComplete）直接无任何效果；原版建筑与已启用入侵的自定义建筑不受影响。

    /// <summary>[Prefix] ObjectReal.HackObject — 远程按 E 入侵汇聚点（InteractFarHook 调它）。</summary>
    public static bool ObjectReal_HackObject(ObjectReal __instance, Agent agent) => AllowBuildingHack(__instance);

    /// <summary>[Prefix] ObjectReal.LaptopHack — 用笔记本电脑点击建筑的入侵路径。</summary>
    public static bool ObjectReal_LaptopHack(ObjectReal __instance, Agent agent) => AllowBuildingHack(__instance);

    /// <summary>[Prefix] ObjectReal.HackingToolHack — 用黑客工具点击建筑的入侵路径。</summary>
    public static bool ObjectReal_HackingToolHack(ObjectReal __instance, Agent agent) => AllowBuildingHack(__instance);

    /// <summary>入侵门禁统一判定：非自定义建筑放行；自定义建筑需 override CanBeHacked 或 OnHackingComplete 才放行。</summary>
    private static bool AllowBuildingHack(ObjectReal obj)
    {
        if (obj is CustomObjectReal custom)
        {
            bool enabled = custom.CanBeHacked || custom.IsHackingEnabledByOverride();
            if (!enabled)
            {
                LogWarning($"[CustomBuildings] 入侵门禁拦截：{custom.ObjectName}（未启用 CanBeHacked/OnHackingComplete）");
                return false;
            }
        }
        return true;
    }

    // ==================== Patch: GameController.SetVersionText（左下角版本签名） ====================
    // 参考 RogueLibsCore：把自定义签名追加到左下角版本号文本 versionText2（GameController.SetVersionText 后）。
    // RogueLibs 已把签名追加成 "SoR xx, RL v4.0.0-rc.2"；本方法在其后继续追加 " , MyAwesomeMod v0.1.0"，
    // 文本靠右排在同一行末尾，不遮挡 RogueLibs 签名。幂等：已含签名则跳过（SetVersionText 可能被多次调用）。

    /// <summary>[Postfix] GameController.SetVersionText — 在左下角版本号末尾追加自定义签名。</summary>
    public static void GameController_SetVersionText(GameController __instance)
    {
        try
        {
            if (__instance == null || __instance.versionText2 == null) return;
            Text t = __instance.versionText2;
            if (string.IsNullOrEmpty(t.text) || t.text.Contains(VersionSignature)) return;   // 幂等：防重复追加
            t.text = t.text + " , " + VersionSignature;
        }
        catch (Exception e)
        {
            LogWarning($"[CustomBuildings] GameController.SetVersionText 钩子异常: {e.Message}");
        }
    }

    // ==================== Patch: Unlocks.CopyToCorrupted（存档损坏恢复保护，兜底） ====================
    // 原版流程：存档读不出来时（SaveGame.Load catch）调用 CopyToCorrupted 把损坏档"归档"到 Corrupted 目录。
    // 第一次失败是 File.Copy（原档保留），但 Corrupted 里已有同名文件时走 File.Replace（Unlocks.cs 1257 行）
    // —— 会把 CloudData/BackupData 里的原档【移动】进 Corrupted，玩家的存档文件就此消失；
    // 若玩家随后在无 mod 状态继续玩，退出时 Save() 还会用残缺状态覆写，mod 内容永久丢失。
    // 本补丁把"移动/替换"改为"只复制"：原档永远保留在原地，只往 Corrupted 放一份副本，
    // 任何原因（mod 卸载、特质序列化失败、版本不兼容等）导致的读档失败都不会让存档文件消失，
    // 玩家重装 mod 后原档仍在、可恢复。

    /// <summary>[Prefix] Unlocks.CopyToCorrupted — 只复制不移动，保留原存档文件（兜底）。</summary>
    public static bool Unlocks_CopyToCorrupted(Unlocks __instance, string myFileName, string myFileNameOnly, string saveSlot)
    {
        try
        {
            GameController? gc = GameController.gameController;
            if (gc == null) return true;

            // 与原版一致的存档根目录解析
            string dataBasePath = Application.persistentDataPath;
            if (gc.usingMyDocuments && !gc.macVersion && !gc.linuxVersion && !gc.usingUWP)
            {
                dataBasePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/" + gc.dataFolder;
            }

            string sourcePath = dataBasePath + myFileName;
            if (!File.Exists(sourcePath)) return false;   // 原档不存在，无需处理

            // 确保 Corrupted 目录存在
            string corruptedDir = dataBasePath + "/Corrupted/";
            if (!Directory.Exists(corruptedDir))
            {
                Directory.CreateDirectory(corruptedDir);
            }

            // 只复制到 Corrupted，绝不动原档（原版此处 File.Replace 会移走/覆盖原档）
            string destPath = corruptedDir + saveSlot + myFileNameOnly;
            File.Copy(sourcePath, destPath, overwrite: true);

            return false;   // 已处理，拦截原版
        }
        catch (Exception e)
        {
            LogWarning($"[CustomBuildings] CopyToCorrupted 保护异常: {e.Message}");
            return true;    // 异常时放行原版，避免影响游戏
        }
    }

    // ==================== Patch: InvSlot.BuyItem（购买回调拦截） ====================
    // 原版：玩家右键点击商店物品 → BuyItem() → 自动扣钱+移货。
    // 自定义建筑（IStore）：拦截原版自动购买，改为触发 OnItemBought(item, buyer) 回调，
    // 由用户端判断是否购买（用户回调里调用 PurchaseItem 完成购买）。

    /// <summary>[Prefix] InvSlot.BuyItem — 自定义建筑商店：拦截自动购买，触发用户回调。</summary>
    public static bool InvSlot_BuyItem(InvSlot __instance)
    {
        try
        {
            // 仅拦截自定义建筑商店的购买
            Agent agent = __instance.agent;
            if (agent == null || __instance.slotType != "NPCChest") return true; // 非商店槽位，放行原版
            if (agent.interactionHelper == null || agent.interactionHelper.interactionObjectReal == null) return true;
            ObjectReal objReal = agent.interactionHelper.interactionObjectReal;
            if (!(objReal is IStore store)) return true; // 非自定义建筑商店，放行原版

            // 玩家选中的商品
            InvItem item = __instance.item;
            if (item == null || string.IsNullOrEmpty(item.invItemName)) return false; // 空物品，拦截

            // 触发用户回调（由用户端判断是否购买；回调内可调 PurchaseItem 完成购买）
            try
            {
                store.OnItemBought(item, agent);
            }
            catch (Exception eCb)
            {
                CustomBuildingsPlugin.LogWarning($"[{objReal.objectName}] OnItemBought 回调异常: {eCb.Message}");
            }

            return false; // 拦截原版自动购买（购买已由用户回调决定）
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.LogWarning($"[CustomBuildings] InvSlot.BuyItem 钩子异常: {e.Message}");
            return true; // 异常时放行原版，避免卡死
        }
    }

    // ==================== Patch: InvSlot.UpdateInvSlot（购买价格显示） ====================
    // 原版价格显示分支硬编码 LoadoutMachine/ATMMachine，自定义建筑（IStore）不在分支内 →
    // 购买界面物品价格不显示。此 Postfix 在 UpdateInvSlot 后修正价格文本。

    
    /// <summary>[Postfix] InvSlot.UpdateInvSlot — 自定义建筑购买界面修正价格显示（$ + determineMoneyCost）。</summary>
    public static void InvSlot_UpdateInvSlot(InvSlot __instance)
    {
        try
        {
            // 仅处理 NPC 商店槽位（购买界面）
            if (__instance == null || __instance.slotType != "NPCChest") return;
            Agent agent = __instance.agent;
            if (agent == null || agent.worldSpaceGUI == null || !agent.worldSpaceGUI.openedNPCChest) return;
            if (agent.interactionHelper == null || agent.interactionHelper.interactionObjectReal == null) return;

            // 仅处理实现 IStore 的自定义建筑
            ObjectReal objReal = agent.interactionHelper.interactionObjectReal;
            if (!(objReal is IStore)) return;

            // 槽位对应商品：NPCChest 槽位的商品来自 invInterface.chestDatabase（ShowNPCChest 设置）
            InvDatabase? chestDb = agent.mainGUI != null && agent.mainGUI.invInterface != null
                ? agent.mainGUI.invInterface.chestDatabase : null;
            if (chestDb == null || chestDb.InvItemList == null
                || __instance.slotNumber < 0 || __instance.slotNumber >= chestDb.InvItemList.Count) return;
            InvItem item = chestDb.InvItemList[__instance.slotNumber];
            if (item == null || string.IsNullOrEmpty(item.invItemName)) return;

            // 注意：放在所有 return 分支之前，保证自定义价格 override 的槽位也染色。
            IStore store = (IStore)objReal;
            Color? overrideColor = null;
            if (__instance.backgroundImage2 != null)
            {
                switch (__instance.slotNumber)
                {
                    case 0: overrideColor = store.PriceOverrideColor1; break;
                    case 1: overrideColor = store.PriceOverrideColor2; break;
                    case 2: overrideColor = store.PriceOverrideColor3; break;
                    case 3: overrideColor = store.PriceOverrideColor4; break;
                    case 4: overrideColor = store.PriceOverrideColor5; break;
                }
                if (overrideColor != null)
                {
                    __instance.backgroundImage2.enabled = true;
                    __instance.backgroundImage2.color = (Color)overrideColor;
                }
                
            }

            // 自定义价格覆盖：IStore 的 5 个价格变量对应商店 5 个槽位（slotNumber 0-4），
            // 对应位置变量非空时，用该变量内容直接显示（可任意，如 "￥50"、"免费"、"x2"）。
            // 注意：必须先于 itemValue==0 隐藏判断——免费商品（itemValue=0）也允许用 override 显示价格。
            string? overrideText = null;
            switch (__instance.slotNumber)
            {
                case 0: overrideText = store.PriceOverride1; break;
                case 1: overrideText = store.PriceOverride2; break;
                case 2: overrideText = store.PriceOverride3; break;
                case 3: overrideText = store.PriceOverride4; break;
                case 4: overrideText = store.PriceOverride5; break;
            }

            // ===== 第二层防护 =====
            // 第一层（上面）已立即设置标签/颜色；再挂一个短暂延迟的校验协程：
            // 若第一层的设置被其他补丁/刷新逻辑覆盖（未生效），则重新设置一遍。
            // 同一槽位只挂一个协程（pendingPriceVerify 标记），避免重复堆积。
            if ((overrideColor != null || !string.IsNullOrEmpty(overrideText))
                && !pendingPriceVerify.TryGetValue(__instance, out _))
            {
                try
                {
                    pendingPriceVerify.Add(__instance, new object());
                    __instance.StartCoroutine(VerifyPriceOverrideCoroutine(
                        __instance, store, __instance.slotNumber, item.invItemName, overrideText, overrideColor));
                }
                catch (Exception eC)
                {
                    pendingPriceVerify.Remove(__instance);
                    LogWarning($"[CustomBuildings] 启动第二层价格校验协程失败: {eC.Message}");
                }
            }

            if (!string.IsNullOrEmpty(overrideText))
            {
                __instance.toolbarNumText.enabled = true;
                __instance.toolbarNumText.text = overrideText;
                return;
            }

            // 默认定价：交易类型=本建筑名（determineMoneyCost 默认分支：原价 + 关卡缩放）
            int cost = objReal.determineMoneyCost(item, item.itemValue, objReal.objectName);
            __instance.toolbarNumText.enabled = true;
            __instance.toolbarNumText.text = "$" + cost;
        }
        catch (Exception e)
        {
            LogWarning($"[CustomBuildings] InvSlot.UpdateInvSlot 钩子异常: {e.Message}");
        }
    }

    /// <summary>第二层价格校验进行中的槽位集合（防同一槽位重复挂校验协程）。
    /// 用弱引用表：槽位销毁后自动清理，不泄漏。</summary>
    private static readonly ConditionalWeakTable<InvSlot, object> pendingPriceVerify =
        new ConditionalWeakTable<InvSlot, object>();

    /// <summary>
    /// 第二层防护协程：第一层（<see cref="InvSlot_UpdateInvSlot"/> 立即设置）应用后，
    /// 短暂延迟再校验一次——若 override 标签/颜色被其他补丁/刷新逻辑覆盖（未生效），则重新设置一遍。
    /// 用 <see cref="WaitForSecondsRealtime"/> 保证商店界面暂停（timeScale=0）时也能执行；
    /// 校验前确认商品未变化，避免玩家买走/换货后把旧标签误贴到新商品上。
    /// </summary>
    /// <param name="slot">商店槽位。</param>
    /// <param name="store">实现 <see cref="IStore"/> 的建筑。</param>
    /// <param name="slotNumber">槽位号（0-4）。</param>
    /// <param name="itemName">第一层处理时的商品名。</param>
    /// <param name="overrideText">期望的标签（null/空 = 无标签覆盖）。</param>
    /// <param name="overrideColor">期望的颜色（null = 无颜色覆盖）。</param>
    private static System.Collections.IEnumerator VerifyPriceOverrideCoroutine(
        InvSlot slot, IStore store, int slotNumber, string itemName, string? overrideText, Color? overrideColor)
    {
        // 短暂延迟（真实时间，暂停也生效），等可能覆盖第一层设置的逻辑先执行完
        yield return new WaitForSecondsRealtime(0.1f);

        try
        {
            // 商店已关闭 → 无需再校验
            if (slot == null || slot.agent == null || slot.agent.worldSpaceGUI == null
                || !slot.agent.worldSpaceGUI.openedNPCChest) yield break;
            if (slot.agent.mainGUI == null || slot.agent.mainGUI.invInterface == null) yield break;

            // 槽位对应商品：确认还是第一层处理的同一商品，防止买走/换货后误覆盖
            InvDatabase? chestDb = slot.agent.mainGUI.invInterface.chestDatabase;
            if (chestDb == null || chestDb.InvItemList == null
                || slotNumber < 0 || slotNumber >= chestDb.InvItemList.Count) yield break;
            InvItem? item = chestDb.InvItemList[slotNumber];
            if (item == null || item.invItemName != itemName) yield break;

            // ===== 第二层校验：第一层未生效（被覆盖）则重新设置 =====
            // 颜色 override
            if (overrideColor != null && slot.backgroundImage2 != null)
            {
                if (slot.backgroundImage2.color != (Color)overrideColor.Value)
                {
                    slot.backgroundImage2.enabled = true;
                    slot.backgroundImage2.color = overrideColor.Value;
                    LogInfo($"[CustomBuildings] 槽位{slotNumber} 颜色未生效，第二层已重新设置");
                }
            }
            // 标签 override
            if (!string.IsNullOrEmpty(overrideText) && slot.toolbarNumText != null)
            {
                if (slot.toolbarNumText.text != overrideText)
                {
                    slot.toolbarNumText.enabled = true;
                    slot.toolbarNumText.text = overrideText;
                    LogInfo($"[CustomBuildings] 槽位{slotNumber} 标签未生效，第二层已重新设置");
                }
            }
        }
        catch (Exception e)
        {
            LogWarning($"[CustomBuildings] 第二层价格校验协程异常: {e.Message}");
        }
        finally
        {
            // 校验完成（成功/失败/提前退出），允许该槽位后续再次挂校验
            pendingPriceVerify.Remove(slot);
        }
    }

    // ==================== Patch: LoadLevel.SetupMore4（建筑刷新） ====================
    // 普通关卡（非关卡编辑器）建筑刷新：关卡加载 100%（SetupMore4）阶段调用，
    // 此时区块、玩家、StartingPoint/ExitPoint 全部就绪。
    // 遍历所有实现 IBuildingSpawner 的注册建筑，调用其 OnLevelSpawn 回调，
    // 让自定义建筑像原版建筑一样在普通关卡中自动出现。
    // 注意：prefab 在场景切换后可能被 Unity 销毁（objectPrefabDic 残留失效引用），
    // 回调前必须确保 prefab 有效（失效则重建），否则拿不到模板实例会跳过刷新。
    // 与 LoadLevel_SetupMore4（容器重置）是两个独立 Postfix，互不影响。

    /// <summary>[Postfix] LoadLevel.SetupMore4 — 普通关卡：刷新所有实现 IBuildingSpawner 的自定义建筑。</summary>
    public static void LoadLevel_SetupMore4_SpawnBuildings(LoadLevel __instance)
    {
        try
        {
            GameController gc = GameController.gameController;
            // 仅服务端 + 非内存测试（见 LoadLevel 文档：gc.serverPlayer && !memoryTest）
            if (gc == null || !gc.serverPlayer || __instance == null || __instance.memoryTest) return;
            // 关卡编辑器 / 编辑器测试中不自动刷新（只在普通关卡生成）
            if (gc.levelEditing || gc.wasLevelEditing) return;

            foreach (KeyValuePair<string, CustomObjectMetadata> kv in CustomObjects.Registry)
            {
                CustomObjectMetadata meta = kv.Value;
                if (meta == null || !typeof(IBuildingSpawner).IsAssignableFrom(meta.Type)) continue;

                // 确保 prefab 有效（失效则重建），并取组件作为回调载体（不再依赖 LiveInstances）
                CustomObjectReal? template = EnsurePrefabValid(meta);
                if (template == null)
                {
                    LogWarning($"[{meta.Name}] LoadLevel.SetupMore4: 无法获取 prefab 模板实例，跳过刷新");
                    continue;
                }
                if (template is IBuildingSpawner spawner)
                {
                    try
                    {
                        spawner.OnLevelSpawn(__instance);
                        LogInfo($"[{meta.Name}] 普通关卡刷新回调已执行");
                    }
                    catch (Exception e)
                    {
                        LogError($"[{meta.Name}] OnLevelSpawn 回调异常: {e}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            LogWarning($"[CustomBuildings] LoadLevel.SetupMore4 建筑刷新钩子异常: {e.Message}");
        }
    }

    // ==================== Patch: LoadLevel.SetupMore4 (Prefix) ====================
    // 关卡加载前先销毁所有旧的自定义建筑实例。
    // 这些实例跨场景残留后会导致：
    //   1. 小地图标记不刷新/丢失
    //   2. 建筑越积越多（日志里 LiveInstances 可到几百个）
    // 用 DestroyImmediate 确保在后续 Postfix 生成新建筑前旧建筑已真正移除。

    /// <summary>[Prefix] LoadLevel.SetupMore4 — 销毁所有旧自定义建筑实例。</summary>
    public static void LoadLevel_SetupMore4_DestroyOldBuildings()
    {
        try
        {
            // 跳过 prefab 模板：prefab 组件（PrefabObject 指向自身）跨场景存活（DontDestroyOnLoad），
            // 若销毁它，每次进关 Spawn 时 EnsurePrefabValid 都会判定失效并重建（"prefab 已失效"警告刷屏）。
            // 实例的 PrefabObject 指向原 prefab（!= 自身 gameObject），不受影响。
            List<CustomObjectReal> instances = new List<CustomObjectReal>(CustomObjectReal.LiveInstances);
            int destroyed = 0, skippedPrefabs = 0, keptEditorPlaced = 0;
            foreach (CustomObjectReal custom in instances)
            {
                if (custom == null) continue;
                if (custom.PrefabObject != null && custom.gameObject == custom.PrefabObject)
                {
                    skippedPrefabs++;   // prefab 模板，保留
                    continue;
                }
                // 保留本关编辑器/瓦片放置的建筑（BasicObject.SpawnPostfix 已打 IsEditorPlaced 标记）——
                // 它们属于当前关卡，不能被当"上一关残留"销毁（否则编辑器放置的建筑进游戏后消失）。
                // 运行时刷新（IBuildingSpawner / spawnObjectReal / KMap.SpawnObject）不打此标记，照常清理。
                if (custom.IsEditorPlaced)
                {
                    keptEditorPlaced++;
                    continue;
                }
                if (custom.gameObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(custom.gameObject);
                    destroyed++;
                }
            }
            LogInfo($"[CustomBuildings] LoadLevel.SetupMore4: 销毁 {destroyed} 个旧建筑（跳过 {skippedPrefabs} 个 prefab 模板，保留 {keptEditorPlaced} 个编辑器放置建筑）");
        }
        catch (Exception e)
        {
            LogWarning($"[CustomBuildings] LoadLevel.SetupMore4 销毁旧建筑异常: {e.Message}");
        }
    }

    // ==================== Patch: LoadLevel.SetupMore4 ====================
    // 关卡加载完成（每关 100% 时调用）。重置所有存活自定义建筑实例的容器填充状态，
    // 使重新进入关卡（包括退出回主菜单后再进）时容器重新填充，避免"空空如也"。

    /// <summary>[Postfix] LoadLevel.SetupMore4 — 关卡加载完成，重置所有自定义建筑容器并重新填充。</summary>
    public static void LoadLevel_SetupMore4()
    {
        try
        {
            List<CustomObjectReal> instances = new List<CustomObjectReal>(CustomObjectReal.LiveInstances);
            foreach (CustomObjectReal custom in instances)
            {
                if (custom == null) continue;
                // prefab 模板的 TryFillContainer 会自行跳过
                custom.ResetAndRefillContainer();
            }
            if (instances.Count > 0)
            {
                LogInfo($"[CustomBuildings] LoadLevel.SetupMore4: 已重置 {instances.Count} 个自定义建筑容器");
            }
        }
        catch (Exception e)
        {
            LogWarning($"[CustomBuildings] LoadLevel.SetupMore4 钩子异常: {e.Message}");
        }
    }

    // ==================== Patch: NameDB.GetName ====================
    // 名称查询失败时原版返回 "E_"+名称（错误标记），去掉前缀避免 UI 显示 E_#sym:xxx。
    // 只去掉第一个 "E_"，不动其他内容。

    /// <summary>[Postfix] NameDB.GetName — 只对自定义建筑按钮名去掉查询失败的错误前缀 "E_"（只去第一个）。
    /// 原版用 "E_xxx" 作为"无文本"标记（如主菜单返回按钮 tooltip），UI 检测到会隐藏提示，
    /// 所以不能全局剥除——只处理含 "RogueForge_" 的请求名，其余保持原样。</summary>
    public static void NameDB_GetName(string myName, ref string __result)
    {
        try
        {
            if (myName != null && myName.Contains("RogueForge_")
                && __result != null && __result.StartsWith("E_"))
            {
                __result = __result.Substring(13);
            }
        }
        catch (Exception e)
        {
            LogWarning($"[CustomBuildings] NameDB.GetName 钩子异常: {e.Message}");
        }
    }

    // ==================== Patch: Bed.Interact（诊断钩子） ====================
    // 验证原版"近距离才交互"机制：玩家靠近床按 E 时，Bed.Interact 被调用。
    // 记录触发时玩家与床的距离，确认交互距离限制的实际情况。

    /// <summary>[Prefix] Bed.Interact(Agent) — 记录床交互触发情况。</summary>
    public static void Bed_Interact(Bed __instance, Agent agent)
    {
        try
        {
            if (__instance == null) return;
            // 只记录本地玩家触发的交互，避免 NPC 交互刷屏
            bool isLocal = agent != null && agent.localPlayer;
            string agentInfo = agent != null ? ("玩家" + agent.isPlayer) : "null";
            float dist = -1f;
            if (agent != null && __instance.tr != null)
                dist = Vector2.Distance(agent.tr.position, __instance.tr.position);

            LogInfo($"[CustomBuildings] Bed.Interact 触发! 触发者={agentInfo}, 本地玩家={isLocal}, "
                + $"玩家到床中心距离={dist:F2}, 床位置={__instance?.tr?.position}");
        }
        catch (Exception e)
        {
            LogWarning($"[CustomBuildings] Bed.Interact 钩子异常: {e.Message}");
        }
    }

    // ==================== Patch: GameResources.SetupDics ====================
    // 为每个注册建筑克隆 prefab（默认克隆 Chair），移除 NetworkIdentity，注册进 objectPrefabDic/objectDic/objectVarDic。

    /// <summary>[Postfix] GameResources.SetupDics — 注册所有自定义建筑 prefab。</summary>
    public static void GameResources_SetupDics(GameResources __instance)
    {
        LogInfo($"[CustomBuildings] SetupDics Postfix 触发，Registry={CustomObjects.Names.Count} 个: [{string.Join(", ", CustomObjects.Names)}]");
        foreach (KeyValuePair<string, CustomObjectMetadata> kv in CustomObjects.Registry)
        {
            string objectName = kv.Key;
            CustomObjectMetadata meta = kv.Value;
            try
            {
                if (__instance.objectPrefabDic.ContainsKey(objectName))
                {
                    LogInfo($"[CustomBuildings] SetupDics: {objectName} 已在 objectPrefabDic，跳过");
                    continue;
                }

                if (!__instance.objectPrefabDic.ContainsKey(meta.CloneSource))
                {
                    LogError($"[CustomBuildings] 克隆源 {meta.CloneSource} 不存在（建筑 {objectName}），跳过注册");
                    continue;
                }

                GameObject basePrefab = __instance.objectPrefabDic[meta.CloneSource];
                GameObject newPrefab = UnityEngine.Object.Instantiate(basePrefab);
                newPrefab.name = objectName;

                ObjectReal old = newPrefab.GetComponent<ObjectReal>();
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
                RemoveNetworkIdentity(newPrefab);

                // 挂载建筑组件（用元数据里的类型）
                CustomObjectReal nm = (CustomObjectReal)newPrefab.AddComponent(meta.Type);
                nm.PrefabObject = newPrefab;

                // 容器支持：prefab 加 InvDatabase 组件（打开容器界面/打碎掉落物品都需要它）。
                // 只有 override 了 FillContainer 的子类才实际填充物品。
                if (newPrefab.GetComponent<InvDatabase>() == null)
                    newPrefab.AddComponent<InvDatabase>();

                // 关键：prefab 必须跨场景存活，否则切场景后 Unity 会销毁它，
                // 字典里留下失效引用，Spawn 时取用抛 NRE 中断加载协程（卡 37%）
                UnityEngine.Object.DontDestroyOnLoad(newPrefab);

                // 防幽灵：prefab 只是实例化模板，移出场景 + 禁用渲染
                newPrefab.transform.position = new Vector3(9999f, 9999f, -9999f);
                Renderer[] prefabRends = newPrefab.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in prefabRends)
                {
                    if (r != null) r.enabled = false;
                }

                GameController gc = GameController.gameController;
                if (gc != null)
                {
                    gc.objectRealList.Remove(nm);
                    gc.objectRealListWithDestroyed.Remove(nm);
                }

                __instance.objectPrefabDic.Add(objectName, newPrefab);
                // objectDic 存的是小地图/编辑器图标 Sprite。优先用自定义建筑精灵（否则继承克隆源图标，如沙发）。
                // QuestMarker.StartReal 用 gr.objectDic[objectName] 作为大地图标记图标。
                // 注意：总是覆盖（不判断 ContainsKey）——SetupDics 可能触发多次，
                // 第一次注册时自定义精灵可能未就绪（图集未初始化）导致存了沙发图标。
                UnityEngine.Sprite? customIcon = meta.GetSprite()?.Sprite;
                if (customIcon != null)
                {
                    __instance.objectDic[objectName] = customIcon;
                    LogInfo($"[CustomBuildings] objectDic[{objectName}] 已更新为自定义精灵图标 (Sprite={customIcon.name})");
                }
                else if (!__instance.objectDic.ContainsKey(objectName) && __instance.objectDic.ContainsKey(meta.CloneSource))
                {
                    __instance.objectDic.Add(objectName, __instance.objectDic[meta.CloneSource]);
                    LogInfo($"[CustomBuildings] objectDic[{objectName}] 使用克隆源图标 {meta.CloneSource}（自定义精灵为空）");
                }

                // objectVarDic 双保险注册
                ObjectVars? objectVars = gc != null ? gc.objectVars : null;
                if (objectVars == null) objectVars = UnityEngine.Object.FindObjectOfType<ObjectVars>();
                if (objectVars != null && !objectVars.objectVarDic.ContainsKey(objectName))
                {
                    objectVars.objectVarDic.Add(objectName, new ObjectVar
                    {
                        initialSpawns = 0,
                        shiftTowardWalls = true,
                        // 四方向建筑：编辑器/流式世界支持四方向旋转放置（参考 ATM）
                        fourDirection = meta.IsFourDirection,
                    });
                }

                LogInfo($"[CustomBuildings] prefab 注册成功：{objectName}（克隆源 {meta.CloneSource}）");
            }
            catch (Exception e)
            {
                LogError($"[CustomBuildings] prefab 注册失败：{objectName} - {e}");
            }
        }
    }

    // ==================== Patch: ObjectVars.Awake ====================

    /// <summary>[Postfix] ObjectVars.Awake — 注册所有建筑到 objectVarDic。</summary>
    public static void ObjectVars_Awake(ObjectVars __instance)
    {
        foreach (string objectName in CustomObjects.Names)
        {
            if (__instance.objectVarDic.ContainsKey(objectName)) continue;
            // 四方向建筑：ObjectVar 注册 fourDirection，支持编辑器四方向旋转放置
            bool fourDir = CustomObjects.GetObject(objectName)?.IsFourDirection ?? false;
            __instance.objectVarDic.Add(objectName, new ObjectVar
            {
                initialSpawns = 0,
                shiftTowardWalls = true,
                fourDirection = fourDir,
            });
        }
    }

    // ==================== Patch: LevelEditor.OpenObjectLoad ====================
    // 把自定义建筑注入物件放置面板（2 参重载）。

    /// <summary>[Prefix] LevelEditor.OpenObjectLoad(2参) — 注入所有注册建筑名。</summary>
    public static void LevelEditor_OpenObjectLoad(LevelEditor __instance, List<string> dataList, List<string> dataList2)
    {
        if (dataList2 is null)
        {
            LogInfo($"[CustomBuildings] OpenObjectLoad Prefix: dataList2 为 null");
            return;
        }

        // 只认物件放置面板：第二组列表必含 Window / ATMMachine
        // （墙/地板/灯/居民/道具栏的 dataList2 都不含，不会误判）
        bool isObjectPanel = dataList2.Contains("Window") || dataList2.Contains("ATMMachine");
        LogInfo($"[CustomBuildings] OpenObjectLoad Prefix: dataList2.Count={dataList2.Count}, 含Window={dataList2.Contains("Window")}, 含ATMMachine={dataList2.Contains("ATMMachine")}, 判定为物件面板={isObjectPanel}, Registry={CustomObjects.Names.Count}个");
        if (!isObjectPanel) return;

        // 把每个未注册的建筑名插到 "------------------------" 分隔线之后（最终位置交给 Postfix 处理）
        int insertIndex = dataList2.Count > 0 && dataList2[0] == "------------------------" ? 1 : 0;
        foreach (string objectName in CustomObjects.Names)
        {
            if (dataList2.Contains(objectName)) continue;
            dataList2.Insert(insertIndex, objectName);
            insertIndex++;
            LogInfo($"[CustomBuildings] 已插入建筑名: {objectName} @位置{insertIndex - 1}");
        }
        if (CustomObjects.Registry.Count > 0)
            LogInfo($"[CustomBuildings] 已注入物件栏 {CustomObjects.Registry.Count} 个建筑");
    }

    /// <summary>[Postfix] LevelEditor.OpenObjectLoad(2参) — Sort 后把自定义建筑按钮挪到分隔线正下方 + 刷新滚动列表。</summary>
    public static void LevelEditor_OpenObjectLoadPostfix(LevelEditor __instance)
    {
        List<ButtonData> list = __instance.buttonsDataLoad;

        // 找到第一个分隔线位置
        int separatorIndex = -1;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].scrollingButtonType == "------------------------")
            {
                separatorIndex = i;
                break;
            }
        }
        if (separatorIndex == -1) return;

        // 收集所有自定义建筑按钮，从原位置移除
        List<ButtonData> customButtons = new List<ButtonData>();
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (CustomObjects.IsRegistered(list[i].scrollingButtonType))
            {
                customButtons.Add(list[i]);
                list.RemoveAt(i);
            }
        }

        // 依次插到分隔线正下方（保持注册顺序）
        int insertIndex = separatorIndex + 1;
        foreach (ButtonData button in customButtons)
        {
            list.Insert(insertIndex, button);
            insertIndex++;
        }

        // 诊断（always-on）：物件面板里一个自定义建筑按钮都没有 → 说明注入失败（注册表/面板判定问题），
        // 与"按钮在但画不出来"（SetTileImage 的 id<=0）是两个不同的故障层
        if (customButtons.Count == 0)
            LogWarning($"[CustomBuildings] OpenObjectLoad: 物件面板未找到自定义建筑按钮（Registry={CustomObjects.Names.Count}个，按钮总数={list.Count}）");

        // 让滚动列表按新顺序重建，并回到顶部，保证一打开就能看到
        __instance.scrollerControllerLoad.RefreshMenuData(true);
    }

    // ==================== Patch: LevelEditor.SetTileImage ====================
    // 原版 SetTileImage 对未知物件名会画成第 0 号贴图（地板），这里强制重画 + 材质修复。

    /// <summary>材质修复缓存（按建筑名）。</summary>
    private static readonly HashSet<string> materialFixed = new HashSet<string>();

    /// <summary>[Postfix] LevelEditor.SetTileImage — 自定义建筑网格重画。</summary>
    public static void LevelEditor_SetTileImage(LevelEditor __instance, LevelEditorTile myTile)
    {
        if (myTile == null || myTile.tileType != "Objects" || myTile.tileMap == null) return;
        if (!CustomObjects.IsRegistered(myTile.tileName)) return;
        string objectName = myTile.tileName;

        // 图集选择：优先用 RogueLibs 注册的 Objects 图集（RogueFramework.ObjectSprites，自定义精灵一定在里面）；
        // 编辑器的 objectSprites 可能是不同实例/不含自定义精灵（会导致 GetSpriteIdByName 返回 -1 → 画不出）。
        // 只有前者不可用时才退回编辑器图集。
        tk2dSpriteCollectionData? source = __instance.objectSprites;
        tk2dSpriteCollectionData? mapCollection = RogueFramework.ObjectSprites;
        if (mapCollection == null || mapCollection.GetSpriteIdByName(objectName) <= 0)
            mapCollection = source;
        if (mapCollection == null) return;

        if (myTile.tileMap.Editor__SpriteCollection != mapCollection)
            myTile.tileMap.Editor__SpriteCollection = mapCollection;

        tk2dSpriteCollectionData inst = mapCollection.inst;
        int id = mapCollection.GetSpriteIdByName(objectName);
        if (id <= 0 || inst == null)
        {
            // 诊断（always-on）：找不到自定义精灵 → 记录图集信息，定位"编辑器画自定义建筑不显示"
            LogWarning($"[CustomBuildings] SetTileImage: 找不到自定义建筑精灵 {objectName} (id={id})。" +
                $"编辑器图集='{(source != null ? source.name : "null")}'，RogueFramework图集='{(RogueFramework.ObjectSprites != null ? RogueFramework.ObjectSprites.name : "null")}'，" +
                $"是否同实例={source == RogueFramework.ObjectSprites}，Registry={CustomObjects.Names.Count}个");
            return;
        }

        // materialInsts 修复（RogueLibs.AddDefinition 扩容后缓存未同步，新槽位材质无效）
        if (!materialFixed.Contains(objectName))
        {
            try
            {
                if (inst.materialInsts == null || inst.materialInsts.Length < inst.materials.Length)
                {
                    Material[]? old = inst.materialInsts;
                    inst.materialInsts = new Material[inst.materials.Length];
                    for (int i = 0; i < inst.materials.Length; i++)
                    {
                        if (old != null && i < old.Length) inst.materialInsts[i] = old[i];
                        else if (inst.materials[i] != null) inst.materialInsts[i] = UnityEngine.Object.Instantiate(inst.materials[i]);
                    }
                }
                tk2dSpriteDefinition def = inst.spriteDefinitions[id];
                // 越界兜底：materialId 无效时钳制到有效范围，避免"透明瓦片"且被 materialFixed 永久缓存
                if (def.material != null && inst.materialInsts != null && inst.materialInsts.Length > 0)
                {
                    int matId = Mathf.Clamp(def.materialId, 0, inst.materialInsts.Length - 1);
                    inst.materialInsts[matId] = UnityEngine.Object.Instantiate(def.material);
                    def.materialInst = inst.materialInsts[matId];
                }
                materialFixed.Add(objectName);
            }
            catch (Exception e)
            {
                LogError($"[CustomBuildings] materialInsts 修复失败：{objectName} - {e}");
            }
        }

        myTile.tileMap.SetTile((int)myTile.posX, (int)myTile.posY, 0, id);
        // 必须用 ForceBuild：编辑器模式下默认 Build 可能跳过网格重建
        myTile.tileMap.ForceBuild();
        myTile.tileFilled = true;
    }

    // ==================== Patch: BasicObject.Spawn ====================
    // prefab 失效（场景切换被销毁）时从克隆源重建。

    /// <summary>[Prefix] BasicObject.Spawn — 自定义建筑 prefab 失效重建兜底。</summary>
    public static void BasicObject_Spawn(BasicObject __instance, SpawnerBasic spawner, string objectRealName, Vector2 myPos, Vector2 myScale, Chunk startingChunkReal)
    {
        if (!CustomObjects.IsRegistered(objectRealName)) return;
        CustomObjectMetadata meta = CustomObjects.GetObject(objectRealName)!;

        // 四方向建筑默认朝北：原版 BasicObject.Spawn 在无方向（spawner.direction==""）时会
        // 把 direction 固定为 "S"（见 spawnDirection switch 默认分支），且 faceAwayFromWalls
        // 逻辑会尝试自动靠墙定向。这里在 Spawn 前置把空方向设为 "N"，让四方向建筑默认显示北向图，
        // 同时跳过 faceAwayFromWalls 自动定向（自定义建筑不依赖它）。
        if (spawner != null && meta.IsFourDirection && string.IsNullOrEmpty(spawner.direction))
        {
            spawner.direction = "N";
        }

        // prefab 失效重建兜底（与 SetupMore3_3 共用同一方法）
        EnsurePrefabValid(meta);
    }

    /// <summary>[Postfix] BasicObject.Spawn — 标记"编辑器/瓦片放置"生成的建筑（IsEditorPlaced）。
    /// 关卡加载清理（DestroyOldBuildings）只销毁旧建筑、保留本关编辑器放置的建筑；
    /// 运行时刷新（spawnObjectReal / KMap.SpawnObject）不走此路径，不会被误标。</summary>
    public static void BasicObject_SpawnPostfix(BasicObject __instance, SpawnerBasic spawner, string objectRealName, Vector2 myPos, Vector2 myScale, Chunk startingChunkReal)
    {
        if (!CustomObjects.IsRegistered(objectRealName)) return;
        try
        {
            // LiveInstances 按生成顺序追加：最后匹配的 = 刚生成的那个
            CustomObjectReal? found = null;
            foreach (CustomObjectReal c in CustomObjectReal.LiveInstances)
            {
                if (c != null && c.ObjectName == objectRealName) found = c;
            }
            if (found != null) found.IsEditorPlaced = true;
        }
        catch { }
    }

    /// <summary>
    /// 确保指定建筑类型的 prefab 在 objectPrefabDic 中有效；失效（场景切换被销毁）时从克隆源重建。
    /// 返回有效的 prefab 上的 <see cref="CustomObjectReal"/> 组件（供接口回调载体或生成使用）。
    /// </summary>
    /// <param name="meta">建筑元数据。</param>
    /// <returns>有效的 CustomObjectReal 组件；失败返回 null。</returns>
    private static CustomObjectReal? EnsurePrefabValid(CustomObjectMetadata meta)
    {
        GameController gc = GameController.gameController;
        if (gc == null || meta == null) return null;
        GameResources? gr = gc.gameResources;
        if (gr == null || gr.objectPrefabDic == null) return null;

        string objectRealName = meta.Name;
        GameObject? existing = gr.objectPrefabDic.ContainsKey(objectRealName) ? gr.objectPrefabDic[objectRealName] : null;
        if (existing != null)
        {
            CustomObjectReal? comp = existing.GetComponent<CustomObjectReal>();
            if (comp != null) return comp;
        }

        // prefab 已失效（场景切换被销毁）或组件丢失 → 从克隆源重建
        if (!gr.objectPrefabDic.ContainsKey(meta.CloneSource))
        {
            LogError($"[CustomBuildings] 克隆源 {meta.CloneSource} 不存在（建筑 {objectRealName}），无法重建 prefab");
            return null;
        }
        LogWarning($"[CustomBuildings] prefab 已失效（场景切换被销毁），从 {meta.CloneSource} 重建：{objectRealName}");
        try
        {
            GameObject basePrefab = gr.objectPrefabDic[meta.CloneSource];
            GameObject newPrefab = UnityEngine.Object.Instantiate(basePrefab);
            newPrefab.name = objectRealName;

            ObjectReal old = newPrefab.GetComponent<ObjectReal>();
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            RemoveNetworkIdentity(newPrefab);

            CustomObjectReal nm = (CustomObjectReal)newPrefab.AddComponent(meta.Type);
            nm.PrefabObject = newPrefab;

            // 容器支持：重建的 prefab 也要加 InvDatabase（与 SetupDics 注册一致）
            if (newPrefab.GetComponent<InvDatabase>() == null)
                newPrefab.AddComponent<InvDatabase>();

            if (gc != null)
            {
                gc.objectRealList.Remove(nm);
                gc.objectRealListWithDestroyed.Remove(nm);
            }

            UnityEngine.Object.DontDestroyOnLoad(newPrefab);

            // 防幽灵：移出场景 + 禁用渲染
            newPrefab.transform.position = new Vector3(9999f, 9999f, -9999f);
            Renderer[] prefabRends = newPrefab.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in prefabRends)
            {
                if (r != null) r.enabled = false;
            }

            gr.objectPrefabDic[objectRealName] = newPrefab;
            // 重建后恢复自定义图标（与 SetupDics 注册一致，避免回退到克隆源 Chair 图标）
            UnityEngine.Sprite? rebuildIcon = meta.GetSprite()?.Sprite;
            if (rebuildIcon != null)
            {
                gr.objectDic[objectRealName] = rebuildIcon;
            }
            else if (gr.objectDic.ContainsKey(objectRealName))
            {
                gr.objectDic[objectRealName] = gr.objectDic[meta.CloneSource];
            }

            LogInfo($"[CustomBuildings] prefab 重建成功：{objectRealName}");
            return nm;
        }
        catch (Exception e)
        {
            LogError($"[CustomBuildings] prefab 重建失败：{objectRealName} - {e}");
            return null;
        }
    }

    // ==================== 工具 ====================

    /// <summary>移除 GameObject 上的 NetworkIdentity 组件（反射，无需编译期类型引用）。
    /// prefab 被改造后 NetworkIdentity 状态非法，Instantiate 克隆时其 Awake 会自我销毁。</summary>
    private static void RemoveNetworkIdentity(GameObject go)
    {
        try
        {
            if (go == null) return;
            System.Type? niType = typeof(UnityEngine.Component).Assembly.GetType("UnityEngine.Networking.NetworkIdentity")
                ?? System.Type.GetType("UnityEngine.Networking.NetworkIdentity, UnityEngine.Networking")
                ?? System.Type.GetType("Mirror.NetworkIdentity, com.unity.multiplayer-hlapi.Runtime")
                ?? System.Type.GetType("Mirror.NetworkIdentity, Mirror");
            if (niType == null)
            {
                // 兜底：按名字找组件
                foreach (UnityEngine.Component c in go.GetComponents<UnityEngine.Component>())
                {
                    if (c != null && c.GetType().Name == "NetworkIdentity")
                    {
                        UnityEngine.Object.DestroyImmediate(c);
                        LogInfo($"[CustomBuildings] 已移除 prefab 的 {c.GetType().FullName}");
                        return;
                    }
                }
                return;
            }
            UnityEngine.Object? ni = go.GetComponent(niType);
            if (ni != null)
            {
                UnityEngine.Object.DestroyImmediate(ni);
                LogInfo($"[CustomBuildings] 已移除 prefab 的 {niType.FullName}（防 Instantiate 时自我销毁）");
            }
        }
        catch (Exception e)
        {
            LogWarning($"[CustomBuildings] RemoveNetworkIdentity 异常: {e.Message}");
        }
    }
}
