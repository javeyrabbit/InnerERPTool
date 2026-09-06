# Technology Selection Document (TSD)

**Project:** PLErpTool (ERP 管理工具)
**Version:** v1
**Author:** CTO — Dr. Kenji Nakamura (小T)
**Date:** 2026-08-08
**Status:** Draft — Pending User Approval
**Referenced Artifacts:** HTML 紫罗兰主题原型 (`docs/gemini-code-1786199581879.html`)、ExcelProject 账套转换工具源码、Stage 3 UML Package

---

## 1. Technology Decisions Summary

| Domain | Selected Technology | Rationale | Alternatives Rejected |
|---|---|---|---|
| 应用平台 | WPF, .NET 8 (`net8.0-windows`) | 桌面端 ERP 工具，需富 UI（树/表格/分页/弹窗）；WPF 数据绑定 + XAML 适合复杂布局；.NET 8 LTS | WinForms（数据绑定弱，无法实现紫罗兰主题样式）；MAUI（过度工程化，无跨平台需求） |
| 架构模式 | DDD 四层 (Domain/Application/Infrastructure/Presentation) | 用户明确要求 DDD；四项目分层强制依赖单向向内，可测试性高，契合 Clean Architecture | 单项目文件夹分层（依赖难约束）；模块化多项目（启动成本过高） |
| UI 模式 | MVVM + CommunityToolkit.Mvvm | WPF 原生 MVVM；`ObservableObject`/`RelayCommand` 减少样板；`[ObservableProperty]` 源生成器现代化 | Caliburn.Micro（约定式，调试难）；手写 INotifyPropertyChanged（样板过多） |
| UI 主题 | 紫罗兰主题 (`#5448c8` 主色) | 沿用 HTML 原型视觉规范；资源字典集中管理 | MahApps.Metro（改造成本高，色系不符） |
| 持久化 | SQLite + EF Core 8 | 嵌入式单文件部署，零配置；EF Core LINQ + 迁移成熟，与 DDD 仓储模式契合 | 纯 Excel（关系查询/聚合/分页性能差）；Dapper（仓储样板多） |
| EF Core Provider | `Microsoft.Data.Sqlite` | EF Core 官方 SQLite 提供器，跨平台单文件 | System.Data.SQLite（维护滞后，EF Core 兼容差） |
| Excel 读写 | NPOI 2.8 | ExcelProject 已用，团队熟悉；支持公式/合并单元格/模板复制，账套转换场景必备 | EPPlus 5.0+（商业许可）；ClosedXML（合并单元格/公式能力弱） |
| 依赖注入 | `Microsoft.Extensions.DependencyInjection` | 官方 DI，与 .NET 8 生态统一；WPF 通过 `App.xaml.cs` 组合根注入 | 手写 DI 容器；Autofac（桌面端过度） |
| 日志 | Serilog + `Serilog.Sinks.File` | 结构化日志，本地滚动文件 + 控制台双 sink；与 ExcelProject 的 `log.txt` 习惯延续 | NLog（配置略繁）；`Microsoft.Extensions.Logging`（需额外配 sink） |
| MVVM 框架 | CommunityToolkit.Mvvm 8.x | 微软官方，源生成器减少样板，文档完善 | Prism（已停更，许可证敏感） |
| 对象映射 | Mapster | 高性能对象映射，DTO ↔ Domain 转换 | AutoMapper（性能较低，配置重） |
| 单元测试 | xUnit + Moq | .NET 主流测试栈；Moq 模拟接口做仓储/服务测试 | NUnit（生态略旧） |
| 数据库测试 | SQLite in-memory (`:memory:`) | EF Core 同一 Provider，测试即内存库，无需外部依赖 | LocalDB（部署依赖重，CI 环境复杂） |
| 导出 Excel | NPOI (同读写库) | 复用 ExcelProject 失败数据导出模式 (`loser_*.xlsx`) | ClosedXML（见上） |
| 数据校验 | FluentValidation | 领域命令/DTO 校验，与 DDD 应用层契合 | DataAnnotations（表达力弱，难跨层） |

---

## 2. Comparative Technology Analysis

### 2.1 持久化方案选择

| Option | Pros | Cons | TCO (24-month) | Lock-in Risk | Verdict |
|---|---|---|---|---|---|
| SQLite + EF Core | 单文件零配置；LINQ 强类型；迁移版本化；DDD 仓储契合 | 复杂并发写入弱（桌面单用户无影响） | 低（免费 + 1 人日学习） | 无 | ✅ Selected |
| 纯 Excel (NPOI) | 无数据库；主账即数据源 | 关系查询/分页/聚合性能差；并发写冲突；树状检索难 | 低 | 无 | ❌ Rejected |
| SQLite + Dapper | 轻量、性能高 | 手写 SQL，仓储样板多，无迁移工具 | 低 | 无 | ❌ Rejected |

