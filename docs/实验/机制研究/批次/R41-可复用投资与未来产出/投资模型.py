"""R41冻结投资1、三回合目标16。有限数学；非正式内容。"""
from pathlib import Path
from functools import lru_cache
import runpy, json, time, gc, hashlib
HERE = Path(__file__).resolve().parent
K = runpy.run_path(str(HERE.parent/'R21-伤势与目标偏好/伤势模型.py'))
ODDS, HANDS, PAY = K['ODDS'], K['HANDS'], K['PAY']
GOAL = 16
POLICIES = ('自由', '无投资', '最多一次尝试', '仅首回合投资')

class Model:
    def __init__(self, objective='completion', policy='自由'):
        assert objective in ('completion', 'early') and policy in POLICIES
        self.objective, self.policy = objective, policy
        self.value = lru_cache(None)(self._value)
        self.end = lru_cache(None)(self._end)
        self.decisions = {}
    spend = staticmethod(K['Model'].spend)
    @staticmethod
    def fail(k): return (0., 0., 0., float(k>0), 0., 0.)
    def score(self, v):
        return sum(v[:3]) if self.objective == 'completion' else 3*v[0]+2*v[1]+v[2]
    def choose(self, cs):
        top = max(self.score(v) for v,ch in cs)
        pool = [(v,ch) for v,ch in cs if top-self.score(v)<=1e-12]
        if self.objective == 'early':
            top = max(sum(v[:3]) for v,ch in pool)
            pool = [(v,ch) for v,ch in pool if top-sum(v[:3])<=1e-12]
        for j in (3,4):
            low = min(v[j] for v,ch in pool)
            pool = [(v,ch) for v,ch in pool if v[j]-low<=1e-12]
        top = max(v[5] for v,ch in pool)
        return next((v,ch) for v,ch in pool if top-v[5]<=1e-12)
    def win(self, c,k,t):
        v = [0.]*6; v[3-t] = 1.; v[3:] = [float(k>0), float(k), float(c)]
        return tuple(v)
    def _end(self,p,f,u,c,k,q,t):
        out = [0.]*6
        for w,nc,nk,nq in self.spend(c,k,q,1):
            if nk>=7 or t<=1: children = [(1.,self.fail(nk))]
            elif p+f>=GOAL: children = [(1.,self.win(nc,nk,t-1))]
            else:
                children = [(z,self.value(p+f,f,u,nc,nk,nq,t-1,h))
                            for h,z in HANDS[3 if nk>=4 else 4]]
            for z,v in children:
                for j in range(6): out[j] += w*z*v[j]
        return tuple(out)
    def action_value(self,p,f,u,c,k,q,t,h,route,act,d):
        rest = list(h); rest.remove(d); rest = tuple(rest); out = [0.]*6
        for w,(gain,dc) in zip(ODDS(d,0 if 1<=k<=3 and q else 1),PAY[act]):
            if not w: continue
            for z,nc,nk,nq in self.spend(c,k,q,-dc):
                if nk>=7: v = self.fail(nk)
                else:
                    np = min(GOAL,p+gain) if route=='推进' else p
                    nf = int(bool(f or gain>0)) if route=='投资' else f
                    nu = int(bool(u or route=='投资')) if self.policy=='最多一次尝试' else 0
                    v = self.value(np,nf,nu,nc,nk,nq,t,rest)
                for j in range(6): out[j] += w*z*v[j]
        return tuple(out)
    def candidates(self,p,f,u,c,k,q,t,h):
        routes = ['推进']
        if not f and self.policy!='无投资' and not (self.policy=='最多一次尝试' and u) and not (self.policy=='仅首回合投资' and t!=3):
            routes.append('投资')
        return [(self.end(p,f,u,c,k,q,t),('结束回合',None,None))] + [
            (self.action_value(p,f,u,c,k,q,t,h,r,a,d),(r,a,d))
            for r in routes for a in ('稳做','快做') for d in sorted(set(h))]
    def _value(self,p,f,u,c,k,q,t,h):
        if k>=7: return self.fail(k)
        if p==GOAL: return self.win(c,k,t)
        assert 0<=p<GOAL and f in (0,1) and u in (0,1) and 1<=t<=3
        v,ch = self.choose(self.candidates(p,f,u,c,k,q,t,h))
        self.decisions[(p,f,u,c,k,q,t,h)] = ch
        return v
    def summary(self,forced_first=False):
        start = time.perf_counter(); out = [0.]*6; perhand = []
        for h,w in HANDS[4]:
            args = (0,0,0,3,0,0,3,h)
            if forced_first:
                v,ch = self.choose([(self.action_value(*args,'投资',a,min(h)),('投资',a,min(h))) for a in ('稳做','快做')])
            else: v = self.value(*args)
            perhand.append(dict(hand=h,probability=w,value=v))
            for j in range(6): out[j] += w*v[j]
        return dict(objective=self.objective,policy='首手最低骰投资' if forced_first else self.policy,
                    value=out,actual_completion=sum(out[:3]),early_reward=3*out[0]+2*out[1]+out[2],
                    perhand=perhand,states=self.value.cache_info().currsize,seconds=time.perf_counter()-start)

def main():
    rows = []
    for obj in ('completion','early'):
        for policy in POLICIES:
            m = Model(obj,policy); row = m.summary(); rows.append(row)
            print(json.dumps({k:v for k,v in row.items() if k!='perhand'},ensure_ascii=False),flush=True)
            if policy=='自由':
                row = m.summary(True); rows.append(row)
                print(json.dumps({k:v for k,v in row.items() if k!='perhand'},ensure_ascii=False),flush=True)
            m.value.cache_clear(); m.end.cache_clear(); del m; gc.collect()
    for obj in ('completion','early'):
        base = next(r for r in rows if r['objective']==obj and r['policy']=='自由')
        for r in rows:
            if r['objective']!=obj: continue
            r['gap_to_free'] = Model(obj).score(base['value'])-Model(obj).score(r['value'])
            for x,y in zip(base['perhand'],r['perhand']):
                assert x['hand']==y['hand']
                assert Model(obj).score(x['value'])>=Model(obj).score(y['value'])-1e-10
    result = dict(batch='R41',goal=GOAL,investment_threshold=1,payout='仅进入第二、三回合各+1；税／7伤／期限先处理；产出完成算新回合',
                  results=rows,scope='共享R21校准输入，公平独立骰与首次部位均匀；完整伤势；正式0、真人0；两目标分别优化')
    (HERE/'投资结果.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__': main()
