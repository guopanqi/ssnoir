"""R07：最小关系筛选，沿用坏/中/好共同收益，不是游戏运行器。"""
from pathlib import Path
from functools import lru_cache
from itertools import combinations_with_replacement
from math import factorial
import json,re,hashlib,time

ROOT=next(p for p in Path(__file__).resolve().parents if (p/'AGENTS.md').exists())
SOURCE=ROOT/'Engine/Runtime/Core/FateStrip.cs'
source=SOURCE.read_text()
# 直接读取本次官方源码的分布映射，不维护第二张长期概率表。
entries=re.findall(r'(<= 1|[2-6]|_) => \((\d), (\d)\)',source)
assert len(entries)==7,entries
TABLE={key:(int(b),int(m),6-int(b)-int(m)) for key,b,m in entries}
def odds(die,modifier=0):
    b=die+1+modifier
    return tuple(n/6 for n in TABLE['<= 1' if b<=1 else '_' if b>=7 else str(b)])
old=json.loads((ROOT/'docs/实验/机制研究/第二轮/定稿/数学分析/赔率.json').read_text())['1']['ordinary']
assert all(all(abs(a-b)<1e-12 for a,b in zip(odds(int(d)),v)) for d,v in old.items())
HANDS=[]
for h in combinations_with_replacement(range(1,7),4):
    n=factorial(4)
    for d in set(h):n//=factorial(h.count(d))
    HANDS.append((h,n/6**4))
assert len(HANDS)==126 and abs(sum(p for _,p in HANDS)-1)<1e-12
PAYOFF={'稳健':((0,1),(0,0),(1,0)),'风险':((0,1),(1,1),(2,0))}

