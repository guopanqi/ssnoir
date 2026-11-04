import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameSceneDirector} from "../src/runtime/scene-director.ts";

const root="../UnityClient/Assets/Resources/Content",sources={};
function scan(dir){
  for(const item of readdirSync(join(root,dir),{withFileTypes:true})){
    const path=join(dir,item.name).replaceAll("\\","/");
    if(item.isDirectory())scan(path);
    else if(item.isFile()&&path.endsWith(".scm"))
      sources[path]=readFileSync(join(root,path),"utf8");
  }
}
scan("scripts");scan("scenes");
const std=readFileSync("node_modules/lips/dist/std.xcb");

test("five genuine high-success recognition rolls resolve the original car identification scenario",async()=>{
  // This deterministic controller test supplies a repeatable die/random stream,
  // but uses the unmodified authored scenario, its clocks and real slot costs.
  const director=await GameSceneDirector.create(sources,std,()=>0.999);
  const originalDay=director.state.getGlobal("世界日");
  const originalDice=director.state.team.diceSnapshot();
  await director.world.evaluate(
    "(start-scenario \"雨夜跟车（识别版）\" (lambda (result) (set-global! '跟车回调结果 result)))"
  );
  await director.flush();
  assert.equal(director.mode,"scenario");
  assert.equal(director.getPreparedRoot()?.name,"雨夜追车");
  for(let count=1;count<=5;count++){
    if(director.state.team.dice("player").length===0){
      const frames=await director.endRound();
      assert(frames.length>0);
      assert.equal(director.mode,"scenario");
      assert.equal(director.state.getGlobal("世界日"),originalDay,
        "One chase round cannot advance the city's authored day");
    }
    const slot=director.state.team.dice("player")[0].slotId;
    const result=await director.actions.performSlots("辨认车辆特征",[
      {type:"die",actorId:"player",slotId:slot}
    ]);
    assert.equal(result.outcome,"success",
      "A high prepared roll with percentile 100 should use the authored success effect");
    assert(result.clockEffects.some(x=>x.label==="识别进度"&&x.delta===2));
    await director.flush();
    if(director.mode==="scenario")await director.render();
  }
  assert.equal(director.mode,"world",
    "Filling the original recognition Clock must end the scene through end-scenario");
  assert.equal(String(director.state.getGlobal("跟车回调结果")),"取得识别线索");
  assert.equal(director.state.getGlobal("世界日"),originalDay);
  assert.deepEqual(director.state.team.diceSnapshot(),originalDice,
    "Successful scene exit returns the untouched city die identities");
  await director.flush();
  assert.equal(director.mode,"world",
    "The successful transition must not leave a stale scenario restart request");
});
