# -*- coding: utf-8 -*-
"""
为 .roomodes 中所有模式的 customInstructions 追加 RAG MCP 使用策略章节。
用法: python tools/_update_roomodes.py
"""
import json
import sys
import os

ROOMODES_PATH = os.path.join(os.path.dirname(__file__), "..", ".roomodes")

RAG_STRATEGY = """

## RAG MCP 工具使用策略（节省请求）
- 策略一\u00b7限定触发范围：只有当你确认需要查阅外部文档或最新代码库时，才调用 code-rag 的 search_code/index_codebase 工具；不要在每个问题中都自动调用 RAG
- 策略二\u00b7区分全局搜索与本地读取：如果问题只需要看当前项目里的几个已知文件，直接用 read_file 或命令行（不耗 MCP 请求）；只有当问题涉及记不清的老代码、外部最新 API 文档或公司内部庞大知识库时，才动用 RAG MCP
- 策略三\u00b7合并查询：尽量在一次提问中把问题描述清楚，让 RAG MCP 一次返回尽可能全面的结果，避免\u201c追问式\u201d的多次检索"""


def main():
    path = os.path.abspath(ROOMODES_PATH)
    print(f"Reading: {path}")

    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)

    # 幂等性检查
    for mode in data.get("customModes", []):
        if "RAG MCP 工具使用策略" in mode.get("customInstructions", ""):
            print(f"Mode '{mode['slug']}' already has RAG strategy. Aborting.")
            sys.exit(0)

    # 追加策略
    for mode in data.get("customModes", []):
        instructions = mode.get("customInstructions", "")
        mode["customInstructions"] = instructions + RAG_STRATEGY
        print(f"Added RAG strategy to mode: {mode['slug']}")

    # 写回，保持原始缩进风格（2 空格）
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)

    print(f"\nFile saved: {path}")


if __name__ == "__main__":
    main()
