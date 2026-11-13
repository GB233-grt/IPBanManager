# 净域（IPBanManager）独立 QA 验收报告

- 报告版本：v1.0（最终）
- 报告时间：2026-11-13 13:57（Asia/Shanghai）
- 被测对象：`D:\Repository`（源码 + `dist\净域\IPBanManager.exe` 发布产物）
- QA 边界：只写入 `tests/QA-REPORT.md`；未修改 `src/`、`build.ps1`、`README.md`、`docs/`、`tests/CoreTests`；**真实系统 HOSTS 全程未被修改**（SHA256 前后一致）；未写 HKCU Run；未修改防火墙/驱动/网卡；全部 HOSTS 实验使用 `%TEMP%\IPBanManagerQA*` 下的临时 HOSTS，数据目录通过 `--data-dir`/`StoragePaths.Initialize` 指向临时目录。

> ### ⚠️ 快照与变更说明（重要）
> 本报告的**动态验证（test.ps1 / build.ps1 / 探针 / 预览版）执行于源码快照 A**：`HostsManager.cs` 13:41:59、`AppState.cs` 13:42:59，对应 `dist\净域\IPBanManager.exe` 构建于 13:43:34（SHA256 `6AF8F7CD…`）。
> 报告收尾时发现工作区在 **13:57:16–13:57:26 又修改了 6 个文件**（`DataService.cs`、`AppSettings.cs`、`HostsManager.cs`、`BackupManager.cs`、`AppState.cs`、`MainWindow.xaml.cs`），且 `dist/`、zip **尚未随新源码重建**。已核对到以下**进行中的修复**（均需在最终冻结构建上复测）：
> - P2-1：`DataService.cs:40,55-…` 新增 `RemoveNullItems` 清理 null 记录；
> - P2-3：`HostsManager.cs:451` 模板已清洗 CR/LF/TAB；
> - P2-4：`BackupManager.cs:127-153` 恢复流程已改为临时文件 + `File.Replace`（`WriteHosts` 本身仍只 `catch (IOException)`，见 P2-4）；
> - P2-5：`AppState.cs:56` 新增 `PersistenceFailed` 事件，`MainWindow.xaml.cs:135-138` 增加失败 Toast。
> 因此：**第 2 节的命令结果与第 4 节的复现结论对应快照 A；P1-1 发布包一致性结论对应报告时点的 `dist/`。最终发布前必须在冻结源码上重跑 `test.ps1` + 完整 `build.ps1` 并复核清单/zip/SHA（见第 6 节清单）。**

---

## 1. 执行环境

| 项 | 值 |
| --- | --- |
| 操作系统 | Microsoft Windows 11 Home China，10.0.22631（64 位） |
| PowerShell | 5.1.22621.6133 |
| .NET Framework | Release 533320（4.8 已安装，满足脚本 `>= 528040` 检查） |
| MSBuild | 4.8.9032.0（`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe`） |
| 运行权限 | **非管理员**（`IsInRole(Administrator)=False`） |
| 系统 ANSI 代码页 | 936（GBK，影响 HOSTS 编码实验） |
| 真实 HOSTS | `C:\Windows\System32\drivers\etc\hosts`，890 B，mtime 2026-11-13 13:03:00，SHA256 `01DA8B13564B80E4EF488AF8075052BEE13A6832276D39090DE6A5EFC48869DB` |
| 开工时进程 | 无 `IPBanManager` / `IPBanManager.Preview` 运行 |
| HKCU Run | 值名 `IPBanManager` **原本不存在**，本次未创建、未写入 |
| 报告时源码快照 | 动态验证基于快照 A：`HostsManager.cs` 13:41:59、`AppState.cs` 13:42:59、`DataService.cs` 13:37:09（`dist` 中 exe 构建于 13:43:34，包含以上源码）。报告收尾时工作区又有 13:57:16–13:57:26 的 6 文件修改（见页首「快照与变更说明」），尚未重新构建 |
| 验证材料 | 探针源码/输出、预览测试脚本与输出均位于 `%TEMP%\IPBanManagerQA\`（临时产物，不入库） |

---

## 2. 执行命令与结果

### 2.1 `test.ps1`（核心自动化测试）

命令：

```powershell
powershell -ExecutionPolicy Bypass -File test.ps1
```

结果：**通过 12，失败 0，退出码 0**（12/12）。输出摘要：

```text
构建核心测试...
  CoreTests -> D:\Repository\tests\CoreTests\bin\Release\CoreTests.exe
