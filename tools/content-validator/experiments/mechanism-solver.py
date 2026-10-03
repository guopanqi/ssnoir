"""小交锋的有限状态数学分析，不是游戏运行器。

概率从正式会话的 Odds 读取；参数从正式配置读取。回合新手牌按当前
无伤主角四枚独立均匀d6枚举。目标先最大化成功概率，概率相同时减少
成功轨迹上的期望结束回合次数；效率目标则最大化成功收益12/9/6、失败0。
浮点计算有容差，不称为解析证明。

公开/隐藏信息边界：情报用未知/已知/已押匹配/已押失配四个信息状态，
未查明前押定按公开的1/2先验求期望，绝不读取隐藏真值或未来随机序列。
"""
import argparse
from functools import lru_cache
from itertools import combinations_with_replacement
import json
from math import factorial
from pathlib import Path
import re
import runpy

ROOT=Path(__file__).resolve().parents[3]
HELPERS=runpy.run_path(str(Path(__file__).with_name('mechanism-study.py')))
HANDS=[]
for hand in combinations_with_replacement(range(1,7),4):
    weight=factorial(4)
    for die in set(hand):weight//=factorial(hand.count(die))
    HANDS.append((hand,weight/6**4))
assert abs(sum(p for _,p in HANDS)-1)<1e-12


def collect_odds(growth):
    found={}
    for seed in range(1,30):
        session=HELPERS['Session']('研究·分段兑现',seed,growth)
        try:
            for op in session.observation['Operations']:
                if op['Card']=='备料':
                    p=HELPERS['odds'](op['Odds'])
                    found[op['DieValue']]=(p['坏'],p['中'],p['好'])
        finally:session.close()
        if len(found)==6:return found
    raise RuntimeError('没有覆盖六种骰面')


