"""
Code RAG MCP Server
====================
基于 MCP (Model Context Protocol) 的语义代码检索服务器。

提供以下工具：
  1. index_codebase  - 索引代码目录，构建向量数据库
  2. search_code     - 语义搜索代码（自然语言查询）
  3. get_code_context - 获取指定文件某行的代码上下文
  4. list_indexed_files - 列出已索引的文件
  5. clear_index     - 清空索引
  6. get_index_stats - 获取索引统计信息

运行方式：
  python server.py

MCP 客户端（如 Zoo Code）通过 stdio 与本服务器通信。
"""

import os
import sys
import json
import logging
from typing import Any, Dict

# 确保能导入同目录模块
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from mcp.server.fastmcp import FastMCP
from code_indexer import CodeIndexer
from vector_store import VectorStore

# ============================================================
# 配置
# ============================================================

# 向量数据库持久化目录（相对于项目根目录）
PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
VECTOR_DB_DIR = os.path.join(PROJECT_ROOT, "tools", "code-rag-server", ".vector_db")
DEFAULT_INDEX_PATH = os.path.join(PROJECT_ROOT, "src")

# 日志配置
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    handlers=[logging.StreamHandler(sys.stderr)],
)
logger = logging.getLogger("code-rag-server")

# ============================================================
# 初始化
# ============================================================

# 全局实例（延迟初始化）
_indexer: CodeIndexer = None
_vector_store: VectorStore = None


def get_vector_store() -> VectorStore:
    """获取或初始化向量存储实例（单例）"""
    global _vector_store
    if _vector_store is None:
        _vector_store = VectorStore(persist_dir=VECTOR_DB_DIR)
    return _vector_store


def get_indexer() -> CodeIndexer:
    """获取或初始化代码索引器实例（单例）"""
    global _indexer
    if _indexer is None:
        _indexer = CodeIndexer()
    return _indexer


# ============================================================
# MCP Server 定义
# ============================================================

mcp = FastMCP(
    "code-rag-server",
    instructions=(
        "Code RAG Server - 语义代码检索工具。"
        "先调用 index_codebase 索引代码目录，然后使用 search_code 进行语义搜索。"
        "支持 C#、Python、JavaScript、TypeScript 代码。"
    ),
)


# ============================================================
# MCP 工具定义
# ============================================================

@mcp.tool()
def index_codebase(path: str = "") -> str:
    """
    索引代码目录，构建向量数据库。

    扫描指定目录下的所有源代码文件（.cs, .py, .js, .ts），
    按类/方法/函数边界分块，生成 embedding 并存入向量数据库。

    Args:
        path: 要索引的目录路径。留空则使用默认路径（项目 src 目录）。

    Returns:
        索引结果统计信息（JSON 字符串）
    """
    try:
        index_path = path if path else DEFAULT_INDEX_PATH
        index_path = os.path.abspath(index_path)

        if not os.path.isdir(index_path):
            return json.dumps({
                "success": False,
                "message": f"目录不存在: {index_path}",
            }, ensure_ascii=False)

        logger.info(f"开始索引目录: {index_path}")

        # 分块
        indexer = get_indexer()
        chunks = indexer.index_directory(index_path)
        logger.info(f"代码分块完成: {len(chunks)} 个块")

        if not chunks:
            return json.dumps({
                "success": False,
                "message": "未找到任何可索引的源代码文件",
            }, ensure_ascii=False)

        # 存入向量数据库
        vs = get_vector_store()
        added = vs.add_chunks(chunks)

        stats = indexer.get_stats()
        result = {
            "success": True,
            "message": f"索引完成: {added} 个代码块已存入向量数据库",
            "stats": stats,
            "index_path": index_path,
        }
        logger.info(f"索引完成: {result['message']}")
        return json.dumps(result, ensure_ascii=False, indent=2)

    except Exception as e:
        logger.error(f"索引失败: {e}", exc_info=True)
        return json.dumps({
            "success": False,
            "message": f"索引失败: {str(e)}",
        }, ensure_ascii=False)


@mcp.tool()
def search_code(query: str, top_k: int = 5, language: str = "") -> str:
    """
    语义搜索代码。

    使用自然语言或代码片段进行搜索，返回语义最相似的代码块。
    必须先调用 index_codebase 建立索引。

    Args:
        query: 搜索查询，可以是自然语言描述（如"裁剪圆弧的几何计算"）或代码片段。
        top_k: 返回结果数量，默认 5，最大 20。
        language: 可选，按语言过滤（csharp, python, javascript, typescript）。留空则搜索所有语言。

    Returns:
        搜索结果列表（JSON 字符串），每个结果包含文件路径、行号、代码内容、相似度分数。
    """
    try:
        if not query or not query.strip():
            return json.dumps({
                "success": False,
                "message": "查询不能为空",
            }, ensure_ascii=False)

        top_k = max(1, min(top_k, 20))
        filter_lang = language.strip() if language and language.strip() else None

        vs = get_vector_store()
        if vs.count() == 0:
            return json.dumps({
                "success": False,
                "message": "索引为空，请先调用 index_codebase 建立索引",
            }, ensure_ascii=False)

        results = vs.search(query, top_k=top_k, filter_language=filter_lang)

        if not results:
            return json.dumps({
                "success": True,
                "message": "未找到匹配结果",
                "results": [],
            }, ensure_ascii=False)

        output_results = []
        for r in results:
            output_results.append({
                "file_path": r.chunk.file_path,
                "start_line": r.chunk.start_line,
                "end_line": r.chunk.end_line,
                "chunk_type": r.chunk.chunk_type,
                "name": r.chunk.name,
                "language": r.chunk.language,
                "score": round(r.score, 4),
                "summary": r.chunk.summary,
                "content": r.chunk.content,
            })

        result = {
            "success": True,
            "message": f"找到 {len(results)} 个匹配结果",
            "query": query,
            "results": output_results,
        }
        return json.dumps(result, ensure_ascii=False, indent=2)

    except Exception as e:
        logger.error(f"搜索失败: {e}", exc_info=True)
        return json.dumps({
            "success": False,
            "message": f"搜索失败: {str(e)}",
        }, ensure_ascii=False)


