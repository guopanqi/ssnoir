"""选定最优策略的正常路径统计；共享主模型，不是独立优化。"""
from pathlib import Path
from fractions import Fraction as F
from collections import defaultdict
import json,hashlib
import 专注模型 as m
HERE=Path(__file__).resolve().parent
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    model=m.Model();active=defaultdict(F)
    # first target (-1 at root); flag whether commitment selects a different target.
    for h in m.HANDS:model.solve(3,6,h);active[3,6,h,-1,-1,False,-1]=m.weight(h)
    end=[F(0)]*3;timing=defaultdict(F);changed=F(0);steps=0
    delayed=F(0);witnesses=[]
    while active:
        nxt=defaultdict(F)
        for key,mass in active.items():
            a,b,h,lock,first,switched,commit_at=key
            done=m.terminal(a,b,h)
            if done is not None:
                for i,p in enumerate(done):end[i]+=mass*p
                timing[str(commit_at) if commit_at>=0 else 'never']+=mass
                if switched:changed+=mass
                if commit_at>0:delayed+=mass
                continue
            v=model.solve(a,b,h,lock);ch=model.choices[a,b,h,lock]
            if ch[0]=='commit':
                t=ch[1];nxt[a,b,h,t,first,first>=0 and first!=t,4-len(h)]+=mass
            else:
                _,d,t,r=ch;rest=list(h);rest.remove(d)
                if lock<0:
                    bestlock=max(m.score(model.solve(a,b,h,x)) for x in (0,1))
                    if m.score(v)>bestlock:
                        witnesses.append(dict(remaining=[a,b],dice=h,visit_mass=str(mass),free=str(m.score(v)),best_immediate_commit=str(bestlock),action=ch))
                for g,p in enumerate(m.odds(d,lock)):
                    if p:
                        needs=[a,b];needs[t]=max(0,needs[t]-(g if r else int(g==2)))
                        nxt[*needs,tuple(rest),lock,t if first<0 else first,switched,commit_at]+=mass*p
        active=nxt;steps+=1;assert steps<=9
    assert sum(end)==sum(timing.values())==1
    exact=sum((m.weight(h)*m.score(model.solve(3,6,h)) for h in m.HANDS),F(0));assert m.score(end)==exact
    out=dict(batch='R45',terminal_distribution=list(map(str,end)),value=str(exact),commit_timing={k:str(v) for k,v in sorted(timing.items())},
             delayed_commit_probability=str(delayed),commit_different_from_first_advance=str(changed),
             strict_delay_witnesses=witnesses,
             input_sha256={'专注模型.py':sha(HERE/'专注模型.py')},
             scope='指定自由最优策略，平手优先立即承诺；正常模型前向，非玩家频率／独立优化；正式0真人0')
    (HERE/'承诺前向结果.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({k:v for k,v in out.items() if k!='strict_delay_witnesses'},ensure_ascii=False))
if __name__=='__main__':main()
