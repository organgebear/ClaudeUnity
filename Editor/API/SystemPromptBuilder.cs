using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ClaudeUnity
{
    public static class SystemPromptBuilder
    {
        public static string BuildSystemPrompt(string skillsPath, string customSuffix)
        {
            var sb = new StringBuilder();
            sb.AppendLine("你是一个 Unity Editor AI 助手。你可以通过调用工具来控制 Unity 编辑器。");
            sb.AppendLine("用户会用自然语言描述需求，你需要理解需求并调用对应的工具来执行操作。");
            sb.AppendLine();
            sb.AppendLine("## 重要规则");
            sb.AppendLine("1. 每次操作都使用工具调用，不要输出 JSON 代码块");
            sb.AppendLine("2. 可以一次调用多个工具来完成复杂任务");
            sb.AppendLine("3. 如果需要查询信息再操作，先调用查询工具，等待结果后再继续");
            sb.AppendLine("4. 操作完成后，必须用文字简要总结做了什么，例如「已创建 Cube 并添加了 Rigidbody」，不要只调用工具而不回复文字");
            sb.AppendLine("5. 如果操作失败，分析原因并尝试用其他方式修复，不要直接放弃");
            sb.AppendLine("6. 创建脚本时，生成完整可编译的 C# 代码，包含正确的 using、namespace 和类定义");
            sb.AppendLine("7. 所有对场景的修改都支持 Ctrl+Z 撤销，完成操作后提醒用户可以用 Ctrl+Z 撤销");
            sb.AppendLine("8. 设置组件属性时，Vector2 用 [x,y]，Vector3 用 [x,y,z]，Color 用 #RRGGBB 或 [r,g,b,a] 格式");
            sb.AppendLine();
            sb.AppendLine("## 迭代开发模式");
            sb.AppendLine("当用户要求根据需求文档进行开发时，遵循以下流程：");
            sb.AppendLine("1. 先用 ReadFile 读取需求文档，理解完整需求");
            sb.AppendLine("2. 用 ListFiles 了解项目现有文件结构");
            sb.AppendLine("3. 用 ReadScript/ReadFile 阅读相关现有代码，避免重复或冲突");
            sb.AppendLine("4. 逐步创建/修改脚本，每次只做一个功能点");
            sb.AppendLine("5. 每次创建或修改脚本后，调用 CompileScripts 触发编译");
            sb.AppendLine("6. 编译后调用 GetCompileErrors 检查是否有错误");
            sb.AppendLine("7. 如果有编译错误，用 ReadScript 读取出错文件，分析错误原因，用 EditScript 修复，然后重新编译检查");
            sb.AppendLine("8. 重复步骤 5-7 直到没有编译错误");
            sb.AppendLine("9. 所有功能完成后，给出总结说明完成了哪些功能");
            sb.AppendLine();

            // Load skill files if path provided
            if (!string.IsNullOrEmpty(skillsPath) && Directory.Exists(skillsPath))
            {
                sb.AppendLine("## 可用技能参考");
                var files = Directory.GetFiles(skillsPath, "*.md");
                foreach (var file in files)
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (name == "unity") continue; // skip the index file
                    var content = File.ReadAllText(file);
                    sb.AppendLine($"### {name}");
                    sb.AppendLine(content);
                    sb.AppendLine();
                }
            }

            if (!string.IsNullOrEmpty(customSuffix))
            {
                sb.AppendLine();
                sb.AppendLine(customSuffix);
            }

            return sb.ToString();
        }

        public static List<ToolDefinition> BuildToolDefinitions()
        {
            var tools = new List<ToolDefinition>();

            // GameObject operations
            tools.Add(MakeTool("CreateGameObject", "在场景中创建新的 GameObject",
                Props(Prop("name", "string", "物体名称"),
                      Prop("primitiveType", "string", "基础类型: cube/sphere/cylinder/plane/capsule/quad，留空创建空物体")),
                new[] { "name" }));

            tools.Add(MakeTool("DeleteGameObject", "删除场景中的 GameObject",
                Props(Prop("target", "string", "目标物体名称")),
                new[] { "target" }));

            tools.Add(MakeTool("SetTransform", "设置物体的位置、旋转、缩放",
                Props(Prop("target", "string", "目标物体名"),
                      PropArray("position", "number", "位置 [x, y, z]"),
                      PropArray("rotation", "number", "旋转 [x, y, z]"),
                      PropArray("scale", "number", "缩放 [x, y, z]")),
                new[] { "target" }));

            tools.Add(MakeTool("SetParent", "设置物体的父子关系",
                Props(Prop("target", "string", "子物体名"),
                      Prop("parent", "string", "父物体名，空则移到根级"),
                      Prop("worldPositionStays", "boolean", "是否保持世界坐标")),
                new[] { "target" }));

            tools.Add(MakeTool("SetActive", "设置物体的激活状态",
                Props(Prop("target", "string", "目标物体名"),
                      Prop("active", "boolean", "是否激活")),
                new[] { "target", "active" }));

            tools.Add(MakeTool("GetGameObjectInfo", "查询物体信息",
                Props(Prop("target", "string", "目标物体名")),
                new[] { "target" }));

            // Component operations
            tools.Add(MakeTool("AddComponent", "给物体添加组件",
                Props(Prop("target", "string", "目标物体名"),
                      Prop("componentType", "string", "组件类型 (Rigidbody, BoxCollider, SphereCollider, CapsuleCollider, MeshCollider, AudioSource, Light, Camera, Animator 等)")),
                new[] { "target", "componentType" }));

            tools.Add(MakeTool("RemoveComponent", "移除物体上的组件",
                Props(Prop("target", "string", "目标物体名"),
                      Prop("componentType", "string", "组件类型")),
                new[] { "target", "componentType" }));

            tools.Add(MakeTool("GetComponents", "获取物体上的所有组件",
                Props(Prop("target", "string", "目标物体名")),
                new[] { "target" }));

            tools.Add(MakeTool("SetComponentProperty", "设置组件属性",
                Props(Prop("target", "string", "目标物体名"),
                      Prop("componentType", "string", "组件类型"),
                      Prop("propertyName", "string", "属性名"),
                      Prop("value", "string", "属性值 (JSON格式)")),
                new[] { "target", "componentType", "propertyName", "value" }));

            // Scene operations
            tools.Add(MakeTool("Scene", "场景管理操作",
                Props(Prop("action", "string", "操作: new/save/load/saveas"),
                      Prop("name", "string", "场景名称 (new时使用)"),
                      Prop("path", "string", "场景路径 (load/saveas时使用)")),
                new[] { "action" }));

            tools.Add(MakeTool("GetSceneInfo", "获取当前场景信息", Props(), System.Array.Empty<string>()));

            tools.Add(MakeTool("GetSceneHierarchy", "获取场景层级结构",
                Props(Prop("includeComponents", "boolean", "是否包含组件信息")),
                System.Array.Empty<string>()));

            // Prefab operations
            tools.Add(MakeTool("CreatePrefab", "将物体保存为 Prefab",
                Props(Prop("target", "string", "目标物体名"),
                      Prop("path", "string", "保存路径 (如 Assets/Prefabs/MyPrefab.prefab)")),
                new[] { "target", "path" }));

            tools.Add(MakeTool("InstantiatePrefab", "实例化 Prefab",
                Props(Prop("path", "string", "Prefab 路径"),
                      Prop("name", "string", "实例名称"),
                      PropArray("position", "number", "位置 [x, y, z]")),
                new[] { "path" }));

            tools.Add(MakeTool("ApplyPrefab", "应用 Prefab 修改",
                Props(Prop("target", "string", "Prefab 实例名")),
                new[] { "target" }));

            tools.Add(MakeTool("RevertPrefab", "还原 Prefab 实例",
                Props(Prop("target", "string", "Prefab 实例名")),
                new[] { "target" }));

            tools.Add(MakeTool("UnpackPrefab", "解包 Prefab",
                Props(Prop("target", "string", "Prefab 实例名"),
                      Prop("mode", "string", "解包模式: OutermostRoot 或 Completely")),
                new[] { "target" }));

            // Asset operations
            tools.Add(MakeTool("FindAssets", "搜索项目资源",
                Props(Prop("searchFilter", "string", "搜索过滤器 (如 t:Material, t:Prefab, t:Script)"),
                      PropArray("searchInFolders", "string", "搜索目录列表"),
                      Prop("limit", "integer", "最大结果数"),
                      Prop("detailed", "boolean", "是否返回详细信息")),
                new[] { "searchFilter" }));

            tools.Add(MakeTool("CreateAsset", "创建新资源",
                Props(Prop("assetType", "string", "资源类型: Material/AnimationClip/ScriptableObject"),
                      Prop("name", "string", "资源名称"),
                      Prop("folder", "string", "保存目录")),
                new[] { "assetType", "name", "folder" }));

            tools.Add(MakeTool("ManageAsset", "管理资源 (删除/获取信息/导入)",
                Props(Prop("action", "string", "操作: delete/getinfo/import/reimport"),
                      Prop("path", "string", "资源路径")),
                new[] { "action", "path" }));

            // Material operations
            tools.Add(MakeTool("CreateMaterial", "创建材质",
                Props(Prop("name", "string", "材质名"),
                      Prop("folder", "string", "保存目录 (如 Assets/Materials)"),
                      Prop("shader", "string", "Shader 名称 (Standard, Unlit/Color 等)"),
                      Prop("color", "string", "颜色 #RRGGBB")),
                new[] { "name", "folder" }));

            tools.Add(MakeTool("SetMaterial", "将材质应用到物体",
                Props(Prop("target", "string", "目标物体名"),
                      Prop("material", "string", "材质路径 (如 Assets/Materials/Red.mat)"),
                      Prop("index", "integer", "材质索引 (默认0)")),
                new[] { "target", "material" }));

            tools.Add(MakeTool("SetMaterialProperty", "设置材质属性",
                Props(Prop("materialPath", "string", "材质路径"),
                      Prop("propertyName", "string", "属性名 (如 _Color)"),
                      Prop("propertyType", "string", "属性类型: color/float/vector/texture"),
                      Prop("value", "string", "属性值")),
                new[] { "materialPath", "propertyName", "propertyType", "value" }));

            tools.Add(MakeTool("FindShader", "搜索 Shader",
                Props(Prop("keyword", "string", "搜索关键词")),
                new[] { "keyword" }));

            tools.Add(MakeTool("ListShaders", "列出所有可用 Shader", Props(), System.Array.Empty<string>()));

            tools.Add(MakeTool("GetMaterialProperties", "获取材质属性",
                Props(Prop("materialPath", "string", "材质路径")),
                new[] { "materialPath" }));

            tools.Add(MakeTool("SetMaterialShader", "更改材质的 Shader",
                Props(Prop("materialPath", "string", "材质路径"),
                      Prop("shader", "string", "新 Shader 名称")),
                new[] { "materialPath", "shader" }));

            tools.Add(MakeTool("GetShaderProperties", "获取 Shader 属性定义",
                Props(Prop("shader", "string", "Shader 名称")),
                new[] { "shader" }));

            // Light
            tools.Add(MakeTool("Light", "创建或设置灯光",
                Props(Prop("action", "string", "操作: create/set"),
                      Prop("name", "string", "灯光名称"),
                      Prop("lightType", "string", "灯光类型: Directional/Point/Spot/Area"),
                      Prop("color", "string", "颜色 #RRGGBB"),
                      Prop("intensity", "number", "亮度"),
                      Prop("range", "number", "范围 (Point/Spot)"),
                      Prop("spotAngle", "number", "聚光角度 (Spot)"),
                      Prop("shadows", "string", "阴影: none/hard/soft")),
                new[] { "action", "name" }));

            // Animator
            tools.Add(MakeTool("Animator", "动画控制器管理",
                Props(Prop("action", "string", "操作: createcontroller/addparameter/setparameter/play"),
                      Prop("name", "string", "控制器名称"),
                      Prop("folder", "string", "保存目录"),
                      Prop("controller", "string", "控制器路径"),
                      Prop("target", "string", "目标物体名"),
                      Prop("paramName", "string", "参数名"),
                      Prop("paramType", "string", "参数类型: float/int/bool/trigger"),
                      Prop("value", "string", "参数值"),
                      Prop("state", "string", "动画状态名"),
                      Prop("layer", "integer", "动画层")),
                new[] { "action" }));

            // UI
            tools.Add(MakeTool("UI", "创建或设置 UI 元素",
                Props(Prop("action", "string", "操作: create/set"),
                      Prop("uiType", "string", "UI类型: canvas/panel/button/text/image/inputfield/slider/toggle/dropdown"),
                      Prop("name", "string", "元素名称"),
                      Prop("parent", "string", "父物体名称"),
                      Prop("text", "string", "文本内容"),
                      Prop("color", "string", "颜色 #RRGGBB"),
                      Prop("fontSize", "integer", "字体大小"),
                      Prop("placeholder", "string", "占位符文本")),
                new[] { "action" }));

            // Editor control
            tools.Add(MakeTool("EnterPlayMode", "进入播放模式", Props(), System.Array.Empty<string>()));
            tools.Add(MakeTool("ExitPlayMode", "退出播放模式", Props(), System.Array.Empty<string>()));
            tools.Add(MakeTool("RefreshAssets", "刷新资源数据库", Props(), System.Array.Empty<string>()));
            tools.Add(MakeTool("CompileScripts", "编译脚本", Props(), System.Array.Empty<string>()));
            tools.Add(MakeTool("ClearConsole", "清空控制台", Props(), System.Array.Empty<string>()));

            tools.Add(MakeTool("SetSelection", "选中物体",
                Props(Prop("target", "string", "目标物体名")),
                new[] { "target" }));

            tools.Add(MakeTool("GetSelection", "获取当前选中的物体", Props(), System.Array.Empty<string>()));

            tools.Add(MakeTool("ExecuteMenuItem", "执行菜单项",
                Props(Prop("menuPath", "string", "菜单路径 (如 GameObject/3D Object/Cube)")),
                new[] { "menuPath" }));

            tools.Add(MakeTool("GetEditorInfo", "获取编辑器信息", Props(), System.Array.Empty<string>()));

            // Debug
            tools.Add(MakeTool("DebugLog", "输出调试日志",
                Props(Prop("message", "string", "日志内容"),
                      Prop("logType", "string", "日志类型: Log/Warning/Error")),
                new[] { "message" }));

            tools.Add(MakeTool("GetLogs", "获取最近的日志",
                Props(Prop("count", "integer", "获取数量"),
                      Prop("logType", "string", "过滤类型: Log/Warning/Error")),
                System.Array.Empty<string>()));

            tools.Add(MakeTool("PauseEditor", "暂停编辑器", Props(), System.Array.Empty<string>()));
            tools.Add(MakeTool("ResumeEditor", "恢复编辑器", Props(), System.Array.Empty<string>()));

            // Project management
            tools.Add(MakeTool("GetProjectStructure", "获取项目目录结构",
                Props(Prop("path", "string", "起始路径 (默认 Assets)"),
                      Prop("depth", "integer", "遍历深度")),
                System.Array.Empty<string>()));

            tools.Add(MakeTool("MoveFile", "移动资源文件",
                Props(Prop("sourcePath", "string", "源路径"),
                      Prop("destPath", "string", "目标路径")),
                new[] { "sourcePath", "destPath" }));

            tools.Add(MakeTool("RenameFile", "重命名资源",
                Props(Prop("path", "string", "资源路径"),
                      Prop("newName", "string", "新名称")),
                new[] { "path", "newName" }));

            tools.Add(MakeTool("DuplicateFile", "复制资源",
                Props(Prop("path", "string", "资源路径")),
                new[] { "path" }));

            // Validation
            tools.Add(MakeTool("ValidateScene", "验证场景 (检查缺失脚本、预制体、重复物体)",
                Props(), System.Array.Empty<string>()));

            tools.Add(MakeTool("ValidateAssets", "验证资源引用",
                Props(Prop("path", "string", "检查路径")),
                System.Array.Empty<string>()));

            tools.Add(MakeTool("FindMissingScripts", "查找缺失脚本引用",
                Props(), System.Array.Empty<string>()));

            tools.Add(MakeTool("CleanupEmptyFolders", "清理空文件夹",
                Props(Prop("dryRun", "boolean", "仅预览不删除")),
                System.Array.Empty<string>()));

            tools.Add(MakeTool("OptimizeTextures", "优化纹理大小",
                Props(Prop("maxSize", "integer", "最大尺寸"),
                      Prop("path", "string", "检查路径")),
                System.Array.Empty<string>()));

            // Script operations
            tools.Add(MakeTool("CreateScript", "创建新的 C# 脚本文件",
                Props(Prop("scriptName", "string", "脚本名称 (不含 .cs 后缀)"),
                      Prop("folder", "string", "保存目录 (默认 Assets/Scripts)"),
                      Prop("code", "string", "完整的 C# 代码内容")),
                new[] { "scriptName", "code" }));

            tools.Add(MakeTool("ReadScript", "读取已有脚本的内容",
                Props(Prop("path", "string", "脚本路径 (如 Assets/Scripts/MyScript.cs)")),
                new[] { "path" }));

            tools.Add(MakeTool("EditScript", "修改已有脚本的内容 (整体替换)",
                Props(Prop("path", "string", "脚本路径"),
                      Prop("code", "string", "替换后的完整 C# 代码")),
                new[] { "path", "code" }));

            // File system operations
            tools.Add(MakeTool("ReadFile", "读取任意文件内容 (支持 .md/.txt/.json/.xml/.cs 等)",
                Props(Prop("path", "string", "文件路径 (如 Doc/需求文档.md 或 Assets/Scripts/Test.cs)")),
                new[] { "path" }));

            tools.Add(MakeTool("WriteFile", "写入任意文件 (自动创建目录)",
                Props(Prop("path", "string", "文件路径"),
                      Prop("content", "string", "文件内容")),
                new[] { "path", "content" }));

            tools.Add(MakeTool("ListFiles", "列出目录中的文件和子目录",
                Props(Prop("path", "string", "目录路径 (默认 Assets)"),
                      Prop("pattern", "string", "文件匹配模式 (如 *.cs, *.md, 默认 *)"),
                      Prop("recursive", "boolean", "是否递归搜索子目录")),
                System.Array.Empty<string>()));

            tools.Add(MakeTool("GetCompileErrors", "获取当前编译错误列表，用于检查脚本是否有语法或类型错误",
                Props(), System.Array.Empty<string>()));

            tools.Add(MakeTool("DeleteFile", "删除文件",
                Props(Prop("path", "string", "文件路径")),
                new[] { "path" }));

            return tools;
        }

        // Helper methods for building tool schemas
        private static ToolDefinition MakeTool(string name, string desc, Dictionary<string, object> properties, string[] required)
        {
            var schema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties
            };
            if (required != null && required.Length > 0)
                schema["required"] = required;

            return new ToolDefinition { name = name, description = desc, input_schema = schema };
        }

        private static Dictionary<string, object> Props(params KeyValuePair<string, object>[] props)
        {
            var dict = new Dictionary<string, object>();
            foreach (var p in props) dict[p.Key] = p.Value;
            return dict;
        }

        private static KeyValuePair<string, object> Prop(string name, string type, string desc)
        {
            var prop = new Dictionary<string, object>
            {
                ["type"] = type,
                ["description"] = desc
            };
            return new KeyValuePair<string, object>(name, prop);
        }

        private static KeyValuePair<string, object> PropArray(string name, string itemType, string desc)
        {
            var prop = new Dictionary<string, object>
            {
                ["type"] = "array",
                ["items"] = new Dictionary<string, object> { ["type"] = itemType },
                ["description"] = desc
            };
            return new KeyValuePair<string, object>(name, prop);
        }
    }
}
