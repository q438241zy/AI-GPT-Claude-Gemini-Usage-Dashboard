# AI GPT Claude Gemini 流量仪表工具

一款常驻 Windows 桌面右下角的 AI 余额看板娘。它可以展示 Codex、Claude、Gemini 的额度信息，并用角色状态直观表现余额变化。

![小埋展示 Codex 用量资讯](docs/images/codex-umaru-preview.png)

> 当前版本：V1.0（免安装绿色版）  
> 作者：**QQ奶茶大神**  
> 本项目仅供个人学习、交流与非商业传播，**禁止商用**。

## 主要功能

- 支持 Codex、Claude、Gemini，默认平台为 Codex。
- 展示 5 小时额度、重置时间、周额度、周重置时间。
- Codex 支持显示可用重置卡数量与到期信息。
- 桌面右下角显示、永远置顶、透明度与角色大小可调。
- 支持小埋、多啦A梦和自定义 PNG、JPG、GIF 人物包。
- 根据额度切换站立、紧张、哭泣、睡眠、坐下和压扁状态。
- 支持右键设置、鼠标穿透、紧急解锁快捷键和开机启动。
- 中文、English、日本語界面。

## 快速使用

1. 下载或解压整个项目文件夹。
2. 双击 `Release/CodexWidget.exe`。
3. 右键看板娘，打开“设置”，选择平台、角色、大小和透明度。
4. 点击“登录 / 官方用量页”，在打开的浏览器窗口中登录对应平台。
5. 保持官方 Usage 页面可访问，挂件会按设置的周期刷新。

Codex 用户如果本机已登录 Codex CLI，程序会尝试读取本机登录状态并调用官方接口同步。身份凭据仅在本机使用，不会上传到本项目或第三方服务器。

### 快捷键

| 快捷键 | 功能 |
| --- | --- |
| `Ctrl+Alt+L` | 紧急解除鼠标穿透 |
| `Ctrl+Alt+Shift+F12` | 备用强制解锁 |
| `Ctrl+Alt+U` | 显示或隐藏挂件 |
| `Ctrl+Alt+S` | 打开设置 |
| `Ctrl+Alt+R` | 立即刷新 |
| `Ctrl+Alt+Q` | 退出程序 |

## 项目结构

```text
AI余额看板娘-V1.0/
├─ README.md                 项目介绍、使用和维护说明
├─ 待办事项.md               可持续增删和勾选的开发清单
├─ LICENSE                   个人非商用许可说明
├─ Release/                  可直接运行的绿色版
└─ Source/                   C# / WPF 源码
   ├─ Assets/
   │  ├─ Umaru/
   │  │  ├─ stand/
   │  │  │  ├─ happy.png
   │  │  │  ├─ nervous.png
   │  │  │  ├─ crying.png
   │  │  │  └─ sleeping.png
   │  │  ├─ sit/default.png
   │  │  └─ crushed/default.png
   │  └─ Doraemon/default.png
   ├─ MainWindow.xaml(.cs)   主看板窗口、菜单和人物状态切换
   ├─ SettingsWindow.xaml(.cs) 设置窗口
   ├─ UsageWindow.xaml(.cs)  用量详情窗口
   ├─ CodexLocalApi.cs       Codex 本机登录与用量同步
   ├─ CharacterPack.cs       人物包发现、导入与素材解析
   ├─ MascotState.cs         余额到表情、姿势的映射规则
   ├─ HotkeyManager.cs       全局快捷键
   ├─ WidgetSettings.cs      本机设置读写
   └─ CodexWidget.csproj     .NET 项目配置
```

`Source/bin` 和 `Source/obj` 是构建缓存，不提交到仓库；可随时删除并重新生成。

## 人物包规范

推荐使用以下分层格式。图片可为 PNG、GIF、JPG 或 JPEG；GIF 会循环播放。

```text
我的人物/
├─ character.json
├─ stand/
│  ├─ happy.png
│  ├─ nervous.png
│  ├─ crying.png
│  └─ sleeping.png
├─ sit/default.png
└─ crushed/default.png
```

可选的 `character.json` 示例：

```json
{
  "name": "人物显示名称",
  "author": "作者名称"
}
```

导入方法：右键看板娘 →“设置”→“导入人物包”，选择人物文件夹。程序仍兼容旧版的 `stand-happy.png`、`sit.png`、`default.png` 等平铺命名。

状态规则：

| 指标 | 状态 |
| --- | --- |
| 5 小时余额高于 50% | happy |
| 5 小时余额 20%～50% | nervous |
| 5 小时余额 0%～20% | crying |
| 5 小时余额为 0% | sleeping |
| 周余额 0%～30% | sit |
| 周余额为 0% | crushed |

## 从源码构建

开发环境：Windows、.NET 10 SDK。

```powershell
cd Source
dotnet restore
dotnet build CodexWidget.csproj -c Release
```

编译结果位于 `Source/bin/Release/net10.0-windows/`。发布前请将其中的程序文件同步到根目录的 `Release/`，并实际启动检查右键菜单、设置、人物切换、GIF 和快捷键。

## 一同维护

欢迎提交 Issue 或 Pull Request。开始前建议先阅读并更新 [`待办事项.md`](待办事项.md)，避免多人重复处理同一问题。

建议的协作流程：

1. Fork 仓库并为每项修改建立独立分支。
2. 在 `待办事项.md` 中写明目标，或先建立 Issue。
3. 修改后运行 Release 构建，确保 0 错误。
4. 不提交 `bin`、`obj`、登录资料、Cookie、Token 或个人设置。
5. Pull Request 中写清测试平台、修改内容和可能影响。
6. 涉及人物素材时，注明来源和授权状态；未经许可的素材不要加入公共仓库。

适合参与的方向包括：

- 改进 Codex、Claude、Gemini 用量页面解析的稳定性。
- 增加更多无版权风险的人物包和 GIF 动画。
- 改进高 DPI、多显示器和任务栏位置适配。
- 补充自动化测试、错误日志与升级机制。
- 优化中文、英文和日文翻译。

## 隐私与安全

- 登录在本机浏览器窗口完成。
- 程序只读取本机登录资料或官方用量页面，不应把密码、Cookie、Token 上传到其他服务。
- 请勿把 `%LOCALAPPDATA%/CodexUmaruWidget`、浏览器配置目录或任何凭据提交到 GitHub。
- 第三方平台页面结构改变时，解析功能可能需要维护。

## 授权与素材说明

代码和项目文件依据根目录 [`LICENSE`](LICENSE) 提供：仅允许个人学习、修改与非商业传播，禁止商业使用、销售、付费捆绑和商业广告投放。

“小埋”“多啦A梦”等角色形象的著作权与商标权归各自权利方所有。本项目中的相关素材只用于个人、非商业演示，不代表获得官方授权，也不代表与原权利方存在合作关系。如权利方提出要求，应及时移除相关素材。贡献者必须自行确认新增素材拥有合法使用权限。

---

署名：**QQ奶茶大神**
