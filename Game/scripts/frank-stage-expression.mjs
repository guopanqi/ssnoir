import {readFileSync} from "node:fs";
const source=readFileSync("../UnityClient/Assets/Resources/Content/scenes/world/人物/弗兰克.scm","utf8");
const start=source.indexOf("(play-stage!");
if(start<0)throw Error("original Frank stage source removed");
let depth=0,quote=false,escape=false,comment=false,end=-1;
for(let i=start;i<source.length;i++){
  const ch=source[i];
  if(comment){if(ch==="\n")comment=false;continue;}
  if(quote){
    if(escape){escape=false;continue;}
    if(ch==="\\"){escape=true;continue;}
    if(ch==='"')quote=false;
    continue;
  }
  if(ch===";"){comment=true;continue;}
  if(ch==='"'){quote=true;continue;}
  if(ch==="(")depth++;
  if(ch===")"&&--depth===0){end=i+1;break;}
}
if(end<0)throw Error("unterminated source stage");
process.stdout.write(source.slice(start,end));
