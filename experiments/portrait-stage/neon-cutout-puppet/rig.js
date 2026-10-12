// Explicitly-authored rigid cutout rig. No vertex warping or coordinate-guessing.
export class CutoutRig {
  constructor(spec, atlas) {
    if(spec.schema!==1 || !Array.isArray(spec.parts)) throw Error('Invalid CutoutRig spec');
    this.spec=spec; this.atlas=atlas;
    this.nodes=new Map();
    for(const part of spec.parts){
      if(this.nodes.has(part.id)) throw Error('Duplicate cutout ID '+part.id);
      this.nodes.set(part.id,{...part,children:[]});
    }
    for(const part of this.nodes.values()){
      if(part.parent){
        if(!this.nodes.has(part.parent)) throw Error('Missing parent '+part.parent);
        this.nodes.get(part.parent).children.push(part.id);
      }
      const [x,y,w,h]=part.crop;
      if([x,y,w,h,...part.joint,...part.sourcePivot].some(v=>!Number.isFinite(v)) || w<=0 || h<=0) throw Error('Invalid part '+part.id);
      if(x<0||y<0||x+w>spec.sourceSize[0]||y+h>spec.sourceSize[1])throw Error('Invalid crop '+part.id);
    }
    // Explicit no-cycles guarantee; helps isolate erroneous data instead of tolerating it.
    const active=new Set(),done=new Set();
    const visit=id=>{if(active.has(id))throw Error('Rig cycle '+id);if(done.has(id))return;active.add(id);const node=this.nodes.get(id);
      if(node.parent)visit(node.parent);active.delete(id);done.add(id);};
    this.nodes.forEach((_,id)=>visit(id));
    this.order=[...this.nodes.values()].sort((a,b)=>a.z-b.z);
    this.parts=Object.fromEntries([...this.nodes].map(([id,node])=>[id,node]));
  }
  // Draws from the atlas region preserving its own silhouette and entire drawing.
  render(ctx, opts={}){
    const {x=0,y=0,scale=.46,angles={},offsets={},showJoints=false,explode=0,alpha=1,flip=false}=opts;
    const [ox,oy]=this.spec.origin;
    const atlasFactor=this.atlas.width/this.spec.sourceSize[0];
    // Root coordinates: (0,0) = shoe baseline; positive y is down in the source sheet.
    ctx.save();ctx.globalAlpha*=alpha;ctx.translate(x,y);ctx.scale((flip?-1:1)*scale,scale);ctx.translate(-ox,-oy);
    const matrices=new Map();
    const getMatrix=(id)=>{
      if(matrices.has(id))return matrices.get(id);
      const node=this.nodes.get(id);
      const m=node.parent?getMatrix(node.parent).slice():[1,0,0,1,0,0];
      const [jx,jy]=node.joint;
      const parent=node.parent?this.nodes.get(node.parent).joint:[0,0];
      multiply(m,translation(jx-parent[0],jy-parent[1]));
      if(offsets[id])multiply(m,translation(offsets[id][0],offsets[id][1]));
      if(angles[id])multiply(m,rotation(angles[id]));
      matrices.set(id,m);return m;
    };
    const drawOrder=this.order;
    for(const part of drawOrder){
      const matrix=getMatrix(part.id).slice(),[x1,y1,w,h]=part.crop;
      if(explode){
        const offsets={head:[0,-245],torso:[0,-80],pelvis:[0,45],
          armUpperL:[-190,-85],armLowerL:[-270,-22],
          armUpperR:[190,-85],armLowerR:[270,-22],
          thighL:[-55,95],shinL:[-105,165],thighR:[55,95],shinR:[105,165],
          coatL:[-255,55],coatR:[255,55]};
        const c=offsets[part.id]||[0,0];
        // Offset in stage reference coordinates, never inherited by children.
        matrix[4]+=c[0]*explode;matrix[5]+=c[1]*explode;
      }
      ctx.save();ctx.transform(...matrix);
      // Bone pivot does not depend on cropped image origin. This is the main invariant.
      const dx=x1-part.sourcePivot[0],dy=y1-part.sourcePivot[1];
      ctx.drawImage(this.atlas,x1*atlasFactor,y1*atlasFactor,w*atlasFactor,h*atlasFactor,dx,dy,w,h);
      if(showJoints){
        ctx.strokeStyle='#d2ad65';ctx.fillStyle='#dfb76d';ctx.lineWidth=2.5;
        ctx.beginPath();ctx.arc(0,0,9,0,Math.PI*2);ctx.fill();
        ctx.beginPath();ctx.moveTo(-16,0);ctx.lineTo(16,0);ctx.moveTo(0,-16);ctx.lineTo(0,16);ctx.stroke();
      }
      ctx.restore();
    }
    ctx.restore();
  }
  bounds(){return {height:1055,parts:this.nodes.size};}
}
function translation(x,y){return [1,0,0,1,x,y];}
function rotation(rad){const c=Math.cos(rad),s=Math.sin(rad);return [c,s,-s,c,0,0];}
function multiply(a,b){
  const [a0,a1,a2,a3,a4,a5]=a,[b0,b1,b2,b3,b4,b5]=b;
  a[0]=a0*b0+a2*b1;a[1]=a1*b0+a3*b1;
  a[2]=a0*b2+a2*b3;a[3]=a1*b2+a3*b3;
  a[4]=a0*b4+a2*b5+a4;a[5]=a1*b4+a3*b5+a5;
}
