---
classless: 2026-08-16T16:16:00
---
***引言：***
1. 所有的工具类都是可以直接在CustomObjectReal类中调用。
2. 提供一些关于区块和建筑的方法。生成建筑的方法也包含在此类。
3. 在一些关卡进程中KMap提供的获取方法可能无效，目前为止只保证在LoadLevel.SetupMore4进程和此进程后的进程中，所有方法全部有效。
# 一、获取特殊区块
- KMap提供了地图入口区块和地图出口区块这两个区块的获取方式。
- 这两个方法在极少部分情况下会获取不到对应区块，原因不明。
-  如果找不到统一返回”null“。

如下：
```
Chunk? GetStartingChunk();// 获取关卡入口区块
Chunk? GetEndingChunk();  // 获取关卡出口区块
```

# 二、普通区块查找
- 区块类型建议去游戏源码中查找。
- 如果找不到统一返回”null“。

如下：
```
List<Chunk> GetChunksExceptExitAndEntrance(); // 获取关卡内除了出口和入口以外的所有区块。

List<Chunk> GetChunksByDescription(string description); //获取关卡内所有该类型的区块。
Chunk? GetChunkByID(int chunkID);
```

# 三、区块内的空地查找
- 如果想要让建筑的刷新与游戏内垃圾桶、ATM、自动贩卖机等建筑一样，就将Exclude_owner、Exclude_prison、NearWater均设置为true。
- 找不到返回”Vector2.zero“或空列表。

如下：
```
/// 获取指定区块内的所有合法空地坐标（遍历瓦片，严格校验）。
/// spacing：采样间距（默认 0.64，即一个瓦片格）。
/// Exclude_owner：排除有所有者的位置
/// Exclude_prison：排除监狱位置
/// NearWater：是否包含水附近的位置
/// Exclude_NonOwner：是否排除没有所有者的位置
/// edgeMargin：边缘排除范围
List<Vector2> GetAllEmptySpotsInChunk(Chunk? chunk, float spacing = 0.64, fExclude_owner = false, bool Exclude_prison = false, bool NearWater = false,Exclude_NonOwner = false, edgeMargin = 0.64f);

/// 在指定的区块内查找一个随机空地（严格校验墙、水、物体重叠）。
/// maxRetries：最大查询次数
/// Exclude_owner：排除有所有者的位置
/// Exclude_prison：排除监狱位置
/// NearWater：是否包含水附近的位置
/// Exclude_NonOwner：是否排除没有所有者的位置
/// edgeMargin：边缘排除范围
Vector2 FindEmptySpotByChunk(Chunk targetChunk, int maxRetries = 100,bool Exclude_owner = false, bool Exclude_prison = false, bool NearWater = false, Exclude_NonOwner = false, edgeMargin = 0.64f);
```

# 四、建筑相关
- 所有的objectName都必须是建筑类名。
- 大批量删除建筑可能会出现BUG，目前不清楚原因。提这个的原因，是因为我曾删除了关卡的全部建筑，然后就出现了BUG，所以提一句。

生成建筑：
```
// 按照建筑类名生成建筑，失败返回null。
ObjectReal? SpawnObject(string objectName, Vector2 position); 
```
删除建筑：
```
// 删除某个建筑，noRecycle默认即可，目前未实现。成功返回删除建筑坐标，失败返回null。
Vector2? RemoveBuilding(ObjectReal obj, bool noRecycle = false);

/// 根据建筑名称批量删除建筑。
static List<Vector2> RemoveBuildingsByName(string objectName, bool noRecycle = false)

/// 删除特定区块内的所有建筑。
static List<Vector2> RemoveBuildingsInChunk(int chunkID, bool noRecycle = false);
```
获取建筑：
```
/// 获取当前关卡中所有已生成的建筑实例(也许能返回物品实例，存疑)，找不到时返回空列表。
static List<ObjectReal> GetAllBuildings();

/// 根据名称获取所有匹配的建筑，找不到时返回空列表。
static List<ObjectReal> GetBuildingsByName(string objectName);

/// 获取特定区块（Chunk）内的所有建筑。
static List<ObjectReal> GetBuildingsInChunk(int chunkID);

```

# 五、实验性工具
- 未确定是否可行

示：
```
/// 查询某一个坐标周围的所有建筑，step建议默认即可。
List<ObjectReal> FindBuildingsAround(Vector2 center, string buildingName, float radius, float step = 0.64f)
```


# 六、额外补充
- 原游戏的区块查找比较丰富，这里不能涵盖全面，故列出FindEmptySpotByChunk的源码，可参考，以进行更加丰富的空地查询。
- 建议参考文件：原游戏TileInfo类。

源代码：
```
/// <summary>
/// 在指定 chunkID 的区块内查找一个随机空地（严格校验墙、水、物体重叠）。
/// </summary>
public static Vector2 FindEmptySpotByChunk(Chunk targetChunk, int maxRetries = 100, bool Exclude_owner = false, bool Exclude_prison = false, bool NearWater = false)
{
	for (int attempt = 0; attempt < maxRetries; attempt++)
	{
		// 在区块边界内随机取点（世界坐标）
		float randX = Random.Range(targetChunk.chunkEdgeW + 0.32f, targetChunk.chunkEdgeE - 0.32f);
		float randY = Random.Range(targetChunk.chunkEdgeS + 0.32f, targetChunk.chunkEdgeN - 0.32f);
		Vector2 testPos = new Vector2(randX, randY);
		// 获取瓦片数据
		TileData tile = GC!.tileInfo.GetTileData(testPos);
		// 1.必须属于目标区块（防止浮点误差）
		if (tile.chunkID != targetChunk.chunkID) continue;
		// 2.排除墙壁（包括未销毁的墙）
		if (tile.wallMaterial != wallMaterialType.None) continue;
		// 3.排除空洞、水、冰、传送带、危险、固体物体
		if (tile.hole || tile.water || tile.ice || tile.conveyorBelt || tile.dangerousToWalk || tile.solidObject) continue;
		// 4.是否忽略有所有者的建筑
		if (Exclude_owner)
		{
			if (tile.owner > 0) continue;
		}
		// 5.是否忽略有所有者的建筑
		if (Exclude_prison)
		{
			if (tile.prison > 0) continue;
		}
		// 6.检查是否与现有物体重叠（使用原代码的 IsOverlapping）
		if (GC!.tileInfo.IsOverlapping(testPos, "ObjectRealSprite", 0.64f) != null) continue;
		// 7.是否在水体附近
		if (NearWater)
		{
			if (GC.tileInfo.WaterNearby(testPos) || GC.tileInfo.IceNearby(testPos)) continue;
		}
		// 找到合法空地，返回坐标
		return testPos;
	}
	return Vector2.zero;
}
```






