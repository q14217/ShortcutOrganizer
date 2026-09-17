; -- 安装脚本 for ShortcutOrganizer --
#define MyAppName "奥格快捷收纳箱"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "奥格数码"
#define MyAppURL "q14217.top"
#define MyAppExeName "奥格快捷收纳箱.exe"

[Setup]
; 应用唯一标识，使用 Tools -> Generate GUID 生成一个新的替换
AppId={{你的-GUID-在这里}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; 默认安装目录
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}

; 安装包输出设置
OutputDir=.\installer_output
OutputBaseFilename=ShortcutOrganizer_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern

; 安装包图标（可选，需要 .ico 文件）
SetupIconFile=app.ico

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Default.isl"

[Tasks]
; 创建桌面图标任务（默认不勾选）
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 打包 publish 文件夹下的所有内容
Source: ".\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; 创建开始菜单快捷方式
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
; 创建桌面快捷方式（关联到上面的任务）
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; 安装完成后可选择运行程序
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent