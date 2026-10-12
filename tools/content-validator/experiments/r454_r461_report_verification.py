#!/usr/bin/env python3
"""R454-R461: offstage report, verification, and feedback-timing dominance.
Four independent truth bits, fixed hidden report orientation, no fake human.
"""
from functools import lru_cache
from fractions import Fraction as F
import json

T=4

def truth_report(truth,theta):
    assert truth in (0,1) and theta in ('H','L')
    return truth if theta=='H' else 1-truth

def best(feedback='none',audit=True,reset_type=False):
    # An ordinary guess, if its correctness is reported, exactly reveals
    # whether the source is HONEST or INVERTING (stable theta only).
    # Arrival is the first turn when this previously obtained label
    # can be used by the decision maker.
    latency={'none':999,'immediate':1,'one_full_turn':2}[feedback]
    @lru_cache(None)
    def V(t,known,arrival):
        if t==T:return F(0)
        if reset_type: # current sender's orientation is redrawn every turn
            return F(1,2)+V(t+1,False,999)
        current_known=known or t>=arrival
        immediate=F(1) if current_known else F(1,2)
        next_arrival=arrival if current_known else min(arrival,t+latency)
        guess=immediate+V(t+1,current_known,next_arrival)
        inspect=V(t+1,True,999) if audit else F(-999)
        return max(guess,inspect)
    return V(0,False,999)

def audit():
    for truth in (0,1):
        assert truth_report(truth,'H')==truth
        assert truth_report(truth,'L')==1-truth       # R454
    no_evidence=best('none',audit=False)
    explicit_audit=best('none',audit=True)
    free_evidence=best('immediate',audit=False)
    free_with_audit=best('immediate',audit=True)
    lag=best('one_full_turn',audit=False)
    lag_with_audit=best('one_full_turn',audit=True)
    fresh=best('immediate',audit=True,reset_type=True)
    assert no_evidence==2                            # R455
    assert explicit_audit==3                         # R456
    assert free_evidence==F(7,2)                     # R457
    assert free_with_audit==F(7,2)                   # R458
    assert lag==3                                   # R459
    assert lag_with_audit==3                        # R460
    assert fresh==2                                 # R461
    # Constructible lower-bound scripts independently of Bellman recursion:
    assert F(4)*F(1,2)==no_evidence
    assert F(0)+3==explicit_audit                    # audit then three perfect
    assert F(1,2)+3==free_evidence                   # act once, learn orientation
    assert F(1,2)*2+2==lag                           # label usable from turn 2
    return {'schema':'ssnoir.r454-r461.report-verification.v1',
      'protocol':'4 independent uniform truth bits. Each round a source sends a report z; if stable latent theta=H then z=truth, if theta=L then z=1-truth. Player sees z and chooses TRUST(z) or INVERT(z) to predict truth for one point if correct, or spend entire turn AUDIT to learn that rounds truth and therefore theta without scoring. theta equally likely H/L. Feedback conditions: none (ordinary predictions never reveal correctness before end), immediate (correctness reported next turn), one_full_turn (correctness reported on turn t+2). If source redraws theta every turn, prior evidence no longer predicts new messages. No extra parallel player slot, no adaptive social behavior and no real person.',
      'R454_source_signal_invariants':'Both report types are equally informative a priori and raw z has no statistical ability to reveal theta; on any audited report or scored prediction with correctness feedback, theta is perfectly identified.',
      'R455_no_feedback_no_audit':no_evidence,
      'R456_paid_audit_only':explicit_audit,
      'R457_free_score_feedback_no_audit':free_evidence,
      'R458_free_feedback_plus_audit':free_with_audit,
      'R459_one_full_turn_feedback_lag_no_audit':lag,
      'R460_lag_plus_audit':lag_with_audit,
      'R461_fresh_type_every_round':fresh,
      'verdict':'This minimal cognitive delegation is a hidden-reliability diagnosis/information purchase. When normal predictions disclose correctness, first useful work also trains the player (7/2), making AUDIT weakly dominated. When ordinary feedback is completely withheld, first-turn paid AUDIT is worth 3 versus uninformed 2. Longer feedback latency reduces opportunity; iid hidden behavior kills the learned type advantage. No separate interpersonal/trust gameplay or human enjoyment established.'}

def enc(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {k:enc(v) for k,v in x.items()}
    if isinstance(x,(list,tuple)):return [enc(z) for z in x]
    return x
if __name__=='__main__':print(json.dumps(enc(audit()),ensure_ascii=False,indent=2))