净域核心测试，临时目录：C:\Users\user\AppData\Local\Temp\IPBanManagerTests_20261113_134320
[PASS] 域名/URL 归一化            [PASS] 域名拒绝 IP 与通配符
[PASS] 常见子域扩展              [PASS] 批量文本解析
[PASS] 规则去重更新              [PASS] 托管块生成
[PASS] 托管块合并保持用户内容    [PASS] HOSTS UTF-8 写入读取
[PASS] 备份创建/恢复/保留        [PASS] 完整 Apply 与冲突检测
[PASS] JSON 数据持久化           [PASS] 设置/主题序列化
结果：通过 12，失败 0
```

### 2.2 `build.ps1 -SkipTests -NoZip`（Release 构建）

命令：

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1 -SkipTests -NoZip
```

结果：**退出码 0**。输出摘要：

```text
净域构建脚本 / 配置：Release / 构建类型：正式版（requireAdministrator）
MSBuild ...\IPBanManager.csproj /nologo /v:m /p:Configuration=Release
  IPBanManager -> D:\Repository\src\IPBanManager\bin\Release\IPBanManager.exe
构建完成：
  D:\Repository\dist\净域\IPBanManager.exe
  SHA256 6AF8F7CD16B60BACCA84D2CEB989F8EDBA7BBC4230063288E32187347270CEEC
```

构建后 `dist\净域\` 内容：`IPBanManager.exe`（232448 B，13:43:34，FileVersion/ProductVersion 1.0.0.0）、`快速开始.txt`、`版本信息.txt`、`用户手册.md`；`版本信息.txt` 内记录 SHA256 与实际 exe 一致（6AF8F7CD…）。

### 2.3 RT_MANIFEST 提取结果

使用 Win32 资源 API（`LoadLibraryEx(LOAD_LIBRARY_AS_DATAFILE)` + `FindResource(RT_MANIFEST=24, id=1)` + `LockResource`）从 PE 资源节直接提取，非文本猜测。

- `dist\净域\IPBanManager.exe`（SHA256 6AF8F7CD…）：

```xml
<trustInfo xmlns="urn:schemas-microsoft-com:asm.v3">
  <security>
    <requestedPrivileges>
      <requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
    </requestedPrivileges>
  </security>
