using System;
using RogueLibsCore;
using UnityEngine;

#nullable enable
namespace RogueForge;

/// <summary>
/// 自定义建筑构建器，使用建造者模式为自定义建筑配置名称、描述、精灵图、放大倍率和克隆源。
/// 格式仿照 RogueLibsCore 的 <see cref="RogueLibsCore.ItemBuilder"/>。
/// </summary>
public class ObjectBuilder
{
    /// <summary>使用指定的自定义建筑元数据初始化构建器。</summary>
    /// <param name="metadata">自定义建筑的元数据。</param>
    public ObjectBuilder(CustomObjectMetadata metadata) => this.Metadata = metadata;

    /// <summary>获取与此建筑关联的自定义建筑元数据。</summary>
    public CustomObjectMetadata Metadata { get; }

    /// <summary>获取该建筑的自定义名称（如果已设置）。</summary>
    public CustomName? Name { get; private set; }

    /// <summary>获取该建筑的自定义描述（如果已设置）。</summary>
    public CustomName? Description { get; private set; }

    /// <summary>获取该建筑的自定义精灵图（如果已设置）。</summary>
    public RogueSprite? Sprite { get; private set; }

    /// <summary>为该建筑设置自定义显示名称。</summary>
    /// <param name="info">包含多语言名称信息的结构体。</param>
    /// <returns>当前构建器实例，支持链式调用。</returns>
    public ObjectBuilder WithName(CustomNameInfo info)
    {
        this.Name = RogueLibs.CreateCustomName(this.Metadata.Name, "Object", info);
        this.Metadata.name = this.Name;
        return this;
    }

    /// <summary>为该建筑设置自定义描述文本。</summary>
    /// <param name="info">包含多语言描述信息的结构体。</param>
    /// <returns>当前构建器实例，支持链式调用。</returns>
    public ObjectBuilder WithDescription(CustomNameInfo info)
    {
        // 描述名称为 "D_" + 名称（ObjectReal.Start 用 gc.nameDB.GetName(objectName, "Description") 查询）
        this.Description = RogueLibs.CreateCustomName("D_" + this.Metadata.Name, "Description", info);
        this.Metadata.description = this.Description;
        return this;
    }

    /// <summary>使用原始图片数据为该建筑设置精灵图（注入 ObjectReals 图集）。</summary>
    /// <param name="rawData">图片的原始字节数据。</param>
    /// <param name="ppu">每单位像素数，默认为64。</param>
    /// <returns>当前构建器实例，支持链式调用。</returns>
    public ObjectBuilder WithSprite(byte[] rawData, float ppu = 64f)
    {
        this.Sprite = RogueLibs.CreateCustomSprite(this.Metadata.Name, SpriteScope.Objects, rawData, ppu);
        this.Metadata.sprite = this.Sprite;
        return this;
    }

    /// <summary>使用原始图片数据和指定裁剪区域为该建筑设置精灵图。</summary>
    /// <param name="rawData">图片的原始字节数据。</param>
    /// <param name="region">精灵图在原始图片中的裁剪区域。</param>
    /// <param name="ppu">每单位像素数，默认为64。</param>
    /// <returns>当前构建器实例，支持链式调用。</returns>
    public ObjectBuilder WithSprite(byte[] rawData, Rect region, float ppu = 64f)
    {
        this.Sprite = RogueLibs.CreateCustomSprite(this.Metadata.Name, SpriteScope.Objects, rawData, region, ppu);
        this.Metadata.sprite = this.Sprite;
        return this;
    }

    /// <summary>
    /// 为该建筑设置四方向精灵图（参考 ATM 的 fourDirection：不同朝向用不同贴图）。
    /// 图片顺序为<b>北、东、南、西</b>，生成建筑时按朝向自动切换对应贴图，默认朝向为北。
    /// 精灵注册名（与<b>原版 fourDirection 规则</b>一致，见 BasicObject.Spawn）：
    /// 南 = 建筑名（无后缀，同时作为图标/基础精灵）；北 = 建筑名 + "N"；东 = 建筑名 + "E"；西 = 建筑名 + "W"。
    /// 注意：与 <see cref="WithSprite"/> 二选一使用，调用本方法后基础精灵即南向图。
    /// </summary>
    /// <param name="north">北向贴图原始字节数据。</param>
    /// <param name="east">东向贴图原始字节数据。</param>
    /// <param name="south">南向贴图原始字节数据。</param>
    /// <param name="west">西向贴图原始字节数据。</param>
    /// <param name="ppu">每单位像素数，默认为64。</param>
    /// <returns>当前构建器实例，支持链式调用。</returns>
    public ObjectBuilder WithSprites(byte[] north, byte[] east, byte[] south, byte[] west, float ppu = 64f)
    {
        // 南方向：注册为无后缀名（原版 fourDirection 规则：S 用基础名），同时作为图标/基础精灵
        this.Metadata.spriteS = RogueLibs.CreateCustomSprite(this.Metadata.Name, SpriteScope.Objects, south, ppu);
        this.Metadata.spriteN = RogueLibs.CreateCustomSprite(this.Metadata.Name + "N", SpriteScope.Objects, north, ppu);
        this.Metadata.spriteE = RogueLibs.CreateCustomSprite(this.Metadata.Name + "E", SpriteScope.Objects, east, ppu);
        this.Metadata.spriteW = RogueLibs.CreateCustomSprite(this.Metadata.Name + "W", SpriteScope.Objects, west, ppu);
        this.Metadata.sprite = this.Metadata.spriteS;   // 基础精灵 = 南向图（图标/默认）
        this.Sprite = this.Metadata.spriteS;
        return this;
    }

    /// <summary>设置精灵放大倍率（64x64 贴图=游戏1格，2f 约等于 ATM 大小）。</summary>
    /// <param name="scale">放大倍率。</param>
    /// <returns>当前构建器实例，支持链式调用。</returns>
    public ObjectBuilder WithScale(float scale)
    {
        this.Metadata.SpriteScale = scale;
        return this;
    }

    /// <summary>设置 prefab 克隆源物件名（默认 "Chair"）。</summary>
    /// <param name="cloneSource">游戏内现有物件名，如 "Chair"、"Table"、"Refrigerator"。</param>
    /// <returns>当前构建器实例，支持链式调用。</returns>
    public ObjectBuilder WithCloneSource(string cloneSource)
    {
        if (string.IsNullOrEmpty(cloneSource))
            throw new ArgumentException("cloneSource cannot be null or empty!", nameof(cloneSource));
        this.Metadata.CloneSource = cloneSource;
        return this;
    }
}
