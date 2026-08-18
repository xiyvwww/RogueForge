using UnityEngine;

#nullable enable
namespace RogueForge;

/// <summary>
/// 建筑刷新接口：用于<b>普通关卡</b>（非关卡编辑器）中自动刷新自定义建筑。
/// 实现此接口的建筑，会在关卡加载生成阶段（<see cref="LoadLevel.SetupMore3_3"/> 钩子，
/// 仅服务端、非关卡编辑器、非内存测试模式）自动调用 <see cref="OnLevelSpawn"/>，
/// 让自定义建筑像原版建筑一样在普通关卡中自动出现。
///
/// 使用方式（在自定义建筑类中实现）：
/// <code>
/// public class MyBuilding : CustomObjectReal, IObjectInteraction, IBuildingSpawner
/// {
///     // 普通关卡刷新回调：关卡生成环境物体时调用（仅服务端）
///     public void OnLevelSpawn(LoadLevel level)
///     {
///         // 1. 找到目标类型的所有区块（如银行）
///         List&lt;Chunk&gt; banks = KMap.GetChunksByDescription("Bank");
///         if (banks.Count == 0) return;
///
///         // 2. 在第一个银行区块内找随机空地
///         Vector2 pos = KMap.FindEmptySpotInChunk(banks[0]);
///         if (pos == Vector2.zero) return;
///
///         // 3. 在空地生成自己的建筑
///         KMap.SpawnObject("MyBuilding", pos);
///     }
/// }
/// </code>
/// 注意：本接口方法通过该类型的 <b>prefab 模板实例</b> 调用（模板跨场景存活），
/// 因此回调内<b>不要依赖 this 的实例状态</b>（如物体位置），只用 <see cref="KMap"/>
/// 辅助方法或静态数据生成建筑。
/// </summary>
public interface IBuildingSpawner
{
    /// <summary>
    /// 普通关卡刷新回调（仅服务端，<see cref="LoadLevel.SetupMore3_3"/> 钩子调用）。
    /// 在此使用 <see cref="KMap"/> 的辅助方法生成自己的建筑。
    /// </summary>
    /// <param name="level">当前 <see cref="LoadLevel"/> 实例（可访问 levelChunks、tileInfo 等）。</param>
    void OnLevelSpawn(LoadLevel level);
}
