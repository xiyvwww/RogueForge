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