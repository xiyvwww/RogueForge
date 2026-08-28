---
classless: 2026-08-16T15:13:00
---
***引言：***
1. 实现类似原版垃圾桶的效果，并支持物品掉落。

# 一、填充容器初始物品
- 必须实现此函数
- 如果你的建筑没有IStore接口，该接口将隐式执行，即按下交互键显示建筑物容器界面。

例：
```
public void FillContainer(InvDatabase database)
{
	database.AddItem("BananaPeel", 500);   // 500 个香蕉皮，玩家打开容器可以拿
}
```

# 二、容器打开回调
- this.OpenContainer((ObjectReal)m.Object, m.Agent);使用此函数打开建筑容器。

例子
```
public override void OnContainerOpened()
{
	interactingAgent.Say("我打开了这个建筑");
}
```

# 三、常用方法

示：
```
// 读取容器内所有物品
List<InvItem> items = this.GetContainerItems();

// 整体替换容器内容（先清空再放入）
this.SetContainerItems(new List<InvItem> { new InvItem { invItemName = "BananaPeel", invItemCount = 3 }
```

# 四、InvDatabase类常用用法

示：
```
/// 添加物品
InvItem AddItem(InvItem item)  /// 放置物品
InvItem AddItem(string itemName, int itemCount) /// 按名字放入物品
bool AddItemAtEmptySlot(InvItem item, bool showAnim, bool puttingBack) /// 指定空槽位

/// 查找 或 判断
bool HasItem(string itemName) /// 是否拥有（含临时槽位/安全列表 这两个槽位定义存疑。）
InvItem FindItem(string itemName) /// 按名字找，未找到返回 null
InvItem FindItem(string itemName, List<string> contents) / FindItemWithCount(name, count)
bool isEmpty() / hasEmptySlot() / hasEmptySlotForItem(InvItem) /// 空判断

//// 删除 或 清空 或 修改数量

void DestroyItem(InvItem item) / DestroyItem(int slotNum) /// 清空物品或槽位
void DestroyAllItems() /// 全部清空
void SubtractFromItemCount(InvItem item, int amount) / ChangeItemCount(InvItem, int) /// 删除物品
```