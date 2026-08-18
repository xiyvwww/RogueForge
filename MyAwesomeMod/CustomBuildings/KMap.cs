using System.Collections.Generic;
using UnityEngine;

#nullable enable
namespace RogueForge;

public static class KMap
{
    public static GameController? GC => GameController.gameController;
    public static LoadLevel? Level => GC?.loadLevel;
    public static TileInfo? TileInfo => GC?.tileInfo;
    public static SpawnerMain? Spawner => GC?.spawnerMain;

    // ==================== 区块查找 ===================

    /// <summary>
    /// 获取当前关卡的<b>出生点区块</b>（玩家开始游戏所在区块，对应原版 LevelStart 标记）。
    /// 依次尝试多种来源（按可靠性排序）：
    ///   0. 玩家 Agent 的 startingChunkReal——玩家一定在出生点区块出生，关卡生成早期即有效（最可靠）
    ///   1. <see cref="StartingPoint"/> 的 startingChunkReal（电梯激活出生点后才设置，早期可能为 null）
    ///   2. StartingPoint 的 startingChunk（chunkID → 在 levelChunks 里找）
    ///   3. 遍历 LoadLevel.mapChunkArray 找 special == "LevelStart"（兜底）
    /// 注意：不依赖 mapChunkArray 的 special——标准方形地图（levelShape==0）里该标记写在
    /// CreateInitialMap 的局部变量上，不一定反映到 mapChunkArray，导致遍历找不到。
    /// </summary>
    /// <returns>出生点区块；未找到返回 null。</returns>
    public static Chunk? GetStartingChunk()
    {
        try
        {
            GameController? gc = GC;

            // 来源 0：玩家 Agent 的出生区块（最可靠——玩家在关卡生成早期就已生成）
            if (gc != null && gc.playerAgent != null)
            {
                if (gc.playerAgent.startingChunkReal != null)
                {
                    CustomBuildingsPlugin.Logger?.LogInfo($"[KMap] GetStartingChunk: 来源0 玩家区块 startingChunkReal @ {gc.playerAgent.startingChunkReal.name} (chunkID={gc.playerAgent.startingChunkReal.chunkID})");
                    return gc.playerAgent.startingChunkReal;
                }
                if (gc.playerAgent.startingChunk != 0)
                {
                    Chunk? byPlayerId = GetChunkByID(gc.playerAgent.startingChunk);
                    if (byPlayerId != null)
                    {
                        CustomBuildingsPlugin.Logger?.LogInfo($"[KMap] GetStartingChunk: 来源0b 玩家 startingChunk={gc.playerAgent.startingChunk}");
                        return byPlayerId;
                    }
                }
            }

            // 来源 1：StartingPoint 直接持有 Chunk 引用（电梯激活出生点后才设置）
            if (gc != null && gc.startingPoint != null)
            {
                if (gc.startingPoint.startingChunkReal != null)
                {
                    CustomBuildingsPlugin.Logger?.LogInfo($"[KMap] GetStartingChunk: 来源1 startingPoint.startingChunkReal @ {gc.startingPoint.startingChunkReal.name}");
                    return gc.startingPoint.startingChunkReal;
                }
                // 来源 2：只有 chunkID，去 levelChunks 里找
                Chunk? byId = GetChunkByID(gc.startingPoint.startingChunk);
                if (byId != null)
                {
                    CustomBuildingsPlugin.Logger?.LogInfo($"[KMap] GetStartingChunk: 来源2 startingPoint.startingChunk={gc.startingPoint.startingChunk}");
                    return byId;
                }
            }
            // 来源 3：遍历 mapChunkArray 找 special == "LevelStart"（兜底，部分模式可用）
            LoadLevel? level = Level;
            if (level != null && level.mapChunkArray != null)
            {
                for (int x = 0; x < level.levelSizeAxis; x++)
                {
                    for (int y = 0; y < level.levelSizeAxis; y++)
                    {
                        if (x < level.mapChunkArray.GetLength(0) && y < level.mapChunkArray.GetLength(1))
                        {
                            MapChunk? mapChunk = level.mapChunkArray[x, y];
                            if (mapChunk != null && mapChunk.special == "LevelStart")
                            {
                                Chunk? c = GetChunkByID(mapChunk.chunkID);
                                if (c != null)
                                {
                                    CustomBuildingsPlugin.Logger?.LogInfo($"[KMap] GetStartingChunk: 来源3 mapChunkArray[{x},{y}] special=LevelStart chunkID={mapChunk.chunkID}");
                                    return c;
                                }
                            }
                        }
                    }
                }
            }
            CustomBuildingsPlugin.Logger?.LogWarning("[KMap] GetStartingChunk: 所有来源均未找到出生点区块");
        }
        catch (System.Exception e)
        {
            CustomBuildingsPlugin.Logger?.LogWarning($"[KMap] GetStartingChunk 异常: {e.Message}");
        }
        return null;
    }

