const TYPES=new Set(['puppet','prop']);
const STYLE_MATCHES=new Set(['regular','precise','flexible']);

export function normalizeAssetRequest(input){
  if(!input||typeof input!=='object') throw new Error('Asset request must be an object.');

  const id=String(input.id??'').trim();
  if(!/^[a-z0-9][a-z0-9._-]+$/i.test(id)) throw new Error('Asset request id must be a stable catalog id.');

  const type=String(input.type??'prop');
  if(!TYPES.has(type)) throw new Error(`Unsupported asset type: ${type}`);

  const subject=String(input.subject??'').trim();
  if(!subject) throw new Error('Asset request requires subject.');

  const targetHeight=Number(input.targetHeight??(type==='puppet'?3.15:1.5));
  if(!Number.isFinite(targetHeight)||targetHeight<=0||targetHeight>12){
    throw new Error('targetHeight must be a finite number in (0, 12].');
  }

  let seed=null;
  if(input.seed!==undefined&&input.seed!==null){
    seed=Number(input.seed);
    if(!Number.isInteger(seed)||seed<0) throw new Error('seed must be a non-negative integer.');
  }

  const model=String(input.model??'recraftv4_1_vector');
  if(!model.endsWith('_vector')) throw new Error(`Vector asset requests require a *_vector model, got ${model}`);

  const styleMatch=input.styleMatch?String(input.styleMatch):null;
  if(styleMatch&&!STYLE_MATCHES.has(styleMatch)){
    throw new Error(`Unsupported styleMatch: ${styleMatch}`);
  }
  if(styleMatch){
    const v4=model.startsWith('recraftv4');
    const v23=model.startsWith('recraftv2')||model.startsWith('recraftv3');
    if(v4&&styleMatch==='regular') throw new Error('Recraft V4/V4.1 styleMatch must be flexible or precise.');
    if(v23&&styleMatch!=='regular') throw new Error('Recraft V2/V3 styleMatch must be regular.');
  }

  return {
    id,type,subject,
    view:String(input.view??(type==='puppet'?'full-body three-quarter view':'isolated three-quarter view')).trim(),
    action:String(input.action??'').trim(),
    constraints:Array.isArray(input.constraints)?input.constraints.map(String).map(x=>x.trim()).filter(Boolean):[],
    targetHeight,
    seed,
    model,
    styleId:input.styleId?String(input.styleId):null,
    styleMatch
  };
}

export function buildVectorPrompt(request){
  const req=normalizeAssetRequest(request);
  const lines=[
    'Create one isolated editable vector asset for a graphic noir adventure game.',
    `Subject: ${req.subject}.`,
    `View/composition: ${req.view}.`,
    req.action?`Pose/action: ${req.action}.`:null,
    'Visual language: flat black silhouette, thin warm-ivory contour lines, very sparse muted gold accent only when structurally useful.',
    'Use clean economical shapes, elegant asymmetry, long confident curves, and readable negative space.',
    'Transparent empty background. No scenery, no frame, no border, no drop shadow.',
    'No gradients, no photorealistic shading, no texture, no 3D rendering, no raster effects.',
    'Keep interior detail sparse. The silhouette must remain legible at small size.',
    'Prefer a small number of editable paths over many tiny fragments.',
    'Palette intent: near-black ink, warm ivory line/light, optional muted gold accent.'
  ].filter(Boolean);
  for(const item of req.constraints) lines.push(`Constraint: ${item}.`);
  return lines.join(' ');
}
