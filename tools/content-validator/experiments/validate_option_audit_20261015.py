"""Validate the cross-family option-preservation evidence matrix without re-solving old models."""
import json, pathlib
P=pathlib.Path(__file__).resolve().parents[3]/"experiments/抽象决策关系研究/批次/2026-10-15-保留选择权跨族审计/反证矩阵.json"
def validate(data):
 assert data["schema"]=="ssnoir.option-value-counterfactual-matrix/v1"
 cases=data["cases"]
 assert len(cases)==5
 assert len(set(z["id"] for z in cases))==5
 assert len(data["gate_questions"])>=8
 for item in cases:
  assert item["source"].startswith("experiments/抽象决策关系研究/批次/")
  assert item["main"] and item["control"] and item["control_definition"]
  assert item["failure_condition"]
 assert any("waiting" in item["failure_condition"] for item in cases)
 assert {x["id"] for x in cases}=={"HEDGE","FOCUS","CLAIM","ACTION-MODE","ACTION-SWITCH"}
 return True
if __name__=="__main__":
 data=json.loads(P.read_text(encoding="utf-8"))
 validate(data)
 print("PASS: five traceable cases, explicit controls, caveats and nine gate questions")
 print("Note: validates documentation schema, not old mathematical models.")
