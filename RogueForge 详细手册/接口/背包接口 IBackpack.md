---
classless: 9998-08-17T18:40:00
---
# 实现
- UseItemOnObject()函数本身是ObjectReal类里的方法，RogueForge加了一些辅助代码，让它可直接使用。
- CustomObjectReal.ShowUseOn()此函数可以打开玩家背包，接收一个String类型入参，该入参用于区分打开类型。
- 对于combineType的用法，我不是很清楚，因为完全可以根据item进行是否可用的判断。
- 如果不想使用此方法以读取玩家背包，可以不用实现。

例：
```
public void SetupInteractions(SimpleInteractionProvider h)
{
    h.AddButton("RogueForge_识别物品", m =>
		{
			m.Object.ShowUseOn("RogueForge_RecognizeItem");
		});
}

/// 玩家选背包物品后的回调
public override bool UseItemOnObject(InvItem item, int slotNum, string combineType, string useOnType)
{
	if (useOnType != "RogueForge_RecognizeItem") return false;
	// UI 高亮检测：所有物品都可用
	if (combineType != "Combine") return true;
	{
		if (interactingAgent != null)
		{
			interactingAgent.Say(item.invItemName);
		}
		return true;
	}
}
```