@mcp.tool()
def get_code_context(file_path: str, line_number: int) -> str:
    """
    获取指定文件某行代码的上下文。

    返回包含该行的完整代码块（类/方法/函数），
    帮助理解代码在上下文中的含义。

    Args:
        file_path: 文件路径
        line_number: 行号

    Returns:
        包含该行的代码块信息（JSON 字符串）
    """
    try:
        if not file_path:
            return json.dumps({
                "success": False,
                "message": "文件路径不能为空",
            }, ensure_ascii=False)

        file_path = os.path.abspath(file_path)
        line_number = max(1, int(line_number))

        vs = get_vector_store()
        chunk = vs.get_context(file_path, line_number)

        if chunk is None:
            return json.dumps({
                "success": False,
                "message": f"未找到包含行 {line_number} 的代码块（文件: {file_path}）",
            }, ensure_ascii=False)

        result = {
            "success": True,
            "message": f"找到上下文: {chunk.chunk_type} {chunk.name}",
            "chunk": {
                "file_path": chunk.file_path,
                "start_line": chunk.start_line,
                "end_line": chunk.end_line,
                "chunk_type": chunk.chunk_type,
                "name": chunk.name,
                "language": chunk.language,
                "summary": chunk.summary,
                "content": chunk.content,
            },
        }
        return json.dumps(result, ensure_ascii=False, indent=2)

    except Exception as e:
        logger.error(f"获取上下文失败: {e}", exc_info=True)
        return json.dumps({
            "success": False,
            "message": f"获取上下文失败: {str(e)}",
        }, ensure_ascii=False)


@mcp.tool()
def list_indexed_files() -> str:
    """
    列出所有已索引的文件。

    Returns:
        已索引文件列表（JSON 字符串），包含文件路径、语言、块数量。
    """
    try:
        vs = get_vector_store()
        files = vs.list_files()

        result = {
            "success": True,
            "message": f"共 {len(files)} 个文件已索引",
            "total_chunks": vs.count(),
            "files": files,
        }
        return json.dumps(result, ensure_ascii=False, indent=2)

    except Exception as e:
        logger.error(f"列出文件失败: {e}", exc_info=True)
        return json.dumps({
            "success": False,
            "message": f"列出文件失败: {str(e)}",
        }, ensure_ascii=False)


@mcp.tool()
def clear_index() -> str:
    """
    清空向量数据库索引。

    删除所有已索引的代码块。清空后需要重新调用 index_codebase 建立索引。

    Returns:
        操作结果（JSON 字符串）
    """
    try:
        vs = get_vector_store()
        deleted = vs.clear()

        result = {
            "success": True,
            "message": f"已清空索引，删除 {deleted} 个代码块",
        }
        return json.dumps(result, ensure_ascii=False)

    except Exception as e:
        logger.error(f"清空索引失败: {e}", exc_info=True)
        return json.dumps({
            "success": False,
            "message": f"清空索引失败: {str(e)}",
        }, ensure_ascii=False)


@mcp.tool()
def get_index_stats() -> str:
    """
    获取索引统计信息。

    Returns:
        统计信息（JSON 字符串），包括总块数、文件数、按类型和语言的分布。
    """
    try:
        vs = get_vector_store()
        indexer = get_indexer()

        stats = indexer.get_stats()
        stats["vector_db_count"] = vs.count()
        stats["vector_db_path"] = VECTOR_DB_DIR

        result = {
            "success": True,
            "stats": stats,
        }
        return json.dumps(result, ensure_ascii=False, indent=2)

    except Exception as e:
        logger.error(f"获取统计失败: {e}", exc_info=True)
        return json.dumps({
            "success": False,
            "message": f"获取统计失败: {str(e)}",
        }, ensure_ascii=False)


# ============================================================
# 启动入口
# ============================================================

if __name__ == "__main__":
    logger.info("Code RAG MCP Server 启动中...")
    logger.info(f"项目根目录: {PROJECT_ROOT}")
    logger.info(f"向量数据库目录: {VECTOR_DB_DIR}")
    logger.info(f"默认索引路径: {DEFAULT_INDEX_PATH}")

    # 通过 stdio 运行 MCP 服务器
    mcp.run(transport="stdio")
