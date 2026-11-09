import ssnoirStdlib from "../../../UnityClient/Assets/Resources/Content/scripts/stdlib.scm?raw";
import ssnoirEngine from "../../../UnityClient/Assets/Resources/Content/scripts/engine.scm?raw";
import ssnoirTheatre from "../../../UnityClient/Assets/Resources/Content/scripts/theatre.scm?raw";
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
    }
  );
  if (await session.evaluateNumber('(item-count "金钱")') !== 15) {
    throw new Error("Original game runtime cash initialization mismatch");
  }
  return { state, session };
}