    public static List<Chunk> GetChunksByDescription(string description)
    {
        var result = new List<Chunk>();
        if (string.IsNullOrEmpty(description)) return result;
        LoadLevel? level = Level;
        if (level == null || level.levelChunks == null) return result;
        foreach (Chunk chunk in level.levelChunks)
            if (chunk != null && chunk.description == description)
                result.Add(chunk);
        return result;
    }

    /// <summary>
    /// 获取当前关卡的<b>出口区块</b>（玩家离开关卡所在区块，对应原版 LevelEnd 标记）。
    /// 依次尝试多种来源（按可靠性排序）：
    ///   0. <see cref="ExitPoint"/> 的 startingChunkReal（最可靠——出口点在关卡生成早期即创建）
    ///   1. ExitPoint 的 startingChunk（chunkID → 在 levelChunks 里找）
    ///   2. 遍历 LoadLevel.mapChunkArray 找 special == "LevelEnd"（兜底，部分模式可用）
    /// 注意：不依赖 mapChunkArray 的 special——标准方形地图（levelShape==0）里该标记写在
    /// CreateInitialMap 的局部变量上，不一定反映到 mapChunkArray，导致遍历找不到。
    /// </summary>
    /// <returns>出口区块；未找到返回 null。</returns>
    public static Chunk? GetEndingChunk()
    {
        try
        {
            GameController? gc = GC;

            // 来源 0：ExitPoint 直接持有 Chunk 引用（最可靠）
            if (gc != null && gc.exitPoint != null)
            {
                if (gc.exitPoint.startingChunkReal != null)
                {
                    CustomBuildingsPlugin.Logger?.LogInfo($"[KMap] GetEndingChunk: 来源0 exitPoint.startingChunkReal @ {gc.exitPoint.startingChunkReal.name} (chunkID={gc.exitPoint.startingChunkReal.chunkID})");
                    return gc.exitPoint.startingChunkReal;
                }
                // 来源 1：只有 chunkID，去 levelChunks 里找
                Chunk? byId = GetChunkByID(gc.exitPoint.startingChunk);
                if (byId != null)
                {
                    CustomBuildingsPlugin.Logger?.LogInfo($"[KMap] GetEndingChunk: 来源1 exitPoint.startingChunk={gc.exitPoint.startingChunk}");
                    return byId;
                }
            }
            // 来源 2：遍历 mapChunkArray 找 special == "LevelEnd"（兜底，部分模式可用）
            LoadLevel? level = Level;
            if (level != null && level.mapChunkArray != null)
            {
                for (int x = 0; x < level.levelSizeAxis; x++)
                {
                    for (int y = 0; y < level.levelSizeAxis; y++)
                    {
                        if (x < level.mapChunkArray.GetLength(0) && y < level.mapChunkArray.GetLength(1))
                        {
                            MapChunk? mapChunk = level.mapChunkArray[x, y];
                            if (mapChunk != null && mapChunk.special == "LevelEnd")
                            {
                                Chunk? c = GetChunkByID(mapChunk.chunkID);
                                if (c != null)
                                {
                                    CustomBuildingsPlugin.Logger?.LogInfo($"[KMap] GetEndingChunk: 来源2 mapChunkArray[{x},{y}] special=LevelEnd chunkID={mapChunk.chunkID}");
                                    return c;
                                }
                            }
                        }
                    }
                }
            }
            CustomBuildingsPlugin.Logger?.LogWarning("[KMap] GetEndingChunk: 所有来源均未找到出口区块");
        }
        catch (System.Exception e)
        {
            CustomBuildingsPlugin.Logger?.LogWarning($"[KMap] GetEndingChunk 异常: {e.Message}");
        }
        return null;
    }

    /// <summary>按 chunkID 在 levelChunks 中查找区块（找不到返回 null）。</summary>
    public static Chunk? GetChunkByID(int chunkID)
    {
        LoadLevel? level = Level;
        if (level == null || level.levelChunks == null) return null;
        foreach (Chunk chunk in level.levelChunks)
        {
            if (chunk != null && chunk.chunkID == chunkID)
                return chunk;
        }
        return null;
    }


    // ==================== 空地查找====================

