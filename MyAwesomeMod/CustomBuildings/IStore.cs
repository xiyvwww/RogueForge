using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

#nullable enable
namespace RogueForge;

/// <summary>
/// 商店接口（由原 IBuyable 拆分而来，背包物品点击功能见 <see cref="IBackpack"/>）。
/// 实现此接口的自定义建筑支持"购买物品"功能：玩家靠近后打开购买界面，
/// 右键商品即可购买（原版 <see cref="InvSlot.BuyItem"/> 自动处理定价/扣款/移交）。
///
/// 使用方式：
///   1. 实现 <see cref="GetBuyItems"/> 提供销售清单（每次打开界面都会调用，返回全新 InvItem）。
///   2. 实现 <see cref="OnItemBought"/> 处理购买成功回调（用户操作部分）。
///   3. 打开购买窗口的方法由接口层提供（扩展方法 <see cref="IStoreExtensions.OpenBuyChest"/>），
///      在交互按钮中调用：<c>h.AddButton("RogueForge_购买", m => this.OpenBuyChest(m.Object, m.Agent))</c>
///      （按钮名称用 "RogueForge_" 前缀，显示文本注册由用户自行处理）
///   4. 若建筑未 override <see cref="IObjectInteraction.SetupInteractions"/>，基类
///      <see cref="CustomObjectReal"/> 会自动添加购买按钮（按钮名 "RogueForge_购买"，隐式按 E 直接打开）。
///
/// 购买机制（原版 <see cref="InvSlot.BuyItem"/>）：
///   - 打开界面：<see cref="ObjectReal.ShowNPCChest"/> → NPC 商店界面，右键商品购买
///   - 定价：<c>determineMoneyCost(item, item.itemValue, 建筑名)</c>（交易类型=本建筑名，走默认分支：原价 + 关卡难度缩放）
///   - 扣款：<c>moneySuccess(价格)</c>（失败自动提示"买不起"）
///   - 移货：<c>MoveFromChestToInventory</c>（原版自动处理）
///
/// 标签（<see cref="PriceOverride1"/>-<see cref="PriceOverride5"/>）与颜色
/// （<see cref="PriceOverrideColor1"/>-<see cref="PriceOverrideColor5"/>）是<b>默认空实现</b>
/// （空 = null = 使用默认行为），不实现也能用；本意是"默认接口方法"，但目标框架 net471 不支持
/// 默认接口实现（编译报 CS8701），故声明为抽象成员 + 基类 <see cref="CustomObjectReal"/> 提供
/// virtual 空实现（返回 null）兜底——继承基类的实现类无需实现这些成员。
/// </summary>
public interface IStore
{
    /// <summary>
    /// 获取可购买的物品列表（每次打开购买界面时被调用）。必须实现。
    /// 注意：返回全新 InvItem 实例（勿复用同一引用）。
    /// 例：new InvItem { invItemName = "BananaPeel", invItemCount = 3 } + ItemSetup(notNew: true)
    /// 售价 = item.itemValue：ItemSetup 后默认取游戏配置值；
    /// 如需自定义售价，在 ItemSetup 之后设置 <c>item.itemValue = X</c>（非 0 才覆盖，0 保持默认）。
    /// </summary>
    /// <returns>可售物品列表。</returns>
    List<InvItem> GetBuyItems();

    /// <summary>
    /// 玩家选中商店内物品时的回调（右键点击商品触发）。必须实现。
    /// 参数 <paramref name="item"/> 为玩家选中的物品，**由用户端判断是否购买**。
    /// 若要执行购买，调用类方法 <see cref="IStoreExtensions.PurchaseItem"/>（扣钱 + 移货到玩家背包）；
    /// 若拒绝购买（价格不符/条件不满足/自定义扣款），直接返回即可（原版自动购买已被拦截）。
    /// </summary>
    /// <param name="item">玩家选中的商店物品。</param>
    /// <param name="buyer">购买者（玩家）。</param>
    void OnItemBought(InvItem item, Agent buyer);

    /// <summary>商店第 1 个槽位的自定义价格文本（默认空实现，空 = null = 默认定价；非空时覆盖该位置价格显示，如 "￥50"、"免费"）。</summary>
    string? PriceOverride1 { get; }

    /// <summary>商店第 2 个槽位的自定义价格文本（默认空实现，空 = null = 默认定价）。</summary>
    string? PriceOverride2 { get; }

    /// <summary>商店第 3 个槽位的自定义价格文本（默认空实现，空 = null = 默认定价）。</summary>
    string? PriceOverride3 { get; }

    /// <summary>商店第 4 个槽位的自定义价格文本（默认空实现，空 = null = 默认定价）。</summary>
    string? PriceOverride4 { get; }

