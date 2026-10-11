import compiledStdBase64 from "virtual:ssnoir-lips-stdlib";
import ssnoirStdlib from "../../../UnityClient/Assets/Resources/Content/scripts/stdlib.scm?raw";
import ssnoirEngine from "../../../UnityClient/Assets/Resources/Content/scripts/engine.scm?raw";
import ssnoirTheatre from "../../../UnityClient/Assets/Resources/Content/scripts/theatre.scm?raw";
import { base64Bytes } from "./base64";
import { GameRuntimeState } from "./game-state";
import { GameScriptSession } from "./script-session";

export async function createHostScriptRuntime() {
  const state = new GameRuntimeState();
  const session = await GameScriptSession.create(
    "SSNoir-game-world", state,
    {
      "scripts/stdlib.scm": ssnoirStdlib,
      "scripts/engine.scm": ssnoirEngine,
      "scripts/theatre.scm": ssnoirTheatre
    },
    base64Bytes(compiledStdBase64)
  );
  if (await session.evaluateNumber('(item-count "金钱")') !== 15) {
    throw new Error("Original game runtime cash initialization mismatch");
  }
  return { state, session };
}
