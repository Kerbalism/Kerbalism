## 温度

太空里的温度可以从极低跳到极高。Kerbalism 的温度模型考虑：

- [太阳辐照](https://en.wikipedia.org/wiki/Solar_irradiance)（恒星能量通量，未被挡住时）
- [反照辐照](https://en.wikipedia.org/wiki/Albedo)（天体反射到飞船上的能量）
- [天体热辐射](https://en.wikipedia.org/wiki/Radiative_cooling)（近旁天体的红外冷却通量）
- [宇宙微波背景](https://en.wikipedia.org/wiki/Cosmic_microwave_background)

再按 [斯特藩–玻尔兹曼定律](https://en.wikipedia.org/wiki/Stefan%E2%80%93Boltzmann_law)，把飞船当成理想 [黑体](https://en.wikipedia.org/wiki/Black_body) 算出温度。进入大气后，改用原版大气温度模型。

## 辐射

天体和辐射的关系很复杂：有的有磁层挡辐射，有的有高能粒子辐射带；天体本身可能有放射性，从表层放出伽马；恒星还有强弱起伏的活动周期。

这些用 *辐射场* 来建模——天体周围一块块带辐射等级的区域。飞船总辐射，是所有重叠在当前位置上的场叠出来的。

辐射场可以在地图视角或追踪站画出来，用小键盘 *0/1/2/3*，或天体信息窗口切换。

[![Kerbalism 辐射带](https://github.com/Kerbalism/Kerbalism/raw/master/misc/img/showcase/radiation.png)](https://www.youtube.com/watch?v=CXmeSMBzf1c)

辐射模型可以改，详见技术指南的辐射章节。

## 日冕物质抛射（CME）

[日冕物质抛射](https://en.wikipedia.org/wiki/Coronal_mass_ejection) 从恒星日冕甩出来，朝行星系统或绕星飞船砸过去。一旦侦测到会打到你船的 CME，就会提前警告。击中时，与恒星视线直通的飞船吃额外辐射；躲在磁层里的船则会遭遇通信中断。效应持续一阵子，再恢复正常。

CME 的频率和强度跟着太阳活动走，而太阳活动又大致按约 11 年的周期从低到高再回落；太阳表面辐射（若有）也会跟着变。行星际航行的一种取巧办法：挑太阳活动偏低的窗口出门。

CME 期间，对有人船要格外上心。最稳的防护是调整姿态，让乘员舱躲在发动机或大贮箱后面——挡太阳那一侧质量越大越好。这能削掉一部分粒子辐射，但也会砸出伽马形式的 [轫致辐射](https://en.wikipedia.org/wiki/Bremsstrahlung)。理想情况是：一块很重的东西，尽量离乘员舱远一点。设计磁层外的载人任务时，值得提前想好。

太阳活动很高时，少安排 EVA，能待在阴影里就待着。

![用船体质量挡住 CME](../assets/rdr/deep_space_station.png)

## 放射性部件

有些部件自己就是放射源，比如 NERV、核反应堆、RTG。离乘员舱远一点，限制辐射剂量；EVA 时也别凑近。距离就是最好的屏蔽——你也没有别的选项。
