"""独立 Unity 工程的公共同步层；调用方负责缓存归属、资源处理与执行方式。"""
import argparse
import filecmp
from pathlib import Path
import shutil
import subprocess
import sys

PROJECT_FOLDERS = ('Assets', 'Packages', 'ProjectSettings')


def sync_tree(source: Path, target: Path):
    target.mkdir(parents=True, exist_ok=True)
    expected = set()
    for item in source.iterdir():
        if '.bak_' in item.name:
            continue
        expected.add(item.name)
        dest = target / item.name
        if item.is_dir():
            sync_tree(item, dest)
        elif not dest.exists() or not filecmp.cmp(item, dest, shallow=False):
            shutil.copy2(item, dest)
    for item in target.iterdir():
        if item.name not in expected:
            if item.is_dir(): shutil.rmtree(item)
            else: item.unlink()


def sync_project(source: Path, target: Path):
    source, target = source.resolve(), target.resolve()
    if source == target or source in target.parents or target in source.parents:
        raise ValueError('staging 必须在源工程之外，不能相互嵌套')
    for folder in PROJECT_FOLDERS:
        if not (source / folder).is_dir():
            raise ValueError(f'Unity 源工程缺少 {folder}')
    # 只在源工程关闭时以 APFS clone 建立初始 Library；之后由目标自己维护。
    # 源工程正在运行时，其导入数据库不是一致快照，宁可首次重新导入。
    library = source / 'Library'
    if (sys.platform == 'darwin' and library.is_dir() and not (target / 'Library').exists()
            and not (source / 'Temp/UnityLockfile').exists()):
        target.mkdir(parents=True, exist_ok=True)
        subprocess.run(['cp', '-cR', str(library), str(target / 'Library')], check=True)
    for folder in PROJECT_FOLDERS:
        sync_tree(source / folder, target / folder)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True, type=Path)
    parser.add_argument('--target', required=True, type=Path)
    args = parser.parse_args()
    sync_project(args.source, args.target)
