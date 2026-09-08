# QuickLook Origin Preview

Windows QuickLook 的 Origin `.opju` 工程预览插件。当前基线：**v1.4.2**。

## 行为

- 有高清缓存时直接显示；首次打开先显示 Origin 官方原始预览，再自动替换为高清图。
- 高清阶段只导出 `isEmbedded=0` 的独立 Graph，排除嵌入式拟合和残差分析图。原始预览阶段由 Origin 官方处理器决定显示内容。
- 每张高清图显示 Graph 名称，按窗口宽高等比例适配，多图纵向连续滚动。
- 图片最长边 1000 像素；无切换按钮、生成提示或进度文字。
- 提前关闭窗口仍完成后台提取，随后退出私有 Origin 会话。失败时保留原始预览并记录日志。
- 使用工程临时副本，不保存原文件；缓存按源文件 SHA-256 匹配。

## 环境与安装

已验证环境：Windows、QuickLook 4.5.0、Origin 2026。
需要已注册的 Origin OPJU 预览处理器，以及安装 OriginExt 和 Pillow 的 Python。

1. 从 Releases 下载 `.qlplugin`，通过 QuickLook 安装并重启。
2. 在安装目录的 `OriginPreview.config` 中，将 `PythonPath` 设置为上述 Python 的绝对路径。
3. 安装目录通常为 `%APPDATA%\pooi.moe\QuickLook\QuickLook.Plugin\QuickLook.Plugin.OriginPreview`。

发布包的 PythonPath 留空，必须配置后才能生成高清预览。本机已安装版本的配置不受影响。
缓存和日志位于 `%LOCALAPPDATA%\QuickLook\OriginPreview`。

## 构建

在 PowerShell 中执行 `./build.ps1`。脚本使用 Windows .NET Framework C# 编译器和已安装 QuickLook 的 Common DLL。
将生成 DLL、Metadata.config、OriginPreview.config 和 generate-preview.py 放在 ZIP 根目录，再把后缀改成 `.qlplugin`。

## 基线与回退

`v1.4.2` 是用户验收后的 Git 标签和 Release 基线。后续更改从此版本继续。
可检出该标签重新构建，或安装对应 Release 包；回退前备份并保留本机 PythonPath 配置。

## 验证范围

- 本机测试工程由 24 张图过滤为 6 张独立 Graph，原工程 SHA-256 未变化。
- 1000×800 和 640×480 的 WPF 布局测试通过，图名与图像合计高度不超过视口，多图可滚动。
- 缓存缺失的真实 WPF 窗口测试确认原始预览宿主出现后自动切换高清内容。
- 已验证关闭预览后提取继续并退出 Origin；未覆盖所有 Origin/QuickLook 版本和工程类型。
- 科研工程、缓存、含科研内容的截图和机器专用配置不随仓库发布。
