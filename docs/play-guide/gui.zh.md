## 天体信息

在追踪站或地图视角按 **B**，打开天体信息窗口。这里能看到大气与辐射环境，也能开关辐射场的显示。

![](../assets/gui/body-info.png)

## 规划器

组装厂 / 航天飞机库里有规划器，帮你围着 Kerbalism 的新规矩造船。

计算会相对「目标天体 + 情境」进行：恒星是否被挡住、船上当前搭了多少人（按住 ALT 则按整船满员算）。

目标天体、情境、是否在阴影里，点规划器标题栏上的对应图标就能改。

鼠标悬停某一行，会弹出说明和更多细节。

![](../assets/gui/planner.png)

## 监控器

飞船监控器在航天中心、追踪站和飞行中都能用，用来扫一眼各船状态，以及舱里那些 Kerbal 过得怎样。

![](../assets/gui/monitor.png)

列表里每一行大致是：

- 飞船名
- 所在天体
- 问题图标
- 电量
- 补给
- 可靠性
- 信号

左键点船进入详情；在监控器任意处右键退回列表。

详情里用底部菜单切换面板；中键可以把该面板弹成独立窗口。

**分组与过滤**

详情底部最后一个按钮用来查看 / 修改飞船所属的组。只要有船分了组，底部就会出现过滤栏——输入组名，只留下那一组的船。

![](../assets/gui/monitor-filter.gif)

**问题图标**

|  | sun-black |  | 不在直射阳光下 |
| --- | --- | --- | --- |
|  | storm-yellow |  | 太阳风暴临近 |
|  | storm-red |  | 太阳风暴进行中 |
|  | radiation-yellow |  | 强辐射 |
|  | radiation-red |  | 极端辐射 |
|  | health-yellow |  | Kerbal 身体不适 |
|  | health-red |  | Kerbal 快不行了 |
|  | brain-yellow |  | Kerbal 压力很大 |
|  | brain-red |  | Kerbal 即将崩溃 |
|  | recycle-yellow |  | CO₂ 过了警告线 |
|  | recycle-red |  | CO₂ 过了危险线 |
|  | plant-yellow |  | 温室没在长 |

**电池**

|  | battery-white |  | 电量高于警告阈值 |
| --- | --- | --- | --- |
|  | battery-yellow |  | 电量低于警告阈值 |
|  | battery-red |  | 电量耗尽 |

**补给**

|  | box-white |  | 补给高于警告阈值 |
| --- | --- | --- | --- |
|  | box-yellow |  | 补给低于警告阈值 |
|  | box-red |  | 补给耗尽 |

**可靠性**

|  | wrench-white |  | 一切正常 |
| --- | --- | --- | --- |
|  | wrench-yellow |  | 有故障（可修） |
|  | wrench-red |  | 有致命故障 |

**信号**

|  | signal-white |  | 传输速率高于 5 Kbps |
| --- | --- | --- | --- |
|  | signal-yellow |  | 传输速率低于 5 Kbps |
|  | signal-red |  | 无信号 / 中断 |

## 遥测

读数汇总：乘员体征、资源补给、居住区与环境。

![](../assets/gui/telemetry.png)

## 文件管理器

查看硬盘里的文件，标记传输或分析，或者直接删掉。悬停可看更多信息。

![](../assets/gui/file-manager.png)

## 设备管理器

各组件开关状态一览，也是自动化脚本的编辑入口。

![](../assets/gui/dev-manager.png)

## 配置管理器

决定这艘船要弹哪些消息。

![](../assets/gui/config-man.png)