class Solver:
    def __init__(self,family,variant,growth,probabilities,objective="保守"):
        self.objective=objective
        self.family,self.variant,self.growth=family,variant,growth
        path=ROOT/f'UnityClient/Assets/Resources/Content/scenes/encounters/研究·{variant}.scm'
        self.parameters={k:int(v) for k,v in re.findall(r'\(define ([^\s]+) (\d+)\)',path.read_text())}
        self.target=self.parameters['目标上限'];self.turns=self.parameters['回合上限']
        self.probabilities=probabilities
        self.boosted_probabilities=None
        self.decisions={}

    def outcomes(self,action,remaining,state,die):
        """(概率, 新剩余目标, 新机制状态, 失败)。只建模指定原型。"""
        probs=self.probabilities[die]
        if self.family=='稳险选择':
            gains=(1,2,3) if action=='稳进' else (0,1,5)
            return [(p,remaining-n,0,False) for p,n in zip(probs,gains) if p]
        if self.family=='整备借力':
            if action=='铺垫':return [(1,remaining-1,1,False)]
            if state:
                assert self.boosted_probabilities is not None,'准备赔率必须来自正式会话'
                probs=self.boosted_probabilities[die]
            return [(p,remaining-n,0,False) for p,n in zip(probs,(0,2,4)) if p]
        if self.family=='分段兑现':
            if action=='稳进':return [(1,remaining-1,state,False)]
            if action=='兑现':
                gain=state*(state+1)//2 if self.parameters['递增兑现'] else state*2
                return [(1,remaining-gain,0,False)]
            return [(p,remaining,max(0,min(5,state+n)),False) for p,n in zip(probs,(-2,1,2)) if p]
        if self.family=='余热转用':
            if action=='强推':
                return [(p,remaining-n,min(6,state+2),state+2>=6) for p,n in zip(probs,(0,1,3)) if p]
            bonus=self.parameters['热转倍率']*(state//2)
            return [(p,remaining-n-(bonus if n else 0),max(0,state-3),False) for p,n in zip(probs,(0,1,2)) if p]
        mode,alarm=state
        if action=='押定':
            return [(1,remaining,('M',alarm),False)] if mode=='K' else [(.5,remaining,('M',alarm),False),(.5,remaining,('F',alarm),False)]
        if action=='探查':
            known='K' if self.parameters['信息有效'] or mode=='K' else 'U'
            return [(probs[0],remaining,(mode,alarm),False),
                    (probs[1],remaining,(known,alarm),False),
                    (probs[2],remaining-1,(known,alarm),False)]
        bonus=1 if mode=='M' else 0
        return [(probs[0],remaining,(mode,alarm),False),
                (probs[1],remaining-1-bonus,state,False),
                (probs[2],remaining-2-bonus,state,False)]

    def actions(self,state):
        if self.family=='稳险选择':return ['稳进','冒进']
        if self.family=='整备借力':return ['铺垫','推进']
        if self.family=='分段兑现':return ['备料','稳进']+(['兑现'] if state else [])
        if self.family=='余热转用':return ['强推','借热']
        return ['探查','押定'] if state[0] in ('U','K') else ['执行方案']

    def end_state(self,state):
        if self.family=='分段兑现':return max(0,state-self.parameters['储备衰减'])
        if self.family=='余热转用':return max(0,state-1)
        if self.family=='整备借力':return 0
        return state

    @lru_cache(None)
    def next_hand(self,remaining,state,turns):
        probability,time=0.0,0.0
        for hand,weight in HANDS:
            p,t=self.value(remaining,state,turns,hand)
            probability+=weight*p;time+=weight*t
        return probability,time

    @lru_cache(None)
    def value(self,remaining,state,turns,hand):
        if remaining<=0:return (1.0,0.0)
        if turns<=0:return (0.0,0.0)
        next_state=self.end_state(state)
        # 回合结束可以弃掉剩余骰。时间税在这些条件下不足以产生伤势。
        p,t=self.next_hand(remaining,next_state,turns-1) if turns>1 else (0.0,0.0)
        # t 是只在成功轨迹上累计的负回合数；失败不会因更早结束得到奖励。
        best=(p,t-p);choice=('end-turn',None)
        for die in sorted(set(hand),reverse=True):
            rest=list(hand);rest.remove(die);rest=tuple(rest)
            for action in self.actions(state):
                p,t=0.0,0.0
                for weight,new_r,new_s,failed in self.outcomes(action,remaining,state,die):
                    if not weight or failed:continue
                    pp,tt=self.value(new_r,new_s,turns,rest)
                    p+=weight*pp;t+=weight*tt
                current_reward=(12-3*(self.turns-turns))*p+3*t
                best_reward=(12-3*(self.turns-turns))*best[0]+3*best[1]
                better=(p>best[0]+1e-12 or (abs(p-best[0])<1e-12 and t>best[1]+1e-12))
                if self.objective=="效率":
                    better=current_reward>best_reward+1e-12 or (abs(current_reward-best_reward)<1e-12 and p>best[0]+1e-12)
                if better:
                    best=(p,t);choice=(action,die)
        self.decisions[(remaining,state,turns,hand)]=choice
        return best

    def initial(self):
        state=('U',0) if self.family=='情报承诺' else 0
        return self.next_hand(self.target,state,self.turns)

    def recommend(self,o):
        c={x['Label']:x for x in o['Clocks']}
        remaining=c['目标']['Max']-c['目标']['Current']
        turns=c['已过回合']['Max']-c['已过回合']['Current']
        if self.family=='分段兑现':state=c['储备']['Current']
        elif self.family=='余热转用':state=c['余热']['Current']
        elif self.family=='整备借力':state=c['准备']['Current']
        elif self.family=='稳险选择':state=0
        else:
            cards={x['Name']:x['Text'] for x in o['Cards']}
            if '方案失配' in cards['标注：方案']:mode='F'
            elif '方案匹配' in cards['标注：方案']:mode='M'
            elif '条件是' in cards['标注：线索']:mode='K'
            else:mode='U'
            state=(mode,0)
        hand=tuple(sorted(d['Value'] for a in o['Actors'] if a['Id']=='player' for d in a['Dice']))
        if (remaining,state,turns,hand) not in self.decisions:
            self.value(remaining,state,turns,hand)
        action,die=self.decisions[(remaining,state,turns,hand)]
        if action=='押定':
            clue=next(x['Text'] for x in o['Cards'] if x['Name']=='标注：线索')
            action='押定B' if '条件是B' in clue else '押定A'
        return next(x for x in o['Operations'] if (x['Kind']=='end-turn' if action=='end-turn' else x['Card']==action and x['DieValue']==die))

    def witnesses(self):
        # 同一进度、状态、时间，换手牌导致不同动作；仅列举，不宣称这些手牌全部可达。
        bins={}
        for (remaining,state,turns,hand),choice in self.decisions.items():
            if not hand or remaining>self.target:continue
            bins.setdefault((remaining,state,turns),{}).setdefault(choice[0],(hand,choice))
        examples=[]
        for (remaining,state,turns),actions in bins.items():
            concrete={k:v for k,v in actions.items() if k!='end-turn'}
            if len(concrete)<2:continue
            values=[]
            for action,(hand,choice) in list(concrete.items())[:3]:
                score=self.value(remaining,state,turns,hand)
                # 最优与其他动作的价值差，排除纯粹平手。
                alternatives=[]
                for other in self.actions(state):
                    if other==action:continue
                    p,t=0.0,0.0
                    rest=list(hand);rest.remove(choice[1]);rest=tuple(rest)
                    for w,r,s,fail in self.outcomes(other,remaining,state,choice[1]):
                        if not w or fail:continue
                        pp,tt=self.value(r,s,turns,rest);p+=w*pp;t+=w*tt
                    alternatives.append(dict(action=other,success=p,end_turns=-t))
                values.append(dict(hand=hand,action=action,die=choice[1],success=score[0],
                                   end_turns=-score[1],same_die_alternatives=alternatives))
            examples.append(dict(remaining=remaining,state=state,turns=turns,choices=values))
            if len(examples)>=12:break
        return examples


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output',type=Path)
    parser.add_argument('--growth',type=int,nargs='+',default=[1,2])
    parser.add_argument('--objective',choices=["保守","效率"],default="保守")
    args=parser.parse_args()
    if args.output.exists():raise FileExistsError(args.output)
    args.output.mkdir(parents=True)
    rows=[];odds_by_growth={}
    for growth in args.growth:
        probabilities=collect_odds(growth);odds_by_growth[growth]=probabilities
        for family in ('余热转用','分段兑现','情报承诺'):
            for variant in HELPERS['VARIANTS'][family]:
                solver=Solver(family,variant,growth,probabilities,args.objective)
                p,t=solver.initial()
                row=dict(family=family,variant=variant,growth=growth,success=p,end_turns_on_success=(-t/p if p else None),expected_reward=12*p+3*t,objective=args.objective,
                         states=solver.value.cache_info().currsize,parameters=solver.parameters,
                         witnesses=solver.witnesses())
                rows.append(row)
                print(f'{variant} g{growth}: P={p:.6f}, 成功回合={1-t/p:.4f}, E收益={12*p+3*t:.4f}',flush=True)
                solver.value.cache_clear();solver.next_hand.cache_clear()
    (args.output/'赔率.json').write_text(json.dumps(odds_by_growth,ensure_ascii=False,indent=2)+'\n')
    (args.output/'求解.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')


if __name__=='__main__':main()
