// Neon official line paths -> weighted 2D skeleton -> Three.js ribbon geometry.
// No raster replacement: every rendered stroke begins as a segment in a .neon.json.
const clamp = (v,a=0,b=1) => Math.max(a,Math.min(b,v));
const smooth = (a,b,v) => {const t=clamp((v-a)/(b-a));return t*t*(3-2*t)};
const rot = (p,c,a) => {
  const dx=p.x-c.x, dy=p.y-c.y, cs=Math.cos(a), sn=Math.sin(a);
  return {x:c.x+dx*cs-dy*sn,y:c.y+dx*sn+dy*cs};
};
const mix = (a,b,w) => ({x:a.x+(b.x-a.x)*w,y:a.y+(b.y-a.y)*w});
const p = (x,y) => ({x,y});

export function getAutoBind(data) {
  const all=data.paths.flat();
  if(!all.length) throw new Error('Empty Neon paths');
  const y0=Math.max(...all.map(a=>a[1])), y1=Math.min(...all.map(a=>a[1]));
  const cx=.5, h=y0-y1;
  // Neon is bottom-up in source coordinates: high y means head, low y means feet.
  // Proportional joints are a starting hypothesis, not artist-authored rigging.
  const sy=y0-h*.365, hy=y0-h*.645, ky=y0-h*.815;
  return {
    centerX:cx, footY:y1, height:h,
    hip:p(cx,hy), chest:p(cx,y0-h*.41), neck:p(cx,y0-h*.265), head:p(cx,y0-h*.135),
    shoulderL:p(cx-.067,sy), elbowL:p(cx-.110,y0-h*.53), handL:p(cx-.125,y0-h*.70),
    shoulderR:p(cx+.067,sy), elbowR:p(cx+.110,y0-h*.53), handR:p(cx+.125,y0-h*.70),
    hipL:p(cx-.04,hy), hipR:p(cx+.04,hy),
    kneeL:p(cx-.049,ky), kneeR:p(cx+.049,ky),
    ankleL:p(cx-.06,y1), ankleR:p(cx+.06,y1)
  };
}

function warp(v,pose,b) {
  if(!pose || pose.rigid) return v;
  const a=pose, cx=b.centerX, y=v.y, hx=b.hip.y, sy=b.shoulderL.y;
  const lower=smooth(hx+.055,hx-.055,y);
  let q=v;
  const l=smooth(cx+.022,cx-.065,v.x), r=smooth(cx-.022,cx+.065,v.x);
  const la=(a.legL||0)*lower*l, ra=(a.legR||0)*lower*r;
  if(la) q=mix(q,rot(q,b.hipL,la),l);
  if(ra) q=mix(q,rot(q,b.hipR,ra),r);
  // Knee pivots follow their parent thighs, keeping ankle motion dependent on hips.
  const knees=[['L',l,la],['R',r,ra]];
  for (const [side,sideWeight,parentAngle] of knees) {
    const knee=b['knee'+side], hip=b['hip'+side], movedKnee=rot(knee,hip,parentAngle);
    const w=sideWeight*smooth(knee.y+.035,knee.y-.055,y);
    const angle=(a['knee'+side]||0)*w;
    if(angle) q=rot(q,movedKnee,angle);
  }
  const upper=1-smooth(hx+.065,hx-.07,y);
  const torsoAngle=(a.torso||0)*upper;
  if(torsoAngle) q=rot(q,b.hip,torsoAngle);
  const torsoPivot=vertex=>rot(vertex,b.hip,a.torso||0);
  const armWeight=(side)=> {
    const flank=side==='L'?smooth(cx-.018,cx-.115,v.x):smooth(cx+.018,cx+.115,v.x);
    const vertical=smooth(sy+.055,sy-.10,y)*(1-smooth(hx-.14,hx-.30,y));
    return flank*vertical;
  };
  for(const side of ['L','R']) {
    const w=armWeight(side), angle=(a['arm'+side]||0)*w;
    if(angle) q=rot(q,torsoPivot(b['shoulder'+side]),angle);
    const ew=w*smooth(b['elbow'+side].y+.065,b['elbow'+side].y-.035,y);
    const el=(a['elbow'+side]||0)*ew;
    if(el) q=rot(q,torsoPivot(b['elbow'+side]),el);
  }
  const hw=1-smooth(b.neck.y+.01,b.neck.y-.11,y);
  if(hw && a.head) q=rot(q,torsoPivot(b.neck),a.head*hw);
  return q;
}

