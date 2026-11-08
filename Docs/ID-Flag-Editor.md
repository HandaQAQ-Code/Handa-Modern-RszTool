# 子物体 ID 批量编辑与 UVAR flag 管理

## 启动

双击项目根目录的 `Start-ID-Flag-Editor.cmd`，启动当前发布目录中的 `RszTool.App.exe`。也可以将一个资源文件拖到启动脚本上。

默认隐藏文件侧边栏，点击顶部侧边栏按钮可重新显示。正常编辑树按原始物体层级显示，不合并或折叠同名对象。“折叠同名子对象”仅存在于批量编辑窗口，开关会记忆，保存文件时保留原始层级、顺序和独立对象。

## 批量编辑子物体

在 SCN 或 PFB 的物体树中右键父物体，选择“批量编辑子物体 ContextID / StageID / ItemID”。

- 窗口左侧实际列出扫描到的物体、子对象及路径，可切换“入口物体及全部子对象”“仅入口物体”“仅子对象（递归）”。每项显示 ContextID、StageID、ItemID 数量和当前物品名称。
- 通过复选框决定处理范围，支持全选、全不选、Ctrl / Shift 高亮多选后勾选、分组整组勾选。搜索只是筛选显示，不改变隐藏条目的勾选状态。
- 同一父级下的同名物体可在批量窗口中分组，标为 `子对象 ×23`，默认展开以便检查列表，可手动折叠。正常编辑不使用这个分组。
- 勾选需要修改的 ID。ContextID 设置 Group、起始 Index 和递增步长；StageID、`_DropItemID` 复用普通字段的枚举编辑器，可输入数值或从名称下拉框选择，对应的名称和数值会一起显示。批量物品设置只匹配字段名 `_DropItemID`。
- 顶部 `_ShouldDropItem`、`_ShouldDropItemAtRandom` 为三态复选框：实心方块表示不修改，打勾表示开启，空框表示关闭。可只设置开关，也可以和 ID 修改或掉落随机化一起应用。预览显示原值和新值，列表显示物体当前开关状态。
- 点击“预览修改”查看引用路径、字段、原值、新值，再应用并保存文件。
- 遍历组件中的嵌套对象与数组，共享的 RSZ 实例只处理一次；不展开外部 UserData 文件。
- 保留 ContextID 的 Category、Kind 和未勾选的字段。

也可以在子对象集合或 SCN 文件夹上右键发起批量扫描。正常 SCN 树 Ctrl / Shift 多选物体后，从选中物体右键进入会带入整组选择。批量处理只修改左侧已勾选物体本身的组件和嵌套字段，不会自动把未勾选后代再次加入。

“原始字段 / 多物体批量编辑”复用正常子物体的字段编辑器。左侧点击某个物体后，右侧可直接编辑其原始字段；在普通字段上右键，可将当前值应用到所有已勾选物体的同类同名字段。普通数组项按同类、同字段、同序号匹配。应用时显示修改范围并确认。关闭窗口保留已应用修改，需要回主窗口保存。

## 掉落物随机化

进入批量窗口的“掉落物随机化”，勾选要处理的物体或子对象。添加物品行，通过 ItemID 名称下拉框选择物品并设置比例，例如 `7:3` 或 `70:30`。配置会记忆。

- 按独立目标数量计算比例配额，使用最大余数取整，再随机打散位置，因此小样本的实际比例会受整数数量限制。
- 每个目标分配一种 `_DropItemID`，目标中所有匹配字段一致。共享字段的物体合并为一个分配单元，避免同一字段被反复分配成不同物品；预览会显示物体数量与独立分配单元数量。
- 严格匹配 `_DropItemID`，不再按类名 / 路径中是否包含 Drop 判断，也不修改其他 ItemID 或 Ammo 字段。没有 `_DropItemID` 的物体跳过，工具不新建游戏掉落组件。顶部两个掉落开关可与随机分配同时应用。
- 点击“预览随机分配”查看实际字段、原物品、新物品及各物品数量。种子相同且目标、比例相同，结果保持一致；“换一批位置”会更换种子并更新预览。
- 应用的是当前预览。如果勾选范围、配置或原字段改变，会先刷新预览并要求重新应用。应用完成后回主窗口保存文件。

## USER / savedata 重复多次

数组项的“重复多次”提供副本数量及自动 ContextID 设置。勾选自动设置后，按副本顺序为其全部独立 ContextID 连续分配 Index。源对象不变；取消不会创建副本。

USER 文件的 Objects 节点也可以右键选择“重复 RSZ Object 多次 / 自动 ContextID”。副本加入公开对象表。数组中的对象仍使用数组项自己的“重复多次”。

对话框会记住最后使用的 Group 和 Index，下次默认从最后 Index 的下一项开始。自动分配的范围由用户选择，工具不会自动查找其他游戏资源中的 ID。

## 复制敌人时随机 MontageID

单次重复和重复多次都使用新的 MontageID 配置窗口，默认启用随机化。系统读取每个 MontageID 字段对应下拉框中的全部数值，按枚举类型分别建立候选池；不同敌人的 MontageID 枚举不混用。左侧切换来源，可查看该来源的名称 / 数值列表。