    /// <summary>商店第 5 个槽位的自定义价格文本（默认空实现，空 = null = 默认定价）。</summary>
    string? PriceOverride5 { get; }

    /// <summary>商店第 1 个槽位的自定义颜色（默认空实现，空 = null = 原版颜色）。</summary>
    Color? PriceOverrideColor1 { get; }

    /// <summary>商店第 2 个槽位的自定义颜色（默认空实现，空 = null = 原版颜色）。</summary>
    Color? PriceOverrideColor2 { get; }

    /// <summary>商店第 3 个槽位的自定义颜色（默认空实现，空 = null = 原版颜色）。</summary>
    Color? PriceOverrideColor3 { get; }

    /// <summary>商店第 4 个槽位的自定义颜色（默认空实现，空 = null = 原版颜色）。</summary>
    Color? PriceOverrideColor4 { get; }

    /// <summary>商店第 5 个槽位的自定义颜色（默认空实现，空 = null = 原版颜色）。</summary>
    Color? PriceOverrideColor5 { get; }

}

/// <summary>
/// 商店接口的扩展方法（接口层提供"打开购买窗口"的实现）。
/// 因目标框架 net471 不支持默认接口实现，用扩展方法模拟。
/// </summary>
public static class IStoreExtensions
{
    /// <summary>
    /// 免费商品特殊数字：<see cref="InvItem.itemValue"/> == <see cref="FREE_ITEM_VALUE"/> 时视为免费（不扣钱）。
    /// 兼容 <c>itemValue == 0</c>（老用法）。所有免费判断统一走 <see cref="IsFreeItem"/>。
    /// </summary>
    public const int FREE_ITEM_VALUE = 48484;

    /// <summary>
    /// 判断物品是否为免费商品
    /// </summary>
    public static bool IsFreeItem(InvItem? item)
    {
        return item != null && item.invItemName != null && item.invItemName != "Money"
            && (item.itemValue == FREE_ITEM_VALUE);
    }

    /// <summary>
    /// 免费商品（itemValue == 0 或 48484）槽位的默认显示颜色（默认白色 = 正常显示，不染紫）。
    /// 需要修改默认值时直接改这里（本类是 PurchaseItem 所在类，符合"默认值写在 PurchaseItem 里"的要求）。
    /// </summary>
    public static UnityEngine.Color DefaultFreeItemColor = UnityEngine.Color.white;

    /// <summary>每个建筑的购买状态快照（记录上次商品，用于检测购买回调）。</summary>
    private sealed class BuyState
    {
        public List<InvItem>? LastItems;
        public int LastCount = -1;

    }

    /// <summary>按建筑实例存储购买状态（避免扩展方法无状态限制）。</summary>
    private static readonly ConditionalWeakTable<ObjectReal, BuyState> states = new ConditionalWeakTable<ObjectReal, BuyState>();