</trustInfo>
```

- `src\IPBanManager\bin\Preview\IPBanManager.Preview.exe`（235520 B，13:43:24）：`requestedExecutionLevel level="asInvoker"`、`assemblyIdentity name="IPBanManager.Preview.app"`。
- 结论：正式版 **requireAdministrator 已确认**；预览版 asInvoker 与 README 描述一致。

### 2.4 独立探针（边界实验，链接真实源码，临时 HOSTS）

为覆盖现有 12 项测试之外的边界，QA 在 `%TEMP%\IPBanManagerQA\` 用 csc 编译独立探针，**只读链接** `HostsManager/BackupManager/DnsService/DataService/RuleService/DomainUtil/AppState` 等真实源码（WPF 相关 `App`/`ThemeManager` 使用最小测试桩），所有 HOSTS 写入均发生在其自动生成的临时根目录（如 `…\IPBanManagerQA_20261113_134812\`）。

结果：**16 项中 14 项通过，2 项失败**（2 项失败均为同一个 null 规则健壮性问题，见 P2-1）。

| 编号 | 实验 | 结果 |
| --- | --- | --- |
| A01 | 同一域名重复 upsert（同批 `bilibili.com/BILIBILI.COM/重复/URL` + 跨批） | PASS：规则数 2（根域 + www），Added=2、Updated=2；跨批只更新不追加；非法自定义 IP 报错且不落规则 |
| A02 | 托管块：禁用注释 / 分组暂停 / 重复域名 / 自定义 IP | PASS：暂停与禁用的映射行均以 `# ` 注释；`# [已禁用] … [视频]` 正确；重复域名只写 1 条映射；active=1、disabled=2 |
| A03 | 空规则清空托管块 | PASS：旧块与旧内容移除，块前/块后用户行保留，清空前仍备份 |
| A04 | MergeHosts 边界 | PASS：块外行/带行内注释行/TAB 续行保留；LF、无尾换行、块前块后夹用户行、双托管块（合并为 1 块）、缺结束标记（按文末处理）均符合约定 |
| A05 | 编码/BOM/原子写/只读 | PASS：默认 UTF-8 无 BOM、`useBom=true` 有 BOM、UTF-16LE 输入可读并转写 UTF-8、GBK(936) 输入可读并转写 UTF-8（内容无损）、无 `.tmp` 残留、只读属性抛出明确 IOException 且 Apply 失败后 HOSTS 字节不变 |
| A06 | 备份失败必须中止 | PASS：备份目录不可用时 `Success=False`、`RequiresElevation=False`，消息含「备份 HOSTS 失败，已中止修改」，HOSTS 内容保持原样 |
| A07 | 备份保留/恢复 | PASS：5 次 Apply → 5 个备份；`BackupRetention=2` → 2 个备份（+2 个 meta）；恢复内容一致且恢复前自动新增 1 个备份 |
| A08 | DNS 刷新三模式 + 未知模式 | PASS：auto=Success(ApiOk=True,IpconfigOk=True)、api=Success(仅 API)、ipconfig=Success；未知模式按 auto 处理；非管理员下 `ipconfig /flushdns` 亦成功 |
| A09 | JSON 原子写/损坏恢复/自动修复 | PASS：连续保存无 `.tmp` 残留；损坏 data.json 有警告且保留 1 份 `*.corrupt-*.json`；重复域名去重、非法/空域名禁用、未知分组回退 custom、非法 IpMode 回退 auto；620 条操作裁剪为 500；设置/主题损坏回退默认值；首次运行无警告且自动补齐 5 个内置分组 |
| A10 | 路径含空格/中文 | PASS：HOSTS/备份目录/数据目录均含中文与空格时 Apply 成功、备份生成、data.json 落盘可回读 |
| A11 | 超长域名/URL/IDN/非法输入 | PASS：253 字符域名合法写入、254 字符被拒并写为 `# [无效域名]`；64 字符标签、下划线、首尾连字符、空标签、IPv4/IPv6、通配符、无点域名均被拒；`https://www.Example.COM:8443/path?q=1#frag`→`www.example.com`；`//example.com:80/x`、`user:pass@`、`example.com:8080` 正确；`例子.测试`→`xn--fsqu00a.xn--0zwm56d` |
| A12 | 块外冲突检测 | PASS：识别块外映射 4 条（含行内注释、一行多域名），整行注释与托管块内不计入 |
| A13 | AppState 自动应用链路 | PASS：新增→写 HOSTS（0.0.0.0 + 127.0.0.1）+ data.json + 操作记录 + 备份；重复新增只更新、HOSTS 无重复行/无重复块；禁用→注释、启用→恢复；分组暂停→注释但规则 `Enabled` 不变、`EnabledRuleCount=0`；恢复分组→恢复；编辑为已存在域名→返回 null 并记录错误、目标规则不被改写；删除→托管块移除且用户行保留；手动模式→只落盘不写 HOSTS，显式 ApplyHosts 后写入；模拟重启后规则/启用状态保留；共 9 个自动备份、18 条操作记录 |
| A14 | 模板与备注边界 | PASS（含风险记录）：备注中的 CRLF 被清洗为空格、`#` 被替换为全角；**自定义 BlockTemplate 中的 CRLF 不会被清洗**，可向托管块注入额外 HOSTS 行（实测注入成功，见 P2-3） |
| A15 | data 中存在 null 规则时 Apply | **FAIL**：`HostsManager.Apply` 抛 `NullReferenceException`（见 P2-1） |
| A16 | data.json 中 `[null]` 的清理 | **FAIL**：`DataService.LoadData` 保留 null 元素，未清理（见 P2-1） |

补充实验（文件替换/共享冲突）：

- 外部句柄以 `FileShare.ReadWrite`（不允许 delete 共享）占用目标 HOSTS 时，`WriteHosts` 仍成功并写入新内容 → 证明 `File.Replace` 失败后 `File.Copy` 回退路径有效，且无临时文件残留。
- 外部句柄以 `FileShare.Read` 占用时，`WriteHosts` 抛 `IOException`（共享冲突），目标内容不变、无临时文件残留。
- ACL 拒绝场景：拒绝写/删除权限时 Apply 失败安全（HOSTS 未被修改、`RequiresElevation=True`）。受宿主环境 ACL 语义影响，无法进一步区分 ReplaceFile/CopyFile 的具体失败点（见 P2-4 说明）。

### 2.5 预览版 exe 启动与界面冒烟

被测：`src\IPBanManager\bin\Preview\IPBanManager.Preview.exe`（asInvoker，13:43:24），`--data-dir` 指向 `%TEMP%\IPBanManagerQA\…` 临时目录。

