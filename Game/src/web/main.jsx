import React, { useEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import { createWorldHostRuntime } from "../runtime/world-host";
import { WebGameSession, seededRandom } from "./session";
import { CityScene } from "./city-scene";
import { CityMap } from "./city-map";
import "./styles.css";

const SAVE_KEY = "ssnoir-web-v1";
function randomSeed() {
  const raw = new URLSearchParams(location.search).get("seed");
  if (raw !== null) {
    const seed = Number(raw);
    if (!Number.isSafeInteger(seed) || seed < 0 || seed > 0xffffffff) throw new Error("Invalid seed");
    return seed;
  }
  return crypto.getRandomValues(new Uint32Array(1))[0];
}
function Clock({ value }) {
  const ratio = value.max > 0 ? Math.max(0, Math.min(100, 100 * value.current / value.max)) : 0;
  return <div className="clock">
    <div className="clock-heading"><span>{value.displayLabel}</span><b>{value.current} / {value.max}</b></div>
    <div className="clock-track"><div className={"clock-fill " + value.style} style={{width: ratio + "%"}} /></div>
    {value.note && <p className="clock-note">{value.note}</p>}
  </div>;
}
function NodeCard({ node, locked, onOpen }) {
  const type = node.isPlace ? "地点" : node.childCount ? "进入" :
    node.action === "roll" ? (node.risk || "检定") :
    node.action === "instant" ? "行动" : node.action ? "信息" : "事件";
  const cost = node.requires.map(req =>
    req.type === "die" ? "行动骰" : req.itemId + " × " + req.qty).join(" + ");
  return <button type="button" className="node-card" disabled={locked || node.disabled}
    onClick={() => onOpen(node.id)} data-testid="game-entry" data-node-id={node.id}>
    <span className="node-icon">{node.isPlace ? "◆" : node.childCount ? "◇" : "·"}</span>
    <span className="node-main">
      <span className="node-title">{node.title}</span>
      {node.subtitle && <span className="node-subtitle">{node.subtitle}</span>}
      {cost && <span className="node-cost">{cost}</span>}
      {node.clocks.map(c => <Clock key={c.label} value={c} />)}
    </span>
    <span className="node-type">{type}</span>
  </button>;
}
function DossierPanel({ entries, close }) {
  return <div className="dossier-screen">
    <section className="dossier-sheet" role="dialog" aria-modal="true" aria-label="卷宗">
      <header className="dossier-heading">
        <div><div className="eyebrow">DOSSIER / THE CITY</div><h2>卷宗</h2></div>
        <button type="button" data-testid="dossier-close" onClick={close}>关闭</button>
      </header>
      {entries.length===0 ? <p className="muted">目前没有记录中的故事。</p> :
        <div className="dossier-entries" data-testid="dossier-entries">
          {entries.map(entry => {
            const next=entry.status==="了结"?null:entry.steps.find(step=>!step.done);
            const completed=entry.steps.filter(step=>step.done).length;
            return <article key={entry.id} className={"dossier-entry " + (entry.primary?"primary":"")}>
              <div className="dossier-meta">
                <span>{entry.primary?"主线":entry.kind}</span>
                <span>{entry.status}</span>
              </div>
              <h3>{entry.id}</h3>
              {entry.now && <p className="dossier-now">{entry.now}</p>}
              {entry.where && <p className="dossier-where">地点：{entry.where}</p>}
              {next && <p className="dossier-next">下一步：{next.text}</p>}
              {entry.steps.length>0 && <p className="dossier-progress">已办妥 {completed} 项</p>}
              {entry.clocks.map(c=><Clock key={c.label} value={c}/>)}
            </article>;
          })}
        </div>}
    </section>
  </div>;
}
function App() {
  const [session, setSession] = useState(null);
  const [state, setState] = useState(null);
  const [fatal, setFatal] = useState("");
  const [seed, setSeed] = useState(null);
  const [toast, setToast] = useState("");
  useEffect(() => {
    let disposed = false;
    let unlisten = null;
    const boot = async () => {
      const nextSeed = randomSeed();
      const host = await createWorldHostRuntime(seededRandom(nextSeed));
      if (disposed) return;
      const game = new WebGameSession(host);
      unlisten = game.subscribe(() => setState(game.getSnapshot()));
      setSession(game);
      setState(game.getSnapshot());
      setSeed(nextSeed);
      // Production acceptance reads the current, displayed projection only.
      // Never expose native hosts, Scheme handles, or mutation commands on Pages.
      window.__SSNOIR_WEB__ = import.meta.env.DEV ? {
        snapshot: () => game.getSnapshot(),
        open: id => game.open(id),
        endTurn: () => game.endTurn(),
        seed: nextSeed
      } : Object.freeze({
        snapshot: () => structuredClone(game.getSnapshot())
      });
    };
    boot().catch(error => { if (!disposed) setFatal(String(error?.stack || error)); });
    return () => { disposed = true; unlisten?.(); delete window.__SSNOIR_WEB__; };
  }, []);
  async function save() {
    try {
      const text = await session.exportSave();
      localStorage.setItem(SAVE_KEY, text);
      setToast("已保存到当前浏览器");
    } catch (error) { setToast(String(error)); }
  }
  async function load() {
    const text = localStorage.getItem(SAVE_KEY);
    if (!text) { setToast("当前浏览器没有存档"); return; }
    await session.importSave(text);
    setToast(session.getSnapshot().error ? "读档失败，请检查提示" : "游戏已从存档恢复");
  }
  if (fatal) return <main className="fatal"><h1>游戏启动失败</h1><pre>{fatal}</pre></main>;
  if (!state) return <main className="loading"><span>SSNoir</span><p>正在读取城市与故事…</p></main>;
  const locked = state.busy || Boolean(state.story);
  const navigationLocked = locked || Boolean(state.selectedAction);
  const picked = new Set(state.selectedDice.map(x => x.actorId + ":" + x.slotId));
  const redundantResult = state.lastAction &&
    state.message === state.lastAction.title+" · "+state.lastAction.result;
  return <div className="game-app" data-testid="game-ready">
    <header className="topbar">
      <div className="identity"><span className="brand">SSNoir</span><span className="brand-subtitle">贝尔维尔的夜晚</span></div>
      <div className="stats"><span>第 <b>{state.day}</b> 日</span><span>金钱 <b>{state.money}</b></span><span>伤势 <b>{state.health}</b></span><span>可用行动 <b>{state.dice.length}</b></span></div>
      <div className="top-actions">
        <button data-testid="dossier-toggle" disabled={navigationLocked || state.mode!=="world"}
          onClick={()=>session.toggleDossier()}>卷宗</button>
        <button data-testid="save-game" disabled={navigationLocked || state.mode !== "world"} onClick={save}>存档</button>
        <button data-testid="load-game" disabled={navigationLocked} onClick={load}>读档</button>
        <button className="end-turn" disabled={navigationLocked || (state.mode === "world" && state.path[0] === "家")}
          onClick={() => state.mode === "world" ? session.goHome() : session.endTurn()} data-testid="end-turn">
          {state.mode === "world" ? "回旅馆休息" : "下一回合"}
        </button>
      </div>
    </header>
    <main className="workspace">
      <section className="scene-panel" aria-label="城市舞台">
        {state.mode === "world" && state.path.length === 0 ?
          <CityMap places={state.entries.filter(entry=>entry.isPlace)} restReasons={state.restReasons} locked={navigationLocked}
            onOpen={id=>session.open(id)}/> : <CityScene />}
        <div className="scene-label">
          <div className="eyebrow">{state.mode === "world" ? "THE CITY" : "ENCOUNTER"}</div>
          <h1>{state.path.length ? state.title : state.mode === "world" ? "贝尔维尔" : state.title}</h1>
          <p>{state.subtitle || "在这座城市，每一天都需要选择。"}</p>
        </div>
      </section>
      <section className="actions-panel">
        <div className="panel-heading">
          <div><div className="eyebrow">{state.mode === "world" ? "ACTIONS & PLACES" : "SCENARIO"}</div>
            <h2>{state.path.length ? state.title : "眼前的事情"}</h2></div>
          <button className="back" data-testid="back" disabled={state.busy || Boolean(state.story) || (!state.path.length && !state.selectedAction)}
            onClick={() => session.back()}>返回</button>
        </div>
        <div className="entries" data-testid="game-entries">
          {state.clocks.map(c => <Clock key={c.label} value={c} />)}
          {state.entries.length === 0 ? <p className="empty">这里暂时没有可以进行的行动。</p> :
            state.entries.map(node => <NodeCard key={node.id} node={node} locked={navigationLocked}
              onOpen={id => session.open(id)} />)}
        </div>
      </section>
      <aside className="sidebar" aria-label="行动资源与状态">
        <section className="hud-dice">
          <div className="eyebrow">ACTION DICE / 行动资源</div>
          <div className="hud-section-head"><h2>行动骰</h2><span>{state.dice.length} 枚可用</span></div>
          {state.dice.length ? <div className="dice">
            {state.dice.map(die => <button className={"die " + (picked.has(die.actorId + ":" + die.slotId) ? "picked" : "")}
              key={die.actorId + ":" + die.slotId}
              disabled={!state.selectedAction || locked || picked.has(die.actorId + ":" + die.slotId)}
              title={die.actorName + " · " + die.value}
              onClick={() => session.pickDie(die.actorId, die.slotId)}>
              <strong>{die.value}</strong><small>{die.actorName}</small>
            </button>)}
          </div> : <p className="muted">今天的骰子已用完。</p>}
          {state.selectedAction && <p className="dice-prompt" data-testid="dice-prompt">
            为「{state.entries.find(x => x.id === state.selectedAction)?.title || state.selectedAction}」投入骰子：
            {state.selectedDice.length} / {state.requiredDice}
          </p>}
        </section>
        <section className="hud-actors">
          <div className="eyebrow">CONDITION / 人物状态</div>
          <div className="hud-section-head"><h2>你与同伴</h2><span>伤势 {state.health}</span></div>
          {state.actors.map(actor => <div key={actor.id} className="actor">
            <span>{actor.name}</span><span>{actor.status === "active" ? "冷静 " + actor.composure : "暂时离队"}</span>
          </div>)}
        </section>
        <section className="hud-inventory">
          <div className="eyebrow">INVENTORY / 物品栏</div>
          <div className="hud-section-head"><h2>随身物品</h2><span>{state.inventory.length} 类</span></div>
          {state.inventory.length ? <div className="inventory-items">
            {state.inventory.map(item=><div className="inventory-item" key={item.id}>
              <span>{item.id}</span><b>× {item.quantity}</b>
            </div>)}
          </div> : <p className="muted">背包里没有物品。</p>}
        </section>
        {(state.restBlocked || state.lastAction || state.message || state.error || toast) &&
        <section className="hud-events" aria-live="polite">
          {state.restBlocked && state.mode === "world" &&
            <div className="notice" data-testid="rest-blockers"><b>今天还有必须处理的事情</b>
              {state.restReasons.map(item => <p key={item.id}>{item.reason}</p>)}
            </div>}
          {state.lastAction && <div className="action-result" data-testid="action-result">
            <strong>{state.lastAction.title} · {state.lastAction.result}</strong>
            {state.lastAction.changes.map((c,i)=>
              <div key={c.label+":"+i} className="action-change">
                <span>{c.label}</span><b>{c.delta>0?"+":""}{c.delta}</b>
              </div>)}
          </div>}
          {state.message && !redundantResult && <p className="notice" data-testid="game-message">{state.message}</p>}
          {state.error && <p className="error" role="alert">{state.error}</p>}
          {toast && <p className="notice">{toast}</p>}
        </section>}
      </aside>
    </main>
    {state.dossierOpen && <DossierPanel entries={state.dossiers} close={()=>session.toggleDossier()} />}
    {state.story && <div className="story-screen" role="dialog" aria-modal="true">
      <div className="story-card">
        <div className="eyebrow">STORY · {state.story.kind}</div>
        <h2>{state.story.speaker || "故事"}</h2>
        <p>{state.story.text || "场景演出"}</p>
        <button onClick={() => session.continueStory()} data-testid="story-continue">
          继续 <span>({state.storyCount})</span>
        </button>
      </div>
    </div>}
  </div>;
}
createRoot(document.getElementById("app")).render(<App />);
