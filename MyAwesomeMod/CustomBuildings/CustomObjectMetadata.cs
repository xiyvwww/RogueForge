using System;
using System.Collections.Generic;
using System.Reflection;
using RogueLibsCore;

#nullable enable
namespace RogueForge;

/// <summary>
/// 自定义建筑元数据。存储建筑类型、名称、精灵图、放大倍率、克隆源等信息。
/// 与 RogueLibsCore 的 <see cref="RogueLibsCore.CustomItemMetadata"/> 格式一致。
/// </summary>
public sealed class CustomObjectMetadata
{
    /// <summary>所有建筑元数据的缓存字典。</summary>
    private static readonly Dictionary<Type, CustomObjectMetadata> infos = new Dictionary<Type, CustomObjectMetadata>();

    /// <summary>获取建筑的类型。</summary>
    public Type Type { get; }

    /// <summary>获取建筑的名称标识。</summary>
    public string Name { get; }

    /// <summary>建筑的自定义精灵图（由 Builder.WithSprite 注入）。</summary>
    internal RogueSprite? sprite;

    /// <summary>北向精灵（由 Builder.WithSprites 注入，注册名 = Name + "N"）。</summary>
    internal RogueSprite? spriteN;

    /// <summary>东向精灵（由 Builder.WithSprites 注入，注册名 = Name + "E"）。</summary>
    internal RogueSprite? spriteE;

    /// <summary>南向精灵（由 Builder.WithSprites 注入，注册名 = Name，无后缀——原版 fourDirection 规则）。</summary>
    internal RogueSprite? spriteS;

    /// <summary>西向精灵（由 Builder.WithSprites 注入，注册名 = Name + "W"）。</summary>
    internal RogueSprite? spriteW;

    /// <summary>是否为四方向建筑（调用了 WithSprites 注册了方向精灵）。</summary>
    public bool IsFourDirection => this.spriteN != null || this.spriteE != null || this.spriteS != null || this.spriteW != null;

    /// <summary>精灵放大倍率（由 Builder.WithScale 注入，默认 1f）。</summary>
    public float SpriteScale { get; internal set; } = 1f;

    /// <summary>prefab 克隆源物件名（由 Builder.WithCloneSource 注入，默认 "Chair"）。</summary>
    public string CloneSource { get; internal set; } = "Chair";

    /// <summary>建筑的自定义显示名称（由 Builder.WithName 注入）。</summary>
    internal CustomName? name;

    /// <summary>建筑的自定义描述（由 Builder.WithDescription 注入）。</summary>
    internal CustomName? description;

    /// <summary>获取建筑的显示名称。</summary>
    /// <returns>自定义名称实例，未设置返回 null。</returns>
    public CustomName? GetName() => this.name;

    /// <summary>获取建筑的描述文本。</summary>
    /// <returns>自定义名称实例，未设置返回 null。</returns>
    public CustomName? GetDescription() => this.description;

    /// <summary>获取建筑的精灵图。</summary>
    /// <returns>精灵图实例，未设置返回 null。</returns>
    public RogueSprite? GetSprite() => this.sprite;

    /// <summary>根据类型获取或创建建筑元数据。</summary>
    /// <param name="type">建筑类型。</param>
    /// <returns>建筑元数据实例。</returns>
    public static CustomObjectMetadata Get(Type type)
    {
        if (!infos.TryGetValue(type, out CustomObjectMetadata? metadata))
        {
            metadata = new CustomObjectMetadata(type);
            infos[type] = metadata;
        }
        return metadata;
    }

    /// <summary>根据泛型类型获取或创建建筑元数据。</summary>
    /// <typeparam name="TCustomObject">自定义建筑类型。</typeparam>
    /// <returns>建筑元数据实例。</returns>
    public static CustomObjectMetadata Get<TCustomObject>() where TCustomObject : CustomObjectReal
        => CustomObjectMetadata.Get(typeof(TCustomObject));

    /// <summary>
    /// 使用指定建筑类型初始化元数据。从类型特性中读取名称。
    /// </summary>
    /// <param name="type">建筑类型，必须继承自 <see cref="CustomObjectReal"/>。</param>
    private CustomObjectMetadata(Type type)
    {
        this.Type = typeof(CustomObjectReal).IsAssignableFrom(type)
            ? type
            : throw new ArgumentException("type does not inherit from CustomObjectReal!", nameof(type));
        this.Name = type.GetCustomAttribute<ObjectNameAttribute>()?.Name ?? type.Name;
    }
}
