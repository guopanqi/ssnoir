import type { RuntimeNode } from "./node-converter.ts";
export type RestRequirement = { id: string; reason: string; location: string; node: string };

function hasAction(node:RuntimeNode):boolean {
  if(node.disabled)return false;
  if(node.isPlace&&node.arrivals.length>0)return true;
  if(node.resolve&&node.resolve.kind!=="observe"&&node.resolve.kind!=="note")return true;
  return node.children.some(hasAction);
}

/** Mirrors C# RequiredBeforeRest.Collect; only authored nodes create rest obligations. */
export function collectRequired(root:RuntimeNode, world:boolean):RestRequirement[] {
  const result:RestRequirement[]=[];
  const visit=(node:RuntimeNode,place:RuntimeNode|null,ancestorsEnabled:boolean):void=>{
    if(node.isPlace)place=node;
    const enabled=ancestorsEnabled&&!node.disabled;
    if(node.requiredBeforeRest!=null){
      if(!world||!place)throw new Error("Required node "+node.name+" must belong to a world place");
      if(!enabled||!hasAction(node))throw new Error("Required node "+node.name+" has no enabled processing path");
      result.push({id:node.name,reason:node.requiredBeforeRest,location:place.name,node:node.name});
    }
    for(const child of node.children)visit(child,place,enabled);
  };
  visit(root,null,true);
  return result;
}
export function sameRequired(a:RestRequirement[],b:RestRequirement[]):boolean {
  return a.length===b.length&&a.every((x,i)=>x.id===b[i].id&&x.reason===b[i].reason&&
    x.location===b[i].location&&x.node===b[i].node);
}
