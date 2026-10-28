import test from "node:test";
import assert from "node:assert/strict";
import {GameRuntimeState} from "../src/runtime/game-state.ts";
import {createNativeBridge} from "../src/runtime/native-bridge.ts";

test("native current actor is only available while an action is executing",()=>{
 const state=new GameRuntimeState(),n=createNativeBridge(state);
 assert.equal(n["__game-mode"](),"world");
 assert.throws(()=>n["__current-actor"](),/only valid inside action/);
 state.beginActionContext("player","scenario");
 assert.equal(n["__current-actor"](),"player");
 assert.equal(n["__game-mode"](),"scenario");
 assert.throws(()=>state.beginActionContext("player","world"),/nested/);
 state.clearActionContext();
 assert.throws(()=>n["__current-actor"]());
});
