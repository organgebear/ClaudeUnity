# Claude Unity

一个 Unity Editor 插件，集成 Claude AI 助手，通过自然语言命令控制 Unity 编辑器进行游戏开发。

## 功能特性

- **AI 对话界面**：在 Unity Editor 中与 Claude AI 进行实时对话
- **编辑器自动化**：通过自然语言命令执行 Unity 编辑器操作
- **场景管理**：创建、删除、修改 GameObject 及其组件
- **资源管理**：查找、创建、管理项目资源
- **脚本编辑**：创建和编辑 C# 脚本，支持实时编译检查
- **预制体操作**：创建、实例化、应用预制体
- **材质和着色器**：管理材质属性和着色器
- **动画和 UI**：控制 Animator 和 UI 元素
- **项目验证**：检查场景、资源、缺失脚本等问题
- **流式响应**：支持 API 流式输出，实时显示 AI 回复

## 项目结构

```
Editor/
├── API/                          # Claude API 集成
│   ├── ClaudeApiClient.cs       # API 客户端，处理流式请求
│   ├── ClaudeApiTypes.cs        # API 数据类型定义
│   ├── StreamParser.cs          # 流式响应解析器
│   ├── SimpleJsonParser.cs      # JSON 解析工具
│   └── SystemPromptBuilder.cs   # 系统提示词和工具定义生成
├── Commands/                     # 命令执行系统
│   ├── CommandExecutor.cs       # 命令分发器
│   ├── CommandResult.cs         # 命令执行结果
│   ├── ICommandHandler.cs       # 命令处理器接口
│   └── Handlers/                # 各类命令处理器
│       ├── GameObjectHandler.cs      # GameObject 操作
│       ├── ComponentHandler.cs       # 组件操作
│       ├── SceneHandler.cs           # 场景操作
│       ├── PrefabHandler.cs          # 预制体操作
│       ├── AssetHandler.cs           # 资源操作
│       ├── MaterialHandler.cs        # 材质操作
│       ├── LightHandler.cs           # 灯光操作
│       ├── AnimatorHandler.cs        # 动画操作
│       ├── UIHandler.cs              # UI 操作
│       ├── ScriptHandler.cs          # 脚本操作
│       ├── FileSystemHandler.cs      # 文件系统操作
│       ├── EditorControlHandler.cs   # 编辑器控制
│       ├── DebugHandler.cs           # 调试操作
│       ├── ProjectHandler.cs         # 项目操作
│       └── ValidationHandler.cs      # 验证操作
├── Core/                         # 核心功能
│   ├── ClaudeUnityWindow.cs     # 主 UI 窗口
│   ├── ClaudeUnitySettings.cs   # 设置管理
│   ├── ClaudeUnitySettingsProvider.cs  # 设置提供器
│   ├── SessionState.cs          # 会话状态
│   └── DeferredRefresh.cs       # 延迟刷新管理
├── UI/                           # UI 资源
│   ├── ClaudeUnityWindow.uxml   # UI 布局
│   └── ClaudeUnityWindow.uss    # UI 样式
├── Resources/                    # 资源文件
│   └── ClaudeUnitySettings.asset # 默认设置
└── ClaudeUnity.asmdef           # 程序集定义
```

## 安装

1. 将 `ClaudeUnity` 文件夹放入 Unity 项目的 `Assets/` 目录
2. 在 Unity Editor 中，进入 `Window > AI > Claude Unity` 打开插件窗口

## 使用

### 基本设置

1. 打开 Claude Unity 窗口
2. 在设置面板中输入：
   - **API Key**：从 [Anthropic Console](https://console.anthropic.com) 获取
   - **Base URL**：默认为 `https://api.anthropic.com`
   - **Model**：选择使用的 Claude 模型（如 claude-3-5-sonnet-20241022）
   - **Max Tokens**：单次回复的最大 token 数

### 对话交互

1. 在消息输入框输入自然语言命令
2. 点击发送或按 Enter 键
3. Claude 会理解需求并自动执行相应操作
4. 支持 `@` 符号快速引用项目文件

### 命令示例

- "创建一个名为 Player 的 Cube"
- "给 Player 添加 Rigidbody 组件"
- "将 Player 的位置设置为 (0, 1, 0)"
- "创建一个新的 C# 脚本叫 PlayerController"
- "查找所有 PNG 图片资源"
- "验证场景中是否有缺失的脚本"

## 核心类说明

### ClaudeUnityWindow
主 UI 窗口，管理对话界面、设置面板、消息显示等。支持流式显示 AI 回复。

### ClaudeApiClient
与 Claude API 通信的客户端，支持流式请求和非流式回退。

### CommandExecutor
命令分发器，根据命令类型调用对应的处理器执行操作。

### SystemPromptBuilder
生成系统提示词和工具定义，定义 AI 可用的所有操作。

### 各类 Handler
实现具体的 Unity 编辑器操作，如创建物体、修改组件属性等。

## 支持的操作

### GameObject 操作
- 创建、删除、查询 GameObject
- 设置位置、旋转、缩放
- 设置父物体、激活状态

### 组件操作
- 添加、移除组件
- 获取、设置组件属性

### 场景操作
- 获取场景信息和层级结构
- 场景保存和加载

### 资源操作
- 查找资源
- 创建资源
- 资源管理

### 脚本操作
- 创建 C# 脚本
- 读取和编辑脚本
- 编译检查

### 编辑器控制
- 进入/退出播放模式
- 刷新资源
- 编译脚本
- 执行菜单项

### 验证和优化
- 验证场景和资源
- 查找缺失脚本
- 清理空文件夹
- 优化纹理

## 配置

### 设置文件

设置保存在 `Assets/Resources/ClaudeUnitySettings.asset`，包括：
- API Key 和 Base URL
- 模型选择
- 最大 token 数
- 技能文件路径
- 代理设置

### 技能文件

可在设置中指定技能文件夹路径，插件会自动加载 `.md` 文件作为 AI 的参考知识。

## 开发

### 添加新命令

1. 创建新的 Handler 类实现 `ICommandHandler` 接口
2. 在 `CommandExecutor` 中注册命令
3. 在 `SystemPromptBuilder` 中添加工具定义

### 扩展功能

- 修改 `SystemPromptBuilder.cs` 添加新的工具定义
- 在对应的 Handler 中实现具体逻辑
- 支持自定义系统提示词后缀

## 技术栈

- **语言**：C#
- **框架**：Unity Editor
- **UI**：UIElements (UXML/USS)
- **API**：Anthropic Claude API
- **通信**：HTTP 流式请求

## 许可证

MIT License

## 支持

如有问题或建议，请提交 Issue 或 Pull Request。