**Weighted Scorecard:**

| Criteria | Weight | SQLite+EF Core | 纯 Excel | SQLite+Dapper |
|---|---|---|---|---|
| 查询/聚合能力 | 30% | 5 → 1.50 | 1 → 0.30 | 4 → 1.20 |
| 开发效率 | 25% | 5 → 1.25 | 3 → 0.75 | 3 → 0.75 |
| DDD 契合度 | 20% | 5 → 1.00 | 1 → 0.20 | 3 → 0.60 |
| 维护成本 | 15% | 4 → 0.60 | 2 → 0.30 | 3 → 0.45 |
| 生态成熟度 | 10% | 5 → 0.50 | 4 → 0.40 | 4 → 0.40 |
| **Total** | **100%** | **4.85** | **1.95** | **3.40** |

### 2.2 Excel 读写库选择

| Option | Pros | Cons | TCO (24-month) | Lock-in Risk | Verdict |
|---|---|---|---|---|---|
| NPOI 2.8 | ExcelProject 已用；支持公式/合并单元格/模板复制；Apache 2.0 | API 略冗长 | 低 | 无 | ✅ Selected |
| EPPlus 5.0+ | API 现代、性能好 | 商业许可证（Polyform Noncommercial 限非商用） | 中（许可费或法律风险） | 中 | ❌ Rejected |
| ClosedXML | MIT，API 友好 | 合并单元格/公式复制能力弱，账套模板受限 | 低 | 无 | ❌ Rejected |

**Weighted Scorecard:**

| Criteria | Weight | NPOI | EPPlus | ClosedXML |
|---|---|---|---|---|
| 模板/公式能力 | 35% | 5 → 1.75 | 5 → 1.75 | 2 → 0.70 |
| 团队熟悉度 | 25% | 5 → 1.25 | 2 → 0.50 | 2 → 0.50 |
| 许可证合规 | 20% | 5 → 1.00 | 2 → 0.40 | 5 → 1.00 |
| 维护活跃度 | 10% | 4 → 0.40 | 5 → 0.50 | 4 → 0.40 |
| 性能 | 10% | 3 → 0.30 | 5 → 0.50 | 4 → 0.40 |
| **Total** | **100%** | **4.70** | **3.65** | **3.00** |

### 2.3 MVVM 框架选择

| Option | Pros | Cons | Verdict |
|---|---|---|---|
| CommunityToolkit.Mvvm | 微软官方；源生成器；`[ObservableProperty]`/`[RelayCommand]` 极简 | 无导航/对话框服务（需自建轻量层） | ✅ Selected |
| Prism | 导航/模块化完整 | 已停更；许可证敏感；过度工程化 | ❌ Rejected |
| 手写 INPC | 零依赖 | 样板代码海量，维护痛苦 | ❌ Rejected |

---

## 3. Vendor Assessment

本工具为本地桌面端，无云服务依赖，无 SaaS 供应商。所有组件为开源/官方 NuGet 包：

| Vendor | Product | Support Model | SLA | Exit Cost | Financial Stability | Supply Chain Risk | Contractual Lock-in |
|---|---|---|---|---|---|---|---|
| Microsoft | .NET 8 / WPF / EF Core / CommunityToolkit.Mvvm | 官方 + 社区 | N/A (本地) | 无 | 强 | 无 | 无 |
| NPOI Team | NPOI 2.8 | 社区 (GitHub) | 无 SLA | 无 | 稳定 | 无已知 CVE | 无 |
| Serilog Team | Serilog | 社区 | 无 SLA | 无 | 稳定 | 无 | 无 |

---

## 4. Open-Source Dependency Assessment

| Dependency | License | Maintenance Status | Known Vulnerabilities | Alternative |
|---|---|---|---|---|
| Microsoft.Data.Sqlite | Apache 2.0 | Active (微软维护) | 0 | System.Data.SQLite |
| Microsoft.EntityFrameworkCore.Sqlite | Apache 2.0 | Active | 0 | Dapper |
| NPOI | Apache 2.0 | Active | 0 | EPPlus / ClosedXML |
| Serilog | Apache 2.0 | Active | 0 | NLog |
| CommunityToolkit.Mvvm | MIT | Active (微软) | 0 | Prism |
| Mapster | MIT | Active | 0 | AutoMapper |
| FluentValidation | Apache 2.0 | Active | 0 | DataAnnotations |
| xUnit | Apache 2.0 | Active | 0 | NUnit |
| Moq | BSD-3 | Active | 0 | NSubstitute |

