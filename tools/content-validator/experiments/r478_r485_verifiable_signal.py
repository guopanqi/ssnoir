#!/usr/bin/env python3
"""R478-485: what can a player actually infer from another agent's message?"""
from itertools import product
from fractions import Fraction as F
import json
TYPES='AB'
def enumerate_pure(messages,legal,cost,verified=None):
    games=[];verified=verified or {}
    for signals in product(*(legal[t] for t in TYPES)):
        sent=dict(zip(TYPES,signals))
        for guesses in product(TYPES,repeat=len(messages)):
            reply=dict(zip(messages,guesses))
            if any(reply[k]!=v for k,v in verified.items()):continue
            # Every independently informed type must maximize its OWN
            # benefit from being correctly understood net of evidence cost.
            if any(F(reply[sent[t]]==t)-cost(t,sent[t]) <
                   max(F(reply[m]==t)-cost(t,m) for m in legal[t]) for t in TYPES):continue
            # For every observed message, the receiver best classifies the
            # equally likely types consistent with the sender strategy.
            if any(sum(reply[m]==t for t in TYPES if sent[t]==m) <
                   sum(guess==t for t in TYPES if sent[t]==m)
                   for m in messages for guess in TYPES):continue
            accuracy=sum(reply[sent[t]]==t for t in TYPES)*F(1,2)
            own=sum(F(reply[sent[t]]==t)-cost(t,sent[t]) for t in TYPES)/2
            games.append({'sender':sent,'reader':reply,'accuracy':accuracy,
                          'sender_own':own,'pooling':sent['A']==sent['B']})
    return games
def price(v,unpriced):return lambda t,m:F(0) if m in unpriced else F(v)
def stats(xs):return {'count':len(xs),'pool':sum(x['pooling'] for x in xs),
    'separate':sum(not x['pooling'] for x in xs),
    'accuracy_range':[min(x['accuracy'] for x in xs),max(x['accuracy'] for x in xs)]}
def audit():
    plain=('0','1');all_plain={'A':plain,'B':plain}
    plain0=enumerate_pure(plain,all_plain,price(0,('0',)))
    assert stats(plain0)=={'count':6,'pool':4,'separate':2,'accuracy_range':[F(1,2),F(1)]} #478-479
    plaincost=enumerate_pure(plain,all_plain,price(F(1,4),('0',)))
    assert stats(plaincost)=={'count':4,'pool':2,'separate':2,'accuracy_range':[F(1,2),F(1)]} #480
    proof=('U','PA','PB')
    constrained={'A':('U','PA'),'B':('U','PB')}
    verified={'PA':'A','PB':'B'}
    prooflow=enumerate_pure(proof,constrained,price(F(1,4),('U',)),verified)
    assert stats(prooflow)=={'count':2,'pool':0,'separate':2,'accuracy_range':[F(1),F(1)]} #481
    assert all(x['sender_own']==F(7,8) for x in prooflow)                #482
    assert {x['sender']['A'] for x in prooflow}=={'U','PA'}              #483
    proofzero=enumerate_pure(proof,constrained,price(0,('U',)),verified)
    assert stats(proofzero)=={'count':4,'pool':0,'separate':4,'accuracy_range':[F(1),F(1)]} #484
    atone=enumerate_pure(proof,constrained,price(1,('U',)),verified)
    costly=enumerate_pure(proof,constrained,price(2,('U',)),verified)
    assert stats(atone)=={'count':4,'pool':2,'separate':2,'accuracy_range':[F(1,2),F(1)]}
    assert stats(costly)=={'count':2,'pool':2,'separate':0,'accuracy_range':[F(1,2),F(1,2)]} #485
    return {'schema':'ssnoir.r478-r485.verifiable-signaling.v1',
      'protocol':'Sender has persistent private type A/B, prior 1/2. Receiver guesses type after one message. Sender independently maximizes its OWN success of correct recognition minus any certificate cost; receiver wants accurate classification. Exhaust pure Bayesian Nash: each type best responds, receiver best on all used messages. CHEAP messages are freely chosen by either type, off-path responses are not identified. In PROOF protocol type A alone can create PA and B alone PB; successful physical verification binds the receiver to that factual interpretation even off-path. U is unverified. There is one message, no repeated human interaction.',
      'R478_cheap_talk_all_pure_equilibria':stats(plain0),
      'R479_cheap_talk_accuracy_unidentified':stats(plain0)['accuracy_range'],
      'R480_generic_costly_unverified_message':stats(plaincost),
      'R481_type_bound_verifiable_proof_cost_quarter':stats(prooflow),
      'R482_sender_own_payoff_under_proof':prooflow[0]['sender_own'],
      'R483_which_type_pays_not_identified':[x['sender'] for x in prooflow],
      'R484_zero_cost_verifiable_proof':stats(proofzero),
      'R485_proof_cost_threshold':{'cost_one':stats(atone),'cost_two':stats(costly)},
      'equilibria':{'cheap':plain0,'generic_costly':plaincost,
                    'proof_quarter':prooflow,'proof_one':atone,'proof_two':costly},
      'verdict':'Even with common interest in correct recognition, cheap talk has both pooling and separating equilibria; a generic cost on one unverifiable message does not solve this. Costly type-bound physical evidence with a binding meaning eliminates pooling if its price is below the benefit of truthful recognition; which type pays remains undetermined. With proof cost>=1 pooling returns. This is standard one-shot signaling, NOT a validated new, repeatedly playable social-understanding loop.'}
def enc(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):enc(v) for k,v in x.items()}
    if isinstance(x,(list,tuple)):return [enc(z) for z in x]
    return x
if __name__=='__main__':print(json.dumps(enc(audit()),ensure_ascii=False,indent=2))