function collectSegments(data) {
  const segments=[];
  for(const stroke of data.paths) {
    for(let i=1;i<stroke.length;i++) {
      const a=stroke[i-1], b=stroke[i];
      if(Math.hypot(a[0]-b[0],a[1]-b[1])>1e-5)
        segments.push([p(a[0],a[1]),p(b[0],b[1])]);
    }
  }
  return segments;
}

export function makeNeonActor(THREE,source,options={}) {
  const outer=new THREE.Group();
  const shadow=new THREE.Mesh(new THREE.CircleGeometry(1,32),new THREE.MeshBasicMaterial({color:0x000000,transparent:true,opacity:.47,depthWrite:false}));
  shadow.scale.set(.72,.10,1); shadow.position.set(0,.055,-.03);outer.add(shadow);
  const material=new THREE.MeshBasicMaterial({
    color:options.color||0xe7e0d1,side:THREE.DoubleSide,transparent:true,opacity:options.opacity??1,
    depthTest:false,depthWrite:false
  });
  const mesh=new THREE.Mesh(new THREE.BufferGeometry(),material);
  mesh.frustumCulled=false;outer.add(mesh);
  const guide=new THREE.Group();outer.add(guide);
  const guides=[];
  for(let i=0;i<14;i++) {
    const dot=new THREE.Mesh(new THREE.CircleGeometry(.045,12),
      new THREE.MeshBasicMaterial({color:i<5?0xc8ac64:0x65b7bc,depthTest:false}));
    dot.position.z=.03;guide.add(dot);guides.push(dot);
  }
  const order=['hip','chest','neck','head','shoulderL','elbowL','handL','shoulderR','elbowR','handR','hipL','hipR','kneeL','kneeR'];
  let data,bind,segments,buf;
  function setData(next) {
    if(next===data)return;
    if(!Array.isArray(next.paths)||!next.paths.length)throw new Error('Malformed Neon data');
    // Neon UV uses an origin at the bottom-left (v increases upward). Convert
    // once to the top-down convention used by the estimated rig and pose warp.
    const topDown={...next,paths:next.paths.map(stroke=>stroke.map(([x,y])=>[x,1-y]))};
    data=next; bind=getAutoBind(topDown);segments=collectSegments(topDown);
    buf=new Float32Array(segments.length*18);
    const geom=new THREE.BufferGeometry();
    geom.setAttribute('position',new THREE.BufferAttribute(buf,3).setUsage(THREE.DynamicDrawUsage));
    mesh.geometry.dispose();mesh.geometry=geom;
  }
  function pointToLocal(v,unit) {
    return [(v.x-bind.centerX)*unit,(v.y-bind.footY)*unit];
  }
  function update(t,pose={},settings={}) {
    const unit=settings.unit||4.95, radius=settings.stroke||.0195;
    outer.position.set(settings.x||0,settings.y??-2.50,settings.z||0);
    outer.rotation.z=settings.tilt||0;
    outer.scale.x=settings.flip?-1:1;
    const active=settings.articulate===false?{rigid:true}:pose;
    let offset=0;
    for(const seg of segments) {
      const a=pointToLocal(warp(seg[0],active,bind),unit);
      const b=pointToLocal(warp(seg[1],active,bind),unit);
      const dx=b[0]-a[0],dy=b[1]-a[1],dist=Math.hypot(dx,dy)||1;
      const nx=-dy/dist*radius/2, ny=dx/dist*radius/2;
      const verts=[
        a[0]+nx,a[1]+ny,0,
        a[0]-nx,a[1]-ny,0,
        b[0]+nx,b[1]+ny,0,
        b[0]+nx,b[1]+ny,0,
        a[0]-nx,a[1]-ny,0,
        b[0]-nx,b[1]-ny,0
      ];
      buf.set(verts,offset);offset+=18;
    }
    mesh.geometry.attributes.position.needsUpdate=true;
    mesh.geometry.computeBoundingSphere();
    guide.visible=!!settings.showJoints;
    if(guide.visible)order.forEach((name,i)=>{
      const q=pointToLocal(warp(bind[name],active,bind),unit);
      guides[i].position.set(q[0],q[1],.04);
    });
    shadow.scale.x=.65+Math.abs(settings.tilt||0)*1.5;
  }
  setData(source);
  return {group:outer,update,setData,get bind(){return bind},get segmentCount(){return segments.length}};
}
