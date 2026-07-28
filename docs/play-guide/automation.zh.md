船上组件可以按环境条件自动开开关关。一套变更写进脚本里，配有简单的图形编辑器。条件一变，对应脚本就会在船上执行——加载中的船和后台船一视同仁。

## 脚本

脚本就是「各组件该变成什么状态」的一张清单。每个组件只能是三者之一：*不管（don't care）*、*开*、*关*。

## 编辑器

在监控器里点 **auto** 图标即可打开。用标题栏箭头切换脚本，再点组件改状态。也可以切到「直接控制」页，手动拨开关。

## 直接控制

脚本编辑器还能当远程开关面板用：不必先点到部件，也能改状态；未加载的船同样适用。各组件当前状态也会列出来，权当整船状态摘要。

## 触发条件

| 条件 | 何时触发 |
| --- | --- |
| landed | 飞船变为着陆 |
| atmo | 进入大气 |
| space | 到达太空 |
| sunlight | 恒星重新可见 |
| shadow | 恒星被挡住 |
| power_high | 电量高于 80% |
| power_low | 电量低于 20% |
| rad_low | 辐射低于 0.02 rad/h |
| rad_high | 辐射高于 0.05 rad/h |
| linked | 恢复信号 |
| unlinked | 失去信号 |
| eva_out | 出舱 |
| eva_in | 从 EVA 返回 |
| drive_full | 硬盘用到 90% |
| drive_empty | 硬盘低于 10% |
| action[0-5] | 在活动飞船上按数字键 0–5 |

## 支持的模块

自动化只认下面这些：

| 模块 | 动作 |
| --- | --- |
| Antenna | 展开 / 收回 |
| Experiment | 启用 / 禁用 |
| Emitter | 启用 / 禁用 |
| Gravity Ring | 启用 / 禁用 |
| Greenhouse | 启用 / 禁用 |
| Harvester | 启动 / 停止 |
| Laboratory | 启动 / 停止 |
| Process Controller | 启动 / 停止 |
| 受支持的太阳能板（SSTU 除外） | 展开 / 收回 |
| ModuleGenerator | 启动 / 停止 |
| ModuleLight（及部分衍生） | 开 / 关 |
| ModuleResourceConverter（及部分衍生） | 启动 / 停止 |
| ModuleResourceHarvester | 启动 / 停止 |
| SCANsat | 开始 / 停止扫描 |
