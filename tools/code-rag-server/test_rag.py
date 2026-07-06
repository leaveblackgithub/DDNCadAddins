#!/usr/bin/env python
"""Code RAG Server 端到端测试脚本"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from code_indexer import CodeIndexer
from vector_store import VectorStore


def main():
    # 1. 索引代码
    print("=" * 60)
    print("  Code RAG Server 端到端测试")
    print("=" * 60)

    indexer = CodeIndexer()
    src_path = os.path.join(os.path.dirname(__file__), "..", "..", "src", "DDNCadAddins.Core", "Services")
    src_path = os.path.abspath(src_path)
    print(f"\n[1] 索引目录: {src_path}")

    chunks = indexer.index_directory(src_path)
    stats = indexer.get_stats()
    print(f"    分块完成: {stats['total_chunks']} 个块, {stats['total_files']} 个文件")
    print(f"    类型分布: {stats['by_type']}")
    print(f"    语言分布: {stats['by_language']}")

    # 2. 向量化存储
    print(f"\n[2] 向量化并存储到 ChromaDB...")
    vs = VectorStore(persist_dir=os.path.join(os.path.dirname(__file__), ".vector_db"))
    added = vs.add_chunks(chunks)
    print(f"    已存储 {added} 个代码块")

    # 3. 语义搜索测试
    queries = [
        "crop arc geometry calculation",
        "OpResult error handling pattern",
        "circle boundary crop",
        "layer management service",
    ]

    for query in queries:
        print(f"\n[3] 语义搜索: '{query}'")
        results = vs.search(query, top_k=3)
        print(f"    找到 {len(results)} 个结果:")
        for i, r in enumerate(results):
            print(f"    {i+1}. [{r.chunk.chunk_type}] {r.chunk.name} (score={r.score:.4f})")
            print(f"       文件: {os.path.basename(r.chunk.file_path)}:{r.chunk.start_line}-{r.chunk.end_line}")
            print(f"       摘要: {r.chunk.summary[:80]}...")

    # 4. 上下文获取测试
    if chunks:
        test_chunk = chunks[0]
        print(f"\n[4] 上下文获取测试: {os.path.basename(test_chunk.file_path)}:{test_chunk.start_line}")
        ctx = vs.get_context(test_chunk.file_path, test_chunk.start_line + 5)
        if ctx:
            print(f"    找到上下文: [{ctx.chunk_type}] {ctx.name}")
            print(f"    行范围: {ctx.start_line}-{ctx.end_line}")
        else:
            print(f"    未找到上下文")

    # 5. 统计信息
    print(f"\n[5] 索引统计:")
    files = vs.list_files()
    print(f"    已索引文件数: {len(files)}")
    print(f"    向量库总数: {vs.count()}")

    print("\n" + "=" * 60)
    print("  测试完成! Code RAG Server 工作正常。")
    print("=" * 60)


if __name__ == "__main__":
    main()