    /// <summary>
    /// 打开购买窗口（由接口层提供）：确保特殊库存存在 → 检测上次购买回调 → 清空并填充销售清单 → 打开 NPC 商店界面。
    /// </summary>
    /// <param name="store">实现了 <see cref="IStore"/> 的建筑。</param>
    /// <param name="obj">建筑对应的 <see cref="ObjectReal"/> 实例。</param>
    /// <param name="agent">购买者（玩家）。</param>
    public static void OpenBuyChest(this IStore store, ObjectReal obj, Agent agent)
    {
        if (store == null || obj == null) return;
        try
        {
            BuyState state = states.GetOrCreateValue(obj);

            // 确保特殊库存存在（Instantiate specialInvDatabasePrefab 挂到自身）
            if (obj.specialInvDatabase == null)
            {
                obj.SetupSpecialInvDatabase();
            }
            if (obj.specialInvDatabase == null)
            {
                CustomBuildingsPlugin.Logger.LogWarning($"[{obj.objectName}] OpenBuyChest: specialInvDatabase 创建失败");
                return;
            }

            // 关键：确保库存已初始化（槽位创建）。Instantiate prefab 时 Awake 可能未执行
            // （类似动态 AddComponent 的情况），导致 InvItemList 空 → hasEmptySlot()=false
            // → AddItem 走 tempSlot 分支不真正加入 → 界面看不到商品。
            CustomBuildingsPlugin.Logger.LogInfo($"[{obj.objectName}] OpenBuyChest: createdInventory={obj.specialInvDatabase.createdInventory}, InvItemList.Count={obj.specialInvDatabase.InvItemList?.Count}");
            if (!obj.specialInvDatabase.createdInventory)
            {
                obj.specialInvDatabase.CreateInventory();
                CustomBuildingsPlugin.Logger.LogInfo($"[{obj.objectName}] OpenBuyChest: 手动调用 CreateInventory");
            }

            // 检测上次打开后是否有商品被买走（数量减少 → 触发购买回调）
            // 注意：原版自动购买已被拦截（见 CustomBuildingsPlugin.InvSlot_BuyItem），
            // 购买由用户回调 OnItemBought 决定并调用 PurchaseItem 完成，此处无需再检测。
            // 但仍保留状态记录以防其他路径取走物品。

            // 清空旧货（防止残留上次的商品）
            obj.specialInvDatabase.DestroyAllItems();

            // 填充可售物品（每次打开都重新调用 GetBuyItems，返回全新 InvItem）
            List<InvItem> items = store.GetBuyItems();
            if (items == null || items.Count == 0)
            {
                CustomBuildingsPlugin.Logger.LogInfo($"[{obj.objectName}] OpenBuyChest: 无商品可卖，不打开界面");
                return;
            }
            foreach (InvItem item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.invItemName)) continue;
                // 确保物品已设置详情（否则无图标/价格异常）
                try
                {
                    if (string.IsNullOrEmpty(item.itemType)) item.ItemSetup(notNew: true);
                }
                catch { }

                // 免费商品标记：原版 AddItem 会把 itemValue 覆盖回配置默认价（如 BananaPeel=5），
                // 导致免费商品变收费。统一把免费商品标记为 FREE_ITEM_VALUE（48484），
                // AddItem 会保留它，且所有免费判断（IsFreeItem）能识别。
                if (IsFreeItem(item) && item.itemValue != FREE_ITEM_VALUE)
                {
                    item.itemValue = FREE_ITEM_VALUE;
                }
                obj.specialInvDatabase.AddItem(item);
            }
            state.LastItems = new List<InvItem>(items);
            state.LastCount = obj.specialInvDatabase.InvItemList?.Count ?? 0;
            CustomBuildingsPlugin.Logger.LogInfo($"[{obj.objectName}] OpenBuyChest: 已填充 {items.Count} 件商品, InvItemList.Count={obj.specialInvDatabase.InvItemList?.Count}");

            // 打开购买界面（NPC 商店，右键购买）
            obj.ShowNPCChest();
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.Logger.LogWarning($"[{obj.objectName}] OpenBuyChest 异常: {e}");
        }
    }

    /// <summary>
    /// 执行购买（类方法，用户在 <see cref="IStore.OnItemBought"/> 回调中判断后调用）：
    /// 定价（<c>determineMoneyCost(item, item.itemValue, 建筑名)</c>）→ 扣款（<c>moneySuccess</c>）
    /// → 商品从商店库存移到玩家背包（<c>MoveFromChestToInventory</c> 等价逻辑）。
    /// 扣款失败（钱不够）自动提示，返回 false；购买成功返回 true。
    /// </summary>
    /// <param name="store">实现了 <see cref="IStore"/> 的建筑。</param>
    /// <param name="obj">建筑对应的 <see cref="ObjectReal"/> 实例。</param>
    /// <param name="buyer">购买者（玩家）。</param>
    /// <param name="item">要购买的物品（玩家选中的商品）。</param>
    /// <returns>是否购买成功。</returns>
    public static bool PurchaseItem(this IStore store, ObjectReal obj, Agent buyer, InvItem item)
    {
        if (obj == null || buyer == null || item == null) return false;
        try
        {
            if (obj.specialInvDatabase == null || item.invItemName == null) return false;

            // 定价 + 扣款（moneySuccess 内部处理钱不够提示）
            // 免费商品（itemValue == 48484 或 0）：跳过扣款。
            if (!IsFreeItem(item))
            {
                int cost = obj.determineMoneyCost(item, item.itemValue, obj.objectName);
                if (!obj.moneySuccess(cost)) return false;
            }

            // 移货：商店库存移除商品 → 玩家背包加入
            if (!buyer.inventory.hasEmptySlotForItem(item))
            {
                buyer.inventory.PlayerFullResponse(buyer);
                return false;
            }
            // 从商店库存移除（避免残留）
            InvItem? shopItem = obj.specialInvDatabase.FindItem(item.invItemName);
            if (shopItem != null)
            {
                obj.specialInvDatabase.DestroyItem(shopItem);
            }
            // 加入玩家背包（AddItem 内部处理堆叠）
            InvItem added = buyer.inventory.AddItem(item);
            if (added != null)
            {
                return true;
            }
            return false;
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.Logger.LogWarning($"[{obj.objectName}] PurchaseItem 异常: {e}");
            return false;
        }
    }
}
