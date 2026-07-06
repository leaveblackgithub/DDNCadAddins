"""
Vector Store Module
====================
使用 ChromaDB 作为向量数据库，sentence-transformers 生成 embedding。

特性：
  - 本地运行，无需 API Key
  - 持久化存储到磁盘
  - 支持语义搜索（余弦相似度）
  - 自动管理 embedding 模型缓存
"""

import os
from typing import List, Dict, Optional, Any
from dataclasses import dataclass

import chromadb
from chromadb.config import Settings
from sentence_transformers import SentenceTransformer

from code_indexer import CodeChunk


@dataclass
class SearchResult:
    """搜索结果"""
    chunk: CodeChunk
    score: float  # 相似度分数 (0-1)
    distance: float  # 向量距离


class VectorStore:
    """向量存储，封装 ChromaDB + sentence-transformers"""

    # 使用轻量级模型（~80MB），首次使用会自动下载
    MODEL_NAME = "all-MiniLM-L6-v2"

    # ChromaDB 集合名称
    COLLECTION_NAME = "code_chunks"

    def __init__(self, persist_dir: str, hf_cache_dir: str = None):
        """
        初始化向量存储。

        Args:
            persist_dir: ChromaDB 持久化目录路径
            hf_cache_dir: HuggingFace 模型缓存目录，默认为 persist_dir 同级的 .hf_cache
        """
        self.persist_dir = os.path.abspath(persist_dir)
        os.makedirs(self.persist_dir, exist_ok=True)

        # 设置 HuggingFace 缓存目录（避免环境变量带空格的问题）
        if hf_cache_dir is None:
            hf_cache_dir = os.path.join(os.path.dirname(self.persist_dir), ".hf_cache")
        hf_cache_dir = os.path.abspath(hf_cache_dir)
        os.makedirs(hf_cache_dir, exist_ok=True)
        os.environ["HF_HOME"] = hf_cache_dir
        os.environ["TRANSFORMERS_CACHE"] = os.path.join(hf_cache_dir, "transformers")
        print(f"[VectorStore] HF 缓存目录: {hf_cache_dir}")

        # 初始化 embedding 模型
        print(f"[VectorStore] 加载 embedding 模型: {self.MODEL_NAME}")
        self._encoder = SentenceTransformer(self.MODEL_NAME)
        print(f"[VectorStore] 模型加载完成，向量维度: {self._encoder.get_sentence_embedding_dimension()}")

        # 初始化 ChromaDB
        self._client = chromadb.PersistentClient(
            path=self.persist_dir,
            settings=Settings(anonymized_telemetry=False, allow_reset=True),
        )

        # 获取或创建集合
        self._collection = self._client.get_or_create_collection(
            name=self.COLLECTION_NAME,
            metadata={"hnsw:space": "cosine"},
        )

        print(f"[VectorStore] 向量存储就绪，当前索引数量: {self._collection.count()}")

    def add_chunks(self, chunks: List[CodeChunk]) -> int:
        """
        将代码分块添加到向量数据库。

        Args:
            chunks: 代码分块列表

        Returns:
            成功添加的数量
        """
        if not chunks:
            return 0

        ids = []
        documents = []
        metadatas = []

        for chunk in chunks:
            chunk_id = chunk.id
            ids.append(chunk_id)
            documents.append(chunk.search_text)
            metadatas.append({
                "file_path": chunk.file_path,
                "start_line": chunk.start_line,
                "end_line": chunk.end_line,
                "chunk_type": chunk.chunk_type,
                "name": chunk.name,
                "language": chunk.language,
                "summary": chunk.summary[:500],  # ChromaDB metadata 值有长度限制
            })

        # 生成 embeddings（sentence-transformers v5+ 不再支持 convert_to_list 参数）
        embeddings = self._encoder.encode(
            documents,
            show_progress_bar=True,
        )
        # 手动转换为 list（兼容 numpy/tensor 返回值）
        if hasattr(embeddings, "tolist"):
            embeddings = embeddings.tolist()
        elif hasattr(embeddings, "tolist"):
            embeddings = [e.tolist() if hasattr(e, "tolist") else list(e) for e in embeddings]

        # 批量添加（ChromaDB 限制单批大小）
        batch_size = 100
        added = 0
        for i in range(0, len(ids), batch_size):
            batch_ids = ids[i:i + batch_size]
            batch_docs = documents[i:i + batch_size]
            batch_metas = metadatas[i:i + batch_size]
            batch_embs = embeddings[i:i + batch_size]

            self._collection.upsert(
                ids=batch_ids,
                documents=batch_docs,
                metadatas=batch_metas,
                embeddings=batch_embs,
            )
            added += len(batch_ids)

        print(f"[VectorStore] 已添加/更新 {added} 个代码块")
        return added

    def search(self, query: str, top_k: int = 5, filter_language: Optional[str] = None) -> List[SearchResult]:
        """
        语义搜索代码。

        Args:
            query: 搜索查询（自然语言或代码片段）
            top_k: 返回结果数量
            filter_language: 可选，按语言过滤（"csharp", "python" 等）

        Returns:
            搜索结果列表，按相似度降序排列
        """
        if self._collection.count() == 0:
            return []

        # 生成查询 embedding（sentence-transformers v5+ 不再支持 convert_to_list 参数）
        query_embedding = self._encoder.encode([query])
        if hasattr(query_embedding, "tolist"):
            query_embedding = query_embedding.tolist()
        else:
            query_embedding = [list(e) if not isinstance(e, list) else e for e in query_embedding]

        # 构建过滤条件
        where = None
        if filter_language:
            where = {"language": filter_language}

        # 搜索
        results = self._collection.query(
            query_embeddings=query_embedding,
            n_results=min(top_k, self._collection.count()),
            where=where,
        )

        # 转换结果
        search_results: List[SearchResult] = []
        if results and results["ids"]:
            for i, chunk_id in enumerate(results["ids"][0]):
                metadata = results["metadatas"][0][i]
                document = results["documents"][0][i]
                distance = results["distances"][0][i]

                # 从 metadata 重建 CodeChunk
                chunk = CodeChunk(
                    file_path=metadata.get("file_path", ""),
                    start_line=metadata.get("start_line", 0),
                    end_line=metadata.get("end_line", 0),
                    content=document,
                    chunk_type=metadata.get("chunk_type", "unknown"),
                    name=metadata.get("name", "unknown"),
                    language=metadata.get("language", "unknown"),
                    summary=metadata.get("summary", ""),
                )

                # 余弦距离转相似度分数
                score = max(0.0, 1.0 - distance / 2.0)

                search_results.append(SearchResult(
                    chunk=chunk,
                    score=score,
                    distance=distance,
                ))

        return search_results

    def get_context(self, file_path: str, line_number: int, context_lines: int = 20) -> Optional[CodeChunk]:
        """
        获取指定文件某行附近的代码上下文。

        Args:
            file_path: 文件路径
            line_number: 行号
            context_lines: 上下文行数

        Returns:
            包含该行的代码块，或 None
        """
        # 在向量库中查找该文件的所有块
        results = self._collection.get(
            where={"file_path": file_path},
        )

        if not results or not results["ids"]:
            return None

        # 找到包含该行号的块
        for i, metadata in enumerate(results["metadatas"]):
            start = metadata.get("start_line", 0)
            end = metadata.get("end_line", 0)
            if start <= line_number <= end:
                document = results["documents"][i]
                return CodeChunk(
                    file_path=metadata.get("file_path", ""),
                    start_line=start,
                    end_line=end,
                    content=document,
                    chunk_type=metadata.get("chunk_type", "unknown"),
                    name=metadata.get("name", "unknown"),
                    language=metadata.get("language", "unknown"),
                    summary=metadata.get("summary", ""),
                )

        return None

    def list_files(self) -> List[Dict[str, Any]]:
        """列出所有已索引的文件及其块数量"""
        results = self._collection.get()

        if not results or not results["ids"]:
            return []

        file_map: Dict[str, Dict[str, Any]] = {}
        for metadata in results["metadatas"]:
            fp = metadata.get("file_path", "")
            if fp not in file_map:
                file_map[fp] = {
                    "file_path": fp,
                    "language": metadata.get("language", "unknown"),
                    "chunk_count": 0,
                    "types": set(),
                }
            file_map[fp]["chunk_count"] += 1
            file_map[fp]["types"].add(metadata.get("chunk_type", "unknown"))

        # 转换为列表
        file_list = []
        for fp, info in file_map.items():
            file_list.append({
                "file_path": fp,
                "language": info["language"],
                "chunk_count": info["chunk_count"],
                "types": sorted(list(info["types"])),
            })

        return sorted(file_list, key=lambda x: x["file_path"])

    def clear(self) -> int:
        """清空索引，返回删除的数量"""
        count = self._collection.count()
        if count > 0:
            self._client.delete_collection(self.COLLECTION_NAME)
            self._collection = self._client.get_or_create_collection(
                name=self.COLLECTION_NAME,
                metadata={"hnsw:space": "cosine"},
            )
        print(f"[VectorStore] 已清空索引，删除 {count} 个块")
        return count

    def count(self) -> int:
        """返回当前索引的块数量"""
        return self._collection.count()
