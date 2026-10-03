"""橱窗里的脸的一次性实验策略；仅通过玩家会话协议观察、行动。"""
import argparse, json, pathlib, subprocess

ROOT = pathlib.Path(__file__).resolve().parents[3]

def run(policy, seed, output):
    p = subprocess.Popen(['dotnet', 'run', '--no-build', '--project', 'tools/content-validator/SSNoir.ContentValidator.csproj', '--', '--session', 'encounters/实验·橱窗里的脸', '--seed', str(seed)], cwd=ROOT, stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True)
    def read():
        line = p.stdout.readline()
        if not line: raise RuntimeError('会话提前退出')
        x = json.loads(line)
        if x['type'] == 'error': raise RuntimeError(x['message'])
        return x
    def send(x):
        p.stdin.write(json.dumps(x, ensure_ascii=False)+'\n'); p.stdin.flush()
        return read()
    obs = read()['observation']
    trace = []
    for _ in range(90):
        if obs['EncounterResult'] is not None or not obs['IsInEncounter']: break
        clocks = {c['Label']: c['Current'] for c in obs['Clocks']}
        stage, distance = clocks['路程'], clocks['距离']
        ops = obs['Operations']
        actor = obs['Actors'][0]
        def pick(card, high=False):
            available = [o for o in ops if o['Card'] == card]
            return (max if high else min)(available, key=lambda o: o['DieValue'] or 0) if available else None
        # 所有策略都处理身体资源；避免把每回合时间税误当成模式失败。
        chosen, reason = None, ''
        if actor['Composure'] <= 2 and obs['Inventory'].get('香烟', 0) > 0:
            chosen, reason = pick('抽烟'), '冷静不足，为剩余路线留缓冲'
        if not chosen:
            window = stage in (0, 2, 4)
            look = stage in (1, 4, 5)
            listen_available = any(c['Name']=='贴近偷听' for c in obs['Cards'])
            # 路段中的偷听卡消失，是本段已经尝试过的公开反馈。
            listened = any(t['stage']==stage and t['card']=='贴近偷听' for t in trace)
            want = window and not listened and policy not in ('safe', 'idle')
            if policy == 'selective' and stage == 4: want = False
            if policy == 'late' and stage == 0: want = False
            if want and distance > 1:
                chosen, reason = pick('跟近一步'), '靠近本段即将消失的线索'
            elif want and listen_available:
                chosen, reason = pick('贴近偷听', True), '把好骰留给唯一一次偷听'
            elif policy == 'reckless' and distance > 1:
                chosen, reason = pick('跟近一步'), '始终贴近，检验贪心打法'
            elif policy != 'idle' and policy != 'reckless':
                target = 3 if look else (3 if stage in (2,3) else 2)
                if policy == 'planned' and stage == 3: target = 1
                if distance < target: chosen, reason = pick('拉开一步'), '回头前藏开；听完不继续贴身'
                elif distance > target: chosen, reason = pick('跟近一步'), '避免加速后距离达到5'
        if not chosen:
            chosen = next(o for o in ops if o['Kind']=='end-turn')
            reason = '接受当前位置和即将发生的回应'
        trace.append({'stage':stage, 'distance':distance, 'suspicion':clocks['疑心'], 'memory':clocks['认脸'], 'card':chosen['Card'] or chosen['Kind'], 'reason':reason})
        obs = send({'command':'act', 'version':obs['Version'], 'operationId':chosen['Id'], 'reason':reason})['observation']
    else: raise RuntimeError('策略没有在90步内结束')
    output.mkdir(parents=True, exist_ok=True)
    path = output / f'{policy}-{seed}.json'
    send({'command':'save','path':str(path.resolve())})
    send({'command':'quit'}); p.wait()
    return {'policy':policy,'seed':seed,'result':obs['EncounterResult'],'trace':trace,'injury':obs['Injury']}

if __name__ == '__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('--output', type=pathlib.Path, default=pathlib.Path('/tmp/noir-window-trials')); parser.add_argument('--seeds', nargs='+',type=int,default=[7,11,23])
    args=parser.parse_args()
    results=[run(policy,seed,args.output) for policy in ['safe','selective','late','greedy','planned','reckless','idle'] for seed in args.seeds]
    (args.output/'summary.json').write_text(json.dumps(results,ensure_ascii=False,indent=2))
    for r in results: print(r['policy'],r['seed'],r['result'],r['injury'])
