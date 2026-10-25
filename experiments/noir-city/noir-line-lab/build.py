"""将模板与模型快照打包为可直接打开的设计工具。"""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parent
models=json.loads((ROOT/'models.json').read_text())
assert models and all(m['vertices'] and m['triangles'] and m['edges'] for m in models)
template=(ROOT/'template.html').read_text()
assert template.count('__MODELS__')==1
body=template.replace('__MODELS__',json.dumps(models,ensure_ascii=False,separators=(',',':')).replace('</',r'<\/'))
page='<!doctype html>\n<html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Noir 描线设计工具</title><style>body{margin:0;padding:20px;background:#13171c;--foreground:#e7e9ed;--card:#242b33;--border:#46515e;--muted-foreground:#a6b0bc;--primary:#dce4ed;--primary-foreground:#111820;color:var(--foreground)}h1{font:600 20px system-ui;max-width:1100px;margin:0 auto 16px}</style><h1>Noir 描线设计工具</h1>\n'+body+'\n</html>\n'
(ROOT/'index.html').write_text(page)
print(ROOT/'index.html')
