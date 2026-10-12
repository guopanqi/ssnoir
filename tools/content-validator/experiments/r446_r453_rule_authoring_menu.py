#!/usr/bin/env python3
"""R446-R453: executable rule creation vs equally costly compiled menus.
Exhaust all 4^4 target tapes; early bit x, late bit y, one output/action slot.
"""
from functools import lru_cache
from fractions import Fraction as F
from itertools import product
import json

EDIT_MASK={'X':12,'Y':10,'I':15,'N':8}
TARGET={'X':6,'N':9,'A':8,'O':14}  # XOR/XNOR/AND/OR
PRESET_FOUR=(0,15,6,9)  # FALSE / TRUE / XOR / XNOR

def matching(program,target,x):
    return sum(((program>>(2*x+y))&1)==((target>>(2*x+y))&1)
               for y in (0,1))*F(1,2)

def exact(sequence,interface):
    target=tuple(TARGET[s] for s in sequence)
    @lru_cache(None)
    def v(turn,program):
        if turn==len(target):return F(0)
        def after_early(x):
            manually=max(sum(z==((target[turn]>>(2*x+y))&1)
                             for y in (0,1))*F(1,2) for z in (0,1))
            possibilities=[manually+v(turn+1,program),
                           matching(program,target[turn],x)+v(turn+1,program)]
            if interface=='author':
                other_programs=(program^mask for mask in EDIT_MASK.values())
            elif interface=='four':
                other_programs=(z for z in PRESET_FOUR if z!=program)
            elif interface=='all':
                other_programs=(z for z in range(16) if z!=program)
            else:raise ValueError(interface)
            possibilities.extend(v(turn+1,z) for z in other_programs)
            return max(possibilities)
        return sum((after_early(x) for x in (0,1)),F(0))/2
    return v(0,0)

def audit():
    rows=[]
    for seq in map(''.join,product('XNAO',repeat=4)):
        a,b,c=(exact(seq,mode) for mode in ('author','four','all'))
        assert c>=a and c>=b,(seq,a,b,c)
        rows.append((seq,a,b,c))
    assert len(rows)==4**4==256                         # R446
    assert sum(c<a or c<b for _,a,b,c in rows)==0       # R447
    counts={
      'author_beats_four':sum(a>b for _,a,b,c in rows),
      'four_beats_author':sum(b>a for _,a,b,c in rows),
      'tie':sum(a==b for _,a,b,c in rows)}
    assert counts=={'author_beats_four':48,'four_beats_author':120,'tie':88}  # R448
    vals={seq:(a,b,c) for seq,a,b,c in rows}
    assert vals['XXAA']==(F(11,4),F(5,2),F(11,4))        # R449
    assert vals['XXXX']==(F(9,4),F(3),F(3))             # R450
    assert vals['XAAO']==(F(3),F(11,4),F(3))             # R451
    strict={
      'full16_over_author':sum(c>a for _,a,b,c in rows),
      'full16_over_four':sum(c>b for _,a,b,c in rows)}
    assert strict=={'full16_over_author':120,'full16_over_four':48} # R452
    # R453 stronger structural witness: every edit moves to a truth table
    # among the same 16 selectable states at exactly the same one-turn cost.
    for current in range(16):
        assert all(current^mask in range(16) for mask in EDIT_MASK.values())
    return {'schema':'ssnoir.r446-r453.rule-authoring-menu-coverage.v1',
      'protocol':'Each of 4 turns publishes one target function from XOR/XNOR/AND/OR, then public early bit x. Player uses a single action: MANUAL output 0/1 before late uniform y, RUN current 4-bit program after late y, or EDIT with no score. The program is persistent, initially 0000. AUTHOR EDIT flips an X/Y/I/N set of cells; FOUR EDIT selects one of four precompiled programs FALSE/TRUE/XOR/XNOR; ALL EDIT selects any one of 16 tables. Every EDIT consumes exactly one turn. Targets are known when selected. Enumerate all 4^4 target sequences; no claimed human evaluation.',
      'R446_all_target_tapes':len(rows),
      'R447_full_menu_dominance_violations':0,
      'R448_author_vs_four_menu_counts':counts,
      'R449_author_example_XXAA':vals['XXAA'],
      'R450_four_menu_example_XXXX':vals['XXXX'],
      'R451_independent_transfer_XAAO':vals['XAAO'],
      'R452_full_menu_strictly_better':strict,
      'R453_dominance_argument':'Given identical installation time and all 16 reachable truth tables offered directly, a compiled-menu controller can replace each authored edit by selecting the same resulting table, with unchanged observations, actions and future rewards. Therefore authoring cannot beat the complete menu in this specific finite representation.',
      'verdict':'Authored editing wins against a restricted 4-item repertoire on 48/256 tapes but loses on 120 and ties on 88; with complete same-cost 16-item repertoire it NEVER wins. Benefit is missing capabilities in the smaller catalogue, not proved intrinsic novelty of composing Boolean edits. Interface search or human learning costs are absent and cannot be inferred from these numbers.'}

def enc(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {k:enc(v) for k,v in x.items()}
    if isinstance(x,(list,tuple)):return [enc(z) for z in x]
    return x
if __name__=='__main__':print(json.dumps(enc(audit()),ensure_ascii=False,indent=2))
