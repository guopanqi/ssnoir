"""R06 的条件模型：读取第二轮已记录的公开赔率，不运行或修改正式内容。

整备部分只比较未达目标上限时的单轮平均进度。
收益口径部分是单轮、三颗剩余骰、冷静5的条件题面；到期额外花1冷静。
三颗骰及到期最多花4冷静，没有溢出伤势；不模拟整个三回合交锋。
完成价值1，冷静权重lambda只是敏感性分析，不是游戏奖励或玩家偏好。
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations_with_replacement
from pathlib import Path
import hashlib
import json

ROOT = next(p for p in Path(__file__).resolve().parents if (p / 'AGENTS.md').exists())
BASE = ROOT / 'docs/实验/机制研究/第二轮/定稿'
raw = (BASE / '数学分析/赔率.json').read_bytes()
recorded = json.loads(raw)['1']

def probabilities(values):
    result = tuple(F(v).limit_denominator(6) for v in values)
    assert sum(result) == 1
    assert all(abs(float(a) - b) < 1e-12 for a, b in zip(result, values))
    return result

ordinary = {int(d): probabilities(p) for d, p in recorded['ordinary'].items()}
prepared = {int(d): probabilities(p) for d, p in recorded['prepared']['整备借力'].items()}

def exact(n):
    return {'fraction': str(n), 'decimal': float(n)}

def progress(p):
    return sum(pr * n for pr, n in zip(p, (0, 2, 4)))

prep_table = []
for die in range(1, 7):
    unprepared, boosted = progress(ordinary[die]), progress(prepared[die])
    prep_table.append({'die': die, 'ordinary_progress': exact(unprepared),
                       'prepared_progress': exact(boosted),
                       'marginal_progress_from_preparation': exact(boosted - unprepared)})
assert prep_table[-1]['marginal_progress_from_preparation']['fraction'] == '0'
prep_example = {
    'hand': [1, 2, 5, 6], 'scope': '本轮初始目标距离30，不裁切；平均进度，不是最终完成率',
    'low_high_pairs': exact(F(2) + progress(prepared[6]) + progress(prepared[5])),
    'direct_high_pair_low': exact(progress(ordinary[6]) + progress(ordinary[5]) + 1 + progress(prepared[2])),
    'all_direct': exact(sum(progress(ordinary[d]) for d in (1, 2, 5, 6)))
}
assert prep_example['low_high_pairs']['fraction'] == '10'
assert prep_example['direct_high_pair_low']['fraction'] == '37/3'
assert prep_example['all_direct']['fraction'] == '35/3'

# 用户所述的共同收益口径：每项为（进度，冷静消耗），依次坏、中、好。
OUTCOMES = {'稳健': ((0, 1), (0, 0), (1, 0)),
            '风险': ((0, 1), (1, 1), (2, 0))}
HANDS = tuple(combinations_with_replacement(range(1, 7), 3))

def analyze(weight):
    def rank(v):
        return (v[0] - weight * v[1], v[0], -v[1])

    @lru_cache(None)
    def first(remaining, hand, action, die):
        rest = list(hand)
        rest.remove(die)
        chance = expense = F(0)
        for prob, (gain, paid) in zip(ordinary[die], OUTCOMES[action]):
            future = value(remaining - gain, tuple(rest))
            chance += prob * future[0]
            expense += prob * (paid + future[1])
        return chance, expense, action, die

    @lru_cache(None)
    def value(remaining, hand):
        if remaining <= 0:
            return F(1), F(0), '完成', None
        choices = [(F(0), F(1), '结束回合', None)]
        choices.extend(first(remaining, hand, action, die)
                       for die in sorted(set(hand)) for action in OUTCOMES)
        return max(choices, key=rank)

    def best_action(remaining, hand, action):
        return max((first(remaining, hand, action, die) for die in sorted(set(hand))), key=rank)

    counts = {'steady_strictly_better': 0, 'risk_strictly_better': 0,
              'equal_utility': 0, 'end_turn_optimal': 0}
    switches = 0
    for hand in HANDS:
        strict_actions = set()
        for remaining in range(1, 7):
            if value(remaining, hand)[2] == '结束回合':
                counts['end_turn_optimal'] += 1
                continue
            steady = best_action(remaining, hand, '稳健')
            risk = best_action(remaining, hand, '风险')
            delta = (steady[0] - weight * steady[1]) - (risk[0] - weight * risk[1])
            if delta > 0:
                counts['steady_strictly_better'] += 1
                strict_actions.add('稳健')
            elif delta < 0:
                counts['risk_strictly_better'] += 1
                strict_actions.add('风险')
            else:
                counts['equal_utility'] += 1
        switches += len(strict_actions) == 2
    assert sum(counts.values()) == 56 * 6

    def row(remaining, hand, action):
        v = best_action(remaining, hand, action)
        return {'action': action, 'die': v[3], 'completion': exact(v[0]),
                'expected_composure_spent_including_timeout': exact(v[1]),
                'utility': exact(v[0] - weight * v[1])}

    witnesses = []
    if weight == F(1, 2):
        for remaining in (1, 2):
            rows = [row(remaining, (1, 2, 2), action) for action in OUTCOMES]
            witnesses.append({'hand': [1, 2, 2], 'remaining': remaining, 'alternatives': rows,
                              'best': value(remaining, (1, 2, 2))[2]})
        assert witnesses[0]['best'] == '稳健' and witnesses[1]['best'] == '风险'
        assert witnesses[0]['alternatives'][0]['completion']['fraction'] == '26/27'
        assert witnesses[1]['alternatives'][1]['completion']['fraction'] == '11/12'
    return {'composure_weight': str(weight), 'conditional_states': 336,
            'counts': counts, 'hands_with_strict_action_reversal': switches, 'witnesses': witnesses}

sensitivity = [analyze(w) for w in (F(0), F(1, 4), F(1, 2), F(1), F(3, 2))]
assert sensitivity[0]['counts']['steady_strictly_better'] == 0

# 最后一骰、还差1：风险中档多花的1冷静抵消了稳健未完成的回合末1冷静。
last_die = []
for die, (bad, neutral, good) in sorted(ordinary.items()):
    steady_cost = bad + (1 - good)
    risk_cost = bad + neutral + bad
    assert steady_cost == risk_cost
    assert neutral + good >= good
    last_die.append({'die': die, 'remaining': 1, 'steady_completion': exact(good),
                     'risk_completion': exact(neutral + good),
                     'both_expected_composure_cost': exact(steady_cost)})

result = {
    'evidence_level': 'model_only_conditional; no new formal content or native sessions',
    'odds_source': 'docs/实验/机制研究/第二轮/定稿/数学分析/赔率.json',
    'odds_sha256': hashlib.sha256(raw).hexdigest(),
    'source_content_revision': json.loads((BASE / '版本.json').read_text())['ContentRevision'],
    'assumptions': ['技能1；沿用已记录公开赔率', '条件题面有3颗剩余骰，冷静5，结束这一轮即超时',
                    '无恢复品；最多消耗4冷静，无伤势溢出；不求整场交锋或可达频率',
                    '目标函数 P(完成)-lambda*E(冷静消耗)；lambda是研究敏感性参数',
                    '冷静消耗统计全部分支；完成即停止；失败回合末消耗1'],
    'preparation': {'per_die': prep_table, 'example': prep_example},
    'canonical_payoffs': OUTCOMES, 'sensitivity': sensitivity,
    'last_die_remaining_one': last_die,
    'analysis_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
}
path = Path(__file__).with_name('条件分析.json')
path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
print('整备例：固定低高配对10；直接高骰、低骰配对37/3；全直接35/3。')
for r in sensitivity:
    print('冷静权重', r['composure_weight'], r['counts'], '严格切换手牌', r['hands_with_strict_action_reversal'])
print('6种骰面的末骰/差1支配关系已核对；条件分析已保存。')
