#!/usr/bin/env python3
"""直接给成品HTML打补丁的模板：每个 rep(旧,新) 要求旧片段在文件中恰好出现一次，否则报错。
用法: python3 patch_html.py FILE.html   （先改下面的 EDITS）"""
import sys
f=sys.argv[1];s=open(f,encoding='utf-8').read()
def rep(a,b):
    global s
    assert s.count(a)==1,(s.count(a),a[:60]);s=s.replace(a,b)
EDITS=[
 # (旧文本, 新文本),
]
for a,b in EDITS:rep(a,b)
open(f,'w',encoding='utf-8').write(s)
