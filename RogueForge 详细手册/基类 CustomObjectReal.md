---
classless: 2020-08-16T13:12:00
aliases:
  - CustomObjectReal
---
# 一、RogueForge 前置设置
- 你必须按照如下格式进行设置，否则RogueForge不会进行工作。

例：
```
public void Awake()
{
	Logger = base.Logger;
	RogueLibs.LoadFromAssembly();
	RoguePatcher patcher = new RoguePatcher(this);
	
	// 加载RogueForge的所有功能
	RogueForge.CustomBuildingsPlugin.Initialize(this);
	
	/* your Awesome Code */
}
```

# 二、基础实现
1. 实现一个建筑，该建筑必须基础CustomObjectReal类。
2. 你首先需要实现两个方法：Setup()、SetVars()，以设置建筑的基础属性。幸运的是RogueForge提供了与RogueLibs相似格式的构建方法，降低了学习难度。
3. 想使用建筑的一些特殊属性，建议先阅读ObjectReal源码。特殊属性比如interactingAgent建筑交互对象。

例：
```
[ObjectName("YourBuilding")]
public class YourBuilding : CustomObjectReal
{
	[RLSetup]
	public static void Setup()
	{
		CustomObjects.CreateCustomObject<YourBuilding>()
			.WithName(new CustomNameInfo { Chinese = "你的建筑" })
			.WithDescription(new CustomNameInfo { Chinese = "对你建筑功能的描述" })
			.WithSprites(
				Mod.Properties.Resources.YourBuildingSprite_N,   // 北
				Mod.Properties.Resources.YourBuildingSprite_E,   // 东
				Mod.Properties.Resources.YourBuildingSprite_S,   // 南
				Mod.Properties.Resources.YourBuildingSprite_W)   // 西
			.WithScale(1f) // 建筑物缩放，主要用于图片过小或过大的问题。
			.WithCloneSource("Chair"); //该建筑的克隆源
	}
	// 以下是设置自定义建筑的具体属性，仅供参考。
	public override void SetVars()
	{
		base.damageAccumulates = true;         // 像门：伤害累积到阈值后打碎
		base.damageThreshold = 30;             // 累积 30 点伤害后打碎
		base.hackable = true;                  // 可被黑客工具远程入侵
		this.bulletsCanPass = true;            // 子弹可以穿过（像窗）
		this.noInterest = true;                // NPC 不会对它有自发兴趣
		this.strikeOnHit = true;               // 近战打到它会有"打中硬物"反馈
		this.noShadow = true;                  // 不投影地面阴影
		this.collidersDontDisappear = true;    // 被打碎前碰撞始终存在
		this.damageImmediateOnClient = true;   // 多人：客户端立即表现伤害
		this.importantToClient = true;         // 客户端不因流式加载忽略它
		base.fireProof = true;                 // 防火
		base.interactable = true;              // 在家园基地中不可交互
		this.pickUppable = false;              // 可被拾取
	}
}
```

**注：**
1. 你必须保证ObjectName()的入参和你的自定义建筑类名一致。
2. 建议在设置SetVars()时，去源代码参考同类建筑的 SetVars()，复制后修改适配。
3. 对于WithCloneSource()，RogueForge会以原游戏建筑为模板生成自定义建筑，故一些基础的组件或属性会继承克隆建筑。但是对于到底有哪些组件或属性会继承，目前并不清楚，所以非必要情况下建议使用“Chair”，因为RogueForge的一切测试都是以“Chair”为克隆源进行的。
4. 对于WithDescription()，这一项我并没有在游戏中发现实际用途，但也先预留出来吧。
5. 由于原游戏代码的缘故，编辑器里的自定义建筑的碰撞箱和非关卡编辑器内自定义刷新建筑所生成的建筑，碰撞箱会有所差异，目前无法完美保持一致，RogueForge内部会通过硬编码使其趋近一致。
6. CustomBuildingsPlugin.Logger.LogInfo()，RogueForge提供了此方法用于打印日志，你也可以用别的，只是这里说一下。
7. Q.主播主播，可被拾取是什么意思？ A.我也不知道。

# 三、建筑自发光
- 自定义建筑会像垃圾桶一样拥有一个光源，默认为开启。

示：
```
this.SetBuildingLight(false); // 关灯
```

