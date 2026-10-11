/**
 * Extract one balanced Scheme expression from immutable authored source.
 * Used only for the opt-in development screenshot, never for story logic.
 */
export function extractStageExpression(source:string,needle="(play-stage!"):string {
  const start=source.indexOf(needle);
  if(start<0)throw Error("authored stage expression missing: "+needle);
  let depth=0,quote=false,escape=false,comment=false;
  for(let i=start;i<source.length;i++){
    const char=source[i];
    if(comment){if(char==="\n")comment=false;continue;}
    if(quote){
      if(escape){escape=false;continue;}
      if(char==="\\"){escape=true;continue;}
      if(char==='"')quote=false;
      continue;
    }
    if(char===";"){comment=true;continue;}
    if(char==='"'){quote=true;continue;}
    if(char==="(")depth++;
    if(char===")"&&--depth===0)return source.slice(start,i+1);
  }
  throw Error("unterminated authored Scheme stage expression");
}
