---
classless: 2026-08-16 15:43:00
---
***引言：***
1. 写到这里有点困了，不能老熬夜，知道吗？o(´^｀)o

# 实现
- 只需要在你的自定义建筑类开头加上IMinimapTeleportable接口即可。
- 不知道突变是否可以禁用，我没有测试。

例：
```
public class RecycleBin : CustomObjectReal, IMinimapTeleportable
{
}
```

- 使用此方法设置建筑物突变大小，不是必须实现，默认为1f。

示：
```
public float GetMinimapIconScale() => 1f;
```