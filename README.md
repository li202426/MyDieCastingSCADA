# MyDieCastingSCADA

MyDieCastingSCADA 是一个基于 C#、.NET 8 和 WPF 开发的压铸设备 SCADA 上位机学习与实践项目。系统通过 S7 协议与西门子 PLC 或本机仿真环境通信，用于展示设备状态、工艺参数、报警、生产周期和历史数据，并支持 SQLite 本地存储及可选的 SQL Server 同步。

## 核心功能

- 用户首次运行注册、登录和角色权限管理
- 压铸设备与产线状态展示
- 西门子 PLC 连接、周期采集和断线重连
- 压力、温度、冷却流量和工艺参数读取
- 工艺参数范围检查、受控写入和写后回读
- 实时趋势和历史数据查询
- 报警产生、恢复、确认和历史记录
- 生产周期识别及周期采样关联
- SQLite 本地数据持久化
- SQL Server 可选远程存储
- Outbox 待同步队列、失败记录和人工重试
- 操作日志、同步状态和模具信息管理

## 技术栈

| 类别 | 技术 |
| --- | --- |
| 开发语言 | C# |
| 运行平台 | .NET 8 Windows |
| 桌面界面 | WPF、XAML |
| MVVM 支持 | CommunityToolkit.Mvvm |
| PLC 通信 | S7netplus |
| 本地数据库 | SQLite、Microsoft.Data.Sqlite |
| 远程数据库 | SQL Server、Microsoft.Data.SqlClient |
| 图表 | LiveCharts.Wpf |
| 配置与序列化 | JSON、System.Text.Json、Newtonsoft.Json |

## 解决方案结构

```text
MyDieCastingSCADA.sln
├─ MyDieCastingSCADA       WPF窗口、页面、导航和应用运行服务
├─ MySCADA.Models          PLC快照、报警、用户、配置和同步模型
├─ MySCADA.Services        PLC通信、配置、数据库、用户和安全服务
└─ MySCADA.Business        运行数据与压铸工艺业务逻辑
```

主要职责如下：

- `MyDieCastingSCADA`：登录、主窗口、设备窗口、页面切换、实时刷新、报警、周期、日志和同步状态。
- `MySCADA.Models`：跨层传递的数据对象和配置模型。
- `MySCADA.Services`：PLC、SQLite、SQL Server、用户账户、授权和参数写入安全。
- `MySCADA.Business`：面向压铸运行数据和工艺参数的业务处理。

## 运行链路

### 实时采集

```text
JSON设备配置
    → PlcService建立S7连接
    → 周期读取PLC数据
    → 生成PlcRuntimeSnapshot
    → 报警与生产周期处理
    → SQLite本地保存
    → WPF页面和趋势刷新
```

### 参数写入

```text
页面输入
    → 格式与数值范围检查
    → 用户权限检查
    → PLC连接和设备状态检查
    → 写入PLC
    → 从相同地址回读验证
    → 记录操作日志
```

### 数据同步

```text
业务数据写入SQLite
    → 创建Outbox待同步任务
    → SqlSyncService后台处理
    → 写入SQL Server
    → 标记成功或记录失败与重试次数
```

## 主要页面

- 登录和用户管理
- 产线与设备选择
- 设备总览
- 工艺参数
- 温度与冷却通道
- 实时趋势
- 报警记录
- 历史数据
- 模具管理
- 实时日志
- 数据库同步状态

## 运行环境

- Windows 10 或 Windows 11
- Visual Studio 2022
- .NET 8 SDK
- 可选：西门子 PLC、PLCSIM 或其他可访问的 S7 仿真环境
- 可选：SQL Server；不启用远程存储时可仅使用 SQLite

## 启动方法

1. 使用 Visual Studio 打开 `MyDieCastingSCADA.sln`。
2. 将 `MyDieCastingSCADA` 设置为启动项目。
3. 检查 `MyDieCastingSCADA/Configs` 下的设备、工艺和数据库配置。
4. 根据本机 PLC 仿真环境修改设备 IP、CPU 类型、机架、槽号和数据地址。
5. 编译并运行项目。
6. 首次运行时，登录窗口会要求创建第一个管理员账号；账号信息保存在本地 SQLite 数据库中。

## 配置说明

### PLC配置

设备和产线配置位于：

```text
MyDieCastingSCADA/Configs/
```

仓库中的 IP 地址用于本机或局域网仿真环境。使用者需要根据自己的 PLC 或仿真环境修改配置。将项目用于其他网络前，请确认地址不会指向真实生产设备。

### SQL Server配置

远程数据库配置位于：

```text
MyDieCastingSCADA/Configs/DatabaseSettings.json
```

当前配置用于本机 SQL Server 仿真和功能验证，不包含 SQL Server 用户名或密码。没有 SQL Server 环境时，可以将 `SqlServerEnabled` 设置为 `false`，程序仍可使用 SQLite 保存本地数据。

公开仓库中不要提交包含真实服务器地址、SQL Server账号、密码或生产数据库名称的连接字符串。

## 数据可靠性设计

- 采样数据优先保存到本地 SQLite，降低远程数据库不可用对现场采集的影响。
- 待同步业务通过 Outbox 记录，程序重新启动后仍可继续处理。
- 同步失败会保存错误和重试次数，达到限制后进入失败状态并支持人工重新排队。
- 运行数据、生产周期和报警通过稳定标识进行关联。

## 参数写入安全

工艺参数写入前会检查：

- 当前用户权限
- PLC连接状态
- 设备运行状态
- 参数格式与允许范围
- 写入结果和回读值

关键操作会写入操作日志。实际控制系统仍应在 PLC 侧保留最终联锁和安全保护，上位机校验不能替代 PLC 安全逻辑。

## 项目说明

该项目由早期压铸机上位机版本逐步整理并迁移为 WPF 版本，主要用于学习和实践以下内容：

- C#与WPF桌面应用开发
- WPF数据绑定与部分MVVM模式
- PLC通信及数据地址映射
- 后台采集、异步任务和UI线程更新
- 报警、生产周期和工艺参数等工业业务
- 本地数据库与远程数据库同步
- 用户权限、写入安全和操作审计

## 已知问题

- `LiveCharts.Wpf 0.9.7` 等部分旧版组件在 .NET 8 下可能产生兼容性警告。
- 运行项目需要与当前配置匹配的 PLC 或 S7 仿真环境，否则会显示通信失败并尝试重连。
- SQL Server 是可选功能，使用前需要准备目标数据库环境并检查连接配置。
- 当前项目以Windows桌面环境为目标，不支持跨平台运行。

## 隐私与安全

提交公开仓库前，请确认未包含：

- 真实生产PLC地址
- 客户名称、设备编号和工艺机密
- SQL Server用户名和密码
- 本地SQLite数据库文件
- 运行日志和用户账号数据
- `bin`、`obj`、`.vs`等生成目录

## License

本仓库当前未声明开源许可证。未经仓库所有者明确许可，不代表允许将项目用于商业生产环境。

