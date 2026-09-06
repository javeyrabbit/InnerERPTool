# ADR-004: UI 层采用 MVVM + CommunityToolkit.Mvvm + 紫罗兰主题

## Status

Accepted

## Context

Presentation 层（WPF）需要实现 HTML 原型 (`docs/gemini-code-1786199581879.html`) 的紫罗兰主题 UI，包括：

- 主菜单（可折叠深紫侧栏）
- 应收管理：客户/业务员树、工具栏、表格（sticky 表头、行展开子表）、分页、弹窗
- 账套管理：侧栏按钮、搜索栏、表格、终端风格日志
- 状态色：负差额红 `#ff4d4f`、正差额绿 `#107c41`

### 驱动决策的因素

- WPF 原生数据绑定适配 MVVM
- 需减少 `INotifyPropertyChanged`/`ICommand` 样板代码
- 主题需可维护（资源字典集中管理）
- DDD 分层要求 ViewModel 不直连 Repository，须经 Application Service

## Decision

采用 **MVVM 模式** + **CommunityToolkit.Mvvm 8.x** + **紫罗兰主题资源字典**。

| 项 | 选定 |
|---|---|
| MVVM 框架 | CommunityToolkit.Mvvm 8.x（微软官方） |
| ViewModel 基类 | `ObservableObject` |
| 命令 | `[RelayCommand]` 源生成器 |
| 属性通知 | `[ObservableProperty]` 源生成器 |
| 主题 | `Themes/VioletTheme.xaml` + `ControlTemplates.xaml` |
| 自定义控件 | `PaginationControl.xaml`、`TerminalControl.xaml` |
| ViewModel 与 AppService | ViewModel 调 Application 层 AppService，不直连 Repository |
| 对象映射 | Mapster（DTO ↔ Domain） |

### 紫罗兰主题资源键（固化）

| 资源键 | 色值 |
|---|---|
| `PrimaryBrush` | `#5448c8` |
| `PrimaryDarkBrush` | `#4237a0` |
| `MenuBackgroundBrush` | `#2c2c54` |
| `TableHeaderBackgroundBrush` | `#5448c8` |
| `RowAltBackgroundBrush` | `#f7f6fc` |
| `NegativeBrush` | `#ff4d4f` |
| `PositiveBrush` | `#107c41` |
| `TerminalBackgroundBrush` | `#1e1b2e` |
| `TerminalForegroundBrush` | `#d4d4d4` |

## Consequences

### Positive

- 源生成器大幅减少样板（`[ObservableProperty]` 替代手写 INPC）
- 微软官方维护，长期稳定
- 资源字典集中管理主题，色值与 HTML 原型一一对应
- ViewModel 可独立单元测试（不依赖 View）
- MVVM 与 DDD 应用层编排天然契合

### Negative

- CommunityToolkit.Mvvm 无内置导航/对话框服务，需自建轻量层（本系统模块少，可接受）
- 紫罗兰主题需手写 `ControlTemplates`（DataGrid 列模板、分页按钮模板）

## Alternatives Considered

### Alternative 1: Prism

- Pros：导航/模块化/对话框服务完整
- Cons：已停更；许可证敏感；对单机双模块工具过度工程化
- Why rejected：停更 + 过度

### Alternative 2: 手写 INotifyPropertyChanged

- Pros：零依赖
- Cons：样板代码海量，维护痛苦
- Why rejected：效率低

### Alternative 3: MahApps.Metro

- Pros：成熟主题框架
- Cons：改造成本高，色系与紫罗兰不符
- Why rejected：主题不匹配

---

**Decided by:** CTO — Dr. Kenji Nakamura
**Date:** 2026-08-08
**Referenced in:** TSD §1, §2.3, Architecture-Overview §6