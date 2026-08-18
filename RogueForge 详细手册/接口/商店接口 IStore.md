---
classless: 2026-08-16T15:50:00
---
***引言：***
1. 该接口同步实现了比原版丰富的购买功能，不在局限于金钱购买。
2. 读取玩家背包物品的方法也放在了此接口。

# 一、商品添加方法
- 你必须实现此方法
- 如果你的建筑没有IStore接口，该接口将隐式执行，即按下交互键显示建筑物容器界面。
- 每次打开购买窗口时调用，返回全新 InvItem。
- 售价 = item.itemValue，默认由游戏配置。
- 如需自定义售价，在 ItemSetup 之后设置 item.itemValue = X 即可覆盖（非 0 才生效，为0使用游戏默认值）。
- 不需要金钱购买的商品，请将item.itemValue设置为48484

例：
```
public List<InvItem> GetBuyItems()
{
		InvItem bananaPeel = new InvItem();
		bananaPeel.invItemName = "BananaPeel";
		bananaPeel.invItemCount = 2;
		bananaPeel.ItemSetup(notNew: true);
		bananaPeel.itemValue = 5;  
		items.Add(bananaPeel);
		return items;
}
```

# 二、商品购买回调
- 你必须实现此方法。
- 使用this.PurchaseItem()进行商品购买，此次检验前面的特殊数字"48484"，以判断是否为免费商品。
- 如果想实现特殊效果购买，就将商品价格设置为"48484"，然后在this.PurchaseItem()方法前，进行其他条件判断(本来不想说，但是担心你不知道这个接口的自由度在哪里，就说一下吧)。

```
public void OnItemBought(InvItem item, Agent buyer)
{
	buyer!.Say("我买东西了！")
	// 进行购买
	this.PurchaseItem(this, buyer!, item!);
    }
```

# 三、自定义商品标签
- 为null为商品定价
- 需要改哪一个槽位就在你的建筑类中加上就行，不需要全部实现。

例：
```
/// <summary>商店第 1 个槽位。</summary>
public override virtual string? PriceOverride1 => null;
/// <summary>商店第 2 个槽位。</summary>
public override virtual string? PriceOverride2 => null;
/// <summary>商店第 3 个槽位。</summary>
public override virtual string? PriceOverride3 => null;
/// <summary>商店第 4 个槽位。</summary>
public override virtual string? PriceOverride4 => null;
/// <summary>商店第 5 个槽位。</summary>
public override virtual string? PriceOverride5 => null;
```

# 四、自定义商品槽位颜色
- 此处感谢群友@陈的技术支持
- 为null为商品定价不修改颜色
- 需要改哪一个槽位就在你的建筑类中加上就行，不需要全部实现。

例：
```
/// <summary>商店第 1 个槽位的自定义背景颜色</summary>
public override virtual UnityEngine.Color? PriceOverrideColor1 => null;
/// <summary>商店第 2 个槽位的自定义背景颜色</summary>
public override virtual UnityEngine.Color? PriceOverrideColor2 => null;
/// <summary>商店第 3 个槽位的自定义背景颜色</summary>
public override virtual UnityEngine.Color? PriceOverrideColor3 => null;
/// <summary>商店第 4 个槽位的自定义背景颜色</summary>
public override virtual UnityEngine.Color? PriceOverrideColor4 => null;
/// <summary>商店第 5 个槽位的自定义背景颜色</summary>
public override virtual UnityEngine.Color? PriceOverrideColor5 => null;
```

