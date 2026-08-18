#nullable enable
namespace RogueForge;

/// <summary>
/// 建筑破碎接口（已合并原 IObjectExplosive 的爆炸三件套）：
/// 建筑被打碎时（<see cref="CustomObjectReal.BreakOpen"/>）先执行 <see cref="OnBreakOpen"/>，
/// 再执行破碎逻辑（洒落容器物品、爆炸、音效、打通墙、销毁）。
///
/// 基类 <see cref="CustomObjectReal"/> 已实现本接口（<c>public virtual</c> 成员），
/// 子类只需 override 需要的成员即可自定义破碎/爆炸逻辑，无需额外声明接口。
///
/// <b>爆炸触发规则：</b>爆炸三件套（<see cref="ExplosionRadius"/> / <see cref="ExplosionDamage"/> /
/// <see cref="OnExplode"/>）中，<b>只要用户端 override 了 <see cref="OnExplode"/>，爆炸系统即启用</b>：
/// - 用户端 <see cref="OnExplode"/> 方法体为空（只写了 {}，或没有写任何语句）→ <b>参照源码执行默认操作</b>：
///   打碎时自动生成爆炸，威力以用户端 <see cref="ExplosionRadius"/> / <see cref="ExplosionDamage"/>
///   定义为优先（&gt;0 才覆盖默认值；未设置则用原版 "Normal" 爆炸默认值），爆炸后再调用 OnExplode（空，无事发生）。
/// - 用户端 <see cref="OnExplode"/> 方法体非空 → <b>不执行默认操作</b>，只执行用户端方法体内的逻辑
///   （需要爆炸时请在方法体内自行生成）。
/// 未 override <see cref="OnExplode"/> 的类打碎时完全不触发爆炸效果。
///
/// 使用方式：
/// <code>
/// public class MyBuilding : CustomObjectReal
/// {
///     public override void OnBreakOpen(PlayfieldObject damagerObject)
///     {
///         base.OnBreakOpen(damagerObject);
///         // 破碎前执行：播放音效、生成粒子、掉落专属物品等
///     }
///
///     // 想让建筑打碎时自动爆炸（默认操作）：override OnExplode 且方法体留空 + 设置威力
///     public override float ExplosionRadius => 3f;
///     public override int ExplosionDamage => 30;
///     public override void OnExplode(PlayfieldObject? damagerObject) { }
///
///     // 或完全自定义：override OnExplode 并在方法体内自行生成爆炸/后处理（默认操作不会执行）
/// }
/// </code>
/// </summary>
public interface IBreakOpen
{
    /// <summary>
    /// 建筑破碎回调（在物品洒落、音效、销毁之前调用）。必须实现。
    /// </summary>
    /// <param name="damagerObject">造成破碎的物体（可为 null）。</param>
    void OnBreakOpen(PlayfieldObject damagerObject);

    /// <summary>爆炸半径（&gt;0 才覆盖默认值，默认参考值 3f；保持基类默认 0 = 用原版 "Normal" 爆炸默认半径）。</summary>
    float ExplosionRadius { get; }

    /// <summary>爆炸伤害（&gt;0 才覆盖默认值，默认参考值 30；保持基类默认 0 = 用原版 "Normal" 爆炸默认伤害）。</summary>
    int ExplosionDamage { get; }

    /// <summary>爆炸后处理回调（方法体为空 = 库执行默认操作：自动生成爆炸；方法体非空 = 不生成爆炸，只执行本方法）。</summary>
    /// <param name="damagerObject">引爆来源（可为 null）。</param>
    void OnExplode(PlayfieldObject? damagerObject);
}
