---
classless: 2026-08-16T16:10:00
---
***引言：***
1. 实现关卡内刷新建筑。
2. RogueForge 不会帮你自动刷新建筑。

# 实现
- 你必须实现此接口。
- 该接口只提供刷新时机。
- 在LoadLevel.SetupMore4阶段执行。
- 建议使用KMap工具类辅助。
- "level"：当前关卡。

例：
```
// 在关卡出口刷新垃圾桶
public void OnLevelSpawn(LoadLevel level)
{
	Chunk? exit = KMap.GetEndingChunk();
	if (exit == null) return;
	Vector2 pos = KMap.FindEmptySpotByChunkID(exit.chunkID, maxRetries: 500);
	if (pos == Vector2.zero) return;
	KMap.SpawnObject("TrashCan", pos);
}
```