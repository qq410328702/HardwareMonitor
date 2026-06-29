# Hardware Monitor

一个轻量的 Windows 硬件监控工具，基于 WPF 和 LibreHardwareMonitor 构建。当前版本专注于核心硬件状态、真实传感器功耗统计、磁盘 SMART/寿命信息、悬浮窗和系统托盘。

当前正式版：**v1.0.10**

[下载最新版](https://github.com/qq410328702/HardwareMonitor/releases/latest)

## 主要功能

### 硬件概览

- CPU / GPU 温度、使用率、功耗实时监控
- 内存使用率、已用/总量展示
- GPU 显存占用读取
- 多主题切换：深色、浅色、赛博朋克、海洋

### 功耗统计

- 汇总 LibreHardwareMonitor 实际读到的 `Power` 传感器
- 优先使用 PSU 总功耗读数；没有 PSU 读数时汇总已检测硬件功耗
- CPU / GPU 代表传感器去重，避免重复相加
- 明细展示硬件类型、传感器名称、瓦数、计入口径
- 基于实时总功耗统计今日、本月、总计电量和电费
- 支持峰谷电价配置，默认全天 `0.60 元/kWh`
- 不做固定估算；读不到时显示权限或硬件不支持提示

### 存储设备

- 每块硬盘独立显示为一张卡片
- 显示盘符、总线类型、介质类型、温度、实时读取速度、实时写入速度
- 支持 SSD / NVMe SMART 寿命信息：
  - 剩余寿命、已用寿命、可用备用空间
  - 累计读取/写入
  - 通电小时、通电次数、异常断电次数
  - 介质错误、错误日志、过热时间
- 支持坏道风险检测：
  - SMART 坏道风险摘要：重映射扇区、待映射扇区、离线不可纠正、不可纠正读写错误
  - 每块硬盘可打开坏道检测窗口，查看 SMART 计数明细
  - 支持按盘符快速扫描和完整扫描，只读读取，不执行修复或写入
- 存储设备按类型分组：NVMe、SATA SSD、HDD、其它设备
- 磁盘温度规则按类型区分：NVMe 60/70°C，SATA SSD 50/60°C，HDD 42/52°C
- SMART 不可读时会显示清晰降级提示，不影响其它监控数据

### 使用体验

- 主窗口按类型分组展示：硬件概览、功耗统计、存储设备
- 主窗口和悬浮窗使用 Phosphor 风格线性矢量图标
- 悬浮窗模式：置顶展示核心数据，支持拖拽移动
- 系统托盘：显示/隐藏主窗口、打开悬浮窗、检查更新、开机自启、退出
- 版本更新：基于 GitHub Releases 检查新版本，支持自动下载、校验并安装
- 自带应用图标，已应用到 exe、主窗口、悬浮窗和托盘
- 普通权限可运行；部分温度、功耗和 SMART 可靠性计数可能需要管理员权限

## 截图

<img width="320" height="210" alt="Hardware Monitor mini window" src="https://github.com/user-attachments/assets/69061aeb-5775-4684-97f3-d5faed188864" />
<img width="980" height="720" alt="Hardware Monitor main window" src="https://github.com/user-attachments/assets/7b6571b3-c04e-4f85-838e-83ecfe05eb4a" />

## 下载

前往 [Releases](https://github.com/qq410328702/HardwareMonitor/releases/latest) 下载最新正式版。

当前发布包：

- `HardwareMonitor-v1.0.10-win-x64.zip`

解压后运行 `HardwareMonitor.exe` 即可。发布包为 Windows x64 自包含版本，无需额外安装 .NET 运行时。

## 技术栈

| 组件 | 说明 |
|------|------|
| .NET 8 WPF | 桌面 UI 框架 |
| LibreHardwareMonitor | CPU / GPU / 内存 / 磁盘 / 功耗传感器读取 |
| PhosphorIconsWpf | Phosphor 风格 WPF 矢量图标 |
| GitHub Releases | 版本更新检查与发布包下载 |
| Windows Forms NotifyIcon | 系统托盘 |
| MVVM + partial 拆分 | 窗口和 ViewModel 按功能组织 |

## 项目结构

```text
HardwareMonitor/
├── Controls/                 # 自定义控件，例如 ArcGauge 圆弧仪表盘
├── Converters/               # 温度、格式化、磁盘等转换器
├── Resources/                # AppIcon、磁盘卡片模板等资源
├── Services/                 # 硬件、磁盘、托盘、主题、日志等服务
├── ViewModels/               # MainViewModel 等 ViewModel
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
- 托盘菜单：显示主窗口、显示迷你窗口、检查更新、开机自启、退出
- 更新：启动后会自动检查一次，也可通过主窗口标题栏或托盘菜单手动检查
- 电费统计只累计应用运行期间检测到的功耗，配置和累计数据保存在 `%LOCALAPPDATA%\HardwareMonitor\electricity.json`
- 管理员运行可读取更多硬件传感器和 SMART 可靠性字段

## License

MIT
