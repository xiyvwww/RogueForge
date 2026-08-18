# RogueForge
为地痞街区的RougeLibs扩展出自定义建筑功能。/Extend custom building functionality for RogueLibs in Streets of Rogue.

**作者主页：https://space.bilibili.com/3546624213125988?spm_id_from=333.1007.0.0**
**地痞街区2官群：893076691(地痞街区2依旧跳票中，进群获得最新资讯)**
**地痞街区模组存档点：808351674(好多mod作者在此群，开发mod遇到困难可以进此群交流)**
**地痞街区模组存档点网站(网站不安全属于正常现象)：http://www.deepjq.cn**
**(框架做了好久，能搞出来很不容易，给个start和关注吧，求求了(⋟﹏⋞))**

***引言：***
- 有一件事情非常重要，必须要放在开头讲，就是作者为写这个，连最近的黎明杀机的2v8都没去玩。不止如此，为了尽快写出来，作者这期间竟然都没有碰黎明杀机。看在作者如此努力的情况下，关注、点赞、投币、start就都给了吧，求求了ಠ~ಠ 。
1. 该文档的PDF格式由AI生成，原文为.md格式，在Github仓库中即时更新。
2. 该项目算是RogueLibs的扩展，因为使用了好多RogueLibs库里的功能，所以在开发或使用时请引用RogueLibs。
3. RogueForge提供了许多方便的工具类和接口，相互组合可以衍生很多玩法。但毕竟不会做到完全全面，如果在其他地方要判断或使用自定义建筑，建议使用CustomObjectReal(继承ObjectReal)做类判断。
4. RogueForge提供的接口的具体效果有点像是把你创建的自定义建筑自动挂载到所需的钩子(hook)上。
5. 对于RogueForge的测试版和正式版的版本签名，正式版在游戏里(左下角)显示为“RF vx.x”。测试版显示为“*RF vx.x.x”。例：如果一正式版显示为“RF v1.0”，则测试版显示为“*RF v1.0.1”，前面两位“v1.0”与测试版对应。
- Q：主播主播，测试版和正式版有什么区别？难道测试版比正式版稳定吗？
- A：不不不，其实正式版一样不稳定，只是这么分比较帅而已。(*σ´∀`)σ

***目录***
- [基类 CustomObjectReal](./RogueForge%20详细手册/基类%20CustomObjectReal.md)
- 接口
  - [交互接口 IObjectInteraction](./RogueForge%20详细手册/接口/交互接口%20IObjectInteraction.md)
  - [商店接口 IStore](./RogueForge%20详细手册/接口/商店接口%20IStore.md)
  - [背包接口 IBackpack](./RogueForge%20详细手册/接口/背包接口%20IBackpack.md)
  - [容器接口 IObjectContainer](./RogueForge%20详细手册/接口/容器接口%20IObjectContainer.md)
  - [小地图传送接口 IMinimapTeleportable](./RogueForge%20详细手册/接口/小地图传送接口%20IMinimapTeleportable.md)
  - [破碎接口 IBreakOpen](./RogueForge%20详细手册/接口/破碎接口%20IBreakOpen.md)
  - [刷新接口 IBuildingSpawner](./RogueForge%20详细手册/接口/刷新接口%20IBuildingSpawner.md)
- [地图相关工具类 KMap](./RogueForge%20详细手册/工具类/地图相关工具类%20KMap.md)
- 建筑例子
  - [四不像](./RogueForge%20详细手册/建筑例子/四不像.md)
- [RogueForge 错误日志](./RogueForge%20详细手册/RogueForge%20错误日志.md)