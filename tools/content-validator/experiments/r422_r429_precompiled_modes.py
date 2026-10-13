#!/usr/bin/env python3
"""R422–429: same-cost PRECOMPILED modes counterexample to authored rules.
The late sensor and one output slot are unchanged from R414–421.
"""
from fractions import Fraction as F
from functools import lru_cache
import json
from r414_r421_late_sensor import solve as authored

def precompiled(phases,length,late=True,edit_limit=None):
    total=len(phases)*length
    @lru_cache(None)
    def v(t,mode,edits):
        if t==total:return F(0)
        kind=phases[t//length]
        gain=F(mode==kind) if late and mode!='Z' else F(1,2)
        options=[F(1,2)+v(t+1,mode,edits),gain+v(t+1,mode,edits)]
        if edit_limit is None or edits<edit_limit:
            options.extend(v(t+1,next_mode,edits+1)
                           for next_mode in 'XN' if next_mode!=mode)
        return max(options)
    return v(0,'Z',0)

def independent_witness(phases,length,actions):
    assert len(actions)==len(phases)*length
    program='Z';earned=F(0)
    for t,action in enumerate(actions):
        desired=phases[t//length]
        if action.startswith('SET-'):
            program=action[-1]
        elif action=='MANUAL':
            earned+=F(1,2)
        elif action=='RUN':
            earned+=F(program==desired) if program!='Z' else F(1,2)
        else:raise AssertionError(action)
    return earned

def audit():
    a=authored('XN',4,True)[0]
    b=authored('XNX',3,True)[0]
    assert a==F(21,4) and b==F(49,8)  # R422 original exact regression
    x={str(limit):precompiled('XN',4,True,limit) for limit in (0,1,2)}
    y={str(limit):precompiled('XNX',3,True,limit) for limit in (0,1,2)}
    assert (x['0'],x['1'],x['2'])==(4,F(11,2),6) # R423-424
    assert precompiled('XN',4,False)==4           # R425 late sensor causal
    assert (y['0'],y['1'],y['2'])==(F(9,2),F(13,2),F(13,2)) # R426
    assert precompiled('XNX',3,False)==F(9,2)     # R427
    sample_x=['SET-X']+['RUN']*3+['SET-N']+['RUN']*3
    sample_y=['SET-X']+['RUN']*2+['MANUAL']*3+['RUN']*3
    assert independent_witness('XN',4,sample_x)==x['2']==6  # R428
    assert independent_witness('XNX',3,sample_y)==y['1']==F(13,2) # R429
    assert x['1']>a and y['1']>b
    return {
      'schema':'ssnoir.r422-r429.precompiled-modes.v1',
      'protocol':'Exactly the same one action slot, public phase schedule, early x and late y as R414–421. Initial table all zeros (Z). Instead of having to create a truth table by flipping multiple subsets, a single-cost EDIT may choose an already compiled XOR or XNOR output function. RUN is evaluated on late (x,y); MANUAL still sees only x. Each EDIT costs the whole turn. Both cases still require the player to decide when to sacrifice immediate score.',
      'R422_original_authoring_optimum':{'XN_4_plus_4':a,'XNX_3_plus_3_plus_3':b},
      'R423_R425_precompiled_XN':{'zero_edit':x['0'],'one_edit':x['1'],'two_edit':x['2'],'late_sensor_off':precompiled('XN',4,False)},
      'R426_R427_precompiled_XNX':{'zero_edit':y['0'],'one_edit':y['1'],'two_edit':y['2'],'late_sensor_off':precompiled('XNX',3,False)},
      'R428_R429_explicit_constructive_witness':{'XN_two_mode_switches':independent_witness('XN',4,sample_x),'XNX_one_mode_switch':independent_witness('XNX',3,sample_y)},
      'verdict':'The late sensor does real work, but the authored subclause-editor is not uniquely responsible. Same-cost selection from two precompiled rules already exceeds the unrestricted authoring DP in both sampled phase schedules; XNX needs only ONE precompiled edit. No repeated rule authoring, distinct innovation or human enjoyment certified.'}
def enc(v):
    if isinstance(v,F):return str(v)
    if isinstance(v,dict):return {str(k):enc(x) for k,x in v.items()}
    if isinstance(v,(list,tuple)):return [enc(x) for x in v]
    return v
if __name__=='__main__':print(json.dumps(enc(audit()),ensure_ascii=False,indent=2))
