#!/usr/bin/env python3
"""列出这座城里现有的空间锚点名（内容里 :anchor 能填的全部合法值）。

锚点不在 Unity 场景里，而在模型里：Blender 那头建一个叫 `Anchor_<节点名>` 的空节点，
导入时由 SSNoirModelImporter 挂上 NodeAnchor 组件。所以要回答「这个地方有哪些锚点」，
得读模型文件——翻 Main.unity 会漏掉绝大多数，那里只留了几个场景特例。

用法：
    python3 skills/write-scheme/scripts/list-anchors.py
"""

import glob
import re
import sys

MODELS = "UnityClient/Assets/Resources/Models/**/*"
# FBX 把节点名当作带长度前缀的裸字节存放，不能整份 decode 之后再找——那样会在
# 多字节边界上把名字截断。按字节匹配 ASCII 或合法的 UTF-8 三字节序列。
PATTERN = rb"Anchor_(?:[0-9A-Za-z_@.\-]|[\xe0-\xef][\x80-\xbf]{2})+"


def main() -> int:
    names = set()
    for path in glob.glob(MODELS, recursive=True):
        if not path.endswith((".fbx", ".blend")):
            continue
        with open(path, "rb") as handle:
            data = handle.read()
        for match in re.finditer(PATTERN, data):
            names.add(match.group()[len("Anchor_"):].decode("utf-8", "ignore"))

    if not names:
        print("没找到任何 Anchor_*。确认工作目录是仓库根目录。", file=sys.stderr)
        return 1

    for name in sorted(names):
        print(name)
    print(f"\n共 {len(names)} 个锚点。", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
