#!/usr/bin/env python3
"""R414–421: one slot, executable rule has a LATER sensor than manual.
No simultaneous output capacity. Exact rational Bellman enumeration.
A second target schedule is a predeclared transfer/anti-overfit test.
"""
from fractions import Fraction as F
from functools import lru_cache
import json
MASK={'X':0b1100,'Y':0b1010,'I':0b1111,'N':0b1000}

def target(kind,x,y):
    return {'X':x^y,'N':1-(x^y),'A':x&y,'O':x|y}[kind]

def solve(phases,phase_length,late,see_early=True,edit_limit=None):
    n=len(phases)*phase_length
    @lru_cache(None)
    def v(t,program,edits):
        if t==n:return F(0)
        if see_early:return (q(t,program,edits,0)+q(t,program,edits,1))/2
        return q(t,program,edits,None)
    def q(t,program,edits,x):
        xs=(0,1) if x is None else (x,)
        actual=phases[t//phase_length]
        def prob(fn):return sum((F(fn(i,y)==target(actual,i,y),len(xs)*2)
                                 for i in xs for y in (0,1)),F(0))
        gain_manual=max(prob(lambda i,y,z=z:z) for z in (0,1))
        gain_program=prob(lambda i,y:(program>>(2*i+(y if late else 0)))&1)
        actions={'MANUAL':gain_manual+v(t+1,program,edits),
                 'RUN':gain_program+v(t+1,program,edits)}
        if edit_limit is None or edits<edit_limit:
            for name,mask in MASK.items():actions['EDIT-'+name]=v(t+1,program^mask,edits+1)
        return max(actions.values())
    return v(0,0,0),v.cache_info().currsize

def audit():
    baseline=F(4)
    values={
      'R414_eight_turn_full_late':solve('XN',4,True),
      'R415_manual_only_upper_bound':baseline,
      'R416_early_only_sensor':solve('XN',4,False),
      'R417_no_edits_even_with_late':solve('XN',4,True,edit_limit=0),
      'R418_cannot_condition_run_on_early_x':solve('XN',4,True,see_early=False),
      'R419_late_sensor_full_repeated':solve('XN',4,True),
      'R420_max_one_edit':solve('XN',4,True,edit_limit=1),
    }
    assert values['R414_eight_turn_full_late'][0]==F(21,4)
    assert all(x[0]==baseline for k,x in values.items() if k in
               ('R416_early_only_sensor','R417_no_edits_even_with_late'))
    assert values['R418_cannot_condition_run_on_early_x'][0]==5
    assert values['R420_max_one_edit'][0]==F(21,4)
    # R421: transfer XOR -> XNOR -> XOR in 3+3+3 publicly announced turns.
    transfer={str(k):solve('XNX',3,True,edit_limit=k) for k in (0,1,2)}
    transfer['unlimited']=solve('XNX',3,True)
    transfer['early_only']=solve('XNX',3,False)
    assert (transfer['0'][0],transfer['1'][0],transfer['2'][0],
            transfer['unlimited'][0],transfer['early_only'][0])==(
            F(9,2),F(6),F(49,8),F(49,8),F(9,2))
    values['R421_three_phase_transfer']=transfer
    return {'schema':'ssnoir.r414-r421.late-sensor-rule.v1',
      'protocol':'One action slot per turn. Early public x is seen before choosing MANUAL(prediction 0/1), RUN(current 4-cell program) or EDIT-X/Y/I/N(flip cells, no output). Fair independent y arrives after action selection. Manual never sees y; on RUN only the installed program sees both x and late y before producing one output. The target truth function is public per phase. Program initially 0000. No parallel second worker, no extra action point. Program and manual each yield at most one point. Every edit costs the only current action.',
      'results':values,
      'limit':'Exact stylized policy bound, not proof of fun. First scenario fully solved with just one edit; transfer needs two edits only for a 1/8 expected gain above best one-edit controller.'}

def enc(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):enc(v) for k,v in x.items()}
    if isinstance(x,(tuple,list)):return [enc(v) for v in x]
    return x
if __name__=='__main__':print(json.dumps(enc(audit()),ensure_ascii=False,indent=2))