    /// <summary>
    /// 在指定 chunkID 的区块内查找一个随机空地（严格校验墙、水、物体重叠）。
    /// </summary>
    public static Vector2 FindEmptySpotByChunk(Chunk targetChunk, int maxRetries = 100,bool Exclude_owner = false, bool Exclude_prison = false, bool NearWater = false)
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
            if (tile.hole || tile.water || tile.ice || tile.conveyorBelt || tile.dangerousToWalk || tile.solidObject)continue;

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
            if (GC!.tileInfo.IsOverlapping(testPos, "ObjectRealSprite", 0.64f) != null)continue;

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

    // ==================== 获取所有空地 ====================

    /// <summary>
    /// 获取指定区块内的所有合法空地坐标（遍历瓦片，严格校验）。
    /// </summary>
    /// <param name="chunk">目标区块。</param>
    /// <param name="spacing">采样间距（默认 0.64，即一个瓦片格）。</param>
    /// <returns>所有空地坐标列表（可能为空）。</returns>
    public static List<Vector2> GetAllEmptySpotsInChunk(Chunk? chunk, float spacing = 0.64f,bool Exclude_owner = false, bool Exclude_prison = false, bool NearWater = false)
    {
        var result = new List<Vector2>();
        if (chunk == null) return result;

        TileInfo? tileInfo = TileInfo;
        if (tileInfo == null) return result;

        // 根据区块边界计算遍历范围（以瓦片为单位）
        int startX = Mathf.FloorToInt((chunk.chunkEdgeW + 0.32f) / spacing);
        int endX = Mathf.FloorToInt((chunk.chunkEdgeE - 0.32f) / spacing);
        int startY = Mathf.FloorToInt((chunk.chunkEdgeS + 0.32f) / spacing);
        int endY = Mathf.FloorToInt((chunk.chunkEdgeN - 0.32f) / spacing);

        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                Vector2 pos = new Vector2(x * spacing, y * spacing);
                TileData? tile = tileInfo.GetTileData(pos);
                if (tile == null || tile.chunkID != chunk.chunkID)
                    continue;

                if (tile.wallMaterial != wallMaterialType.None)
                    continue;

                if (Exclude_owner)
                {
                    if (tile.owner > 0) continue;
                }

                if (Exclude_prison)
                {
                    if (tile.prison > 0) continue;
                }

                if (NearWater)
                {
                    if (tileInfo.WaterNearby(pos) || tileInfo.IceNearby(pos) || tileInfo.BridgeNearby(pos))
                    continue;
                }

                if ((bool)tileInfo.IsOverlapping(pos, "ObjectRealSprite", 0.64f))
                    continue;

                result.Add(pos);
            }
        }
        return result;
    }

    // ==================== 建筑生成 ====================

    public static ObjectReal? SpawnObject(string objectName, Vector2 position)
    {
        SpawnerMain? spawner = Spawner;
        if (spawner == null || string.IsNullOrEmpty(objectName)) return null;
        try
        {
            return spawner.spawnObjectReal(position, null, objectName);
        }
        catch (System.Exception e)
        {
            CustomBuildingsPlugin.Logger?.LogWarning($"[KMap] 生成建筑 {objectName} 失败: {e.Message}");
            return null;
        }
    }

    // ==================== 获取所有建筑（ObjectReal） ====================

    /// <summary>
    /// 获取当前关卡中所有已生成的建筑/物品实例。
    /// </summary>
    public static List<ObjectReal> GetAllBuildings()
    {
        if (GC == null || GC.objectRealList == null)
            return new List<ObjectReal>();
        
        // 返回副本防止外部修改原列表
        return GC.objectRealList;
    }

    /// <summary>
    /// 根据物体名称获取所有匹配的建筑（例如 "PowerBox" 返回所有配电箱）。
    /// </summary>
    public static List<ObjectReal> GetBuildingsByName(string objectName)
    {
        var result = new List<ObjectReal>();
        if (string.IsNullOrEmpty(objectName) || GC?.objectRealList == null)
            return result;

        foreach (ObjectReal obj in GC.objectRealList)
        {
            if (obj != null && obj.objectName == objectName)
                result.Add(obj);
        }
        return result;
    }

    /// <summary>
    /// 获取特定区块（Chunk）内的所有建筑。
    /// </summary>
    public static List<ObjectReal> GetBuildingsInChunk(int chunkID)
    {
        var result = new List<ObjectReal>();
        if (chunkID <= 0 || GC?.objectRealList == null)
            return result;

        foreach (ObjectReal obj in GC.objectRealList)
        {
            if (obj == null) continue;
            // 通过坐标查询该位置所属的区块 ID
            TileData? tile = GC.tileInfo?.GetTileData(obj.tr.position);
            if (tile != null && tile.chunkID == chunkID)
                result.Add(obj);
        }
        return result;
    }

    // ==================== 删除建筑 ====================

    /// <summary>
    /// 安全地从场景和所有游戏管理列表中移除一个建筑。
    /// 注意：不能用 DestroyMe()——它只是"打碎消失"的异步协程（闪烁+残骸），并不物理删除 GameObject；
    /// 且自定义建筑设置了 dontRecycleOrDestroy=true，对象池回收分支被跳过，建筑会残留场景。
    /// 正确方式是 UnityEngine.Object.Destroy(go)：物理销毁 GameObject，
    /// 由 PlayfieldObject.OnDestroy 兜底清理 playfieldObjectDic / objectRealList 等所有注册。
    /// </summary>
    /// <param name="obj">要删除的 ObjectReal 实例</param>
    /// <param name="noRecycle">是否禁止回收（自定义建筑恒为 true：直接销毁，不走对象池）。</param>
    public static Vector2? RemoveBuilding(ObjectReal obj, bool noRecycle = false)
    {
        if (obj == null || obj.gameObject == null) 
            return null;

        try
        {
            // 先保存坐标，因为 Destroy 后无法再访问
            Vector2 position = obj.tr.position;

            // 自定义建筑恒为 dontRecycleOrDestroy=true，直接物理销毁 GameObject。
            // OnDestroy 回调会自动从 gc.objectRealList / playfieldObjectDic / chestDic 等移除。
            UnityEngine.Object.Destroy(obj.gameObject);

            return position;
        }
        catch (System.Exception e)
        {
            CustomBuildingsPlugin.Logger?.LogWarning($"[KMap] 删除建筑 {obj.objectName} 失败: {e.Message}");
            return null; // 删除失败，返回 null
        }
    }

    /// <summary>
    /// 根据物体名称批量删除（例如删除所有 "PowerBox"）。
    /// </summary>
    public static List<Vector2> RemoveBuildingsByName(string objectName, bool noRecycle = false)
    {
        var targets = GetBuildingsByName(objectName);
        var removedPositions = new List<Vector2>();
        foreach (var obj in targets)
        {
            var pos = RemoveBuilding(obj, noRecycle);
            if (pos.HasValue)
                removedPositions.Add(pos.Value);
        }
        return removedPositions;
    }

    /// <summary>
    /// 删除特定区块内的所有建筑。
    /// </summary>
    public static List<Vector2> RemoveBuildingsInChunk(int chunkID, bool noRecycle = false)
    {
        var targets = GetBuildingsInChunk(chunkID);
        var removedPositions = new List<Vector2>();
        foreach (var obj in targets)
        {
            var pos = RemoveBuilding(obj, noRecycle);
            if (pos.HasValue)
                removedPositions.Add(pos.Value);
        }
        return removedPositions;
    }

    /// <summary>
    /// 查询指定位置周围的建筑（ObjectReal）
    /// </summary>
    /// <param name="center">查询中心点（世界坐标）</param>
    /// <param name="buildingName">建筑名称（如 "PowerBox"），空字符串或 null 表示不过滤名称</param>
    /// <param name="radius">搜索半径</param>
    /// <param name="step">步长（可选）：若 > 0，则只返回距离为中心半径的整数倍（即 step, 2*step, 3*step ...）的建筑；若 <= 0 则忽略此参数</param>
    /// <returns>符合条件的 ObjectReal 列表（按距离升序）</returns>
    public static List<ObjectReal> FindBuildingsAround(Vector2 center, string buildingName, float radius, float step = 0.64f)
    {
        List<ObjectReal> result = new List<ObjectReal>();

        // 获取所有当前活跃的物体（不包括已销毁的）
        List<ObjectReal> allObjects =  GC!.objectRealList; // gc 是 GameController 实例

        foreach (ObjectReal obj in allObjects)
        {
            // 跳过已销毁或正在销毁的物体（可选）
            if (obj.destroyed || obj.destroying) continue;

            // 计算距离
            float dist = Vector2.Distance(center, obj.tr.position);

            // 距离超出半径则跳过
            if (dist > radius) continue;

            // 步长过滤（若 step > 0，则要求距离是 step 的整数倍，允许微小误差）
            if (step > 0f)
            {
                float multiple = dist / step;
                if (Mathf.Abs(multiple - Mathf.Round(multiple)) > 0.001f)
                    continue;
            }

            // 名称过滤（若指定了名称）
            if (!string.IsNullOrEmpty(buildingName))
            {
                if (obj.objectName != buildingName) continue;
            }

            result.Add(obj);
        }

        // 按距离升序排序
        result.Sort((a, b) => Vector2.Distance(center, a.tr.position)
                            .CompareTo(Vector2.Distance(center, b.tr.position)));
        return result;
    }
}