| 用例 | 结果 |
| --- | --- |
| `--page dashboard/rules/groups/theme/advanced/logs` 六个页面逐个启动 | 全部显示主窗口（标题「净域 - 本地 HOSTS 封禁工具」），进程存活，**日志 0 条 ERROR/未处理异常** |
| 单实例 | 同数据目录启动第二个实例：进程存活但显示「净域」提示框，未出现第二个主界面（被单实例 Mutex 拦截） |
| 关闭到托盘（默认 `TrayEnabled/CloseToTray=true`） | `CloseMainWindow()` 后进程仍存活且 `MainWindowHandle=0`（已隐藏到托盘），未退出 |
| 正常关闭 + `App.OnExit` 保存（预置 `TrayEnabled=false, CloseToTray=false`） | 关闭后进程退出、**退出码 0**；`data.json`/`theme.json`/`settings.json` 均写入；预置规则 `qa-persist.com` 仍在；再次启动仍能读取该规则 |
| 主题深色+自定义（IsDark/#FF7D9C/圆角 18/透明度 0.9） | 启动无异常，退出后 theme.json 为准确保存值 |
| 主题非法值（`AccentColor="not-a-color"`、`CornerRadius=99`、`CardOpacity=-5`） | 启动无异常；CardOpacity 夹紧 0.55、CornerRadius 夹紧 24；非法强调色运行时回退默认色（theme.json 原值保留，见 P2-7 备注） |
| data.json 损坏启动 | 窗口正常出现；日志 1 条 ERROR「读取 data.json 失败」（符合预期）；生成 1 份损坏现场文件；退出后 data.json 重新可解析（11 条预览规则），自愈成功 |
| `--data-dir` 含中文与空格（`…\数据 目录 含空格`） | 参数解析正常，data/theme/logs 正确落盘，无异常 |

### 2.6 真实系统 HOSTS 未被修改（硬校验）

| 时点 | SHA256 | mtime |
| --- | --- | --- |
| 全部实验前 | `01DA8B13564B80E4EF488AF8075052BEE13A6832276D39090DE6A5EFC48869DB` | 2026-11-13 13:03:00 |
| 全部实验后 | `01DA8B13564B80E4EF488AF8075052BEE13A6832276D39090DE6A5EFC48869DB` | 2026-11-13 13:03:00 |

结论：**一致，真实 HOSTS 未被任何测试修改**。

---

## 3. 七条验收标准逐条结论

| # | 验收标准 | 结论 | 证据与说明 |
| --- | --- | --- | --- |
| 1 | 添加规则 → 写入 HOSTS | **通过** | 探针 A13：`AppState.AddRules` 后临时 HOSTS 出现 `0.0.0.0` 与 `127.0.0.1` 两行、data.json 持久化、备份生成（`AppState.cs:246-273` → `HostsManager.Apply`）。预览版六页面启动无异常，添加按钮 `Click="AddRule_Click"/"Add_Click"` 均已接线（`DashboardView.xaml:27`、`RulesView.xaml:33`）。对话框级 UI 自动化未覆盖（见第 5 节）。 |
| 2 | 删除/禁用 → 移除或注释 | **通过** | 探针 A02/A13：禁用写 `# 0.0.0.0 x`、分组暂停同样注释且规则 `Enabled` 不变；删除后托管块整块移除、块外用户行保留（`HostsManager.cs:194-238`、`AppState.cs:300-343`）。 |
| 3 | 每次保存先备份 | **通过** | 探针 A07：每次 Apply 新增 1 个 `hosts_yyyyMMdd_HHmmss_fff.bak`；A06：备份失败时**中止写入**，HOSTS 内容不变，消息明确「已中止修改」（`HostsManager.cs:78-96`）；恢复前再次自动备份（`BackupManager.cs:113-129`）。注意：HOSTS 文件本身不存在时无内容可备份、直接创建（合理）。 |
| 4 | 保存后刷新 DNS | **通过** | 探针 A08：`auto`/`api`/`ipconfig` 均实测成功（非管理员下亦成功）；写入后调用 `DnsService.Flush`，成功/失败均有明确消息（`HostsManager.cs:126-136`）；失败时提示「HOSTS 已保存，但 DNS 刷新失败，请手动刷新」；高级设置三种模式可持久化（`AppSettings.cs:83-85`、`AdvancedView.xaml` Tag=auto/api/ipconfig）。 |
| 5 | 主题自定义 | **通过** | 全量校验 XAML 资源键：所有 `DynamicResource/StaticResource` 引用均可解析（缺失 0 个）；`ThemeManager.Apply` 覆盖 31 个颜色/圆角/明暗键；预览版深色、自定义强调色/圆角/透明度、非法值均启动无异常；主题保存/加载/导入导出逻辑见 `ThemeView.xaml.cs`、`DataService.SaveTheme/LoadTheme`（CoreTests 亦覆盖序列化）。说明：未做像素级视觉比对。 |
| 6 | 关闭后规则不丢失 | **通过** | 预览版实测：正常关闭（非托盘）退出码 0，`App.OnExit`（`App.xaml.cs:105-129`）写出 data.json/theme.json/settings.json，预置规则保留，重启后仍读取；关闭到托盘时进程驻留，且每次增删改均已即时 `PersistData`（`AppState.cs:596-607` 及各调用点）；「重启不丢规则」探针 A13 亦验证。 |
| 7 | 非管理员提示 | **通过（静态 + 有限动态）** | 正式版 PE 清单 `requireAdministrator`（2.3 已提取）；`AdminService.IsElevated`、`MainWindow.ShowAdminWarning` 横幅+提权重启（`MainWindow.xaml.cs:360-401`）、`HostsApplyResult.RequiresElevation` → 提权确认框（`App.xaml.cs:155-175`、`HostsManager.cs:106-109`）代码齐全；ACL 拒绝写场景实测返回 `RequiresElevation=True` 且 HOSTS 未被改。**备注**：正式版因强制提权，进程内「非管理员」路径几乎不可达（实际提示由 UAC 承担）；预览版 `IsAdmin` 恒为 true（`App.xaml.cs:58`），故该 UI 路径无法在现有构建中实际触发——建议在文档中说明「非管理员提示由 UAC + 代码兜底共同保证」。 |

