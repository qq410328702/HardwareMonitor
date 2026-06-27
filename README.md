# Hardware Monitor

一个轻量的 Windows 硬件监控工具，基于 WPF、LibreHardwareMonitor 和 LiveCharts2 构建。它提供实时温度、使用率、功耗、磁盘 SMART/寿命、网络、进程、历史记录和告警能力，并支持悬浮窗与系统托盘常驻。

当前正式版：**v1.0.6**

[下载最新版](https://github.com/qq410328702/HardwareMonitor/releases/latest)

## 主要功能

### 硬件概览

- CPU / GPU 温度、使用率、功耗实时监控
- 内存使用率、已用/总量展示
- GPU 显存占用进度条
- CPU、GPU、内存实时趋势图
- 分组首页布局：`硬件概览`、`存储设备`、`运行与工具`

### 存储设备

- 每块硬盘独立显示为一张卡片
- 显示盘符、总线类型、介质类型、温度、实时读取速度、实时写入速度
- 支持 SSD / NVMe SMART 寿命信息：
  - 剩余寿命、已用寿命、可用备用空间
  - 累计读取/写入
  - 通电小时、通电次数、异常断电次数
  - 介质错误、错误日志、过热时间
- SMART 不可读时会显示清晰降级提示，不影响其它监控数据

### 运行与工具

- 网络上传/下载速度与累计流量
- 进程监控和排序
- 历史数据记录、查询、CSV 导出
- 告警规则配置和系统托盘气泡提醒
- 布局设置：可显示/隐藏 CPU、GPU、内存、单块硬盘、网络、进程、图表、历史、告警等卡片

### 使用体验

- 悬浮窗模式：置顶展示核心数据，支持拖拽移动
- 系统托盘：显示/隐藏主窗口、打开悬浮窗、开机自启、退出
- 多主题切换：深色、浅色、赛博朋克、海洋
- 自带应用图标，已应用到 exe、主窗口、悬浮窗和托盘
- 普通权限可运行；部分温度、功耗和 SMART 可靠性计数可能需要管理员权限

## 截图

<img width="320" height="210" alt="Hardware Monitor mini window" src="https://github.com/user-attachments/assets/69061aeb-5775-4684-97f3-d5faed188864" />
<img width="980" height="720" alt="Hardware Monitor main window" src="https://github.com/user-attachments/assets/7b6571b3-c04e-4f85-838e-83ecfe05eb4a" />

## 下载

前往 [Releases](https://github.com/qq410328702/HardwareMonitor/releases/latest) 下载最新正式版。

当前发布包：

- `HardwareMonitor-v1.0.6-win-x64.zip`

解压后运行 `HardwareMonitor.exe` 即可。发布包为 Windows x64 自包含版本，无需额外安装 .NET 运行时。

## 技术栈

| 组件 | 说明 |
|------|------|
| .NET 8 WPF | 桌面 UI 框架 |
| LibreHardwareMonitor | CPU / GPU / 内存 / 磁盘传感器读取 |
| LiveCharts2 + SkiaSharp | 实时趋势图表 |
| Microsoft.Data.Sqlite | 历史数据本地存储 |
| Windows Forms NotifyIcon | 系统托盘 |
| MVVM + partial 拆分 | 窗口和 ViewModel 按功能组织 |

## 项目结构

```text
HardwareMonitor/
├── Controls/                 # 自定义控件，例如 ArcGauge 圆弧仪表盘
├── Converters/               # 温度、格式化、磁盘、告警、布局转换器
├── Resources/                # AppIcon、磁盘卡片模板等资源
├── Services/                 # 硬件、磁盘、网络、进程、历史、告警、托盘、主题等服务
├── ViewModels/               # MainViewModel、布局、历史等 ViewModel
├── MainWindow*.cs/.xaml      # 主窗口与按功能拆分的 partial 代码
├── MiniWindow.xaml(.cs)      # 悬浮窗
├── App.xaml(.cs)             # 应用入口与全局资源
└── HardwareMonitor.csproj    # 项目配置
```

## 构建与运行

### 环境要求

- Windows 10/11
- .NET 8.0 SDK

### 开发运行

```bash
dotnet run
```

### Release 构建

```bash
dotnet build .\HardwareMonitor.csproj -c Release
```

### 发布 Windows x64 自包含包

```bash
dotnet publish .\HardwareMonitor.csproj -c Release -r win-x64 -o .\publish
```

输出目录中的 `HardwareMonitor.exe` 可直接运行。

## 使用说明

- 双击 `HardwareMonitor.exe` 启动
- 悬浮窗：拖拽移动，右键关闭，点击 `□` 展开主窗口
- 主窗口：标题栏拖拽移动，点击 `🔽` 切换悬浮窗
- 主题：通过标题栏下拉框切换
- 托盘菜单：显示主窗口、显示迷你窗口、开机自启、退出
- 管理员运行可读取更多硬件传感器和 SMART 可靠性字段

## 版本亮点

### v1.0.6

- 首页改为按类型分组展示
- 硬盘改为一块硬盘一张卡片
- 增强 SSD / NVMe SMART 寿命信息和不可读提示
- 优化 1 秒刷新轮询路径，降低 UI 重排和后台调度开销
- 增加应用图标并配置到 exe、窗口和托盘
- 按功能拆分窗口、ViewModel、磁盘服务和转换器代码

## License

MIT
