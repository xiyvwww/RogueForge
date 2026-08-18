using System;
using System.Collections.Generic;
using UnityEngine;

#nullable enable
namespace RogueForge;

/// <summary>
/// 可打开容器建筑接口（参考 TrashCan / Chest）。
/// 实现此接口的自定义建筑自动获得"打开容器"交互：玩家靠近后按 E 直接打开容器界面取物品。
///
/// 使用方式：
///   1. 实现 <see cref="IObjectContainer"/>（<see cref="FillContainer"/> 必须实现；
///      <see cref="CanOpenContainer"/> 控制是否可打开；<see cref="OnContainerOpened"/> 是默认空实现
///      （什么都不做），可按需 override 处理打开后的自定义逻辑；
///      <see cref="GetContainerItems"/> / <see cref="SetContainerItems"/> 基类已提供 virtual 默认实现
///      （读取/替换本建筑容器内的全部物品），按需 override）。
///   2. 打开容器的方法由接口层提供（扩展方法 <see cref="IObjectContainerExtensions.OpenContainer"/>），
///      在交互按钮中调用：<c>h.AddButton("RogueForge_打开", m => this.OpenContainer(m.Object, m.Agent))</c>
///      （按钮名称用 "RogueForge_" 前缀，显示文本注册由用户自行处理）
///   3. 若建筑未 override <see cref="IObjectInteraction.SetupInteractions"/>，基类
///      <see cref="CustomObjectReal"/> 会自动添加 "Open" 按钮（隐式按 E 直接打开容器界面，参考原版 TrashCan）。
///
/// 打开机制：<see cref="ObjectReal.ShowChest"/> → 容器界面（原版自动处理拾取/放入）。
/// 说明：<see cref="OnContainerOpened"/> 本意是默认接口方法（空），因目标框架 net471 不支持
/// 默认接口实现（编译报 CS8701），故声明为抽象成员 + 基类 <see cref="CustomObjectReal"/> 提供
/// virtual 空实现兜底——继承基类的实现类无需实现本方法（效果等同"方法体为空"）。
/// </summary>
public interface IObjectContainer
{
    /// <summary>是否允许打开容器（false 时基类不添加 Open 按钮）。</summary>
    bool CanOpenContainer { get; }

    /// <summary>
    /// 填充容器初始物品（容器初始化时由库调用一次，仅服务端）。必须实现。
    /// 用 <see cref="InvDatabase.AddItem(string, int)"/> 添加物品，例如：database.AddItem("BananaPeel", 3);
    /// </summary>
    /// <param name="database">本建筑的物品栏。</param>
    void FillContainer(InvDatabase database);

    /// <summary>容器打开后的回调（默认空实现 = 什么都不做，可 override 添加自定义逻辑）。</summary>
    void OnContainerOpened();

    /// <summary>
    /// 获取当前实例容器内的所有物品（基类默认返回容器物品列表的<b>副本</b>，
    /// 修改返回的列表不影响容器内实际物品；无物品栏时返回空列表）。
    /// 需要直接操作容器时可按需 override。
    /// </summary>
    /// <returns>容器内的全部物品。</returns>
    List<InvItem> GetContainerItems();

    /// <summary>
    /// 设置当前实例容器内的所有物品（基类默认清空容器原有物品后逐件加入给定列表；
    /// 无物品栏时忽略；<paramref name="items"/> 为 null 等于清空容器）。
    /// 需要自定义设置逻辑时可按需 override。
    /// </summary>
    /// <param name="items">要放入容器的新物品列表（可为 null = 清空容器）。</param>
    void SetContainerItems(List<InvItem> items);
}

/// <summary>
/// 容器接口的扩展方法（接口层提供"打开容器"的实现）。
/// 因目标框架 net471 不支持默认接口实现，用扩展方法模拟。
/// </summary>
public static class IObjectContainerExtensions
{
    /// <summary>
    /// 打开容器（由接口层提供）：调用 <see cref="ObjectReal.ShowChest"/> 打开容器界面，
    /// 并触发 <see cref="IObjectContainer.OnContainerOpened"/> 回调。
    /// </summary>
    /// <param name="container">实现了 <see cref="IObjectContainer"/> 的建筑。</param>
    /// <param name="obj">建筑对应的 <see cref="ObjectReal"/> 实例。</param>
    /// <param name="agent">交互者（玩家，可为 null）。</param>
    public static void OpenContainer(this IObjectContainer container, ObjectReal obj, Agent? agent)
    {
        if (container == null || obj == null) return;
        try
        {
            obj.ShowChest();
            container.OnContainerOpened();
            CustomBuildingsPlugin.Logger?.LogInfo($"[{obj.objectName}] 容器界面已打开");
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.Logger?.LogWarning($"[{obj.objectName}] OpenContainer 异常: {e.Message}");
        }
    }
}
