# 奥格快捷收纳箱

> 一个 Windows 快捷方式分类管理工具，帮你把散落各处的 `.lnk` 文件整理得井井有条。

---

## ✨ 功能特性

### 核心功能
- **分类管理**：自由创建分类，支持拖拽排序
- **快捷方式添加**：支持添加 `.lnk` / 任意文件 / 整个文件夹批量导入
- **拖拽归类**：拖卡片到左侧分类即可完成移动
- **双击打开**：双击卡片启动；双击空白处快速添加

### 增强功能
- **⭐ 常用面板**：按打开次数自动排序 + 手动固定置顶
- **🕐 最近使用**：一键查看最近 7 天打开的快捷方式
- **失效扫描**：自动检测目标文件已删除的快捷方式
- **一键卸载**：安装版调用卸载程序，绿色版移动到回收站
- **全局热键**：默认 `Ctrl+Alt+S` 呼出（可自定义）
- **系统托盘**：最小化到托盘，双击图标恢复
- **资源管理器集成**：右键文件 → "发送到奥格快捷收纳箱"
- **撤销操作**：`Ctrl+Z` 撤销删除/移动/重命名

### 数据管理
- **自动备份**：每天首次启动自动备份到本地
- **手动备份/恢复**：导出 `.scbackup` 完整配置
- **CSV 导入/导出**：通用格式，方便迁移或分享
- **路径变量化**：备份跨机器可恢复（`%USERPROFILE%` 等）
- **损坏自愈**：`data.json` 损坏时自动从备份恢复

### 界面
- **深色/浅色主题**：一键切换
- **卡片/列表视图**：两种显示方式自由选择
- **虚拟化列表**：上千条数据依然流畅
- **搜索 & 排序**：按名称、时间、使用频率多种方式
- **快捷键说明**：`F1` 查看所有快捷键

---

## ⌨️ 快捷键

| 快捷键 | 功能 |
|--------|------|
| `Ctrl + Alt + S` | 全局呼出主窗口（可自定义） |
| `F1` | 显示快捷键说明 |
| `Ctrl + F` | 聚焦搜索框 |
| `Ctrl + N` | 新建分类 |
| `Ctrl + A` | 全选当前列表 |
| `Ctrl + Z` | 撤销上一步 |
| `Ctrl + L` | 切换卡片/列表视图 |
| `Ctrl + R` | 从磁盘重新加载 |
| `Delete` | 删除选中项 |
| `F2` | 重命名选中项 |
| `Enter` | 打开选中项 |
| `F5` | 扫描失效项 |
| `Esc` | 清空搜索框 |

---

## 🛠️ 技术栈

| 项目 | 版本/说明 |
|------|----------|
| 目标框架 | .NET 8 (`net8.0-windows`) |
| UI 框架 | WPF |
| 虚拟化列表 | [VirtualizingWrapPanel](https://github.com/sbaeumlisberger/VirtualizingWrapPanel) |
| JSON 序列化 | System.Text.Json |
| 回收站删除 | Microsoft.VisualBasic.FileIO |
| 安装包 | Inno Setup |

---

## 🚀 开发环境

### 环境要求
- **Visual Studio 2022** 或更高版本
- **.NET 8 SDK**
- **Windows 10/11**

### 首次运行

```bash
# 1. 克隆仓库
git clone https://github.com/你的用户名/ShortcutOrganizer.git
cd ShortcutOrganizer

# 2. 还原依赖
dotnet restore

# 3. 运行
dotnet run --project ShortcutOrganizer
```

或者直接在 Visual Studio 中打开 `.sln` 文件，按 `F5` 运行。

---

## 📦 发布

### 一键发布（推荐）

项目根目录已包含 `publish.bat`，双击即可：

```bash
publish.bat
```

### 手动发布

```bash
dotnet publish -c Release -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none ^
  -o ./publish
```

生成的文件在 `./publish/ShortcutOrganizer.exe`，单个文件，双击即可运行。

### 制作安装包

使用 [Inno Setup](https://jrsoftware.org/isdl.php) 编译 `installer.iss`：

1. 用 Inno Setup Compiler 打开 `installer.iss`
2. 按 `F9` 编译
3. 安装包生成在 `./installer_output/ShortcutOrganizer_Setup.exe`

---

## 📁 项目结构

```
ShortcutOrganizer/
├── ShortcutOrganizer.sln
├── README.md
├── CHANGELOG.md
├── LICENSE
├── .gitignore
├── installer.iss
└── ShortcutOrganizer/
    ├── App.xaml / App.xaml.cs
    ├── MainWindow.xaml / .cs
    ├── MainWindow.Categories.cs     # 分类管理
    ├── MainWindow.Shortcuts.cs      # 快捷方式
    ├── MainWindow.Icons.cs          # 图标提取
    ├── MainWindow.Uninstall.cs      # 卸载逻辑
    ├── MainWindow.Backup.cs         # 备份/恢复/CSV
    ├── MainWindow.Data.cs           # 数据持久化
    ├── MainWindow.Tray.cs           # 系统托盘
    ├── MainWindow.HotKey.cs         # 全局热键
    ├── MainWindow.Undo.cs           # 撤销
    ├── MainWindow.FileWatcher.cs    # 文件监听
    ├── Models.cs
    ├── AppSettings.cs
    ├── Logger.cs
    ├── PathHelper.cs
    ├── LruCache.cs
    ├── JsonHelpers.cs
    ├── SingleInstanceHelper.cs
    ├── QuickAddService.cs
    ├── QuickAddWindow.xaml / .cs
    ├── SettingsWindow.xaml / .cs
    ├── ShortcutsHelpWindow.xaml / .cs
    ├── ContextMenuRegistrar.cs
    ├── Themes/
    │   ├── LightTheme.xaml
    │   └── DarkTheme.xaml
    └── app.ico
```

---

## 📂 数据存储位置

程序运行时数据保存在：

```
%LocalAppData%\ShortcutOrganizer\
├── data.json          # 用户数据（分类 + 快捷方式）
├── settings.json      # 应用设置
├── backups/           # 自动备份（每天一份，保留 10 天）
└── logs/              # 日志文件（按天分，保留 7 天）
```

备份/恢复方法：

- **完整备份**：程序内"备份"按钮，导出 `.scbackup` 文件
- **手动备份**：复制整个 `%LocalAppData%\ShortcutOrganizer\` 目录

---

## 🐛 反馈问题

遇到问题时请提供：

1. **日志文件**：`%LocalAppData%\ShortcutOrganizer\logs\log_YYYYMMDD.txt`
2. **复现步骤**：越详细越好
3. **系统信息**：Windows 版本号

- 提交 Issue：[GitHub Issues](https://github.com/你的用户名/ShortcutOrganizer/issues)
- 邮箱：your-email@example.com

---

## 📄 许可证

本项目采用 [MIT License](LICENSE) 许可。

---

## 🙏 致谢

- [VirtualizingWrapPanel](https://github.com/sbaeumlisberger/VirtualizingWrapPanel) - 虚拟化 WrapPanel
- [Inno Setup](https://jrsoftware.org/isdl.php) - 安装包制作工具
- 所有提供反馈和建议的用户