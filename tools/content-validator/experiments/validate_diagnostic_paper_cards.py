#!/usr/bin/env python3
"""Pre-registered paper card integrity, NOT a simulated human playtest.

Usage:
  python3 validate_diagnostic_paper_cards.py --check
  python3 validate_diagnostic_paper_cards.py --role player
  python3 validate_diagnostic_paper_cards.py --role facilitator --seed '<private seed>'
The facilitator's secret sheet is intentionally never included in CI artifacts.
"""
import argparse
import importlib.util
import random
from pathlib import Path

HERE=Path(__file__).resolve().parent
RESEARCH=Path(__file__).resolve().parents[3]/"docs/实验/机制研究/批次/R278-R285-诊断结构迁移与强负例"
CARDS=RESEARCH/"参与者卡片.md"
FACILITATOR=RESEARCH/"研究员规约.md"
PUBLIC_NAME=("第一组","第二组","第三组")
PUBLIC_SETS=(
 ((0,), (0,1), (0,1,2)),
 ((0,1), (0,2), (0,3)),
 ((0,), (1,), (2,))
)
def study():
 modulepath=HERE/"r278_r285_diagnostic_transfer.py"
 spec=importlib.util.spec_from_file_location("diagnostic_transfer",modulepath)
 module=importlib.util.module_from_spec(spec)
 spec.loader.exec_module(module)
 vals=[]
 for tests in PUBLIC_SETS:
  b,f,plans,_=module.exhaustive(tuple(frozenset(t) for t in tests))
  vals.append((b,f,plans))
 return vals
def check():
 assert len(PUBLIC_SETS)==len(PUBLIC_NAME)==3
 assert [x[:2] for x in study()]==[(1,3/4),(1,1),(3/4,3/4)] # rational equality exact
 pub=CARDS.read_text(encoding="utf-8")
 private=FACILITATOR.read_text(encoding="utf-8")
 assert len(pub)>350 and len(private)>350
 assert all(name in pub for name in PUBLIC_NAME)
 assert all(f"属于{{{','.join(map(str,parts))}}}" in pub for tests in PUBLIC_SETS for parts in tests)
 assert "数学最优" not in pub and "ACB" not in pub and "正确顺序" not in pub
 assert "尚未执行" in private and "参与者" in pub
 assert "第一组" in private and "第三组" in private
 print("PASS public paper cards, structural control values, no prewritten solution")
def role_player():
 print(CARDS.read_text(encoding="utf-8"))
def role_facilitator(seed):
 if not seed:raise SystemExit("Provide a private seed for facilitator; never print this sheet to participants.")
 rng=random.Random(seed)
 print("PRIVATE FACILITATOR KEY (DO NOT SHARE WITH PLAYER)")
 for name in PUBLIC_NAME:
  secrets=list(range(4))
  rng.shuffle(secrets)
  print(f"{name}: four truths in scenario order {secrets}")
 print("No human feedback is generated or recorded by this command.")
def main():
 p=argparse.ArgumentParser()
 p.add_argument("--check",action="store_true")
 p.add_argument("--role",choices=("player","facilitator"),default="player")
 p.add_argument("--seed")
 args=p.parse_args()
 if args.check:check()
 elif args.role=="player":role_player()
 else:role_facilitator(args.seed)
if __name__=="__main__":main()
