import React, { useEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import { createWorldHostRuntime } from "../runtime/world-host";
import { WebGameSession, seededRandom } from "./session";
import { CityScene } from "./city-scene";
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
    onClick={() => onOpen(node.id)} data-testid="game-entry">
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
      if (import.meta.env.DEV) {
        window.__SSNOIR_WEB__ = {
          snapshot: () => game.getSnapshot(),
          open: id => game.open(id),
          endTurn: () => game.endTurn(),
          seed: nextSeed
        };
      }
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
    setToast("读档操作已完成");
  }
  if (fatal) return <main className="fatal"><h1>游戏启动失败</h1><pre>{fatal}</pre></main>;
  if (!state) return <main className="loading"><span>SSNoir</span><p>正在读取城市与故事…</p></main>;
  const locked = state.busy || Boolean(state.story);
  const picked = new Set(state.selectedDice.map(x => x.actorId + ":" + x.slotId));
  return <div className="game-app" data-testid="game-ready">
    <header className="topbar">
      <div className="identity"><span className="brand">SSNoir</span><span className="brand-subtitle">贝尔维尔的夜晚</span></div>
      <div className="stats"><span>第 <b>{state.day}</b> 日</span><span>金钱 <b>{state.money}</b></span><span>伤势 <b>{state.health}</b></span><span>可用行动 <b>{state.dice.length}</b></span></div>
      <div className="top-actions">
        <button disabled={locked || state.mode !== "world"} onClick={save}>存档</button>
        <button disabled={locked} onClick={load}>读档</button>
        <button className="end-turn" disabled={locked || (state.mode === "world" && state.restBlocked)}
          onClick={() => session.endTurn()} data-testid="end-turn">
          {state.mode === "world" ? "结束今天" : "下一回合"}
        </button>
      </div>
    </header>
    <main className="workspace">
      <section className="scene-panel" aria-label="城市舞台">
        <CityScene />
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
          <button className="back" disabled={locked || (!state.path.length && !state.selectedAction)}
            onClick={() => session.back()}>返回</button>
        </div>
        <div className="entries" data-testid="game-entries">
          {state.clocks.map(c => <Clock key={c.label} value={c} />)}
          {state.entries.length === 0 ? <p className="empty">这里暂时没有可以进行的行动。</p> :
            state.entries.map(node => <NodeCard key={node.id} node={node} locked={locked}
              onOpen={id => session.open(id)} />)}
        </div>
      </section>
      <aside className="sidebar">
        <div className="eyebrow">PEOPLE & RESOURCES</div>
        <h2>你与同伴</h2>
        {state.actors.map(actor => <div key={actor.id} className="actor">
          <span>{actor.name}</span><span>{actor.status === "active" ? "冷静 " + actor.composure : "暂时离队"}</span>
        </div>)}
        <div className="sidebar-rule" />
        <h2>行动骰</h2>
        {state.dice.length ? <div className="dice">
          {state.dice.map(die => <button className={"die " + (picked.has(die.actorId + ":" + die.slotId) ? "picked" : "")}
            key={die.actorId + ":" + die.slotId}
            disabled={!state.selectedAction || locked || picked.has(die.actorId + ":" + die.slotId)}
            title={die.actorName + " · " + die.value}
            onClick={() => session.pickDie(die.actorId, die.slotId)}>
            <strong>{die.value}</strong><small>{die.actorName}</small>
          </button>)}
        </div> : <p className="muted">骰子已用完，可以考虑结束今天。</p>}
        {state.selectedAction && <p className="dice-prompt" data-testid="dice-prompt">
          为「{state.entries.find(x => x.id === state.selectedAction)?.title || state.selectedAction}」选择骰子：
          {state.selectedDice.length} / {state.requiredDice}
        </p>}
        {state.restBlocked && state.mode === "world" &&
          <p className="notice">还有必须处理的事情，暂时不能结束今天。</p>}
        {state.message && <p className="notice" data-testid="game-message">{state.message}</p>}
        {state.error && <p className="error" role="alert">{state.error}</p>}
        {toast && <p className="notice">{toast}</p>}
        <div className="sidebar-footer">WEB EXPERIMENT · {seed}</div>
      </aside>
    </main>
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