---

## 4. 问题清单

### P0（阻断发布，数据/用户 HOSTS 损坏级）

**无。**

### P1（阻断「直接分发当前发布包」，需处理）

| 编号 | 问题 | 位置/证据 | 复现步骤 | 影响 | 建议 |
| --- | --- | --- | --- | --- | --- |
| P1-1 | **发布 zip 与 dist 目录二进制不一致（陈旧发布包）** | `dist\净域-v1.0.0-win-x64.zip`（mtime 13:41:00）内 `IPBanManager.exe` SHA256 = `BE747E100281144176355F1FA0701CECA3B36540BF8D84BC7767F696E6722D2B`，其内 `版本信息.txt` 构建时间 13:40:59；而 `dist\净域\IPBanManager.exe`（13:43:34）SHA256 = `6AF8F7CD16B60BACCA84D2CEB989F8EDBA7BBC4230063288E32187347270CEEC`。zip 内 `用户手册.md` 19805 B，`dist` 目录为 20802 B。 | 解压 zip → `Get-FileHash` 两处 exe 对比，值不同；对比两处 `版本信息.txt` 的构建时间/SHA。 | 若直接分发当前 zip，用户拿到的是 13:40:59 的旧二进制，不含 13:41:59 `HostsManager`（`File.Replace` 原子写）与 13:42:59 `AppState`（备份目录回退等）的修复；发布说明与实际二进制不一致，破坏可追溯性。 | 最终发布前重跑完整 `build.ps1`（**不要** `-SkipTests -NoZip`），并校验三处一致：zip 内 exe SHA = `dist\净域\IPBanManager.exe` SHA = `版本信息.txt` 记录 SHA；同时核对 zip 内 `用户手册.md` 为 `docs\用户手册.md` 当前版本。 |

> 说明：该问题属发布工程/产物一致性，不是代码缺陷；当前 `dist` 目录中的 exe 与工作区源码快照一致（构建于 13:43:34，晚于各源码最后修改时间）。若 Lead 后续会做最终重建，请在重建后按上表复核。

### P2（不阻断发布，建议后续修复）

