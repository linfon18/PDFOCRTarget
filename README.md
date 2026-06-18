# PDFOCRTarget

全平台 PDF 图片转文字 OCR 识别转换工具

UI 来自 [MinecraftConnectTool](https://github.com/MCZLF/MinecraftConnectTool) 二次优化

## 功能

- **OCR 文字识别** — 支持 PNG/JPG/BMP/TIFF/WebP 图片和 PDF 扫描件
- **PDF 照片转 PDF 文本** — 左图右文对照预览，导出为可搜索 PDF（原图保留 + 隐形文字层，Ctrl+F 可搜索）
- **关键词搜索** — 在 PDF 或图片中搜索关键词并定位到具体页面
- **自定义玻璃模糊背景** — 选择自定义照片作为背景，全局玻璃模糊效果
- **跨平台** — Windows / Linux / macOS

## 环境要求

- **Python 3.8+**（OCR 引擎依赖）

### 安装 Python 依赖

```bash
pip install rapidocr_onnxruntime PyMuPDF
```

- `rapidocr_onnxruntime` — 基于 ONNX Runtime 的 OCR 引擎，支持中英文混合识别
- `PyMuPDF` — PDF 渲染和可搜索 PDF 生成

## 下载

前往 [Releases](https://github.com/linfon18/PDFOCRTarget/releases) 下载对应平台版本：

| 平台 | 文件 |
|------|------|
| Windows x64 | `PDFOCRTarget-win-x64.exe` |
| Linux x64 | `PDFOCRTarget-linux-x64` |
| macOS Apple Silicon | `PDFOCRTarget-osx-arm64` |

## 从源码构建

```bash
# 克隆仓库
git clone https://github.com/linfon18/PDFOCRTarget.git
cd PDFOCRTarget

# 运行
dotnet run

# 发布
dotnet publish -c Release -r win-x64 --self-contained true
dotnet publish -c Release -r linux-x64 --self-contained true
dotnet publish -c Release -r osx-arm64 --self-contained true
```

## 技术栈

- [Avalonia UI](https://avaloniaui.net/) — 跨平台 .NET UI 框架
- [Material.Avalonia](https://github.com/AvaloniaCommunity/Material.Avalonia) — Material Design 3 主题
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM 框架
- [PdfPig](https://github.com/UglyToad/PdfPig) — PDF 文字提取
- [RapidOCR](https://github.com/RapidAI/RapidOCR) — OCR 引擎（Python）

## 开源许可

MIT License

## 致谢

- UI 设计参考自 [MinecraftConnectTool](https://github.com/MCZLF/MinecraftConnectTool)
- Github [@linfon18](https://github.com/linfon18)
