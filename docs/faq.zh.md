---
hide:
  - navigation
---

# 常见问题

!!! tip "这儿没写到？"
    也可以去 [Discord](https://discord.gg/3JAE2JE) 或 [GitHub Issues](https://github.com/Kerbalism/Kerbalism/issues) 问一声。

## 安装需要什么？

两样都得有：

- **Kerbalism**（核心插件）
- **恰好一个**配置包（官方 `KerbalismConfig`，或 ROKerbalism / SIMPLEX 等）

依赖：Module Manager、HarmonyKSP、KSPCommunityFixes。官方配置包还要 Community Resource Pack。详见 [链接](links.md)。

**KSP 版本：** 当前发行版（3.40）只认 **1.12.x**。

## 能同时装好几个配置包吗？

不能——除非那个包自己写明可以叠。混装通常会把平衡和 MM 补丁一起搞乱。

## 我的 Kerbal 怎么又死了？

最常见的几种：

1. 吃喝呼吸断供（食物 / 水 / 氧气）
2. 辐射，或者过热 / 过冷
3. 气压或居住条件长期恶劣，事故连锁反应
4. 和其他模组抢资源，后台船被「饿死」

先看监控面板和规划器，再对照 [模组兼容](mod-support.md)。

## 科学点怎么变少了 / 实验老做不完？

Kerbalism 把多数实验改成了**随时间产出**。要供电、要存储空间，有时还要特定环境或乘员。详见 [科研指南](play-guide/science.md) 和 [科研教程](play-guide/science-tutorial.md)。

## 为什么别的模组坏了？

为了在未加载飞船上也能模拟，Kerbalism 会替换或接管大量原版 / 第三方模块。功能叠在一起、或者没人写兼容时，就容易炸。先读 [模组兼容](mod-support.md)。建议：模组列表别堆成山。

## BackgroundResources / DeepFreeze 警告？

部分 JPLRepo 模组（比如 DeepFreeze）自带的 `BackgroundResources` 会和 Kerbalism 后台打架，启动时可能弹警告。

实践里 **DeepFreeze 不建议和 Kerbalism 同装**：删掉 `GameData/REPOSoftTech/BackgroundResources` 或许能消掉警告，但 DeepFreeze 本身仍和 Kerbalism 的资源 / 后台模型重叠。TAC-LS 同理。

个别启动警告可在难度设置里关掉（`ModsWarning` 等相关选项）。

## RemoteTech？

通信集成代码还在（天线、耗电系数、控制路径），但这**不等于**大型 RT + Kerbalism 组合维护得很好。边角问题会有，请自己测。多数人用原版 CommNet 更省心。

## 时间加速老被警报掐断？

打开 Kerbalism 飞船界面的 **cfg** 页，按船关掉不需要的警报。太阳风暴导致的停加速，只有当该行星系里至少还有一艘船开着太空天气警告时才会发生。

## 样品在部件之间传不过去？

样品需要 Kerbal（EVA 或船上有人）。难度选项里可以允许无人船也始终能传样品。

## 和 [x]Science! 之类能一起用吗？

那些功能 Kerbalism 里大多已经有了（自动跑实验、科目可用性等）。科研系统和原版差太远，双开没有实质意义。

## 为什么 Mk1 座舱没有气压？

Mk1 本来就不是按可加压设计的。先保乘员活命，等解锁更好的栖息地再说。

## 燃料电池不发电？

先确认水有地方存（或者开 Dump 倾倒），再把燃料电池关一下再开。

## 压力超过 100%——会压力致死吗？

压力很高时会触发随机坏事（倒资源、删科研、炸部件……），然后压力回落，再慢慢爬上去。压力本身不是一根「死亡条」，但那些事件往往足以毁任务。

## 文档好多页还是英文？

中文站优先翻导航和玩法页；没翻到的会**回退显示英文**。欢迎来补译文。

## Bug 往哪儿报？

优先 [GitHub Issues](https://github.com/Kerbalism/Kerbalism/issues)，并附上 [KSPBugReport](https://github.com/KSPModdingLibs/KSPBugReport) 打出来的完整日志。缺日志的报告，经常没人理。
