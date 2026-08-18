#nullable enable
namespace RogueForge;

/// <summary>
/// 背包接口（由原 IBuyable 拆分而来，商店购买功能见 <see cref="IStore"/>）。
/// 实现此接口的建筑支持"背包物品点击回调"：玩家通过交互按钮打开背包选择界面
/// （<c>m.Object.ShowUseOn("自定义useOnType")</c>）后点选背包物品，
/// 原版链路 InvSlot → <see cref="PlayfieldObject.UseItemOnObject"/> 调用本方法。
///
/// 使用方式：
///   1. 在 <see cref="IObjectInteraction.SetupInteractions"/> 中添加按钮打开背包选择界面：
///      <c>h.AddButton("RogueForge_识别物品", m => m.Object.ShowUseOn("RogueForge_SayItem"))</c>
///   2. 实现 <see cref="UseItemOnObject"/>（override 原版虚方法）：
///      - useOnType 不匹配时返回 false（放行原版/其他用途）
///      - combineType != "Combine" 是 UI 高亮检测阶段（反复调用），只报告可用性，不能有副作用
///      - combineType == "Combine" 是实际点击，执行真正逻辑
///
/// 签名与原版 <see cref="PlayfieldObject.UseItemOnObject"/> / <see cref="ObjectReal.UseItemOnObject"/> 一致；
/// 原版基类已提供默认实现（返回 false），实现本接口的建筑继承基类成员即可满足接口，按需 override。
/// </summary>
public interface IBackpack
{
    /// <summary>
    /// 背包物品点击回调（玩家在"使用物品"界面点选背包物品后触发）。必须实现。
    /// </summary>
    /// <param name="item">玩家选中的背包物品。</param>
    /// <param name="slotNum">物品槽位号。</param>
    /// <param name="combineType">"" = UI 高亮检测（只报告可用性，不能有副作用）；"Combine" = 实际点击。</param>
    /// <param name="useOnType">使用场景标识（ShowUseOn 传入的自定义字符串）。</param>
    /// <returns>是否处理了该物品（true = 已处理，false = 放行原版逻辑）。</returns>
    bool UseItemOnObject(InvItem item, int slotNum, string combineType, string useOnType);
}
