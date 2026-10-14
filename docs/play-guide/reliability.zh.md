## MTBF

每个组件有一个 [平均故障间隔时间（MTBF）](https://en.wikipedia.org/wiki/Mean_time_between_failures)，大致表示「平均多久会坏一次」。

## 故障

故障分两种：

- **故障（malfunction）**：能修
- **致命故障（critical failure）**：更少见，但修不了

两种都会把关联模块弄停工。

无人船上组件坏了时，有概率由任务控制的工程师远程修好。

## 发动机

点火次数、额定烧时和启动失败率由可选 companion **KerbalismEngineFailures** 提供。CKAN 会随官方 KerbalismConfig **推荐**安装；与 TestFlight / RO / RP-1 冲突（用它们自己的引擎可靠性）。核心 Kerbalism 的 MTBF 可靠性不受影响。

装了 companion 后：多数发动机有点火次数和烧时上限，点火也有小概率失败；EVA 检查/维修可在一定程度上恢复剩余点火与烧时。

## 质量等级

组装厂里可以为各组件指定制造质量。质量越高，MTBF 越长，但更贵、更重——可靠性和成本 / 质量得自己权衡。额外成本和质量按部件原价、原质量的比例加。

## 检查与维修

所有 Kerbal 都能检查组件，拿到「距下次故障还有多久」的模糊提示。

有相应专业和经验等级时，还能修已经坏掉的组件。难度选项可以要求维修时消耗 **维修包**（默认关）。

## 冗余

对付故障，最靠谱的还是冗余。每个组件属于某个冗余组，规划器会据此分析船上还剩几条退路。可选地：同组里有一个坏了，其余几个短期内会更不容易坏。

## 支持的模块

系统通过 Reliability 模块，可以对部件上任意模块触发故障。多数原版组件会自动挂上。

| 组件 | MTBF 标准 | MTBF 高 | 谁能修 | 冗余组 | 额外成本 | 额外质量 |
| --- | --- | --- | --- | --- | --- | --- |
| Solar Panel (standalone) | 4 年 | 16 年 | 任何人 | Power Generation | 2.5 | 1.0 |
| Solar Panel (embedded) | 4 年 | 16 年 | 任何人 | Power Generation | 0.25 | 0.1 |
| Solar Panel (manned) | 4 年 | 16 年 | 任何人 | Power Generation | 0.125 | 0.05 |
| Reaction Wheel (standalone) | 4 年 | 16 年 | 任何人 | Attitude Control | 2.0 | 1.0 |
| Reaction Wheel (embedded) | 4 年 | 16 年 | 任何人 | Attitude Control | 0.25 | 0.15 |
| Reaction Wheel (manned) | 4 年 | 16 年 | 任何人 | Attitude Control | 0.2 | 0.05 |
| RCS (standalone) | 8 年 | 32 年 | 工程师 | Attitude Control | 2.0 | 1.0 |
| RCS (embedded) | 8 年 | 32 年 | 工程师 | Attitude Control | 0.2 | 0.1 |
| RCS (manned) | 8 年 | 32 年 | 工程师 | Attitude Control | 0.1 | 0.05 |
| Light (standalone) | 4 年 | 16 年 | 任何人 |  | 5.0 | 1.0 |
| Light (embedded) | 4 年 | 16 年 | 任何人 |  | 0.1 | 0.05 |
| Light (manned) | 4 年 | 16 年 | 任何人 |  | 0.05 | 0.01 |
| Parachute | 8 年 | 32 年 | 任何人 | Landing | 2.5 | 0.5 |
| Engine | 8 年 | 32 年 | 工程师 | Propulsion | 1.0 | 0.1 |
| Radiator* (standalone) | 8 年 | 32 年 | 工程师 |  | 1.0 | 0.25 |
| Radiator* (embedded) | 8 年 | 32 年 | 工程师 |  | 0.2 | 0.1 |
| Radiator* (manned) | 8 年 | 32 年 | 工程师 |  | 0.1 | 0.05 |
| Resource Converter | 8 年 | 32 年 | 工程师 |  | 1.0 | 0.2 |
| Resource Harvester | 8 年 | 32 年 | 工程师 |  | 1.0 | 0.2 |
| Experiment (standalone) | 8 年 | 32 年 | 工程师 |  | 0.5 | 0.1 |
| Experiment (embedded) | 8 年 | 32 年 | 工程师 |  | 0.05 | 0.01 |
| Experiment (manned) | 8 年 | 32 年 | 工程师 |  | 0.025 | 0.005 |
| Antenna (standalone) | 8 年 | 32 年 | 工程师 | Comms | 1.0 | 0.1 |
| Antenna (embedded) | 8 年 | 32 年 | 工程师 | Comms | 0.5 | 0.01 |
| Antenna (manned) | 8 年 | 32 年 | 工程师 | Comms | 0.05 | 0.001 |
| Treadmill (in Hitchhiker) | 4 年 | 16 年 | 工程师 |  | 0.1 | 0.05 |
| ECLSS (standalone) | 8 年 | 32 年 | 任何人 | Life Support | 2.5 | 0.1 |
| LSS (manned) | 8 年 | 32 年 | 任何人 | Life Support | 0.625 | 0.025 |
| Fuel Cell | 8 年 | 32 年 | 工程师 | Power Generation | 1.0 | 0.5 |
| Chemical Plant | 8 年 | 32 年 | 工程师 |  | 1.0 | 0.2 |
| Crustal Harvester | 8 年 | 32 年 | 工程师 |  | 1.0 | 0.2 |
| Atmospheric Harvester | 8 年 | 32 年 | 工程师 |  | 1.0 | 0.5 |

*\*适用于散热器电机与散热板。*

表中 MTBF 是估计平均值，独立部件大体接近。嵌在大部件里的模块（比如载人舱内置反作用轮）波动更大：MTBF 跟部件质量有关；嵌入模块按部件质量的某一比例算；有乘员容量也会纳入。为避免出现离谱数字，MTBF 下限 4 年、上限 64 年。经验上，更重的部件往往坏得更勤。

额外成本 / 额外质量是相对部件原值的倍率：0.1 = +10%，2.5 = +250%。