- 默认勾选“包含该下拉框的所有 MontageID”。额外 ID 输入区支持空格、换行、逗号分隔的 UInt32 十进制或 `0x` 十六进制数，自动与枚举候选去重合并。
- 取消“包含全部”后，可只使用自定义 ID。没有枚举且没有额外 ID 的字段保持原值。
- `_RandomMontageID` 这类没有专用枚举的整数辅助字段，会复用同一实例中主 MontageID 的枚举来源。名称包含 MontageID 的布尔开关不参与 ID 随机赋值。
- 输入预设名称后点击“保存 / 覆盖预设”，记录所有当前来源的枚举开关和额外 ID。支持载入、覆盖、删除和恢复默认全部枚举。载入预设时，预设未记录的来源使用默认全部枚举。
- 预设及最后使用的候选配置保存在 `RszTool.App.SaveData.json`，重启后保留。只有新副本参与随机赋值，原敌人不变。

## UVAR v3

点击“设置 UVAR 文件”，配置用于 flag 管理及 GUID 选择的 `*.uvar.3`，保存设置后会记忆路径。点击主窗口“UVAR / Flag 管理”切换到管理界面，再点“返回文件编辑”回到原来的文件标签页。通过菜单、拖放或文件树打开 UVAR，也只切换到这个管理界面，不创建独立标签页。切换界面会保留 USER 编辑状态与 UVAR 未保存修改。

- 分组下拉框可显示全部分组或指定分组，搜索可匹配名称、GUID、分组与备注。
- “批量创建 flag（数字递增）”沿用选中 flag 的命名格式，未选择时沿用该分组已有名称。例如 `Location47_000` 会保留 `Location47_` 前缀和三位编号，默认从同规则的最大编号继续。可指定起始编号、数量、步长、默认 True/False，并预览整批名称。
- “按名称创建”仍支持每行一个名称。所有新 flag 自动分配独立 GUID；同组名称或名称哈希冲突会拒绝整批创建。
- 使用 Ctrl / Shift 多选后，可以删除、标记为已使用、标记为未使用或统一设置使用备注。
- “已使用”只表示自己的用途记录，与游戏默认 True / False 分开。标记和备注即时写入本地，无需保存游戏文件。
- 使用状态支持全部、已使用、未使用筛选；已使用条目加粗。
- 游戏默认值、创建和删除操作需要通过保存或另存为写入 UVAR。

本地记忆文件：`%LOCALAPPDATA%\RszTool\uvar-flag-usage.json`。记录按完整文件路径和 flag GUID 区分；另存为会复制标记。删除 flag 不清除其历史标记，避免同 GUID 恢复后丢失记录。

覆盖保存 UVAR 时，自动生成带时间戳的 `.bak` 备份，并在临时文件写完后原子替换。保持既有嵌套分组和数据位置，修改分组的变量表与哈希表追加在文件末尾。删除 flag 不会自动修改其他游戏文件中的引用。

## USER 的 System.GUID 字段

在 GUID 输入框旁点击“UVAR…”打开已配置文件的 flag 选择器，可按分组、名称、GUID、备注过滤。双击一项或点“使用此 GUID”即可填入当前字段，USER 会进入未保存状态，仍需正常保存 USER。

选择器按 UVAR 完整路径记忆最后的分组、搜索条件、筛选状态及选中 GUID，下次自动滚动到该条目。选择器的筛选独立于 flag 管理界面；另存 UVAR 会复制选择位置。位置在关闭选择器时写入设置，重启后仍可恢复。

默认勾选“选用后标记为已使用”，可自行取消；选用已使用的 flag 会再次提示用途备注。若 UVAR 有未保存修改，选择器会先提示保存，避免将尚未落盘的新 flag GUID 填入 USER。

## 代码位置

| 文件 | 职责 |
| --- | --- |
| `RszTool/RszFile/Re4IdEditor.cs` | 子物体、嵌套 RSZ 遍历，生成批量修改计划，递增 ContextID |
| `RszTool.App/Views/BatchIdEditWindow.xaml` | 批量 ID 输入与修改预览 |
| `RszTool.App/Views/DuplicateContextWindow.xaml` | 重复次数和自动 ContextID 配置 |
| `RszTool.App/ViewModels/BaseRszFileViewModel.cs` | 连接 SCN/PFB 批量编辑、USER 对象和数组重复命令 |
| `RszTool/RszFile/UvarFile.cs` | UVAR v3 读写、嵌套分组、flag 创建、索引更新和备份保存 |
| `RszTool/Common/UvarUsageStore.cs` | 使用标记、备注及另存为迁移 |
| `RszTool.App/ViewModels/UvarFileViewModel.cs` | UVAR 筛选、多选操作与保存状态 |
| `RszTool.App/Views/UvarFileView.xaml` | UVAR flag 管理界面 |
| `RszTool.App/ViewModels/MainWindowModel.cs` | 文件打开、标签页和格式选择 |
| `RszTool/Common/FlagNameSequence.cs` | 识别已有编号格式，生成保持补零位数的递增名称 |
| `RszTool.App/ViewModels/UvarWorkspace.cs` | flag 管理与 GUID 选择器共享 UVAR 文档 |
| `RszTool.App/Views/BatchFlagCreateWindow.xaml` | 批量编号创建与名称预览 |
| `RszTool.App/Views/UvarSettingsWindow.xaml` | UVAR 文件配置 |
| `RszTool.App/Views/UvarGuidPickerWindow.xaml` | GUID 选择与上次位置恢复 |

修改前的源码备份位于 `D:\WorkPlace\Re4R\RETOOL-feature-backups\20261030-ids-uvar`。原有未提交改动在当前工程中保留。