---

## 5. CI/CD Technology Stack

桌面端单机工具，CI/CD 为轻量本地构建 + 可选 GitHub Actions：

| Component | Technology | Notes |
|---|---|---|
| 构建 | `dotnet build` / Visual Studio | .NET 8 SDK |
| 单元测试 CI | `dotnet test` | xUnit，可选 GitHub Actions |
| 代码分析 | Roslyn Analyzer + `dotnet format` | 启用 `Nullable enable` 静态检查 |
| 打包 | 单文件自包含发布 (`PublishSingleFile`) | `dotnet publish -r win-x64 --self-contained` |
| 版本号 | SemVer，`AssemblyVersion` | 见 ADR-001 |
| 离线分发 | ZIP / 安装包 | 无应用商店依赖 |

---

## 6. Test Technology Stack

| Layer | Technology | Notes |
|---|---|---|
| 领域层单元测试 | xUnit + Moq | 纯领域逻辑，无依赖 |
| 应用层单元测试 | xUnit + Moq | Mock 仓储/服务 |
| 基础设施层集成测试 | xUnit + EF Core SQLite in-memory | `:memory:` 内存库，测试仓储实现 |
| Excel 导入/导出测试 | xUnit + NPOI + 临时 xlsx | 用 `docs/` 下样本文件 |
| UI (ViewModel) 测试 | xUnit + CommunityToolkit.Mvvm | 测试 ViewModel 状态机 |
| UI (View) 手测 | 人工 | WPF View 不做自动化 UI 测试（ROI 低） |

---

## 7. Migration Risk Matrices

| Technology | Migration Trigger | Migration Cost | Rollback Plan |
|---|---|---|---|
| SQLite → PostgreSQL/SQLServer | 多用户/网络化需求出现 | 中（EF Core 切 Provider + 迁移） | EF Core 迁移可双向；保留 SQLite 快照 |
| NPOI → 其他 Excel 库 | NPOI 停更/严重 CVE | 中（封装于基础设施层，接口隔离） | 仓储/服务接口不变，替换实现 |
| CommunityToolkit.Mvvm → 其他 | 框架停更 | 低（ViewModel 接口稳定） | 替换 base 类，保持 View 不变 |

---

## 8. Technology Recommendations

### 8.1 Recommended

| Technology | Success Criteria | Failure Criteria |
|---|---|---|
| SQLite + EF Core | 应收查询 <200ms（单机，万级数据）；迁移无冲突 | 写入丢失/查询超 2s |
| NPOI 账套转换 | 转换正确率 100%（公式/合并单元格无损）；耗时 <30s/万行 | 公式丢失/合并单元格错位 |
| DDD 四层分层 | 领域层零基础设施依赖；单元测试覆盖率 ≥75% | 领域层引用 EF/NPOI（P1） |

### 8.2 Not Recommended

| Technology | Reason | Revisit Condition |
|---|---|---|
| WinForms | 数据绑定弱，无法实现紫罗兰主题 | 不再考虑 |
| EPPlus 5.0+ | 商业许可证风险 | 除非确认仅非商用且接受 Polyform |
| Prism | 已停更，许可证敏感 | 不再考虑 |

---

## 9. Technology Radar

| Technology | Quadrant | Rationale | Action Required |
|---|---|---|---|
| WPF + .NET 8 | Adopt | 桌面端主力 | 当前项目采用 |
| EF Core + SQLite | Adopt | 持久化主力 | 当前项目采用 |
| NPOI 2.8 | Adopt | Excel 读写主力 | 当前项目采用 |
| CommunityToolkit.Mvvm | Adopt | MVVM 主力 | 当前项目采用 |
| DDD 分层 | Trial | 团队首次完整 DDD 实践 | Stage 5 验证落地效果 |
| MAUI | Assess | 未来跨平台可能 | 暂无需求 |
| Avalonia | Assess | 未来跨平台 WPF 替代 | 暂无需求 |

---

**Approved by CTO (Dr. Kenji Nakamura) on 2026-08-08**
**CIO Security Sign-off: Pending** (ADR-002/003 涉及本地数据与文件读写安全)
**Locked at Stage 3 gate approval — not revisable in Stage 4+.**
**Any deviation from technology selections requires a new ADR and constitutes Stage 3 re-entry.**