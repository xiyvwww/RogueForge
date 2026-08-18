using System;

#nullable enable
namespace RogueForge;

/// <summary>
/// 自定义建筑名称特性。指定自定义建筑类对应的游戏内物件名称标识。
/// 与 RogueLibsCore 的 <see cref="RogueLibsCore.ItemNameAttribute"/> 格式一致。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ObjectNameAttribute : Attribute
{
    /// <summary>获取建筑名称标识。</summary>
    public string Name { get; }

    /// <summary>
    /// 使用指定名称初始化建筑名称特性。
    /// </summary>
    /// <param name="name">建筑名称标识（必须与 Sprite 名一致，否则编辑器只显示占位图）。</param>
    public ObjectNameAttribute(string name)
    {
        this.Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}
