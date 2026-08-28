using System;
using BepInEx;
using RogueLibsCore;

#nullable enable
namespace RogueForge;

/// <summary>
/// 加载页提示管理器：接管加载页（<see cref="ProTip"/>）的原版随机提示，替换/追加为自定义提示。
///
/// 用法（在 mod 插件的 <c>Awake()</c> 中调用一次）：
/// <code>
/// KLoadingTips.Initialize(this, keepOriginalTips: false);
/// </code>
/// 自定义提示通过 <c>RogueLibs.CreateCustomName("Protip_数字", "Dialogue", ...)</c> 注册，
/// 本类会在 <see cref="TipKeyStart"/> ~ <see cref="TipKeyEnd"/>（不含）范围内随机抽取一条显示。
///
/// 解决的原版问题：
/// 1. 原版加载页提示只在"上一关结束"时随机选一条、下一关加载页显示，新手期（Protip_Early 池）和
///    调试模式下根本不显示；
/// 2. "先显示原版提示、进新关后变成自定义提示"的视觉闪烁——本类同步修正已写入的
///    <see cref="ProTip.myText"/>（Postfix 与原方法同帧执行，渲染在帧末，原版文本不会被渲染出来）。
/// </summary>
public static class KLoadingTips
{
    /// <summary>自定义提示 key 范围起点（含）。默认 900，即 Protip_900。</summary>
    public static int TipKeyStart = 900;

    /// <summary>自定义提示 key 范围终点（不含）。默认 920，即 Protip_900 ~ Protip_919。</summary>
    public static int TipKeyEnd = 920;

    /// <summary>是否保留原版提示文本：true = 原版提示 + 自定义提示都显示；false = 只显示自定义提示（替换原版）。</summary>
    public static bool KeepOriginalTips { get; private set; }

    /// <summary>加载页自定义提示是否启用（默认开启；调用 <see cref="ToggleTips"/> 切换，钩子也可同步）。</summary>
    private static bool tipsEnabled = true;

    /// <summary>当前是否启用了加载页自定义提示。</summary>
    public static bool TipsEnabled => tipsEnabled;

    /// <summary>
    /// 切换加载页自定义提示的启用状态：<b>调用一次开启，再次调用关闭</b>。
    /// </summary>
    /// <returns>切换后的当前状态（true = 已启用）。</returns>
    public static bool ToggleTips()
    {
        tipsEnabled = !tipsEnabled;
        CustomBuildingsPlugin.LogInfo($"[KLoadingTips] 加载页自定义提示已{(tipsEnabled ? "开启" : "关闭")}");
        return tipsEnabled;
    }

    /// <summary>是否已初始化（防止重复注册钩子）。</summary>
    private static bool initialized;

    /// <summary>
    /// 初始化加载页提示管理器：注册 <see cref="ProTip.SetActualText"/> 的 Postfix 钩子。
    /// 每次关卡加载页都会从自定义提示（Protip_{<see cref="TipKeyStart"/>}~{<see cref="TipKeyEnd"/>-1}）
    /// 里随机抽一条，按 <paramref name="keepOriginalTips"/> 决定是追加到原版提示后还是替换原版提示。
    /// </summary>
    /// <param name="host">宿主 BepInEx 插件实例（用于 RoguePatcher 注册钩子）。</param>
    /// <param name="keepOriginalTips">是否保留原版提示文本：true = 都显示；false = 只显示自定义提示。</param>
    public static void Initialize(BaseUnityPlugin host, bool keepOriginalTips)
    {
        if (host == null) throw new ArgumentNullException(nameof(host));
        if (initialized) return;
        initialized = true;

        KeepOriginalTips = keepOriginalTips;

        RoguePatcher patcher = new RoguePatcher(host, typeof(KLoadingTips));
        patcher.Postfix(typeof(ProTip), "SetActualText", "ProTip_SetActualText");

        CustomBuildingsPlugin.LogInfo(
            $"[KLoadingTips] 已注册加载页提示钩子（保留原版提示={(keepOriginalTips ? "是" : "否")}，key 范围 Protip_{TipKeyStart}~{TipKeyEnd - 1}）");
    }

    /// <summary>随机抽取一条自定义提示文本；范围内没有可用提示时返回 null。</summary>
    private static string? PickRandomTip()
    {
        try
        {
            GameController? gc = GameController.gameController;
            if (gc?.nameDB == null || TipKeyEnd <= TipKeyStart) return null;

            // 随机抽，最多尝试若干次，跳过未注册的 key（GetName 返回 "E_" 开头）
            for (int attempt = 0; attempt < 10; attempt++)
            {
                int key = UnityEngine.Random.Range(TipKeyStart, TipKeyEnd);
                string text = gc.nameDB.GetName($"Protip_{key}", "Dialogue");
                if (!string.IsNullOrEmpty(text) && !text.Contains("E_"))
                    return text;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// [Postfix] ProTip.SetActualText — 关卡结束时（SetActualText 选完提示后）替换/追加自定义提示，
    /// 并同步修正已写入 <see cref="ProTip.myText"/> 的文本（防"先原版后自定义"闪烁）。
    /// </summary>
    public static void ProTip_SetActualText(ProTip __instance)
    {
        try
        {
            if (!tipsEnabled) return;   // 未启用：放行原版提示，不做任何替换

            GameController? gc = GameController.gameController;
            if (gc?.sessionDataBig == null) return;

            string? customTip = PickRandomTip();
            if (string.IsNullOrEmpty(customTip)) return;

            if (KeepOriginalTips)
            {
                // ===== 保留原版：自定义提示追加到原版提示后面（两条都显示） =====
                if (string.IsNullOrEmpty(gc.sessionDataBig.proTipText))
                    gc.sessionDataBig.proTipText = customTip;
                else
                    gc.sessionDataBig.proTipText += "\n\n" + customTip;

                // 同步修正 myText.text：在原版提示后追加自定义提示（防闪烁）
                if (__instance != null && __instance.myText != null && !string.IsNullOrEmpty(__instance.myText.text))
                {
                    __instance.myText.text = __instance.myText.text + "\n\n" + customTip;
                }
            }
            else
            {
                // ===== 不保留原版：只显示自定义提示（替换） =====
                gc.sessionDataBig.proTipText = customTip;

                // 同步修正 myText.text：原版结构为 "...\n0%\n\n原版提示"，
                // 把最后一个 "\n\n" 之后的部分换成自定义提示（防闪烁）
                if (__instance != null && __instance.myText != null && !string.IsNullOrEmpty(__instance.myText.text))
                {
                    int idx = __instance.myText.text.LastIndexOf("\n\n", StringComparison.Ordinal);
                    if (idx >= 0)
                        __instance.myText.text = __instance.myText.text.Substring(0, idx) + "\n\n" + customTip;
                    else
                        __instance.myText.text = __instance.myText.text + "\n\n" + customTip;
                }
            }
        }
        catch { }
    }
}
