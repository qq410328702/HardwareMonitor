# Hardware Monitor

一个轻量美观的 Windows 硬件监控工具，基于 WPF + LibreHardwareMonitor 构建。

## 功能

- **CPU / GPU 温度监控** — 实时圆弧仪表盘，颜色随温度动态变化（绿→黄→红）
- **CPU / GPU / 内存使用率** — 百分比仪表盘 + 实时趋势图
- **功耗显示** — CPU / GPU 各自功耗及总功耗
- **GPU 显存** — 进度条显示显存占用
- **悬浮窗模式** — 迷你置顶窗口，显示核心数据，支持拖拽移动
- **多主题切换** — 深色 / 浅色 / 赛博朋克 / 海洋，实时切换
- **非管理员兼容** — 普通用户可运行（使用率正常，温度需管理员权限）

## 截图
<img width="320" height="210" alt="image" src="https://github.com/user-attachments/assets/69061aeb-5775-4684-97f3-d5faed188864" />
<img width="980" height="720" alt="image" src="https://github.com/user-attachments/assets/7b6571b3-c04e-4f85-838e-83ecfe05eb4a" />

> 启动后默认显示浅色主题悬浮窗，点击 □ 按钮展开主窗口。

## 技术栈

| 组件 | 说明 |
|------|------|
| .NET 8 WPF | UI 框架 |
| LibreHardwareMonitor | 硬件传感器读取 |
| LiveCharts2 (SkiaSharp) | 实时趋势图表 |
| MVVM | 架构模式 |

## 项目结构

```
HardwareMonitor/
├── Controls/          # 自定义控件（ArcGauge 圆弧仪表盘）
├── Converters/        # 值转换器（温度→颜色、格式化等）
├── Services/          # 硬件读取服务、主题服务
├── ViewModels/        # MVVM ViewModel
├── MainWindow.xaml    # 主窗口
├── MiniWindow.xaml    # 悬浮窗
└── App.xaml           # 应用入口 + 全局资源
```

## 构建 & 运行

### 环境要求

- Windows 10/11
- .NET 8.0 SDK

### 开发运行

```bash
dotnet run
```

### 发布单文件 EXE

```bash
dotnet publish -c Release -r win-x64 -o ./single
```

输出 `single/HardwareMonitor.exe`，约 74MB，包含运行时，无需安装 .NET。

## 使用说明

- 双击 EXE 启动，默认显示浅色主题悬浮窗
- **悬浮窗操作**：拖拽移动 / 右键关闭 / □ 展开主窗口
- **主窗口操作**：标题栏拖拽移动 / 🔽 切换悬浮窗 / 下拉框切换主题
- **管理员运行**可获取完整数据（CPU 温度、功耗等），普通用户运行使用率仍可正常显示

## License

MIT
