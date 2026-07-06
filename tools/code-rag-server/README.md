# Code RAG MCP Server

基于 MCP (Model Context Protocol) 的**语义代码检索服务器**，为 Zoo Code 提供真正的 RAG（检索增强生成）能力。

## ✨ 功能特性

- **语义搜索**：用自然语言搜索代码，而非简单的正则匹配
- **本地运行**：使用 `sentence-transformers/all-MiniLM-L6-v2` 模型，无需 API Key
- **持久化存储**：ChromaDB 向量数据库，索引后跨会话保留
- **多语言支持**：C#、Python、JavaScript、TypeScript
- **智能分块**：按类/方法/函数边界切分，保留完整语义单元

## 🛠️ 提供的 MCP 工具

| 工具名 | 功能 | 示例调用场景 |
|--------|------|-------------|
| `index_codebase` | 索引代码目录 | 首次使用或代码变更后重新索引 |
| `search_code` | 语义搜索代码 | "裁剪圆弧的几何计算"、"OpResult 失败处理" |
| `get_code_context` | 获取某行代码的上下文 | 知道文件和行号，需要完整函数定义 |
| `list_indexed_files` | 列出已索引文件 | 检查索引覆盖范围 |
| `clear_index` | 清空索引 | 需要完全重建索引时 |
| `get_index_stats` | 获取统计信息 | 查看索引状态 |

## 📦 安装

### 方式一：自动安装（推荐）

```cmd
tools\code-rag-server\setup.bat
```

### 方式二：手动安装

```cmd
cd tools\code-rag-server
python -m venv .venv
.venv\Scripts\pip install -r requirements.txt
```

## 🚀 使用流程

### 1. 索引代码

在 Zoo Code 对话中，AI 会自动调用 `index_codebase` 工具：

```
请索引 src 目录的代码
```

或指定路径：

```
请索引 d:\leaveblackgithub\DDNCadAddins\src 目录
```

### 2. 语义搜索

```
搜索：裁剪圆弧的几何计算逻辑
```

AI 会调用 `search_code` 工具，返回语义最相似的代码块。

### 3. 获取上下文

```
获取 CropArcService.cs 第 45 行的上下文
```

AI 会调用 `get_code_context` 返回包含该行的完整方法/类定义。

## 📁 目录结构

```
tools/code-rag-server/
├── server.py            # MCP Server 主程序
├── code_indexer.py      # 代码分块与索引模块
├── vector_store.py      # 向量存储模块（ChromaDB + Embeddings）
├── requirements.txt     # Python 依赖
├── setup.bat            # 一键安装脚本
├── README.md            # 本文档
├── .venv/               # Python 虚拟环境（安装后生成）
├── .vector_db/          # ChromaDB 持久化数据（索引后生成）
└── .hf_cache/           # HuggingFace 模型缓存（首次运行生成）
```

## ⚙️ 技术栈

| 组件 | 技术 | 说明 |
|------|------|------|
| MCP 框架 | `mcp` Python SDK | Model Context Protocol 服务器 |
| Embedding 模型 | `all-MiniLM-L6-v2` | 80MB 轻量模型，384 维向量 |
| 向量数据库 | ChromaDB | 本地持久化，余弦相似度 |
| 代码分块 | 自研正则分块器 | 按类/方法/函数边界切分 |

## 🔧 配置说明

MCP Server 注册在 `.roo/mcp.json` 中：

```json
{
  "mcpServers": {
    "code-rag": {
      "command": "${workspaceFolder}/tools/code-rag-server/.venv/Scripts/python.exe",
      "args": ["${workspaceFolder}/tools/code-rag-server/server.py"],
      "env": {
        "PYTHONUNBUFFERED": "1",
        "HF_HOME": "${workspaceFolder}/tools/code-rag-server/.hf_cache"
      }
    }
  }
}
```

## 📝 注意事项

1. **首次安装**：`setup.bat` 会下载模型和依赖，可能需要 5-10 分钟
2. **首次索引**：`index_codebase` 需要为所有代码块生成 embedding，视代码量而定
3. **模型缓存**：模型文件缓存在 `.hf_cache/` 目录，删除后需重新下载
4. **向量数据**：索引数据存储在 `.vector_db/` 目录，删除后需重新索引
5. **代码变更**：修改代码后重新运行 `index_codebase` 即可更新索引（使用 upsert）
