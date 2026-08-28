using System;
using RogueLibsCore;

#nullable enable
namespace RogueForge;

/// <summary>
/// 交互配置接口（替代基类虚方法 SetupInteractions）。
/// 实现此接口的建筑，其 <see cref="SetupInteractions"/> 会在玩家交互时被库自动调用，
/// 用 <paramref name="h"/>（<see cref="SimpleInteractionProvider"/>）添加交互按钮。
///
/// 示例：
/// <code>
/// public class MyBuilding : CustomObjectReal, IObjectInteraction
/// {
///     public void SetupInteractions(SimpleInteractionProvider h)
///     {
///         h.AddButton("RogueForge_打开", m => { /* 处理点击 */ });
///     }
///
///     public override void OnHackingComplete(Agent hacker)
///     {
///         // 可选：玩家黑客入侵成功后执行自定义逻辑（不 override 则什么都不做）
///     }
/// }
/// </code>
///
/// 说明：
/// - <see cref="SetupInteractions"/>：<b>必须实现</b>。基类 <see cref="CustomObjectReal"/> 已实现本接口，
///   其默认 SetupInteractions 委托给默认交互（实现 <see cref="IStore"/> 自动添加购买按钮 /
///   实现 <see cref="IObjectContainer"/> 自动添加 Open 按钮）；子类 override 后完全自定义按钮
///   （默认交互不再自动添加，需要时请在 SetupInteractions 中自行添加）。
/// - <see cref="OnHackingComplete"/>：<b>默认空实现</b>（什么都不做），子类按需 override。
///   <b>入侵门禁</b>：override 本方法即视为该建筑启用了黑客入侵（无需再 override
///   <see cref="CustomObjectReal.CanBeHacked"/>）；未 override 的建筑默认不可被入侵。
///   本意是"默认接口方法"，但目标框架 net471 不支持默认接口实现（编译报 CS8701），
///   故声明为抽象成员 + 基类 <see cref="CustomObjectReal"/> 提供 virtual 空实现兜底——
///   实现本接口的类继承基类即可（不写也不会报错，效果等同"方法体为空"）。
/// </summary>
public interface IObjectInteraction
{
    /// <summary>设置本建筑的交互按钮（库在玩家交互时自动调用）。必须实现。</summary>
    /// <param name="h">交互提供者：用 h.AddButton / h.AddImplicitButton 添加按钮。</param>
    void SetupInteractions(SimpleInteractionProvider h);

    /// <summary>
    /// 黑客入侵完成回调（默认空实现 = 什么都不做，子类可 override）。
    /// 玩家手持黑客工具/笔记本电脑远程按 E 入侵本建筑，2 秒进度条走完后调用
    /// （参考原版 Computer：HackingToolHack/LaptopHack → 进度条 barType "Hacking" → FinishedOperating）。
    /// 可用于执行黑客成功效果：解锁设备、发放奖励、Say 台词、洒落物品、触发任务等。
    /// 注意：不会自动弹出操作按钮菜单；如需菜单，override 后调用 ShowObjectButtons()
    /// 并在 DetermineButtons() 中添加按钮。
    /// 说明：本意是默认接口方法（空），因 net471 不支持（CS8701）改为抽象成员，
    /// 基类 <see cref="CustomObjectReal"/> 已提供 virtual 空实现，继承基类的实现类无需实现本方法。
    /// </summary>
    /// <param name="hacker">执行黑客入侵的玩家（可为 null）。</param>
    void OnHackingComplete(Agent hacker);
}

/// <summary>
/// 延迟操作接口（操作进度条，参考 ATM 收集外星人零件：点按钮 → 进度条 → 完成回调；中断则取消）。
/// 实现此接口的建筑可使用基类 <see cref="CustomObjectReal.StartDelayedAction"/> 启动操作进度条：
///
/// <code>
/// public class MyBuilding : CustomObjectReal, IDelayedOperating
/// {
///     public void DoSomething(Agent agent)
///     {
///         StartDelayedAction(agent, 2f, "RogueForge_操作", () =>
///         {
///             // 进度条走完后的具体内容
///         });
///     }
/// }
/// </code>
/// 进度条标题（barType）需注册 Interface 名称显示文本（用户自行处理）。
/// 中断（离开范围/移动/死亡/按取消键）→ 进度条取消，回调不执行，需重新交互触发。
/// </summary>
public interface IDelayedOperating
{
    // 标记接口：方法实现由基类 CustomObjectReal 提供（protected StartDelayedAction + FinishedOperating）。
    // net471 不支持默认接口实现，故方法留在基类。
}
