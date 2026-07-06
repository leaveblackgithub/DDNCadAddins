"""
Code Indexer Module
====================
负责将源代码文件分块（chunking），生成语义化的代码片段用于向量化。

支持的语言：C#（.cs）、Python（.py）、JavaScript/TypeScript（.js/.ts）

分块策略：
  - 按类/方法/函数边界切分
  - 每个块包含：文件路径、起始行号、结束行号、代码内容、类型（class/method/function）
  - 保留完整的语义单元，不截断函数体
"""

import os
import re
from dataclasses import dataclass, field
from typing import List, Optional


@dataclass
class CodeChunk:
    """一个代码分块，代表一个语义单元（类、方法、函数等）"""
    file_path: str
    start_line: int
    end_line: int
    content: str
    chunk_type: str  # "class", "method", "function", "block", "file"
    name: str  # 类名/方法名/函数名
    language: str  # "csharp", "python", "javascript", "typescript"
    summary: str = ""  # 自动生成的摘要

    @property
    def id(self) -> str:
        """唯一标识符：文件路径 + 起始行号"""
        return f"{self.file_path}:{self.start_line}"

    @property
    def search_text(self) -> str:
        """用于生成 embedding 的文本：类型 + 名称 + 代码内容"""
        return f"[{self.chunk_type}] {self.name}\n{self.content}"


