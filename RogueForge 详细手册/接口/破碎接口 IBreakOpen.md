---
classless: 2262-08-16T15:50:00
---
***引言：***
- 使用此接口可以丰富建筑损坏的效果
- 该接口提供了类原版发电机爆炸的功能

# 一、建筑基础破碎
- 你必须实现此接口。
- damagerObject：破碎建筑。

例：
```
public void OnBreakOpen(PlayfieldObject damagerObject)
{
	CustomBuildingsPlugin.Logger.LogInfo("[RogueForge] IBreakOpen 破碎回调触发，容器物品将洒落");
}
```

# 二、破碎爆炸
- 你必须在项目中实现OnExplode()才能有爆炸效果，但不是必须实现。
- ExplosionRadius：爆炸半径（>0 才覆盖原版值，默认参考值 3f）。
- ExplosionDamage：爆炸伤害（>0 才覆盖原版值，默认参考值 30）。
- OnExplode：爆炸后处理回调，未实现执行游戏默认爆炸（库已生成爆炸后调用，可添加音效/粒子/额外逻辑）。
- "damagerObject"：引爆来源。

例：
```
override float ExplosionRadius { get; }
 
override int ExplosionDamage { get; }

public override void OnExplode(PlayfieldObject? damagerObject)
{
}
```