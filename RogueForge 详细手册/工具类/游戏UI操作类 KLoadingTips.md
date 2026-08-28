---
classless: 2026-08-26T12:13
---
***引言：***
1. 目前支持操作加载页面的提示文本。

# 一、添加你需要的加载页面提示文本
- 首先你需要在Awake()注册你的文本。
- Protip_900的后缀必须是900+
- 你只能修改Chinese对应的文本和Protip_的后缀数字

例：
```
public void Awake()
{
	RogueLibs.CreateCustomName("Protip_900", "Dialogue",
                new CustomNameInfo { Chinese = "你的文本。" });
}
```

# 二、具体方法
- Initialize()为初始注册必须在Awake()调用。
- Initialize()执行后默认开启。

示：
```
/// keepOriginalTips是否覆盖原版提示。
KLoadingTips.Initialize(this, keepOriginalTips: false)

/// 是否启用你的加载页面提示文本。开启时，调用关闭，反之亦然。
KLoadingTips.ToggleTips();

/// 获取当前加载页面提示文本启用状态。
KLoadingTips.TipsEnabled
```