| 编号 | 问题 | 位置 | 复现/证据 | 影响 | 建议 |
| --- | --- | --- | --- | --- | --- |
| P2-1 | **数据中的 null 规则未清理，导致 Apply 抛 NullReferenceException**（快照 A 上探针 A15/A16 实测失败） | `HostsManager.cs:71-76`（`managedDomains` 循环未判空）配合 `DataService.cs:159-247`（`NormalizeRules` 对 null `continue` 但不移除） | 快照 A：data.json 写入 `{"Rules":[null],…}` → `DataService.LoadData` 后集合仍含 null；再调用 `HostsManager.Apply`/界面应用 → `NullReferenceException`（`AppState.ApplyHosts` 捕获后提示「应用 HOSTS 失败：未将对象引用设置到对象的实例」） | 数据文件被外部破坏/手工编辑后，应用 HOSTS 持续失败；自动修复未覆盖该形态。概率低（正常流程不会产生 null），但恢复体验差 | **工作区 13:57 已修复中**：`DataService.cs:40,55-…` 新增 `RemoveNullItems`（含 warning「移除空记录」）；建议最终构建后按 A15/A16 复测。仍建议 `Apply` 的 managedDomains 循环加判空作纵深防御（与 `BuildManagedBlock` 一致） |
| P2-2 | **ANSI/GBK 编码的 HOSTS 被整体重写为 UTF-8** | `HostsManager.cs:292-298`（读取自动识别）、`300-363`（始终按 UTF-8 写）；`JsonHelper.cs:88-108`（解码回退 `Encoding.Default`） | 在 ANSI=936 环境用 GBK 写入含中文注释的 HOSTS → Apply 后文件变为 UTF-8；内容以 UTF-8 读回无损，但按 ANSI 读取的旧工具会看到乱码；若原字节恰为合法 UTF-8 而实为 GBK，则存在误解码可能 | 只影响块外非 ASCII 用户内容（中文注释等），Windows 解析器通常容忍 UTF-8；属兼容性风险，非数据破坏（内容不丢失） | 记录原编码并在写回时保持；或在高级设置中提供「保持原编码/UTF-8」选项（当前仅有 BOM 开关）。13:57 工作区 `AppSettings` 无相关新选项，仍建议后续版本处理 |
| P2-3 | **自定义 BlockTemplate 未清洗 CR/LF，可向托管块注入额外 HOSTS 行**（快照 A 上实测注入成功） | 快照 A：`HostsManager.cs:448-454`（`RenderTemplate` 仅替换占位符）；`AdvancedView.xaml.cs:266-273`（保存时只补 `{domain}`，不校验换行） | 快照 A：高级设置把模板改为 `0.0.0.0 {domain}\r\n1.2.3.4 injected.com` → Apply 后托管块内出现额外的 `1.2.3.4 injected.com`（实测注入成功） | 模板属管理员自配置，风险低；但导入/被篡改的 settings.json 可借此写入额外映射 | **工作区 13:57 已修复**：`HostsManager.cs:450-451` 已把 CR/LF/TAB 替换为空格；最终构建后按 A14 复测即可关闭 |
| P2-4 | **`File.Replace` 仅捕获 `IOException`，其他异常不会回退 `File.Copy`** | `HostsManager.cs:322-336`（13:57 工作区仍为 `catch (IOException)`）；`BackupManager.cs:127-153` 恢复流程同样只 catch IOException | 代码审查；ACL 拒绝场景下观察到 `WriteHosts` 直接抛 `UnauthorizedAccessException`（`File.Replace` 需要目标 DELETE 权限；`catch (IOException)` 不会命中，故不会走 `File.Copy` 回退）。受宿主环境 ACL 语义限制，未能稳定构造「有写无删」场景以证明 Copy 本可成功，故列为待确认风险 | 目标 HOSTS 由管理员全控，实际系统几乎不会触发；若触发会被 Apply 映射为 `RequiresElevation=true` 并提示「请以管理员身份」 | `catch (Exception)`（仅对 Replace 尝试）后统一回退 `File.Copy`，再在 Copy 失败时抛原始异常；`BackupManager.RestoreBackup` 同步处理 |
| P2-5 | **数据/设置保存失败被吞掉，调用方仍提示成功**（快照 A） | 快照 A：`AppState.cs:596-607`（`PersistData` catch 仅记日志与 `LastOperationError`）；13:57 工作区已新增 `PersistenceFailed` 事件与 UI Toast（`AppState.cs:56,586-632`、`MainWindow.xaml.cs:135-138`） | 代码审查；若磁盘满/数据目录被杀软锁定，`data.json` 写入失败时界面仍走成功 Toast（13:57 已增加失败 Toast） | 极端环境下可能出现「提示已保存、重启后规则丢失」，与验收标准 6 存在理论冲突 | **部分修复中**：已能把持久化失败提示给用户；建议最终复测「数据目录只读/磁盘满」场景，并考虑在关键变更 Toast 上区分「已写入 HOSTS 但数据未保存」 |
| P2-6 | **未使用的资源键 / 仓库残留临时文件** | `ThemeManager.cs:76-83` 设置 `WindowBorderBrush`/`ShadowColor`/`IsDarkTheme`，但全仓库 XAML 无引用（缺失 0 个是好事，多出的属死资源，见 `Styles.xaml:32,34`）；`src\IPBanManager\MainWindow.recovered.xaml`（16896 B，未加入 `IPBanManager.csproj` 的 Page 列表，不参与编译/发布）；根目录 `_capture.ps1`/`_themetest.ps1` 等调试脚本与 `_buildtest\` | 静态扫描 + `IPBanManager.csproj:47-100` 页面白名单核对 | 不影响发布产物与功能；增加维护困惑与误改风险 | 清理死资源与残留文件（或将调试脚本移入 `tools/`） |
| P2-7 | 主题非法颜色值不做持久化校验 | `ThemeSettings.Normalize`（`ThemeSettings.cs:34-45`）只兜底空白值，不校验颜色格式；`ColorUtil.Parse`（`ThemeManager.cs:106-123`）运行时回退 | 预置 `AccentColor="not-a-color"` 的 theme.json → 启动/应用无异常（回退默认色），但退出后 theme.json 仍保留非法值 | 每次启动都走回退路径，无功能影响；仅数据自洁问题 | `Normalize` 中用 `ColorUtil.Parse` 校验并回写合法值 |

> 另记录一条**非缺陷观察**：`HostsManager.Apply` 在写入前就创建备份，因此「写入失败」时也会留下一个备份文件；这符合「每次保存先备份」的验收要求，不影响正确性，但会略微增加备份数量。

---

## 5. 未覆盖项与限制（诚实声明）

1. **真实系统 HOSTS 写入未实测**：按 QA 边界要求，全部写入实验使用临时 HOSTS；本机非管理员且禁止修改真实 HOSTS。仅对真实 HOSTS 做了只读读取与前后 SHA256 比对（未被修改）。
2. **正式版 exe 未实际双击启动/UAC 交互**：`requireAdministrator` 在无交互环境下会触发 UAC 同意框，无法自动化完成；已通过 PE 清单提取 + 提权分支代码审查 + ACL 拒绝场景验证 `RequiresElevation` 映射。正式版非管理员 UI 路径（横幅/提权按钮）因此未能动态触发。
3. **对话框 UI 自动化未完成**：尝试用 UI Automation 点击「添加规则」打开 `RuleEditDialog` 未成功（自动化会话中 UIA Invoke 对自定义模板按钮可触发普通按钮如「立即备份」，但模态对话框定位不稳定），因此未做「UI 点击 → 对话框填写 → 写 HOSTS」的完整点击链路。已做的替代覆盖：六个页面级启动冒烟（0 异常）、所有对话框 XAML 资源键静态校验（缺失 0）、`AppState` 层添加/编辑/禁用/删除/分组暂停/手动应用全链路探针实测。
4. **开机自启未实测**：按边界要求未写 HKCU Run；确认值名 `IPBanManager` 原本不存在且未创建。仅静态审查 `AutoStartService`（HKCU、`--minimized`、删除值、失败回退）与 `AppState.SaveSettings` 的清理逻辑。
5. **备份恢复/清空日志的 UI 点击路径未自动化**：服务层 `BackupManager.RestoreBackup`（含恢复前自动备份）已在探针实测，UI 按钮逻辑经静态审查。
6. **未做**：长时间运行/内存稳定性、海量规则（数千条）性能、多显示器/DPI 视觉像素比对、杀毒软件拦截 HOSTS 写入的真实场景、Windows 10 实机（仅 Win11 22631）。
7. **编码兼容结论依赖系统 ANSI=936**；其他代码页（如 950/1252）下的 GBK/ANSI HOSTS 行为未验证。
8. 预览版与正式版代码路径差异：预览版强制 `IsAdmin=true`、HOSTS 指向临时文件（`App.xaml.cs:58,233-324`），因此预览版不能验证正式版的权限与真实 HOSTS 行为。

---

## 6. 最终结论

- **代码质量**：未发现 P0（数据丢失/用户 HOSTS 损坏/备份缺失/非管理员误写/构建失败均未命中）。核心链路（托管块合并、块外用户行保留、先备份后写入、备份失败中止、空规则清块、禁用/分组暂停注释、DNS 三模式、JSON 原子写与损坏自愈、中文/空格路径、超长域名/URL/IDN）实测符合预期。
- **发布工程**：存在 **1 个 P1：当前 `dist\净域-v1.0.0-win-x64.zip` 内二进制陈旧**，与 `dist\净域\IPBanManager.exe` 及发布说明不一致。**修复方式：最终重建并核对 zip 内 exe/版本信息/手册三处一致。** 该问题不在代码，只需重建发布包。
- **P2**：null 规则健壮性、ANSI→UTF-8 编码保持、模板换行清洗、`File.Replace` 回退范围、保存失败提示、仓库清理等，建议后续版本修复，不阻断本次发布。其中 P2-1/P2-3/P2-5 在 QA 收尾时的工作区中已处于修复状态，需在最终构建上复测确认。
- **总体结论：快照 A 的代码本身可发布（无 P0/P1 代码缺陷）；在完成 P1 发布包重建与一致性复核后，本版本可正式发布。**

### 6.1 最终发布前 QA 复核清单（因源码在报告后仍在变更，必须执行）

1. 冻结源码后重跑 `powershell -ExecutionPolicy Bypass -File test.ps1`，要求仍为 12/12、退出码 0。
2. 重跑**完整** `powershell -ExecutionPolicy Bypass -File build.ps1`（不带 `-SkipTests`、不带 `-NoZip`），确认退出码 0。
3. 复核 `dist\净域\IPBanManager.exe` RT_MANIFEST 仍为 `requireAdministrator`（本报告 2.3 的提取脚本可直接复用）。
4. 复核三处一致：zip 内 `IPBanManager.exe` 的 SHA256 = `dist\净域\IPBanManager.exe` 的 SHA256 = `dist\净域\版本信息.txt` 中记录的 SHA256；并确认 zip 内 `用户手册.md` 与 `docs\用户手册.md` 大小/内容一致（消除 P1-1）。
5. 复核 13:57 的工作区修复：A15/A16（null 规则）、A14（模板换行）、P2-5（持久化失败提示）用本报告探针可在 `%TEMP%` 复跑；如需，QA 可提供探针源码。
6. 再次校验真实 HOSTS SHA256 未被测试修改（本次为 `01DA8B13…`；正式版写真实 HOSTS 的行为仍建议由发布者在管理员环境按用户手册验证一次）。

---

*本报告由独立 QA 生成；复现材料（探针源码/输出、预览测试脚本/输出、zip 解压比对）位于 `%TEMP%\IPBanManagerQA\`，未写入仓库其他位置。*

---

## 7. 最终发布复核（Lead，2026-11-13 14:02）

针对本报告 P1-1 与 §6.1 复核清单，在最终冻结源码上重新执行：

| 复核项 | 结果 |
| --- | --- |
| `test.ps1` / `build.ps1` 内核心测试 | 12/12 通过，失败 0；完整 `build.ps1` 退出码 0 |
| 正式版 exe SHA256 | `F0776E04B76C79C42831E3437CE68430D1F5E499D26DDDE4A20C128AF5D9D886` |
| `dist\净域-v1.0.0-win-x64.zip` 内 exe SHA256 | 与 dist 目录 exe 完全一致（`F0776E04…`，P1-1 已关闭） |
| zip 内 `版本信息.txt` 记录 SHA256 | 与上述一致，构建时间 2026-11-13 14:03:10 |
| zip 内 `用户手册.md` | 21,004 B，与 `docs\用户手册.md` 一致（旧版 19,805 B 已消除） |
| 正式版 RT_MANIFEST | 直接提取 PE 资源确认 `requireAdministrator` |
| 预览版 RT_MANIFEST | `asInvoker`；`dist\净域-界面预览版` 与 zip 已同步生成 |
| 最终预览版启动冒烟 | 主窗口正常显示，日志 0 条 ERROR |
| P2 修复落点 | null 数据清理、模板 CR/LF 清洗、`File.Replace` 全异常回退、`PersistenceFailed` Toast、主题非法颜色归一化均已进入最终构建；对应断言已并入核心测试 |
| 真实 HOSTS | SHA256 `01DA8B13…` 前后一致，未被修改 |
| HKCU Run | 值名 `IPBanManager` 不存在，未被测试写入 |

**最终结论：P1-1 已关闭，发布包与源码/版本信息/用户手册三处一致；正式版可发布。**