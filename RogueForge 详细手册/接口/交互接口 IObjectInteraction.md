---
classless: 2026-08-16T14:07:00
---
***引言：***
1. 该接口同步实现入侵建筑。
2. 该接口同步实现操作进度条机制，但是对应时间是否可控制存疑。
3. 该接口不用实现，默认实现在CustomObjectReal类，用于隐式执行打开建筑物容器或打开建筑物购买页面。

# 一、近距离交互
- 使用交互接口，你首先必须实现SetupInteractions()方法。
- RogueLibsCore为RogueForge提供交互功能
- ”h“是按钮的执行类，继承(SimpleInteractionProvider)：
	- h.AddButton() 添加按钮
- ”m“是按钮的回调参数，它提供四种关键成员：
	- m.Object 继承”PlayfieldObject“，是交互对象。
	- m.Agent  继承”Agent“，交互者，不可使用ObjectReal.interactingAgent属性。
	- m.Helper  交互助手（`InteractionHelper`，含 `interactingFar` 等状态）
	- m.gc        游戏控制器
- 一般使用try { this.StopInteraction(); } catch { }停止交互。

例：
```
public override void SetupInteractions(SimpleInteractionProvider h)
{
	h.AddButton("RogueForge_按钮", m =>
	{
		m.Agent.Say("你点击了一个按钮。");
	});
}
```

**注：**
1. 使用h.AddButton()添加按钮时，你的按钮名必须加上”RogueForge_“前缀，不然你的按钮名称会出现莫名奇妙的前缀。

# 二、入侵交互
- 使用h.Helper的interactingFar属性判断是否为黑客入侵交互。

例：
```
public override void SetupInteractions(SimpleInteractionProvider h)
{
	if (h.Helper.interactingFar)
	{
		// 黑客菜单按钮：启动自毁（调用原版 HackExplode，黑客炸毁本建筑）
		h.AddButton("RogueForge_启动自毁", m =>
		{
			if (m.Object is ObjectReal target)
			{
				target.HackExplode(m.Agent);
			}
		});
			return;   // 入侵交互时不再添加近距离按钮
	}
}
```

# 三、入侵回调
- 使用此方法，你必须在全部路径调用base.OnHackingComplete(hacker)。
- this.ShowObjectButtons()先将h.Helper.interactingFar设为true，然后转入SetupInteractions()方法。

例：
```
protected override void OnHackingComplete(Agent hacker)
{
	base.OnHackingComplete(hacker); //入侵完成函数
    
    this.ShowObjectButtons();  //显示入侵按钮函数
}
```
# 四、操作进度条
- 使用StartDelayedAction()方法，也就是CustomObjectReal类方法，但是否可在其他地方使用存疑。
- 第二个入参为进度条等待时间存疑。

例：
```
public override void SetupInteractions(SimpleInteractionProvider h)
{
	h.AddButton("RogueForge_翻找", m =>
	{
		// 延迟操作：进度条走完才执行Agent.Say();
		StartDelayedAction(m.Agent, 2f, "RogueForge_翻找", () =>
		{
			m.Agent.Say("啥也没有。");
		});
	});
}
```