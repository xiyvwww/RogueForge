#nullable enable
namespace RogueForge;

/// <summary>
/// 小地图传送标记接口。
/// 实现此接口的自定义建筑会自动在小地图/大地图上生成标记
/// （参考 ATM：Start 中调用 MinimapDisplay → 创建 NonQuestObject 标记）。
/// 玩家打开大地图（Tab）点击该标记时，会被传送到本建筑。
/// 标记默认视为已发现（playerSeen=true），无需玩家先接近即可点击传送。
/// </summary>
public interface IMinimapTeleportable
{
    /// <summary>
    /// 返回小地图/大地图图标的缩放倍率。
    /// 1f = 默认大小，2f = 两倍大，0.5f = 一半大小。
    /// 实现 IMinimapTeleportable 的类必须实现此方法。
    /// </summary>
    float GetMinimapIconScale();
}
