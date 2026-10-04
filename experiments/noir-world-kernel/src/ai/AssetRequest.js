const TYPES=new Set(['puppet','prop']);

export function normalizeAssetRequest(input){
  if(!input||typeof input!=='object') throw new Error('Asset request must be an object.');
  const id=String(input.id??'').trim();
  if(!/^[a-z0-9][a-z0-9._-]+$/i.test(id)) throw new Error('Asset request id must be a stable catalog id.');
  const type=String(input.type??'prop');
  if(!TYPES.has(type)) throw new Error(`Unsupported asset type: ${type}`);
  const subject=String(input.subject??'').trim();
  if(!subject) throw new Error('Asset request requires subject.');

  return {
    id,type,subject,
    view:String(input.view??(type==='puppet'?'full-body three-quarter view':'isolated three-quarter view')).trim(),
    action:String(input.action??'').trim(),
    constraints:Array.isArray(input.constraints)?input.constraints.map(String):[],
    targetHeight:Number(input.targetHeight??(type==='puppet'?3.15:1.5)),
    seed:Number.isFinite(Number(input.seed))?Number(input.seed):null,
    model:String(input.model??'recraftv4_1_vector'),
    styleId:input.styleId?String(input.styleId):null,
    styleMatch:input.styleMatch?String(input.styleMatch):null
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
