import { buildVectorPrompt, normalizeAssetRequest } from '../AssetRequest.js';

const DEFAULT_BASE='https://external.api.recraft.ai/v1';

export class RecraftVectorProvider{
  constructor({
    apiToken=process.env.RECRAFT_API_TOKEN,
    baseUrl=process.env.RECRAFT_BASE_URL??DEFAULT_BASE
  }={}){
    this.apiToken=apiToken;
    this.baseUrl=baseUrl.replace(/\/$/,'');
  }

  async generate(input){
    if(!this.apiToken) throw new Error('RECRAFT_API_TOKEN is required for live vector generation.');
    const request=normalizeAssetRequest(input);
    if(!request.model.endsWith('_vector')) throw new Error(`Recraft vector provider requires a vector model, got ${request.model}`);

    const body={
      prompt:buildVectorPrompt(request),
      model:request.model,
      n:1,
      response_format:'b64_json'
    };
    if(request.seed!==null) body.random_seed=request.seed;
    if(request.styleId) body.style_id=request.styleId;
    if(request.styleMatch) body.style_match=request.styleMatch;

    const response=await fetch(`${this.baseUrl}/images/generations/vector`,{
      method:'POST',
      headers:{
        Authorization:`Bearer ${this.apiToken}`,
        'Content-Type':'application/json'
      },
      body:JSON.stringify(body),
      signal:AbortSignal.timeout(180_000)
    });

    const text=await response.text();
    if(!response.ok) throw new Error(`Recraft generation failed (${response.status}): ${text.slice(0,1200)}`);

    let payload;
    try{payload=JSON.parse(text);}catch{throw new Error('Recraft returned non-JSON metadata response.');}
    const encoded=payload?.data?.[0]?.b64_json;
    if(!encoded) throw new Error('Recraft response did not contain data[0].b64_json.');

    const svg=Buffer.from(encoded,'base64').toString('utf8');
    if(!svg.includes('<svg')) throw new Error('Decoded Recraft vector result is not SVG.');

    return {
      svg,
      provider:'recraft',
      model:request.model,
      seed:request.seed,
      prompt:body.prompt,
      credits:payload.credits??null,
      styleId:payload.style_id??request.styleId??null
    };
  }
}
