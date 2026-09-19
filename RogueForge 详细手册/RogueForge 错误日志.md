---
classless: 2026-08-31T18:19:00
---
***引言：***
1. 日志优化说明：成功类日志（LogInfo）已全部移除，**只保留警告与报错**，统一按编号递增输出。输出格式：
2. [RogueForge] Warning: 000;Details: 未知警告。
3. [RogueForge] error: 000;Details: 未知错误。
4. **警告与报错分开编号**：警告 000–056（57 条）、报错 000–017（18 条），各自独立从 000 起；`Details` 后为具体消息（含建筑名/异常信息占位符）。

## 警告编号对照表（Warning，000–056）

### CustomBuildingsPlugin.cs（000–017）

| 编号 | 消息 |
|---|---|
| 000 | 入侵门禁拦截：{建筑}（未启用 CanBeHacked/OnHackingComplete） |
| 001 | GameController.SetVersionText 钩子异常 |
| 002 | CopyToCorrupted 保护异常 |
| 003 | OnItemBought 回调异常 |
| 004 | InvSlot.BuyItem 钩子异常 |
| 005 | 启动第二层价格校验协程失败 |
| 006 | InvSlot.UpdateInvSlot 钩子异常 |
| 007 | 第二层价格校验协程异常 |
| 008 | LoadLevel.SetupMore4: 无法获取 prefab 模板实例，跳过刷新 |
| 009 | LoadLevel.SetupMore4 建筑刷新钩子异常 |
| 010 | LoadLevel.SetupMore4 销毁旧建筑异常 |
| 011 | LoadLevel.SetupMore4 钩子异常 |
| 012 | NameDB.GetName 钩子异常 |
| 013 | Bed.Interact 钩子异常 |
| 014 | OpenObjectLoad: 物件面板未找到自定义建筑按钮 |
| 015 | SetTileImage: 找不到自定义建筑精灵 {objectName} |
| 016 | prefab 已失效（场景切换被销毁），从克隆源重建：{objectRealName} |
| 017 | RemoveNetworkIdentity 异常 |

### CustomObjectReal.cs（018–044）

| 编号 | 消息 |
|---|---|
| 018 | EnsureLightSource 异常 |
| 019 | SetBuildingLight 异常 |
| 020 | SetContainerItems 异常 |
| 021 | 设置 objectName 失败 |
| 022 | 黑客入侵进度条完成但被门禁拦截（未启用 CanBeHacked/OnHackingComplete），无任何效果 |
| 023 | 读取关卡号失败 |
| 024 | 填充容器异常 |
| 025 | ResetAndRefillContainer 异常 |
| 026 | 启动小地图标记协程异常 |
| 027 | 手动初始化小地图标记异常 |
| 028 | 小地图标记异常（reallyStarted/smallImage/smallImage2 异常），销毁重建 |
| 029 | 创建/刷新小地图传送标记异常 |
| 030 | 小地图传送标记创建超时（10秒），稍后由 SetupMore4 重试 |
| 031 | 强制设置标记图标异常 |
| 032 | 再次设置标记图标异常 |
| 033 | Force 应用外观失败 |
| 034 | {调用方} 启用渲染器失败 |
| 035 | {调用方}: RogueFramework.ObjectSprites 为 null（图集未就绪），本次未设置贴图 |
| 036 | {调用方}: 方向精灵未找到，回退基础精灵 |
| 037 | {调用方}: 基础精灵未找到，建筑将无贴图 |
| 038 | {调用方} ForceBuild 失败 |
| 039 | {调用方} 启用碰撞失败 |
| 040 | {调用方} 应用外观失败 |
| 041 | 缩放碰撞器失败 |
| 042 | FixMaterialInsts 异常 |
| 043 | 洒落容器物品异常 |
| 044 | DestroyWallTileAtPosition 异常 |

### CustomObjects.cs（045–046）

| 编号 | 消息 |
|---|---|
| 045 | 加载插件库失败: {dll} |
| 046 | 替换资源管理器失败 |

### IObjectContainer.cs / IStore.cs / KMap.cs（047–056）

| 编号 | 文件 | 消息 |
|---|---|---|
| 047 | IObjectContainer.cs | OpenContainer 异常 |
| 048 | IStore.cs | OpenBuyChest: specialInvDatabase 创建失败 |
| 049 | IStore.cs | OpenBuyChest 异常 |
| 050 | IStore.cs | PurchaseItem 异常 |
| 051 | KMap.cs | GetStartingChunk: 所有来源均未找到出生点区块 |
| 052 | KMap.cs | GetStartingChunk 异常 |
| 053 | KMap.cs | GetEndingChunk: 所有来源均未找到出口区块 |
| 054 | KMap.cs | GetEndingChunk 异常 |
| 055 | KMap.cs | 生成建筑 {objectName} 失败 |
| 056 | KMap.cs | 删除建筑 {obj.objectName} 失败 |

## 报错编号对照表（error，000–017）

### CustomBuildingsPlugin.cs（000–005）

| 编号 | 消息 |
|---|---|
| 000 | OnLevelSpawn 回调异常 |
| 001 | 克隆源不存在（建筑 {objectName}），跳过注册 |
| 002 | prefab 注册失败：{objectName} |
| 003 | materialInsts 修复失败：{objectName} |
| 004 | 克隆源不存在（建筑 {objectRealName}），无法重建 prefab |
| 005 | prefab 重建失败：{objectRealName} |

### CustomObjectReal.cs（006–013）

| 编号 | 消息 |
|---|---|
| 006 | base.Awake 异常 |
| 007 | SetVars 异常 |
| 008 | base.Start 异常 |
| 009 | OnHackingComplete 回调异常 |
| 010 | 延迟操作回调异常 |
| 011 | 注册交互提供者失败 |
| 012 | OnBreakOpen 回调异常 |
| 013 | 爆炸处理异常 |

### CustomObjects.cs（014–017）

| 编号 | 消息 |
|---|---|
| 014 | {类型}: Methods marked with [RLSetup] cannot have any parameters! |
| 015 | {类型}: Methods marked with [RLSetup] must be static! |
| 016 | 注册异常（ex.ToString()） |
| 017 | 扫描插件库异常 |

## 实际输出示例

```
[RogueForge] Warning: 000;Details: 入侵门禁拦截：RedTrashCan（未启用 CanBeHacked/OnHackingComplete）
[RogueForge] Warning: 030;Details: [DemonStatue] 小地图传送标记创建超时（10秒），稍后由 SetupMore4 重试
[RogueForge] error: 000;Details: [CrystalObject] OnLevelSpawn 回调异常: System.Exception: ...
[RogueForge] error: 005;Details: [DemonStatue] prefab 重建失败: ...
```

## 说明

- **警告 000–056 / 报错 000–017 为编译期静态分配**（按调用点顺序），两类各自独立计数，不会随运行变化。
- 若需要细分错误码，调用时可传编号：`LogWarning(100, "...")` / `LogError(100, "...")`，编号按三位数显示（如 100/101）。
- 日志文件位置：`BepInEx\LogOutput.log`（游戏根目录下）。
![[未命名.base]]
