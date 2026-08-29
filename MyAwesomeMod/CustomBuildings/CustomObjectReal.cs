using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using RogueLibsCore;
using UnityEngine;



#nullable enable
namespace RogueForge;

/// <summary>
/// 自定义建筑抽象基类。所有自定义建筑必须继承此类。
/// 由 CustomBuildings 插件自动处理：prefab 注册、NetworkIdentity 移除、编辑器注入、
/// 网格重画、材质修复、生成重建、外观与碰撞应用。
/// 格式仿照 RogueLibsCore 的 <see cref="RogueLibsCore.CustomItem"/>。
/// </summary>
public abstract class CustomObjectReal : ObjectReal, IObjectInteraction
{
    /// <summary>存活的自定义建筑实例（跨场景存活，含 prefab 模板）。
    /// 关卡加载时（LoadLevel.SetupMore4）遍历此列表重置容器填充状态。</summary>
    internal static readonly List<CustomObjectReal> LiveInstances = new List<CustomObjectReal>();

    /// <summary>本建筑类型的 prefab 模板引用（CustomBuildings 插件创建时赋值）。
    /// prefab 是克隆模板，Start 应跳过全部初始化。</summary>
    public GameObject? PrefabObject { get; internal set; }

    /// <summary>获取建筑名称标识（由 [ObjectName] 特性或 Builder 提供）。</summary>
    public virtual string ObjectName => CustomObjectMetadata.Get(this.GetType()).Name;

    /// <summary>精灵放大倍率（默认 1f，可用 [ObjectName] 类的 Builder.WithScale 覆盖）。</summary>
    public virtual float SpriteScale => CustomObjectMetadata.Get(this.GetType()).SpriteScale;

    /// <summary>是否为四方向建筑（注册了四方向精灵，参考 ATM 的 fourDirection）。
    /// 生成时按朝向（北东南西）自动切换对应贴图，默认朝向为北。</summary>
    public bool IsFourDirection => CustomObjectMetadata.Get(this.GetType()).IsFourDirection;

    /// <summary>
    /// 按当前朝向获取精灵名（原版 fourDirection 规则：S 无后缀，N/E/W 加后缀；无方向默认北）。
    /// 由 <see cref="BasicObject.Spawn"/> 设置 <see cref="PlayfieldObject.direction"/> 后，
    /// 外观应用时用此名称在 ObjectReals 图集中查找对应方向的贴图。
    /// </summary>
    private string GetDirectionalSpriteName()
    {
        string dir = string.IsNullOrEmpty(this.direction) ? "N" : this.direction;
        return dir == "S" ? this.ObjectName : this.ObjectName + dir;
    }

    /// <summary>商店第 1 个槽位的自定义价格文本（null = 默认定价）。</summary>
    public virtual string? PriceOverride1 => null;

    /// <summary>商店第 2 个槽位的自定义价格文本（null = 默认定价）。</summary>
    public virtual string? PriceOverride2 => null;

    /// <summary>商店第 3 个槽位的自定义价格文本（null = 默认定价）。</summary>
    public virtual string? PriceOverride3 => null;

    /// <summary>商店第 4 个槽位的自定义价格文本（null = 默认定价）。</summary>
    public virtual string? PriceOverride4 => null;

    /// <summary>商店第 5 个槽位的自定义价格文本（null = 默认定价）。</summary>
    public virtual string? PriceOverride5 => null;

    /// <summary>商店第 1 个槽位的自定义背景颜色（null = 原版 NPCChest 紫色）。</summary>
    public virtual UnityEngine.Color? PriceOverrideColor1 => null;

    /// <summary>商店第 2 个槽位的自定义背景颜色（null = 原版 NPCChest 紫色）。</summary>
    public virtual UnityEngine.Color? PriceOverrideColor2 => null;

    /// <summary>商店第 3 个槽位的自定义背景颜色（null = 原版 NPCChest 紫色）。</summary>
    public virtual UnityEngine.Color? PriceOverrideColor3 => null;

    /// <summary>商店第 4 个槽位的自定义背景颜色（null = 原版 NPCChest 紫色）。</summary>
    public virtual UnityEngine.Color? PriceOverrideColor4 => null;

    /// <summary>商店第 5 个槽位的自定义背景颜色（null = 原版 NPCChest 紫色）。</summary>
    public virtual UnityEngine.Color? PriceOverrideColor5 => null;

    /// <summary>免费商品（itemValue == 0）槽位的显示颜色（null = 用 IStoreExtensions.DefaultFreeItemColor）。</summary>
    public virtual UnityEngine.Color? FreeItemColor => null;