class CodeIndexer:
    """代码索引器，负责扫描目录并分块"""

    # 支持的文件扩展名与语言映射
    SUPPORTED_EXTENSIONS = {
        ".cs": "csharp",
        ".py": "python",
        ".js": "javascript",
        ".ts": "typescript",
    }

    # 忽略的目录
    IGNORE_DIRS = {
        "bin", "obj", ".git", ".vs", "node_modules", "__pycache__",
        ".venv", "venv", "packages", "TestRecords", "ExtentReports",
    }

    def __init__(self):
        self._chunks: List[CodeChunk] = []

    @property
    def chunks(self) -> List[CodeChunk]:
        return self._chunks

    def index_directory(self, root_path: str) -> List[CodeChunk]:
        """
        扫描目录，将所有支持的源代码文件分块。

        Args:
            root_path: 要索引的根目录路径

        Returns:
            所有代码分块列表
        """
        self._chunks = []
        root_path = os.path.abspath(root_path)

        for dirpath, dirnames, filenames in os.walk(root_path):
            # 过滤忽略目录
            dirnames[:] = [d for d in dirnames if d not in self.IGNORE_DIRS]

            for filename in filenames:
                ext = os.path.splitext(filename)[1].lower()
                if ext not in self.SUPPORTED_EXTENSIONS:
                    continue

                file_path = os.path.join(dirpath, filename)
                language = self.SUPPORTED_EXTENSIONS[ext]

                try:
                    chunks = self._index_file(file_path, language)
                    self._chunks.extend(chunks)
                except Exception as e:
                    print(f"[CodeIndexer] 跳过文件 {file_path}: {e}")

        return self._chunks

    def _index_file(self, file_path: str, language: str) -> List[CodeChunk]:
        """索引单个文件，按语言选择分块策略"""
        try:
            with open(file_path, "r", encoding="utf-8", errors="ignore") as f:
                lines = f.readlines()
        except Exception as e:
            print(f"[CodeIndexer] 无法读取 {file_path}: {e}")
            return []

        if not lines:
            return []

        if language == "csharp":
            return self._chunk_csharp(file_path, lines)
        elif language == "python":
            return self._chunk_python(file_path, lines)
        else:
            return self._chunk_generic(file_path, lines, language)

    def _chunk_csharp(self, file_path: str, lines: List[str]) -> List[CodeChunk]:
        """
        C# 代码分块：按 class / interface / method 边界切分。

        识别模式：
          - class/interface/struct/enum 声明
          - 方法声明（public/private/protected/internal + 返回类型 + 方法名）
        """
        chunks: List[CodeChunk] = []
        current_start: Optional[int] = None
        current_name: str = ""
        current_type: str = ""
        brace_depth: int = 0
        in_block: bool = False

        # 正则：匹配类/接口/结构体/枚举声明
        type_pattern = re.compile(
            r'^\s*(?:public|private|protected|internal|static|sealed|abstract|partial|\s)*'
            r'(class|interface|struct|enum)\s+(\w+)',
            re.IGNORECASE
        )

        # 正则：匹配方法声明（简化版）
        method_pattern = re.compile(
            r'^\s*(?:public|private|protected|internal|static|virtual|override|async|new|sealed|\s)+'
            r'(?:[\w<>\[\],\s]+)\s+(\w+)\s*\(',
            re.IGNORECASE
        )

        for i, line in enumerate(lines):
            line_no = i + 1

            if not in_block:
                # 检查是否是类型声明
                type_match = type_pattern.match(line)
                method_match = method_pattern.match(line)

                if type_match:
                    current_type = type_match.group(1).lower()
                    current_name = type_match.group(2)
                    current_start = line_no
                    in_block = True
                    brace_depth = 0
                elif method_match:
                    current_type = "method"
                    current_name = method_match.group(1)
                    current_start = line_no
                    in_block = True
                    brace_depth = 0

            if in_block:
                brace_depth += line.count("{") - line.count("}")

                if brace_depth <= 0 and "{" in "".join(lines[current_start - 1:line_no]):
                    # 块结束
                    content = "".join(lines[current_start - 1:line_no])
                    chunk = CodeChunk(
                        file_path=file_path,
                        start_line=current_start,
                        end_line=line_no,
                        content=content.strip(),
                        chunk_type=current_type,
                        name=current_name,
                        language="csharp",
                        summary=self._generate_summary(current_type, current_name, content),
                    )
                    chunks.append(chunk)
                    in_block = False
                    current_start = None

        # 如果文件没有匹配到任何块，将整个文件作为一个块
        if not chunks:
            content = "".join(lines)
            if content.strip():
                chunks.append(CodeChunk(
                    file_path=file_path,
                    start_line=1,
                    end_line=len(lines),
                    content=content.strip(),
                    chunk_type="file",
                    name=os.path.basename(file_path),
                    language="csharp",
                    summary=f"File: {os.path.basename(file_path)}",
                ))

        return chunks

    def _chunk_python(self, file_path: str, lines: List[str]) -> List[CodeChunk]:
        """
        Python 代码分块：按 class / def 边界切分（基于缩进）。
        """
        chunks: List[CodeChunk] = []
        current_start: Optional[int] = None
        current_name: str = ""
        current_type: str = ""
        current_indent: int = 0

        # 正则：匹配 class 或 def 声明
        decl_pattern = re.compile(r'^(\s*)(class|def)\s+(\w+)')

        for i, line in enumerate(lines):
            line_no = i + 1
            match = decl_pattern.match(line)

            if match:
                # 如果有正在处理的块，先保存
                if current_start is not None:
                    content = "".join(lines[current_start - 1:line_no - 1])
                    if content.strip():
                        chunks.append(CodeChunk(
                            file_path=file_path,
                            start_line=current_start,
                            end_line=line_no - 1,
                            content=content.strip(),
                            chunk_type=current_type,
                            name=current_name,
                            language="python",
                            summary=self._generate_summary(current_type, current_name, content),
                        ))

                current_indent = len(match.group(1))
                current_type = match.group(2)
                current_name = match.group(3)
                current_start = line_no

        # 保存最后一个块
        if current_start is not None:
            content = "".join(lines[current_start - 1:])
            if content.strip():
                chunks.append(CodeChunk(
                    file_path=file_path,
                    start_line=current_start,
                    end_line=len(lines),
                    content=content.strip(),
                    chunk_type=current_type,
                    name=current_name,
                    language="python",
                    summary=self._generate_summary(current_type, current_name, content),
                ))

        # 如果没有匹配到任何块
        if not chunks:
            content = "".join(lines)
            if content.strip():
                chunks.append(CodeChunk(
                    file_path=file_path,
                    start_line=1,
                    end_line=len(lines),
                    content=content.strip(),
                    chunk_type="file",
                    name=os.path.basename(file_path),
                    language="python",
                    summary=f"File: {os.path.basename(file_path)}",
                ))

        return chunks

    def _chunk_generic(self, file_path: str, lines: List[str], language: str) -> List[CodeChunk]:
        """通用分块：按函数声明切分，回退到整个文件"""
        chunks: List[CodeChunk] = []

        # JS/TS function 声明
        func_pattern = re.compile(
            r'^\s*(?:export\s+)?(?:async\s+)?function\s+(\w+)|'
            r'^\s*(?:export\s+)?(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s+)?(?:\([^)]*\)|\w+)\s*=>',
            re.IGNORECASE
        )

        current_start: Optional[int] = None
        current_name: str = ""

        for i, line in enumerate(lines):
            line_no = i + 1
            match = func_pattern.match(line)

            if match:
                if current_start is not None:
                    content = "".join(lines[current_start - 1:line_no - 1])
                    if content.strip():
                        chunks.append(CodeChunk(
                            file_path=file_path,
                            start_line=current_start,
                            end_line=line_no - 1,
                            content=content.strip(),
                            chunk_type="function",
                            name=current_name,
                            language=language,
                            summary=self._generate_summary("function", current_name, content),
                        ))

                current_name = match.group(1) or match.group(2) or "anonymous"
                current_start = line_no

        if current_start is not None:
            content = "".join(lines[current_start - 1:])
            if content.strip():
                chunks.append(CodeChunk(
                    file_path=file_path,
                    start_line=current_start,
                    end_line=len(lines),
                    content=content.strip(),
                    chunk_type="function",
                    name=current_name,
                    language=language,
                    summary=self._generate_summary("function", current_name, content),
                ))

        if not chunks:
            content = "".join(lines)
            if content.strip():
                chunks.append(CodeChunk(
                    file_path=file_path,
                    start_line=1,
                    end_line=len(lines),
                    content=content.strip(),
                    chunk_type="file",
                    name=os.path.basename(file_path),
                    language=language,
                    summary=f"File: {os.path.basename(file_path)}",
                ))

        return chunks

    def _generate_summary(self, chunk_type: str, name: str, content: str) -> str:
        """生成代码块的简短摘要"""
        # 提取前 3 行非空内容作为摘要
        lines = [l.strip() for l in content.split("\n") if l.strip()]
        summary_lines = lines[:3]
        return f"{chunk_type} {name}: " + " | ".join(summary_lines)[:200]

    def get_stats(self) -> dict:
        """获取索引统计信息"""
        type_counts = {}
        lang_counts = {}
        for chunk in self._chunks:
            type_counts[chunk.chunk_type] = type_counts.get(chunk.chunk_type, 0) + 1
            lang_counts[chunk.language] = lang_counts.get(chunk.language, 0) + 1

        file_set = set(chunk.file_path for chunk in self._chunks)

        return {
            "total_chunks": len(self._chunks),
            "total_files": len(file_set),
            "by_type": type_counts,
            "by_language": lang_counts,
        }