class Model:
    def __init__(self,mode,penalty=2,bonus=.5,symmetry=True,policy='求解'):
        self.mode,self.penalty,self.bonus,self.symmetry,self.policy=mode,penalty,bonus,symmetry,policy
        self.calls=0
        self.value=lru_cache(None)(self._value)
    def canonical(self,a,b,h):
        if self.mode=='封口' and self.symmetry and a>b:a,b=b,a
        return a,b,tuple(sorted(h))
    def terminal(self,a,b):
        if self.mode=='封口' and a<=0 and b<=0:return (1.,1.,0.,0.)
        if self.mode=='收尾' and a<=0:return (1.+self.bonus*(b<=0),1.,float(b<=0),0.)
        return None
    def rank(self,v):return (round(v[0],12),round(v[1],12),-round(v[3],12))
    def route_set(self,a,b,h):
        available=[i for i,r in enumerate((a,b)) if r>0]
        if self.policy=='求解':return available
        if self.mode=='封口':
            if self.policy=='先近':return [min(available,key=lambda i:((a,b)[i],i))]
            if self.policy=='先远':return [max(available,key=lambda i:((a,b)[i],-i))]
            if self.policy=='压近终点':
                incomplete=[i for i in available if (a,b)[i]>1]
                return incomplete or available
        if self.mode=='收尾':
            if self.policy=='先必需':return [0]
            if self.policy=='先额外':return [1] if b>0 else [0]
            if self.policy=='留够容量':return [1] if b>0 and len(h)>((a+1)//2) else [0]
        raise AssertionError(self.policy)
    def action_value(self,a,b,h,route,action,die):
        rest=list(h);rest.remove(die);rest=tuple(rest)
        mod=-self.penalty if self.mode=='封口' and min(a,b)==0 else 0
        total=[0.,0.,0.,0.]
        for pr,(gain,cost) in zip(odds(die,mod),PAYOFF[action]):
            if not pr:continue
            aa,bb=(max(0,a-gain),b) if route==0 else (a,max(0,b-gain))
            v=self.value(*self.canonical(aa,bb,rest))
            for i in range(4):total[i]+=pr*(v[i]+(cost if i==3 else 0))
        return tuple(total)
    def candidates(self,a,b,h):
        result=[]
        for route in self.route_set(a,b,h):
            for die in sorted(set(h)):
                for action in PAYOFF:
                    result.append((self.action_value(a,b,h,route,action,die),(route,action,die)))
        result.append(((0.,0.,0.,1.),('结束回合',None,None)))
        return result
    def _value(self,a,b,h):
        self.calls+=1
        done=self.terminal(a,b)
        if done:return done
        if not h:return (0.,0.,0.,1.)
        return max(self.candidates(a,b,h),key=lambda x:self.rank(x[0]))[0]
    def choose(self,a,b,h):
        # 输入可保留原路线身份；递归才做对称化。
        return max(self.candidates(a,b,tuple(sorted(h))),key=lambda x:self.rank(x[0]))[1]
    def summary(self,a=2,b=2):
        start=time.perf_counter();v=[0.]*4
        for h,p in HANDS:
            q=self.value(*self.canonical(a,b,h))
            for i in range(4):v[i]+=p*q[i]
        return dict(mode=self.mode,penalty=self.penalty,bonus=self.bonus,policy=self.policy,
                    expected_reward=v[0],completion=v[1],bonus_completion=v[2],expected_composure=v[3],
                    unique_states=self.calls,seconds=time.perf_counter()-start)

def main():
 results={};models={}
 for mode,policies in [('封口',['求解','先近','先远','压近终点']),('收尾',['求解','先必需','先额外','留够容量'])]:
     results[mode]=[]
     for policy in policies:
         m=Model(mode,policy=policy);models[(mode,policy)]=m
         results[mode].append(m.summary())
         print(mode,policy,'完成',round(results[mode][-1]['completion'],5),
               '收益',round(results[mode][-1]['expected_reward'],5),'状态',m.calls,flush=True)
 
 # 相同目标与规则，仅比较缓存状态的规范化；先确认值一致再报告节省。
 plain=Model('封口',symmetry=False);plain_result=plain.summary()
 normal=results['封口'][0]
 assert abs(plain_result['completion']-normal['completion'])<1e-12
 symmetry=dict(dice_ordered_hands=1296,dice_multisets=126,
               goals_uncanonicalized_states=plain.calls,goals_canonicalized_states=normal['unique_states'],
               values_equal=True)
 
 # 查出有限条件反例：强制某路线/行动第一步，之后恢复全局求解。
 witnesses={}
 for mode in ('封口','收尾'):
     m=models[(mode,'求解')];found=[]
     for a,b in [(1,2),(2,1),(1,1),(2,2)]:
         for n in (2,3,4):
             for h in combinations_with_replacement(range(1,7),n):
                 best=m.value(*m.canonical(a,b,h));cs=m.candidates(a,b,h)
                 grouped={}
                 for value,choice in cs:
                     if choice[0]=='结束回合':continue
                     route=choice[0]
                     if route not in grouped or m.rank(value)>m.rank(grouped[route][0]):grouped[route]=(value,choice)
                 if len(grouped)!=2:continue
                 delta=grouped[0][0][0]-grouped[1][0][0]
                 if abs(delta)<.01:continue
                 choice=0 if delta>0 else 1
                 # 封口关心是否值得让接近完成的一边保持未完成。
                 if mode=='封口' and not (a==1 and b==2):continue
                 found.append(dict(remaining=[a,b],hand=h,best_route=choice,
                                   gap=abs(delta),alternatives={str(k):dict(value=v,choice=c) for k,(v,c) in grouped.items()}))
     witnesses[mode]=found[:8]
     print(mode,'严格路线反例',len(found),'首例',found[:1],flush=True)
 
 ablations=[]
 for mod in (0,1,3):ablations.append(Model('封口',penalty=mod).summary())
 for bonus in (.25,1.):ablations.append(Model('收尾',bonus=bonus).summary())
 
 result=dict(evidence_level='model_only; official FateStrip source; one-round four-dice, composure5, skill1',
             source_sha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest(),source_path=str(SOURCE.relative_to(ROOT)),
             payoff=PAYOFF,results=results,symmetry=symmetry,witnesses=witnesses,ablations=ablations,
             limitations=['没有新伤势：四骰及失败到期最多花5冷静',
                          '收尾收益1+0.5额外完成只是明确的研究偏好，不是游戏金钱',
                          '严格条件反例不等于自然可达或真人犹豫频率',
                          '计时为本机一次执行，不宣称稳定性能基准'])
 Path(__file__).with_name('筛选结果.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')

if __name__=='__main__':main()