    /// <summary>
    /// 确保建筑拥有光源（类似垃圾桶）。若已存在则跳过。
    /// </summary>
    private void EnsureLightSource()
    {
        if (lightReal != null) return;
        if (gc == null || gc.spawnerMain == null) return;
        try
        {
            LightReal newLight = gc.spawnerMain.SpawnLightReal(tr.position, this, 1);
            if (newLight != null)
            {
                newLight.tr.localScale = new Vector3(3f, 3f, 1f);
                lightReal = newLight;
            }
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] EnsureLightSource 异常: {e.Message}");
        }
    }

    /// <summary>
    /// 手动控制建筑光源开关（由子类/外部代码自行调用）。
    /// </summary>
    public void SetBuildingLight(bool enabled)
    {
        if (lightReal == null) return;
        try
        {
            lightReal.gameObject.SetActive(enabled);
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] SetBuildingLight 异常: {e.Message}");
        }
    }

    // ==================== IObjectContainer 默认实现（子类可 override 需要的，无需写默认值） ====================
    // 接口 IObjectContainer：FillContainer 必须由实现类实现（TryFillContainer 通过接口调用）；
    // CanOpenContainer / OnContainerOpened / GetContainerItems / SetContainerItems 由基类提供默认实现。
    // 子类实现 IObjectContainer 时，这些成员由基类满足，只需 override 想自定义的成员。

    /// <summary>是否允许打开容器（默认 true；false 时基类不添加 Open 按钮）。</summary>
    public virtual bool CanOpenContainer => true;

    /// <summary>
    /// 获取当前实例容器内的所有物品（默认返回 <see cref="InvDatabase.InvItemList"/> 的<b>副本</b>，
    /// 修改返回的列表不影响容器内实际物品；无物品栏时返回空列表）。子类可按需 override。
    /// </summary>
    /// <returns>容器内的全部物品。</returns>
    public virtual List<InvItem> GetContainerItems()
    {
        if (base.objectInvDatabase == null || base.objectInvDatabase.InvItemList == null)
        {
            return new List<InvItem>();
        }
        return new List<InvItem>(base.objectInvDatabase.InvItemList);
    }

    /// <summary>
    /// 设置当前实例容器内的所有物品（默认清空容器原有物品后逐件加入给定列表；
    /// 无物品栏时忽略；<paramref name="items"/> 为 null 等于清空容器）。子类可按需 override。
    /// </summary>
    /// <param name="items">要放入容器的新物品列表（可为 null = 清空容器）。</param>
    public virtual void SetContainerItems(List<InvItem> items)
    {
        try
        {
            if (base.objectInvDatabase == null) return;

            // 与 TryFillContainer 一致：确保物品栏已初始化（槽位创建）后再操作
            if (!base.objectInvDatabase.createdInventory)
            {
                base.objectInvDatabase.CreateInventory();
            }

            // 清空原有物品
            base.objectInvDatabase.DestroyAllItems();

            if (items == null) return;
            foreach (InvItem item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.invItemName)) continue;
                base.objectInvDatabase.AddItem(item);
            }
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] SetContainerItems 异常: {e.Message}");
        }
    }

    public void OnContainerOpened(){ }

    /// <summary>碰撞器缩放基准（首次调用时记录克隆源原始尺寸，保证幂等）。</summary>
    private bool _collidersScaled = false;
    private Vector2 _rootColBaseSize;
    private Vector2 _rootColBaseOffset;

    // ==================== 生命周期 ====================

    /// <inheritdoc/>
    protected override void Awake()
    {
        // 注册到存活实例列表（关卡加载时统一重置容器）
        if (!LiveInstances.Contains(this)) LiveInstances.Add(this);

        // 启动时 prefab 克隆阶段（GameController.gameController 未就绪）base.Awake 会 NRE，
        // 此时跳过 SetVars 避免连带异常，prefab 保持半初始化状态不影响使用
        bool awakeOk = false;
        try
        {
            base.Awake();
            awakeOk = true;
        }
        catch (Exception e)
        {
            if (GameController.gameController == null)
                CustomBuildingsPlugin.LogInfo($"[{this.ObjectName}] base.Awake 跳过（启动期 prefab 克隆，gc 未就绪，属正常现象）");
            else
                CustomBuildingsPlugin.LogError($"[{this.ObjectName}] base.Awake 异常: {e}");
        }
        try
        {
            // 关键：本组件挂的是克隆自其他物件的 prefab，
            // 必须覆盖 objectName，否则游戏会把它当成克隆源物件处理
            base.objectName = this.ObjectName;
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] 设置 objectName 失败: {e.Message}");
        }
        if (awakeOk)
        {
            try
            {
                this.SetVars();
            }
            catch (Exception e)
            {
                CustomBuildingsPlugin.LogError($"[{this.ObjectName}] SetVars 异常: {e}");
            }
            this.EnforceHackGate();   // 入侵门禁强制：未启用入侵的建筑 hackable=false（不进 Laptop 选择框/不高亮/遥控器不可操作）

            // 实例脱离对象池，防止被回收/销毁
            if (this.PrefabObject != null && this.gameObject != this.PrefabObject)
            {
                this.dontRecycleOrDestroy = true;
                this.dirtyObject = false;
            }
        }
    }

    /// <inheritdoc/>
    protected override void Start()
    {
        // prefab 模板：跳过全部初始化（不在场景中，只是克隆模板）
        if (this.PrefabObject != null && this.gameObject == this.PrefabObject) return;

        // prefab 克隆阶段（gc 未就绪）直接返回，不执行外观设置
        if (GameController.gameController == null) return;

        try
        {
            base.Start();
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.LogError($"[{this.ObjectName}] base.Start 异常: {e}");
        }
        this.ApplyAppearanceAndColliders("Start");
        this.EnsureLightSource();
        this.SetBuildingLight(true); // 一开始先发光
        this.TryFillContainer();
        this.TrySpawnMinimapMarker();
    }

    /// <inheritdoc/>
    protected virtual void OnDestroy()
    {
        // 实例销毁时从存活列表移除，避免遍历到已销毁对象
        LiveInstances.Remove(this);
    }

    /// <inheritdoc/>
    public override void RecycleAwake()
    {
        base.RecycleAwake();
        this.SetVars();
        this.EnforceHackGate();   // 入侵门禁强制（与 Awake 一致，覆盖子类 SetVars 里设置的 hackable=true）
        try
        {
            this.ApplyAppearanceAndColliders("RecycleAwake");
        }
        catch
        {
        }
        this.TryFillContainer();
    }

    /// <inheritdoc/>
    public override void RevertAllVars()
    {
        base.RevertAllVars();
        this.bulletsCanPass = false;
        this.meleeCanPass = false;
    }

    // ==================== 交互，这里使用RogueLibs ====================
    // 不用 Harmony 钩子 patch PlayfieldObject/ObjectReal（被 RogueLibsPatcher DMD 重写，钩子打空）。
    // 改用 RogueLibs 官方 RogueInteractions.CreateProvider——它由 RogueLibsPatcher 内部的
    // AgentInteractions.AddButton hook 驱动，能拿到 CustomObjectReal 实例，完全绕开 DMD 问题。
    // 交互逻辑已下沉为类方法：子类 override <see cref="SetupInteractions"/> 即可自定义按钮。
    // 基类实现了 <see cref="IObjectInteraction"/>（SetupInteractions / OnHackingComplete 均提供 virtual 实现）。

    /// <summary>
    /// 交互按钮配置（实现 <see cref="IObjectInteraction"/> 接口；库在玩家交互时自动调用）。
    /// 基类默认委托给 <see cref="DefaultSetupInteractions"/>（自动添加购买按钮 / 容器 Open 按钮）；
    /// 子类 override 后完全自定义按钮（默认交互不再自动添加，需要时请在 SetupInteractions 中自行添加）。
    /// </summary>
    /// <param name="h">交互提供者：用 h.AddButton / h.AddImplicitButton 添加按钮。</param>
    public virtual void SetupInteractions(SimpleInteractionProvider h)
    {
        this.DefaultSetupInteractions(h);
    }

    /// <summary>
    /// 默认交互按钮配置（内部调用）：
    /// - 实现 <see cref="IStore"/>：自动添加购买按钮（按钮名 "RogueForge_购买"，隐式按 E 直接打开）
    /// - 否则实现 <see cref="IObjectContainer"/> 且 <see cref="CanOpenContainer"/>：添加 "Open" 隐式按钮直接打开容器界面（参考 TrashCan）
    /// 若子类 override 了 <see cref="SetupInteractions"/>，则完全由子类决定按钮（不再走此默认）。
    /// </summary>
    /// <param name="h">交互提供者：用 h.AddButton / h.AddImplicitButton 添加按钮。</param>
    protected virtual void DefaultSetupInteractions(SimpleInteractionProvider h)
    {
        // 购买功能：实现 IStore 的建筑自动添加购买按钮（隐式按 E 直接打开购买窗口）
        if (this is IStore store)
        {
            const string BUY_BUTTON = "RogueForge_购买";
            h.AddImplicitButton(BUY_BUTTON, m =>
            {
                store.OpenBuyChest((ObjectReal)m.Object, m.Agent);   // 扩展方法：接口层提供打开购买窗口
                CustomBuildingsPlugin.LogInfo($"[{this.ObjectName}] 隐式购买触发，打开购买界面");
            });
            return;
        }

        // 容器型建筑：实现 IObjectContainer 且允许打开时，按 E 直接打开容器界面取东西（参考原版 TrashCan）
        if (this is IObjectContainer container && this.CanOpenContainer)
        {
            const string OPEN_BUTTON = "Open";
            h.AddImplicitButton(OPEN_BUTTON, m =>
            {
                container.OpenContainer((ObjectReal)m.Object, m.Agent);   // 扩展方法：接口层提供打开容器
                CustomBuildingsPlugin.LogInfo($"[{this.ObjectName}] Open 触发，打开容器界面");
            });
            return;
        }
    }

    // ==================== 延迟操作（操作进度条，参考 ATM 收集外星人零件） ====================
    // ATM 收集外星人零件：点按钮 → StartCoroutine(Operating(agent, null, 5f, true, "Collecting"))
    // → 进度条走完 → FinishedOperating() 检测 operatingBarType == "Collecting" → CollectPart() + StopInteraction()。
    // 中断（离开范围/移动/死亡/眩晕/按取消键）→ 进度条取消显示 "Canceled"，需重新交互再触发。
    // 子类实现 <see cref="IDelayedOperating"/> 标记接口后，即可调用 StartDelayedAction 使用操作进度条。

    /// <summary>延迟操作完成回调（StartDelayedAction 设置，进度条走完后由 FinishedOperating 执行）。</summary>
    private Action? _delayedAction;

    /// <summary>延迟操作对应的进度条类型（用于在 FinishedOperating 中识别并防止误触发）。</summary>
    private string? _delayedBarType;

    /// <summary>
    /// 启动延迟操作（显示操作进度条）。进度条走完后调用 <paramref name="onComplete"/>（执行"翻找"等具体内容）；
    /// 中途中断（离开范围/移动/死亡等）则取消，玩家需重新交互再次触发。
    /// 注意：<paramref name="barType"/> 需先注册 Interface 名称作为进度条标题，
    /// 例如 RogueLibs.CreateCustomName("X", "Interface", new CustomNameInfo { Chinese = "翻找" })。
    /// </summary>
    /// <param name="agent">执行操作的交互者（玩家）。</param>
    /// <param name="delay">延迟秒数（进度条时长）。</param>
    /// <param name="barType">进度条类型标识（同时作为进度条标题查询 key）。</param>
    /// <param name="onComplete">进度条走完后的回调。</param>
    protected void StartDelayedAction(Agent agent, float delay, string barType, Action onComplete)
    {
        if (agent == null || onComplete == null) return;
        this._delayedAction = onComplete;
        this._delayedBarType = barType;
        base.StartCoroutine(base.Operating(agent, null, delay, makeNoise: true, barType));
        CustomBuildingsPlugin.LogInfo($"[{this.ObjectName}] 延迟操作开始: {barType} ({delay}s)");
    }

    /// <summary>操作进度条走完回调：处理黑客入侵完成 + 延迟操作（参考 ATM 的 FinishedOperating → CollectPart）。</summary>
    public override void FinishedOperating()
    {
        base.FinishedOperating();

        // 黑客入侵完成（barType == "Hacking"）：玩家手持黑客工具/笔记本电脑远程按 E 入侵本建筑，
        // 2 秒进度条走完后调用（参考原版 Computer.FinishedOperating → ShowObjectButtons）。
        // 优先于延迟操作处理，防止与自定义 barType 混淆。
        if (base.operatingBarType == "Hacking")
        {
            // 入侵门禁兜底：即使进度条因故启动（Prefix 未拦截到），未启用入侵的建筑也不执行任何效果。
            // 启用条件与门禁一致：override CanBeHacked=true 或 override OnHackingComplete。
            try
            {
                if (this.CanBeHacked || this.IsHackingEnabledByOverride())
                {
                    this.OnHackingComplete(this.interactingAgent);
                }
                else
                {
                    CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] 黑客入侵进度条完成但被门禁拦截（未启用 CanBeHacked/OnHackingComplete），无任何效果");
                }
            }
            catch (Exception e)
            {
                CustomBuildingsPlugin.LogError($"[{this.ObjectName}] OnHackingComplete 回调异常: {e}");
            }
            return;
        }

        // 延迟操作（StartDelayedAction 设置的自定义 barType）
        if (this._delayedAction != null && this._delayedBarType != null && base.operatingBarType == this._delayedBarType)
        {
            Action? cb = this._delayedAction;
            this._delayedAction = null;
            this._delayedBarType = null;
            try
            {
                cb();
            }
            catch (Exception e)
            {
                CustomBuildingsPlugin.LogError($"[{this.ObjectName}] 延迟操作回调异常: {e}");
            }
        }
    }

    // ==================== 黑客入侵（参考 Computer：远程黑客工具/笔记本触发） ====================
    // 原版黑客流程：玩家手持 HackingTool（黑客工具）或 Laptop（笔记本电脑）远程按 E 交互
    // （interactingFar）→ RogueLibs 的 InteractFarHook 检测物品含 "Internal:HackInteract" 分类
    // → 调用 HackObject(agent)：Hacker 职业/速写员特质直接显示按钮，否则 2 秒进度条（barType "Hacking"）。
    // 进度条走完 → FinishedOperating（operatingBarType == "Hacking"）→ 本库调用 OnHackingComplete 回调。
    // 前提：本建筑 functional == true 且 tempNoOperating == false（基类 ObjectReal 默认满足）。
    //
    // 入侵门禁（v1.0.1+）：默认所有自定义建筑【不可被入侵】——CustomBuildingsPlugin 的 Prefix 拦截
    // ObjectReal.HackObject，未启用入侵的建筑直接无任何效果。启用方式（二选一）：
    //  1) override <see cref="CanBeHacked"/> 返回 true；
    //  2) override <see cref="OnHackingComplete"/>（自动识别为已启用入侵）。
    // 两者都不做 → 玩家对建筑使用黑客工具/笔记本没有任何效果。

    /// <summary>
    /// 是否允许被黑客入侵（默认 false = 不可入侵）。
    /// 启用方式（二选一）：override 本属性返回 true；或 override <see cref="OnHackingComplete"/>（自动识别）。
    /// 两者都不做 → 玩家对建筑使用黑客工具/笔记本无任何效果。
    /// </summary>
    public virtual bool CanBeHacked => false;

    /// <summary>是否通过 override <see cref="OnHackingComplete"/> 启用了入侵（反射缓存，每个实例算一次）。</summary>
    private bool? _hackingEnabledByOverride;

    /// <summary>检测 OnHackingComplete 是否被子类 override（用于入侵门禁）。</summary>
    internal bool IsHackingEnabledByOverride()
    {
        if (this._hackingEnabledByOverride.HasValue) return this._hackingEnabledByOverride.Value;
        bool enabled = false;
        try
        {
            System.Reflection.MethodInfo? m = this.GetType().GetMethod(nameof(OnHackingComplete));
            enabled = m != null && m.DeclaringType != typeof(CustomObjectReal);
        }
        catch { enabled = false; }
        this._hackingEnabledByOverride = enabled;
        return enabled;
    }

    /// <summary>
    /// 入侵门禁强制（Awake / RecycleAwake 里 SetVars 之后调用）：
    /// 未启用入侵的建筑强制 hackable=false —— 这样 Laptop 的选择框（ItemFunctions.TargetObject 判定
    /// otherObject.hackable）、绿色高亮、遥控器操作等所有以 hackable 为前提的入侵入口全部不可用，
    /// 从源头杜绝"自定义建筑出现在黑客目标列表里"。
    /// 启用入侵（override CanBeHacked=true 或 OnHackingComplete）的建筑保持子类 SetVars 的设置。
    /// </summary>
    protected void EnforceHackGate()
    {
        try
        {
            if (!this.CanBeHacked && !this.IsHackingEnabledByOverride())
            {
                base.hackable = false;
            }
        }
        catch { }
    }

    /// <summary>
    /// 黑客入侵完成回调（虚方法，子类可 override）。
    /// 玩家手持黑客工具/笔记本电脑远程按 E 入侵本建筑，2 秒进度条走完后调用。
    /// 可用于执行黑客成功效果：解锁设备、发放奖励、Say 台词、洒落物品、触发任务等。
    /// 注意：不会自动弹出操作按钮菜单；如需菜单，override 后调用 ShowObjectButtons()
    /// 并在 DetermineButtons() 中添加按钮。
    /// <para><b>入侵门禁</b>：override 本方法即视为该建筑启用了入侵（无需再 override <see cref="CanBeHacked"/>）。
    /// 默认（未 override）建筑不可被入侵。</para>
    /// </summary>
    /// <param name="hacker">执行黑客入侵的玩家（可为 null）。</param>
    public virtual void OnHackingComplete(Agent hacker) { }

    // ==================== 容器（参考 TrashCan：打开拿物品 + 打碎掉落） ====================

    /// <summary>容器是否已填充（防重复填充）。</summary>
    private bool _containerFilled;

    /// <summary>上次填充时的关卡号（gc.sessionDataBig.curLevel）。
    /// 实例跨场景存活（dontRecycleOrDestroy），进入新关卡时用关卡号变化检测并重新填充。</summary>
    private int _containerFilledLevel = -1;

    /// <summary>上次填充时的关卡号（内部访问器：DestroyOldBuildings 用它区分"本关刚生成的建筑"与"上一关残留"）。</summary>
    internal int LastFillLevel => this._containerFilledLevel;

    // 注：填充容器初始物品的钩子 FillContainer(InvDatabase) 由 IObjectContainer 实现类提供
    // （接口必须实现成员），TryFillContainer 在下方通过接口调用它。

    /// <summary>容器初始化：确保 InvDatabase 组件存在 + 标记真容器 + 填充物品（每个关卡一次）。</summary>
    private void TryFillContainer()
    {
        try
        {
            if (GameController.gameController == null)
            {
                CustomBuildingsPlugin.LogInfo($"[{this.ObjectName}] TryFillContainer: gc 未就绪，跳过");
                return;
            }
            // prefab 模板跳过
            if (this.PrefabObject != null && this.gameObject == this.PrefabObject)
            {
                CustomBuildingsPlugin.LogInfo($"[{this.ObjectName}] TryFillContainer: 是 prefab 模板，跳过");
                return;
            }

            // 关键：实例跨场景存活（dontRecycleOrDestroy=true），重新进入关卡时不会重新 Awake/RecycleAwake。
            // 用关卡号变化检测新关卡：关卡变了就重置填充标志，让容器重新填充（像垃圾桶每关都有新垃圾）。
            // 同关卡内玩家拿走物品后不重新填充（保持已取走状态）。
            int curLevel = -1;
            try
            {
                if (this.gc?.sessionDataBig != null) curLevel = this.gc.sessionDataBig.curLevel;
            }
            catch (Exception eL)
            {
                CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] 读取关卡号失败: {eL.Message}");
            }
            if (this._containerFilled && this._containerFilledLevel == curLevel)
            {
                CustomBuildingsPlugin.LogInfo($"[{this.ObjectName}] TryFillContainer: 本关卡已填充过，跳过 (curLevel={curLevel})");
                return;
            }
            // 新关卡（或首次）：重置标志允许重新填充
            this._containerFilled = false;
            this._containerFilledLevel = curLevel;
            CustomBuildingsPlugin.LogInfo($"[{this.ObjectName}] TryFillContainer: 实例处理开始, gc.serverPlayer={this.gc?.serverPlayer}, objectInvDatabase={(base.objectInvDatabase != null ? "已有" : "null")}");

            // 确保 InvDatabase 组件存在（prefab 注册时已加，此处兜底）
            if (base.objectInvDatabase == null)
            {
                base.objectInvDatabase = this.GetComponent<InvDatabase>();
                if (base.objectInvDatabase == null)
                {
                    base.objectInvDatabase = this.gameObject.AddComponent<InvDatabase>();
                    CustomBuildingsPlugin.LogInfo($"[{this.ObjectName}] TryFillContainer: 动态添加了 InvDatabase");
                }
            }

            // 关键：确保物品栏已初始化（槽位创建）。
            // InvDatabase.Awake 会调 CreateInventory 创建空槽位，但动态 AddComponent 时可能未执行，
            // 导致 InvItemList 空 → hasEmptySlot()=false → AddItem 走 tempSlot 分支不真正加入 → 物品数=0。
            if (!base.objectInvDatabase.createdInventory)
            {
                base.objectInvDatabase.CreateInventory();
                CustomBuildingsPlugin.LogInfo($"[{this.ObjectName}] TryFillContainer: 手动调用 CreateInventory 初始化物品栏");
            }

            // 标记为真容器：空箱后变"已空"不可交互 + 打碎可掉落（canSpill 默认 true）
            base.chestReal = true;
            base.chestMoneyTier = 1;

            // 仅服务端填充，避免多人重复
            if (this.gc == null || !this.gc.serverPlayer)
            {
                CustomBuildingsPlugin.LogInfo($"[{this.ObjectName}] TryFillContainer: 非服务端，跳过填充");
                return;
            }

            // 新关卡重新填充：清空上关残留物品，保证全新容器
            if (base.objectInvDatabase.InvItemList != null && base.objectInvDatabase.InvItemList.Count > 0)
            {
                base.objectInvDatabase.DestroyAllItems();
                CustomBuildingsPlugin.LogInfo($"[{this.ObjectName}] TryFillContainer: 已清空上关残留物品");
            }

            this._containerFilled = true;
            // 阻止原版 FillChest：关卡加载时 LoadLevel.SetupMore4_2 会遍历所有 objectInvDatabase 非空
            // 的物体调用 FillChest()，给容器加随机垃圾+金钱。设置在 FillContainer 之前：
            // 即使 FillContainer 抛异常（如实现类取空列表越界），原版垃圾/金钱也不会混入容器。
            base.objectInvDatabase.filledChestStreaming = true;
            if (this is IObjectContainer container)
            {
                container.FillContainer(base.objectInvDatabase);
                CustomBuildingsPlugin.LogInfo($"[{this.ObjectName}] TryFillContainer: 填充完成, 物品数={base.objectInvDatabase?.InvItemList?.Count}, 已阻止原版 FillChest");
            }
            
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] 填充容器异常: {e}");
        }
    }

    /// <summary>关卡加载完成时调用（LoadLevel.SetupMore4 Postfix）：重置填充状态并重新填充。
    /// 解决"退出回主菜单再重新进入显示空空如也"——重新进关卡会重新填充（像垃圾桶每关新垃圾）。</summary>
    internal void ResetAndRefillContainer()
    {
        try
        {
            this._containerFilled = false;
            this._containerFilledLevel = -1;
            this.TryFillContainer();
            this.EnsureLightSource();
            this.SetBuildingLight(true); // 新关卡默认发光
            // 新关卡：小地图标记随旧场景销毁，需重建（若实现 IMinimapTeleportable）
            this.TrySpawnMinimapMarker();
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] ResetAndRefillContainer 异常: {e.Message}");
        }
    }

    // ==================== 小地图传送标记（参考 ATM：MinimapDisplay → 大地图点击传送） ====================
    // ATM 在 Start 中调用 MinimapDisplay() → gc.quests.CreateQuestMarker(this, null, "NonQuestObject")
    // → 创建小地图 + 大地图上的 QuestMarkerSmall 标记。玩家打开大地图（Tab）点击标记 →
    // QuestMarkerSmall.OnPointerDown → TeleportToMarker → agent.TeleportToObject(questMarker.myObject) 传送到物体。
    // 前提：questMarker.playerSeen == true（玩家曾接近物体 13.44 单位，或有 MapFilled 特性）。
    // 自定义建筑实现 IMinimapTeleportable 后，基类自动创建标记并强制 playerSeen=true，玩家可随时点击传送。

    /// <summary>是否已创建过小地图标记（防重复创建）。</summary>
    private bool _minimapMarkerSpawned;
    /// <summary>是否正在创建小地图标记（防重复启动协程）。</summary>
    private bool _minimapMarkerSpawning;




    /// <summary>
    /// 若本建筑实现 <see cref="IMinimapTeleportable"/>，则创建小地图/大地图标记（参考 ATM），
    /// 玩家打开大地图点击标记可传送到本建筑。创建后强制标记已发现（playerSeen=true），
    /// 无需玩家先接近即可点击传送。
    /// </summary>
    private void TrySpawnMinimapMarker()
    {
        try
        {
            if (!(this is IMinimapTeleportable))
            {
                CustomBuildingsPlugin.LogInfo($"[{this.ObjectName}] TrySpawnMinimapMarker: 未实现 IMinimapTeleportable，跳过");
                return;
            }

            if (this.PrefabObject != null && this.gameObject == this.PrefabObject)
            {
                CustomBuildingsPlugin.LogInfo($"[{this.ObjectName}] TrySpawnMinimapMarker: 是 prefab 模板，跳过");
                return;
            }
            if (GameController.gameController == null || this.gc == null)
            {
                CustomBuildingsPlugin.LogInfo($"[{this.ObjectName}] TrySpawnMinimapMarker: gc 未就绪，跳过");
                return;
            }

            if (this._minimapMarkerSpawning)
            {
                return;
            }

            // 已有可用标记则跳过；标记存在但未就绪/已损坏时继续走协程修复
            if (this._minimapMarkerSpawned &&
                base.nonQuestObjectMarker != null &&
                base.nonQuestObjectMarker.reallyStarted &&
                base.nonQuestObjectMarker.smallImage != null &&
                base.nonQuestObjectMarker.smallImage2 != null)
            {
                return;
            }

            this._minimapMarkerSpawning = true;
            this.StartCoroutine(EnsureMinimapMarkerCoroutine());
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] 启动小地图标记协程异常: {e}");
        }
    }

    private System.Collections.IEnumerator EnsureMinimapMarkerCoroutine()
    {
        const float timeout = 10f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (this == null || this.gameObject == null)
            {
                this._minimapMarkerSpawning = false;
                yield break;
            }

            try
            {
                if (this.nonQuestObjectMarker == null)
                {
                    this.MinimapDisplay();
                }

                QuestMarker? marker = this.nonQuestObjectMarker;
                if (marker != null)
                {
                    marker.playerSeen = true;

                    if (marker.reallyStarted && marker.smallImage != null && marker.smallImage2 != null)
                    {
                        RogueLibsCore.RogueSprite? rogueSprite = CustomObjectMetadata.Get(this.GetType()).GetSprite();
                        if (rogueSprite?.Sprite != null)
                        {
                            UnityEngine.Sprite icon = rogueSprite.Sprite;
                            marker.nonQuestSprite = icon;
                            if (marker.smallImage != null) marker.smallImage.sprite = icon;
                            if (marker.smallImage2 != null) marker.smallImage2.sprite = icon;
                            if (marker.smallImage != null) marker.smallImage.color = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
                            if (marker.smallImage2 != null) marker.smallImage2.color = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
                        }

                        // 应用 IMinimapTeleportable 自定义图标缩放
                        if (this is IMinimapTeleportable minimapIcon)
                        {
                            float iconScale = minimapIcon.GetMinimapIconScale();
                            if (marker.smallImage != null)
                                marker.smallImage.transform.localScale = new Vector3(iconScale, iconScale, 1f);
                            if (marker.smallImage2 != null)
                                marker.smallImage2.transform.localScale = new Vector3(iconScale, iconScale, 1f);
                        }

                        this._minimapMarkerSpawned = true;
                        this._minimapMarkerSpawning = false;
                        CustomBuildingsPlugin.LogInfo($"[{this.ObjectName}] 小地图传送标记已就绪");
                        yield break;
                    }

                    // 标记存在但长时间未就绪/图片丢失：视为跨场景损坏的旧标记，销毁后重建
                    if (elapsed >= 2f)
                    {
                        CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] 小地图标记异常（reallyStarted={marker.reallyStarted}, smallImage={marker.smallImage != null}, smallImage2={marker.smallImage2 != null}），销毁重建");
                        UnityEngine.Object.Destroy(marker.gameObject);
                        this.nonQuestObjectMarker = null;
                        this._minimapMarkerSpawned = false;
                    }
                }
            }
            catch (Exception e)
            {
                CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] 创建/刷新小地图传送标记异常: {e.Message}");
            }

            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        this._minimapMarkerSpawning = false;
        CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] 小地图传送标记创建超时（10秒），稍后由 SetupMore4 重试");
    }

    /// <summary>
    /// 等待标记初始化完成后强制设置图标（小地图 + 大地图 + nonQuestSprite）。
    /// 覆盖 StartReal/CheckIfSeen2 从 objectDic 读到的克隆源图标。
    /// </summary>
    private System.Collections.IEnumerator ForceMarkerIconWhenReady(QuestMarker marker, UnityEngine.Sprite icon)
    {
        // 等待标记初始化（StartReal 执行，reallyStarted 置位）
        float waitTime = 0f;
        while (marker != null && !marker.reallyStarted && waitTime < 5f)
        {
            yield return null;
            waitTime += Time.deltaTime;
        }
        if (marker == null) yield break;

        bool ok = true;
        try
        {
            marker.nonQuestSprite = icon;                     // CheckIfSeen2 显示时用它
            if (marker.smallImage != null)
                marker.smallImage.sprite = icon;              // 小地图图标
            if (marker.smallImage2 != null)
                marker.smallImage2.sprite = icon;             // 大地图图标
        }
        catch (Exception e)
        {
            ok = false;
            CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] 强制设置标记图标异常: {e.Message}");
        }
        // 再等一帧（CheckIfSeen2 协程可能在 reallyStarted 后立即设置 sprite），然后再次覆盖
        yield return null;
        if (marker == null) yield break;
        try
        {
            marker.nonQuestSprite = icon;
            if (marker.smallImage != null)
                marker.smallImage.sprite = icon;
            if (marker.smallImage2 != null)
                marker.smallImage2.sprite = icon;
            if (ok)
            { 
                CustomBuildingsPlugin.LogInfo($"[{this.ObjectName}] 标记图标已强制设为自定义精灵: {icon.name}");
            }
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] 再次设置标记图标异常: {e.Message}");
        }
    }

    /// <summary>交互提供者注册（静态，库初始化时调用一次）：拦截自定义建筑交互并委托给实例的虚方法。</summary>
    internal static void RegisterInteractions()
    {
        try
        {
            // 注意：用非泛型 CreateProvider！官方警告：不要用 ObjectReal/PlayfieldObject 类做泛型参数，
            // 否则触发适配器模式（adapter model），按钮能显示但回调里的 Agent 关联会失效。
            // 非泛型 handler 的 h.Object / h.Agent 直接来自真实 model，回调可靠。
            RogueLibsCore.RogueInteractions.CreateProvider(h =>
            {
                // 基类 CustomObjectReal 已实现 IObjectInteraction（SetupInteractions 默认委托给
                // DefaultSetupInteractions）：未 override SetupInteractions 的建筑走默认交互
                // （购买按钮 / 容器 Open），override 的建筑完全自定义按钮。
                if (h.Object is CustomObjectReal customObj)
                {
                    customObj.SetupInteractions(h);
                }
            });
            CustomBuildingsPlugin.LogInfo("[CustomBuildings] 交互提供者已注册（非泛型 CreateProvider → 实例虚方法）");
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.LogError($"[CustomBuildings] 注册交互提供者失败: {e}");
        }
    }

    // ==================== 子类接口 ====================

    /// <summary>
    /// 设置建筑属性（伤害阈值、碰撞、交互等）。子类必须实现。
    /// </summary>
    public abstract void SetVars();

    // ==================== 外观与碰撞 ====================

    /// <summary>强制应用外观（不依赖 Start 生命周期）。</summary>
    public void ForceApplyAppearance()
    {
        if (GameController.gameController == null) return;
        try
        {
            // 组件可能被禁用（enabled=false 时 Start 不执行），强制开启恢复正常
            if (!this.enabled) this.enabled = true;
            this.ApplyAppearanceAndColliders("Force");
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] Force 应用外观失败: {e}");
        }
    }

    /// <summary>统一的外观 + 碰撞应用逻辑（Start / ForceApplyAppearance 调用）。</summary>
    private void ApplyAppearanceAndColliders(string caller)
    {
        try
        {
            // 修复 tk2d materialInsts（RogueLibs.AddDefinition 扩容后缓存未同步 → materialInst 为 null → 渲染空白）
            FixMaterialInsts();

            // 无条件启用所有渲染器（根 + 子物体）
            // prefab 阶段禁用了所有 Renderer 防幽灵，克隆体继承禁用状态，必须在这里恢复。
            try
            {
                Renderer[] allRends = base.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in allRends)
                {
                    if (r != null && !r.enabled) r.enabled = true;
                }
            }
            catch (Exception eR)
            {
                CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] {caller} 启用渲染器失败: {eR.Message}");
            }

            if (this.spr != null && !this.destroyed && !this.destroying)
            {
                tk2dSpriteCollectionData? coll = RogueFramework.ObjectSprites;
                if (coll == null)
                {
                    // 诊断（always-on）：图集未就绪 → 本次无法设置贴图（建筑空白）
                    CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] {caller}: RogueFramework.ObjectSprites 为 null（图集未就绪）——本次未设置贴图");
                }
                else
                {
                    // 四方向建筑：按当前朝向选精灵（S 无后缀，N/E/W 加后缀；找不到时回退基础精灵）。
                    // 参考原版 BasicObject.Spawn 的 fourDirection 分支：spawnDirection 决定精灵名。
                    int spriteId = coll.GetSpriteIdByName(this.ObjectName, -1);
                    if (this.IsFourDirection)
                    {
                        string targetName = this.GetDirectionalSpriteName();
                        int dirId = coll.GetSpriteIdByName(targetName, -1);
                        CustomBuildingsPlugin.LogInfo($"[{this.ObjectName}] {caller}: 四方向 direction='{this.direction}' 目标精灵='{targetName}' dirId={dirId} baseId={spriteId}");
                        if (dirId > 0) spriteId = dirId;
                        else CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] {caller}: 方向精灵 '{targetName}' 未找到(dirId={dirId})，回退基础精灵");
                    }
                    if (spriteId <= 0)
                    {
                        // 诊断（always-on）：基础精灵都没找到 → 该建筑将无贴图（空白）
                        CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] {caller}: 基础精灵 '{this.ObjectName}' 未找到(spriteId={spriteId})，图集='{coll.name}'——建筑将无贴图");
                    }
                    if (spriteId > 0)
                    {
                        this.spr.SetSprite(coll, spriteId);

                        // 关键：SetSprite 不重建 mesh！克隆体 mesh.uv 是克隆源的，
                        // 在新纹理上采样越界 → 透明/空白。ForceBuild() 用新 sprite 重建 mesh。
                        try
                        {
                            this.spr.ForceBuild();
                        }
                        catch (Exception eBuild)
                        {
                            CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] {caller} ForceBuild 失败: {eBuild.Message}");
                        }

                        // 用 SpriteScale 放大到合适尺寸
                        float scale = this.SpriteScale;
                        this.spr.transform.localScale = new Vector3(scale, scale, 1f);

                        tk2dSpriteDefinition? def = coll.inst.spriteDefinitions[spriteId];

                        // 模拟 ObjectReal.RefreshShader：换成本游戏的 lit/normal shader
                        GameController gc2 = GameController.gameController;
                        if (gc2 != null && this.objectSprite != null && def != null)
                        {
                            Material? mat = def.materialInst != null ? def.materialInst : def.material;
                            if (mat != null)
                            {
                                Shader shader = (gc2.lightingType == "Full" || gc2.lightingType == "Med") ? gc2.litShader : gc2.normalShader;
                                if (shader != null)
                                {
                                    if (this.objectSprite.meshRenderer != null)
                                    {
                                        this.objectSprite.meshRenderer.material = mat;
                                        if (this.objectSprite.meshRenderer.material != null)
                                            this.objectSprite.meshRenderer.material.shader = shader;
                                    }
                                    if (this.objectSprite.objectRenderer != null)
                                    {
                                        this.objectSprite.objectRenderer.sharedMaterial = mat;
                                        if (this.objectSprite.objectRenderer.sharedMaterial != null)
                                            this.objectSprite.objectRenderer.sharedMaterial.shader = shader;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // 启用所有碰撞器（根 + 子物体）
            try
            {
                Collider2D[] allCols = base.GetComponentsInChildren<Collider2D>(true);
                foreach (Collider2D c in allCols)
                {
                    if (c != null && !c.enabled) c.enabled = true;
                }
            }
            catch (Exception e3)
            {
                CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] {caller} 启用碰撞失败: {e3.Message}");
            }

            // 碰撞器随精灵放大（spr.localScale 只放大渲染 mesh；根 BoxCollider2D 不跟随）
            ScaleCollidersToSprite();
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] {caller} 应用外观失败: {e}");
        }
    }

    /// <summary>按 SpriteScale 放大根碰撞器（幂等：基于固定的标准基准尺寸计算）。
    /// 注意：碰撞盒钳制最大尺寸（全宽约 1.9），否则玩家被放大碰撞挡住、中心距超过 1.5 → 无法交互。
    /// 基准固定为 0.32×0.28（原版 fourDirection 物体的 mainCollider 标准尺寸，见 BasicObject.Spawn）——
    /// 编辑器放置（BasicObject.Spawn）会先把根碰撞器设为该值；而运行时 spawnObjectReal 不会，
    /// 若直接记录"当前碰撞器尺寸"作为基准，两条路径基准不同会导致运行时碰撞体明显偏大。</summary>
    private void ScaleCollidersToSprite()
    {
        try
        {
            float scale = this.SpriteScale;
            if (scale <= 0f) return;

            BoxCollider2D rootCol = this.objectCollider != null ? this.objectCollider : GetComponent<BoxCollider2D>();
            if (rootCol == null) return;

            // 固定基准：0.32×0.28 + 偏移 (0, 0.06)（与原版 BasicObject.Spawn fourDirection 分支一致）
            if (!_collidersScaled)
            {
                _rootColBaseSize = new Vector2(0.32f, 0.28f);
                _rootColBaseOffset = new Vector2(0f, 0.06f);
                _collidersScaled = true;
            }
            if (_rootColBaseSize == Vector2.zero) return;

            // 交互距离限制：InteractionHelper 按 E 要求玩家中心距建筑中心 ≤ 1.5。
            // 碰撞盒若过大，玩家被挡在外 → 中心距超 1.5 → 无法交互。钳制最大尺寸保证可交互。
            const float MAX_COL = 1.9f;
            float colX = Mathf.Clamp(_rootColBaseSize.x * scale, 0.2f, MAX_COL);
            float colY = Mathf.Clamp(_rootColBaseSize.y * scale, 0.2f, MAX_COL);

            rootCol.size = new Vector2(colX, colY);
            rootCol.offset = new Vector2(_rootColBaseOffset.x * scale, _rootColBaseOffset.y * scale);

            // 额外碰撞器（tr.Find("ExtraCollider")，独立 transform，不会随精灵放大）
            if (this.extraCollider != null)
            {
                this.extraCollider.size = new Vector2(Mathf.Clamp(_rootColBaseSize.x * scale * 0.6f, 0.2f, MAX_COL * 0.6f), Mathf.Clamp(_rootColBaseSize.y * scale * 0.6f, 0.2f, MAX_COL * 0.6f));
                this.extraCollider.offset = Vector2.zero;
            }
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] 缩放碰撞器失败: {e.Message}");
        }
    }

    /// <summary>
    /// tk2d materialInsts 修复（幂等）：RogueLibs.AddDefinition 扩容了 materials/spriteDefinitions 数组，
    /// 但 materialInsts 缓存数组没有同步重建（Init() 只跑一次），导致新精灵槽位 materialInst 为 null
    /// → 渲染空白。用 def.material（必定非 null）兜底填充。
    /// </summary>
    private static void FixMaterialInsts()
    {
        try
        {
            tk2dSpriteCollectionData? coll = RogueFramework.ObjectSprites;
            if (coll == null) return;
            tk2dSpriteCollectionData inst = coll.inst;
            if (inst == null || inst.materials == null || inst.spriteDefinitions == null) return;

            // 遍历所有注册的自定义建筑精灵槽位修复（不只 NewMachine）
            foreach (CustomObjectMetadata meta in CustomObjects.Registry.Values)
            {
                // 四方向建筑：基础名（南）+ N/E/W 后缀精灵都要修复材质
                string[] spriteNames = meta.IsFourDirection
                    ? new[] { meta.Name, meta.Name + "N", meta.Name + "E", meta.Name + "W" }
                    : new[] { meta.Name };
                foreach (string spriteName in spriteNames)
                {
                    int id = coll.GetSpriteIdByName(spriteName, -1);
                    if (id <= 0 || id >= inst.spriteDefinitions.Length) continue;
                    tk2dSpriteDefinition? def = inst.spriteDefinitions[id];
                    if (def == null || def.material == null) continue;

                    // 确保 materialInsts 数组长度足够
                    if (inst.materialInsts == null || inst.materialInsts.Length < inst.materials.Length)
                    {
                        Material[]? old = inst.materialInsts;
                        inst.materialInsts = new Material[inst.materials.Length];
                        for (int i = 0; i < inst.materials.Length; i++)
                        {
                            if (old != null && i < old.Length && old[i] != null)
                                inst.materialInsts[i] = old[i];
                            else if (inst.materials[i] != null)
                                inst.materialInsts[i] = UnityEngine.Object.Instantiate(inst.materials[i]);
                        }
                    }

                    // materialId 修正 + 用 def.material 兜底（必定非 null）
                    if (def.materialId < 0 || def.materialId >= inst.materialInsts.Length)
                        def.materialId = 0;
                    if (inst.materialInsts[def.materialId] == null)
                        inst.materialInsts[def.materialId] = UnityEngine.Object.Instantiate(def.material);
                    def.materialInst = inst.materialInsts[def.materialId];
                }
            }
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.LogWarning($"[CustomBuildings] FixMaterialInsts 异常: {e}");
        }
    }

    // ==================== 受击与打碎（默认实现，参考 Door/Window）====================

    /// <inheritdoc/>
    public override void DamagedObject(PlayfieldObject damagerObject, float damageAmount)
    {
        base.DamagedObject(damagerObject, damageAmount);
        if (damageAmount >= (float)base.damageThreshold && !this.destroying && !this.destroyed)
        {
            this.BreakOpen(damagerObject);
        }
    }

    // ==================== IBreakOpen 爆炸三件套（已合并原 IObjectExplosive） ====================
    // 原 IObjectExplosive 的 ExplosionRadius / ExplosionDamage / OnExplode 已合并进 IBreakOpen 接口。
    // 基类提供"未实现"默认值（半径/伤害为 0、OnExplode 空实现），保证只 override OnBreakOpen 的旧子类
    // 依然能编译。爆炸触发规则（见 IBreakOpen 接口文档）：
    //   用户端 override 了 OnExplode → 爆炸系统启用：
    //     - OnExplode 方法体为空 → 参照源码执行默认操作：自动生成爆炸（半径/伤害以用户端定义优先），爆炸后调用 OnExplode；
    //     - OnExplode 方法体非空 → 不执行默认操作，只执行用户端方法体内的逻辑。
    //   未 override OnExplode → 打碎时完全不触发爆炸效果。
    // 检测方式：反射判断 OnExplode 是否被子类 override（DeclaringType != CustomObjectReal）+
    // 方法体 IL 是否为空（只含 ret/nop 指令），按类型缓存。

    /// <summary>爆炸半径（默认 0 = 未配置。子类 override 并返回 &gt;0 的值才覆盖原版爆炸半径）。</summary>
    public virtual float ExplosionRadius => 0f;

    /// <summary>爆炸伤害（默认 0 = 未配置。子类 override 并返回 &gt;0 的值才覆盖原版爆炸伤害）。</summary>
    public virtual int ExplosionDamage => 0;

    /// <summary>爆炸后处理回调（默认空实现。子类 override 后，按 IBreakOpen 爆炸触发规则决定是否自动生成爆炸）。</summary>
    /// <param name="damagerObject">引爆来源（可为 null）。</param>
    public virtual void OnExplode(PlayfieldObject? damagerObject) { }

    /// <summary>"用户端 OnExplode 是否为空实现"缓存（按运行时类型缓存，避免每次打碎都反射读 IL）。</summary>
    private static readonly ConcurrentDictionary<Type, bool> OnExplodeEmptyCache = new ConcurrentDictionary<Type, bool>();

    /// <summary>用户端是否 override 了 OnExplode（决定爆炸系统是否启用；DeclaringType != CustomObjectReal 即视为用户实现）。</summary>
    private bool HasUserOnExplode()
    {
        Type t = this.GetType();
        return t.GetMethod("OnExplode", InstancePublicFlags)?.DeclaringType != typeof(CustomObjectReal);
    }

    /// <summary>用户端 OnExplode 方法体是否为空（只含 ret/nop 指令 = 空实现；反射失败按非空处理）。</summary>
    private bool OnExplodeBodyIsEmpty()
    {
        Type t = this.GetType();
        return OnExplodeEmptyCache.GetOrAdd(t, static type =>
        {
            System.Reflection.MethodInfo? m = type.GetMethod("OnExplode", InstancePublicFlags);
            if (m == null) return true;
            try
            {
                byte[]? il = m.GetMethodBody()?.GetILAsByteArray();
                if (il == null || il.Length == 0) return true;
                foreach (byte b in il)
                {
                    if (b != 0x2A /* ret */ && b != 0x00 /* nop */) return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    /// <summary>实例公共方法反射标志。</summary>
    private const System.Reflection.BindingFlags InstancePublicFlags =
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;

    /// <summary>
    /// 洒落容器物品：建筑打碎（或黑客成功等）时，把容器里的物品全部弹出
    /// （参考 TrashCan——打碎后内部没捡的东西会爆出来）。条件：有物品栏 + canSpill + 服务端。
    /// 子类可在自定义逻辑中复用（如黑客成功后洒落）。
    /// </summary>
    /// <param name="damagerObject">造成破碎的物体（可为 null）。</param>
    protected void SpillContainerItems(PlayfieldObject damagerObject)
    {
        try
        {
            if (base.objectInvDatabase == null || !this.canSpill) return;
            if (this.gc == null || !this.gc.serverPlayer) return;

            // 复制原版 ObjectReal.DestroyMe2 的洒落逻辑：
            foreach (InvItem spilledInvItem in base.objectInvDatabase.InvItemList)
            {
                // 仅处理有效且标记为可溢出的物品（doSpill 默认 true）
                if (spilledInvItem.invItemName == null || !spilledInvItem.doSpill) continue;

                Item item = this.gc.spawnerMain.SpillItem(base.transform.position, spilledInvItem);
                if (item == null) continue;

                item.SetCantPickUp(type: false);
                item.containerExplosion = this.damagerExplosion;
                item.startingChunk = base.startingChunk;
                item.startingSector = base.startingSector;
                if (spilledInvItem.canHaveStartingOwner)
                {
                    item.startingOwner = this.owner;
                }
                item.source = this;
                CustomBuildingsPlugin.LogInfo($"[{this.ObjectName}] 打碎洒落物品: {spilledInvItem.invItemName} x{spilledInvItem.invItemCount}");
            }

            // 洒完后清空物品栏，避免与 DestroyMe2 的洒落逻辑重复
            base.objectInvDatabase.DestroyAllItems();
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] 洒落容器物品异常: {e}");
        }
    }

    /// <summary>打碎：触发爆炸（若实现接口）→ 音效 + 打通脚下的墙 + 销毁（基类销毁时自动打通寻路、产生残骸）。</summary>
    public void BreakOpen(PlayfieldObject damagerObject)
    {
        if (this.destroying || this.destroyed) return;
        this.meleeCanPass = true; // 碎了之后近战可以挥过

        // 破碎回调：基类已实现 IBreakOpen 接口（OnBreakOpen 虚方法即接口实现），
        // 子类 override 即可自定义破碎逻辑（无需 is 判断，虚方法多态分发）。
        try
        {
            if (this is IBreakOpen obj)
            {
                obj.OnBreakOpen(damagerObject);
            }   
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.LogError($"[{this.ObjectName}] OnBreakOpen 回调异常: {e}");
        }

        // 洒落容器物品：建筑打碎时，内部没捡的物品全部爆出来（像垃圾桶）
        this.SpillContainerItems(damagerObject);

        // 破碎爆炸：爆炸三件套（ExplosionRadius / ExplosionDamage / OnExplode）已合并进 IBreakOpen。
        // 触发规则（见 IBreakOpen 接口文档）：用户端 override 了 OnExplode → 爆炸系统启用；
        // - OnExplode 方法体为空 → 参照源码执行默认操作：自动生成爆炸（用 ExplosionRadius / ExplosionDamage
        //   覆盖威力，用户端定义优先，>0 才覆盖），爆炸后调用 OnExplode（空，无事发生）；
        // - OnExplode 方法体非空 → 不执行默认操作，只执行用户端方法体内的逻辑。
        // 未 override OnExplode 的类打碎时完全不触发爆炸效果。
        try
        {
            if (this.HasUserOnExplode())
            {
                if (this.OnExplodeBodyIsEmpty())
                {
                    GameController gc2 = this.gc;
                    if (gc2 != null && gc2.serverPlayer && !this.spawnedExplosion)
                    {
                        this.spawnedExplosion = true;
                        Explosion explosion = gc2.spawnerMain.SpawnExplosion(
                            damagerObject,                 // 引爆来源（伤害归属/仇恨）
                            this.tr.position,              // 爆炸位置（建筑中心）
                            "Normal",                      // 爆炸类型（威力由下方成员覆盖）
                            immediateHit: false,
                            -1,                            // explosionNetID（-1 = 自动分配）
                            hitMultPlayer: false,
                            this.FindMustSpawnExplosionOnClients(damagerObject)); // 多人：客户端是否同步
                        if (explosion != null)
                        {
                            // 覆盖爆炸威力：用户端定义的 ExplosionDamage / ExplosionRadius 优先（>0 才覆盖默认值）
                            if (this.ExplosionDamage > 0)
                                explosion.damage = this.ExplosionDamage;
                            if (this.ExplosionRadius > 0f && explosion.circleCollider2D != null)
                                explosion.circleCollider2D.radius = this.ExplosionRadius;
                        }
                    }
                }
                this.OnExplode(damagerObject); // 用户端回调（空则无事发生；非空则只执行用户端逻辑）
            }
        }
        catch (Exception e)
        {
            CustomBuildingsPlugin.LogError($"[{this.ObjectName}] 爆炸处理异常: {e}");
        }

        // 注意：不能在此设 this.destroying = true！
        // 基类 ObjectReal.DestroyMe 检查 if (!destroying) 才会启动销毁协程 DestroyMe2，
        // 先设 destroying=true 会导致 DestroyMe 直接 return、物体永远打不碎。
        // 参考 Door：用独立的 destroyingDoor 标志，从不预先设 destroying。

        if (!this.noDestroySound && this.gc != null && this.gc.audioHandler != null)
        {
            this.gc.audioHandler.Play(this, "WindowDamage");
        }

        // 残骸/碎片由基类 DestroyMe2 统一生成

        // 只有服务器负责改 tilemap；客户端等同步（damageImmediateOnClient 会让客户端自行销毁）
        if (this.gc != null && this.gc.serverPlayer && this.gc.tileInfo != null)
        {
            try
            {
                // 像窗一样：如果嵌在墙里，砸碎后把墙打通
                this.gc.tileInfo.DestroyWallTileAtPosition(
                    this.tr.position.x, this.tr.position.y, true, this.lastHitByAgent);
            }
            catch (Exception e)
            {
                CustomBuildingsPlugin.LogWarning($"[{this.ObjectName}] DestroyWallTileAtPosition: {e.Message}");
            }
        }

        this.DestroyMe(damagerObject);
    }
}
