import * as THREE from "three";
import { Container, Graphics, Text, WebGLRenderer as PixiRenderer } from "pixi.js";
import { LipsSession } from "../scheme/lips-session";
import { createHostScriptRuntime } from "../runtime/host-session";
import { createWorldHostRuntime, type WorldHost } from "../runtime/world-host";
import type { RuntimeNode } from "../runtime/node-converter";
import type {DossierEntry} from "../runtime/dossier-converter";
import {StoryPlaybackQueue} from "../runtime/story-player";
import {parseTheatre} from "../runtime/theatre-contract";
import {TheatrePlayback} from "../runtime/theatre-playback";
import {TheatreVectorView} from "./theatre-view";
import {StageVectorView} from "./stage-view";
import {StageImageRegistry} from "./stage-assets";
import {StagePlayback} from "../runtime/stage-playback";
import {parseStage} from "../runtime/stage-contract";
import { createFrameLoop } from "./frame-loop";
import ssnoirStdlib from "../../../UnityClient/Assets/Resources/Content/scripts/stdlib.scm?raw";
import frankAuthoredSource from "../../../UnityClient/Assets/Resources/Content/scenes/world/人物/弗兰克.scm?raw";
import {extractStageExpression} from "../runtime/authored-stage-source";

export interface FoundationOptions {
  canvas: HTMLCanvasElement;
  width: number;
  height: number;
  context?: WebGL2RenderingContext;
  animationFrame: (callback: FrameRequestCallback) => number;
  cancelAnimationFrame: (handle: number) => void;
  onError?: (error: unknown) => void;
}

export interface FoundationSession {
  increment(): Promise<void>;
  activateAt(x: number, y: number): boolean;
  getCount(): number;
  getMoney(): number;
  getRenderDiagnostics(): Record<string, boolean>;
  getStageAssetDiagnostics(): {loaded:number;pending:number;errors:string[]};
  getGamePreviewStatus(): {loaded:boolean;mode:string;day:string;entries:number;places:number;money:number;restores:number;storyKind:string;stageBeat:number;view:string;navigationMs:number|null;materializations:number|null;busy:boolean};
  resize(width: number, height: number): void;
  pause(): void;
  resume(): void;
  dispose(): void;
}

const VIRTUAL_HEIGHT = 576;
const BUTTON = { x: 50, y: 458, width: 220, height: 52 };

export async function mountFoundation(options: FoundationOptions): Promise<FoundationSession> {
  const three = new THREE.WebGLRenderer({
    canvas: options.canvas,
    context: options.context,
    antialias: true,
    stencil: true,
    powerPreference: "high-performance",
  });
  three.setPixelRatio(1);

  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0x080b0f);
  scene.fog=new THREE.FogExp2(0x080b0f,0.045);
  const camera = new THREE.PerspectiveCamera(42, options.width / options.height, 0.1, 100);
  camera.position.set(7, 7, 12);
  camera.lookAt(0, 1, 0);

  const blocks = new THREE.MeshLambertMaterial({ color: 0x222731 });
  for (let i = 0; i < 8; i++) {
    const height = 2 + (i % 4) * 1.15;
    const building = new THREE.Mesh(new THREE.BoxGeometry(1.3, height, 1.6), blocks);
    building.position.set((i - 3.5) * 1.55, height / 2, i % 2 ? -1.4 : 0.4);
    scene.add(building);
  }
  const street = new THREE.Mesh(
    new THREE.PlaneGeometry(40, 40),
    new THREE.MeshLambertMaterial({ color: 0x171b23 })
  );
  street.rotation.x = -Math.PI / 2;
  street.position.y = -0.04;
  scene.add(street);
  scene.add(new THREE.AmbientLight(0xbfc8df, 1.12));
  const lamp = new THREE.DirectionalLight(0xffe6b6, 2.55);
  lamp.position.set(-3, 9, 4);
  scene.add(lamp);

  const sharedContext = three.getContext();
  if (typeof WebGL2RenderingContext !== "undefined" &&
      !(sharedContext instanceof WebGL2RenderingContext)) {
    throw new Error("PixiJS 8 shared rendering requires WebGL2");
  }
  const pixi = new PixiRenderer();
  await pixi.init({
    context: sharedContext as WebGL2RenderingContext,
    canvas: options.canvas,
    width: options.width,
    height: options.height,
    clearBeforeRender: false
  });
  const stage = new Container();
  const search=(globalThis as {location?:{search?:string}}).location?.search??"";
  const qaMode=/(?:^|[?&])foundation-debug=1(?:&|$)/.test(search)||
    Boolean((globalThis as {__SSNOIR_FOUNDATION_DEBUG__?:boolean}).__SSNOIR_FOUNDATION_DEBUG__);
  const ivory=0xf0e9dd,brass=0xc4ad86,muted=0x9eaaac;
  const letter="Songti SC, Noto Serif CJK SC, serif";
  stage.addChild(new Graphics().roundRect(22,343,394,209,4)
    .fill({color:0x0c1117,alpha:0.94}).stroke({color:0x787268,width:1}));
  stage.addChild(new Graphics().rect(45,367,56,2).fill(brass));
  const title=new Text({text:"贝尔维尔",style:{fontFamily:letter,fontSize:33,fill:ivory}});
  title.position.set(45,380);stage.addChild(title);
  const byline=new Text({text:"尼尔 · 私人侦探",style:{fontFamily:"sans-serif",fontSize:14,fill:brass}});
  byline.position.set(47,424);stage.addChild(byline);
  const counterText=new Text({text:"一座城，三封信。",style:{
    fontFamily:"sans-serif",fontSize:16,fill:ivory}});
  counterText.position.set(47,453);stage.addChild(counterText);
  const hudLead=new Text({text:"夜莺剧院 · 老街 · 码头",style:{
    fontFamily:"sans-serif",fontSize:14,fill:muted,wordWrap:true,wordWrapWidth:337}});
  hudLead.position.set(47,486);stage.addChild(hudLead);
  // Phase 1 counter is a QA instrument, not a player-facing game mechanic.
  const debugHud=new Container();debugHud.visible=qaMode;
  debugHud.addChild(new Graphics().roundRect(22,343,394,209,4)
    .fill({color:0x131821,alpha:0.98}).stroke({color:brass,width:1}));
  const debugTitle=new Text({text:"运行时检查 / FOUNDATION",style:{
    fontFamily:"sans-serif",fontSize:20,fill:ivory}});
  debugTitle.position.set(49,370);debugHud.addChild(debugTitle);
  const debugCounter=new Text({text:"Scheme：启动中",style:{
    fontFamily:"sans-serif",fontSize:17,fill:brass}});
  debugCounter.position.set(51,422);debugHud.addChild(debugCounter);
  const button=new Graphics().roundRect(BUTTON.x,BUTTON.y,BUTTON.width,BUTTON.height,7)
    .fill(0xe6d4ac).stroke({color:ivory,width:1});
  button.eventMode="none";debugHud.addChild(button);
  const label=new Text({text:"执行 Scheme 计数",style:{
    fontFamily:"sans-serif",fontSize:17,fill:0x18191d}});
  label.position.set(65,472);debugHud.addChild(label);
  stage.addChild(debugHud);

  // Authored world pane: one shared Pixi renderer across browser and Mini Games.
  // The opening state comes from the ORIGINAL world.scm, not from hardcoded places.
  const worldPanel=new Container();
  const wx=440,wy=27,panelWidth=550;
  worldPanel.addChild(new Graphics().roundRect(wx,wy,panelWidth,412,5)
    .fill({color:0x0c1117,alpha:0.94}).stroke({color:0x676969,width:1}));
  const worldHeading=new Text({
    text:"城市 / 去向",style:{fontFamily:letter,fontSize:25,fill:ivory}
  });
  worldHeading.position.set(wx+20,wy+18);worldPanel.addChild(worldHeading);
  const worldStatus=new Text({
    text:"正在读取贝尔维尔…",style:{fontFamily:"sans-serif",fontSize:14,fill:muted,
      wordWrap:true,wordWrapWidth:panelWidth-40}
  });
  worldStatus.position.set(wx+20,wy+57);worldPanel.addChild(worldStatus);
  const worldButtons: Array<{name:string;x:number;y:number;width:number;height:number}>=[];
  const worldRows=new Container();worldPanel.addChild(worldRows);stage.addChild(worldPanel);
  // Controls operate on the same original Scheme game state, not the legacy
  // foundation counter. End-day uses on-turn-end/on-turn-start from world.scm.
  const previewControls=[
    {id:"day",label:"度过一天",x:wx+18},
    {id:"save",label:"保存",x:wx+137},
    {id:"load",label:"读档",x:wx+256}
  ];
  for(const control of previewControls){
    worldPanel.addChild(new Graphics().roundRect(control.x,wy+424,105,42,6)
      .fill({color:0x363d47}).stroke({color:0x898b8e,width:1}));
    const caption=new Text({text:control.label,style:{
      fontFamily:"sans-serif",fontSize:16,fill:0xf7eee1
    }});
    caption.position.set(control.x+14,wy+435);worldPanel.addChild(caption);
  }
  const previewSaveKey="ssnoir-phase2-preview-v1";
  const miniStorage=(globalThis as {wx?:{
    setStorageSync?:(key:string,value:string)=>void;
    getStorageSync?:(key:string)=>unknown;
  }}).wx;
  const saveLocal=(data:string)=>{
    if(typeof miniStorage?.setStorageSync==="function")
      miniStorage.setStorageSync(previewSaveKey,data);
    else if(typeof localStorage!=="undefined")localStorage.setItem(previewSaveKey,data);
    else throw new Error("此平台暂不提供持久存储接口");
  };
  const loadLocal=():string|null=>{
    if(typeof miniStorage?.getStorageSync==="function"){
      const value=miniStorage.getStorageSync(previewSaveKey);
      return typeof value==="string"&&value?value:null;
    }
    if(typeof localStorage!=="undefined")return localStorage.getItem(previewSaveKey);
    throw new Error("此平台暂不提供持久存储接口");
  };
  let authoredWorld:WorldHost|null=null;
  let restoredGames=0;
  let selectedPlace="";
  let dossierMode:"index"|"detail"|null=null;
  let dossierEntries:DossierEntry[]=[];
  let selectedDossier:DossierEntry|null=null;
  let dossierRowDetails:string[]=[];
  let currentContainer:RuntimeNode|null=null;
  let pendingRoll:RuntimeNode|null=null;
  const navigationStack:string[]=[];
  let displayedNames:string[]=[];
  let listPage=0,hasBack=false;
  const drawRows=()=>{
    worldRows.removeChildren().forEach(child=>child.destroy());
    worldButtons.length=0;
    const visible=displayedNames.slice(listPage*7,(listPage+1)*7);
    for(let i=0;i<visible.length;i++){
      const name=visible[i],x=wx+18,y=wy+92+i*42,width=panelWidth-36,height=35;
      const candidate=(currentContainer?.children??authoredWorld?.root.children??[])
        .find(n=>n.name===name);
      const shownName=name==="__dossier"?"卷宗 · 线索与委托":
        name.startsWith("__dossier-entry:")?"◇ "+name.slice("__dossier-entry:".length):
        name.startsWith("__dossier-row:")?dossierRowDetails[Number(name.slice("__dossier-row:".length))]??name:
        (candidate?.isPlace?"▸  ":candidate?.resolve?.kind==="roll"?"◆  ":
        candidate?.children.length?"◇  ":"·  ")+(candidate?.title??name);
      const disabled=candidate?.disabled??false;
      worldRows.addChild(new Graphics().roundRect(x,y,width,height,3)
        .fill({color:disabled?0x12181b:0x1d262c})
        .stroke({color:disabled?0x33393d:0x465057,width:1}));
      const row=new Text({text:shownName,style:{fontFamily:"sans-serif",
        fontSize:16,fill:disabled?0x65727a:ivory,wordWrap:false}});
      row.position.set(x+12,y+7);worldRows.addChild(row);
      if(candidate){
        const kind=candidate.isPlace?"地点":candidate.resolve?.kind==="roll"?"检定":
          candidate.resolve?.kind==="instant"?"行动":candidate.children.length?"调查":"记录";
        const tag=new Text({text:kind,style:{fontFamily:"sans-serif",
          fontSize:12,fill:disabled?0x6f7478:brass}});
        tag.anchor.set(1,0);tag.position.set(x+width-12,y+11);
        worldRows.addChild(tag);
      }
      if(!disabled)      worldButtons.push({name,x,y,width,height});
    }
    if(hasBack){
      worldButtons.unshift({name:"__back",x:wx+panelWidth-114,y:wy+17,width:100,height:30});
      worldRows.addChild(new Graphics().roundRect(wx+panelWidth-114,wy+17,100,30,5).fill(0x4a5058));
      const back=new Text({text:"返回",style:{fontFamily:"sans-serif",fontSize:14,fill:0xffffff}});
      back.position.set(wx+panelWidth-96,wy+24);worldRows.addChild(back);
    }
    if(displayedNames.length>7){
      for(const [id,label,x,enabled] of [
        ["__prev","上一页",wx+18,listPage>0],
        ["__next","下一页",wx+255,(listPage+1)*7<displayedNames.length]
      ] as const){
        if(!enabled)continue;
        const y=wy+383,width=100,height=34;
        worldRows.addChild(new Graphics().roundRect(x,y,width,height,5)
          .fill({color:0x424a55}));
        const labelText=new Text({text:label,style:{fontFamily:"sans-serif",fontSize:14,fill:0xf0e8dc}});
        labelText.position.set(x+14,y+7);worldRows.addChild(labelText);
        worldButtons.push({name:id,x,y,width,height});
      }
    }
  };
  const renderPlaces=(names:string[],atPlace=false)=>{
    displayedNames=names;listPage=0;hasBack=atPlace;drawRows();
  };
  const updateHud=(subtitle:string)=>{
    if(!authoredWorld)return;
    const team=authoredWorld.state.team.roster().filter(actor=>actor.status==="active");
    const dice=team.reduce((n,actor)=>n+authoredWorld!.state.team.dice(actor.id).length,0);
    counterText.text="第 "+authoredWorld.state.getGlobal("世界日")+" 日  ·  金钱 "+
      authoredWorld.state.getItemCount("金钱")+"  ·  行动 "+dice;
    hudLead.text=subtitle;
  };
  const showRoot=()=>{
    if(!authoredWorld)return;
    selectedPlace="";currentContainer=null;pendingRoll=null;navigationStack.length=0;
    worldHeading.text="贝尔维尔 / 城市";
    const activeClock=authoredWorld.root.clocks.find(c=>c.style==="countdown");
    worldStatus.text="世界日："+String(authoredWorld.state.getGlobal("世界日"))+
      " · 地点："+authoredWorld.getPlaces().length+
      (activeClock?" · "+activeClock.displayLabel+" "+activeClock.current+"/"+activeClock.max:"");
    // Story nodes at world root are not places; hiding them would make
    // first-chapter authored objectives impossible to reach from the map.
    renderPlaces([...authoredWorld.root.children.filter(n=>!n.disabled).map(n=>n.name),"__dossier"]);
    updateHud(activeClock?activeClock.displayLabel+"  "+activeClock.current+"/"+activeClock.max:
      "码头、剧院与老街；每件事都有自己的时限。");
  };
  const showDossierIndex=()=>{
    dossierMode="index";selectedDossier=null;dossierRowDetails=[];
    worldHeading.text="卷宗 / 委托";
    worldStatus.text="原版世界中的故事线："+dossierEntries.length;
    renderPlaces(dossierEntries.map(entry=>"__dossier-entry:"+entry.id),true);
  };
  const showDossierDetail=(entry:DossierEntry)=>{
    dossierMode="detail";selectedDossier=entry;
    worldHeading.text="卷宗 / "+entry.id;
    worldStatus.text=entry.status+(entry.where?" · "+entry.where:"");
    const rows=[
      entry.now?"当前："+entry.now:"",
      ...entry.clocks.map(c=>c.displayLabel+" "+c.current+"/"+c.max+" · "+c.style),
      ...entry.steps.map(step=>(step.done?"✓ ":"○ ")+step.text),
      ...entry.log.map(entry=>"第"+entry.day+"日 · "+entry.text)
    ].filter(Boolean);
    dossierRowDetails=rows;
    renderPlaces(rows.map((_,i)=>"__dossier-row:"+i),true);
  };
  const showContainer=(node:RuntimeNode)=>{
    currentContainer=node;
    worldHeading.text="场景 / "+node.name;
    worldStatus.text=node.subtitle||"查看行动，或返回上一级";
    renderPlaces(node.children.map(n=>n.name),true);
    updateHud(node.subtitle||node.title);
  };
  // Keep foundation startup independent of a content load failure.
  void createWorldHostRuntime().then(async worldHost=>{
    authoredWorld=worldHost;
    const search=(globalThis as {location?:{search?:string}}).location?.search??"";
    if(/(?:^|[?&])stage-preview=frank(?:&|$)/.test(search)){
      // This is the ORIGINAL Scheme beat list. Run on the real live world VM,
      // but only on an opt-in screenshot URL, never during regular gameplay.
      await worldHost.session.evaluate(extractStageExpression(frankAuthoredSource));
    }
    showRoot();receiveStory();
    console.log("[SSNoir] original world.scm ready, places="+worldHost.getPlaces().length);
  }).catch(error=>{
    worldStatus.text="原版世界加载失败";
    console.error("[SSNoir] real world bootstrap failed",error);
    options.onError?.(error);
  });
  let worldQueue=Promise.resolve();
  type DiePick={actorId:string;slotId:number};
  let chosenDice:DiePick[]=[];
  const collectDice=()=>{
    if(!authoredWorld)return;
    const remaining=authoredWorld.state.team.roster().filter(a=>a.status==="active")
      .flatMap(a=>authoredWorld!.state.team.dice(a.id)
        .filter(d=>!chosenDice.some(c=>c.actorId===a.id&&c.slotId===d.slotId))
        .map(d=>({id:a.id,name:a.name,value:d.value,slot:d.slotId})));
    if(!remaining.length){worldStatus.text="没有可用的骰子";pendingRoll=null;return;}
    renderPlaces(remaining.map(d=>"__die:"+d.id+":"+d.slot+" · "+d.name+" "+d.value),true);
    for(const button of worldButtons){
      if(button.name.startsWith("__die:"))button.name=button.name.split(" · ")[0];
    }
  };
  const redrawCurrent=()=>{
    if(!authoredWorld)return;
    if(authoredWorld.mode==="encounter"){
      selectedPlace="__encounter";navigationStack.length=0;
      showContainer(authoredWorld.root);
      worldHeading.text="交锋 / "+authoredWorld.root.name;
    }else{
      const place=authoredWorld.root.children.find(x=>x.name===selectedPlace);
      if(place)showContainer(place);
      else showRoot();
    }
  };
  const executeOriginal=async(target:RuntimeNode)=>{
    if(!authoredWorld)return;
    const picked=[...chosenDice];
    const allocations=target.requires.map(req=>{
      if(req.type==="item")return {type:"item" as const,itemId:req.itemId!,qty:req.qty!};
      const die=picked.shift();
      if(!die)throw new Error("missing selected action die");
      return {type:"die" as const,actorId:die.actorId,slotId:die.slotId};
    });
    const result=await authoredWorld.runner.performSlots(target.name,allocations);
    pendingRoll=null;chosenDice=[];
    await authoredWorld.refresh();
    redrawCurrent();
    const events=authoredWorld.state.takeStoryEvents();
    playback.push(events);updateStory();
    worldStatus.text="行动："+result.name+" · "+
      (result.outcome??"完成")+" · 金钱："+authoredWorld.state.getItemCount("金钱");
  };
  let navigating=false;
  const visitWorld=(name:string)=>{
    if(navigating)return;
    navigating=true;
    const previousStatus=worldStatus.text;
    if(!name.startsWith("__"))worldStatus.text="正在进入 · "+name.slice(0,32);
    worldQueue=worldQueue.then(async()=>{
      // Present the busy state before costly authored tree evaluation begins.
      await new Promise<void>(done=>options.animationFrame(()=>done()));
      if(!authoredWorld)return;
      if(name==="__prev"||name==="__next"){
        listPage+=name==="__next"?1:-1;
        drawRows();return;
      }
      if(name==="__back"){
        if(dossierMode){
          if(dossierMode==="detail"){showDossierIndex();}
          else {dossierMode=null;selectedDossier=null;showRoot();}
          return;
        }
        if(pendingRoll){pendingRoll=null;chosenDice=[];redrawCurrent();return;}
        if(navigationStack.length){
          const previous=navigationStack.pop()!;
          const find=(node:RuntimeNode):RuntimeNode|undefined=>
            node.name===previous?node:
            node.children.map(find).find(Boolean) as RuntimeNode|undefined;
          const container=find(authoredWorld.root);
          if(container){showContainer(container);return;}
        }
        if(authoredWorld.mode==="encounter"){redrawCurrent();return;}
        showRoot();return;
      }
      if(dossierMode){
        if(dossierMode==="index"&&name.startsWith("__dossier-entry:")){
          const id=name.slice("__dossier-entry:".length);
          const entry=dossierEntries.find(x=>x.id===id);
          if(!entry)throw new Error("Dossier entry not found: "+id);
          showDossierDetail(entry);return;
        }
        if(dossierMode==="detail"&&name.startsWith("__dossier-row:")){
          const index=Number(name.slice("__dossier-row:".length));
          const detail=dossierRowDetails[index];
          if(detail){
            playback.push([{kind:"spotlight",args:[selectedDossier?.id??"卷宗",detail],
              blocking:true,immediate:true}]);
            updateStory();
          }
          return;
        }
        throw new Error("Unavailable dossier navigation "+name);
      }
      if(name==="__dossier"){
        if(authoredWorld.mode!=="world")throw new Error("Dossier requires world mode");
        dossierEntries=await authoredWorld.getDossier();
        showDossierIndex();return;
      }
      if(pendingRoll&&name.startsWith("__die:")){
        const match=/^__die:([^:]+):(\\d+)$/.exec(name);
        if(!match)throw new Error("invalid die selection");
        chosenDice.push({actorId:match[1],slotId:Number(match[2])});
        const needed=pendingRoll.requires.filter(r=>r.type==="die").length;
        if(chosenDice.length<needed){collectDice();return;}
        await executeOriginal(pendingRoll);return;
      }
      if(!selectedPlace){
        const top=authoredWorld.root.children.find(x=>x.name===name&&!x.disabled);
        if(!top)throw new Error("unavailable world node "+name);
        if(top.isPlace){
          // Perceived latency: enter the already prepared scene immediately.
          // All interaction is locked by navigating=true until on-enter-place,
          // arrivals, and the authoritative tree have finished executing.
          selectedPlace=name;navigationStack.length=0;showContainer(top);
          worldStatus.text="正在进入 "+name+" · 整理当日事件…";
          // The render loop's RAF callback was registered first; this gives
          // the player an actual painted frame before the heavy Scheme eval.
          await new Promise<void>(done=>options.animationFrame(()=>done()));
          const entered=await authoredWorld.enterPlace(name);
          showContainer(entered);
          receiveStory();return;
        }
        // Main-quest cards are real root children; they are not locations.
        // Enter their child tree without firing the location arrival hook.
        if(top.children.length){
          selectedPlace=name;navigationStack.length=0;showContainer(top);return;
        }
        currentContainer=authoredWorld.root;
        selectedPlace="__world-action";
      }
      if(!currentContainer)return;
      const target=currentContainer.children.find(x=>x.name===name);
      if(!target||target.disabled)throw new Error("unavailable node "+name);
      if(target.children.length){
        navigationStack.push(currentContainer.name);showContainer(target);return;
      }
      if(!target.resolve)return;
      if(target.resolve.kind==="observe"||target.resolve.kind==="note"){
        const clock=target.resolve.noteClock;
        worldStatus.text=target.resolve.text||target.resolve.noteText||target.title;
        if(clock) worldStatus.text+=
          " · "+clock.displayLabel+" "+clock.current+"/"+clock.max;
        return;
      }
      if(target.requires.some(x=>x.type==="die")){
        pendingRoll=target;chosenDice=[];
        worldStatus.text=target.title+" / 选择行动骰";
        collectDice();return;
      }
      await executeOriginal(target);
    }).catch(error=>{
      pendingRoll=null;chosenDice=[];
      if(selectedPlace&&!authoredWorld?.root.children.find(n=>n.name===selectedPlace&&n.isPlace))
        showRoot();
      worldStatus.text="行动失败："+String(error).slice(0,42);
      console.error("[SSNoir] authored-world action",error);options.onError?.(error);
    }).finally(()=>{
      navigating=false;
      if(worldStatus.text.startsWith("正在进入 · "))worldStatus.text=previousStatus;
    });
  };

  // Blocking, input-acknowledged story playback shared by all four hosts.
  let playback=new StoryPlaybackQueue();
  const storyOverlay=new Container();
  storyOverlay.visible=false;
  storyOverlay.addChild(new Graphics().roundRect(22,195,806,172,12)
    .fill({color:0x0e1016,alpha:0.98}).stroke({color:0xc7bca7,width:2}));
  const storySpeaker=new Text({text:"",style:{
    fontFamily:"sans-serif",fontSize:22,fill:0xf4ddad
  }});
  storySpeaker.position.set(48,212);
  storyOverlay.addChild(storySpeaker);
  const storyBody=new Text({text:"",style:{
    fontFamily:"sans-serif",fontSize:20,fill:0xf8f1e7,
    wordWrap:true,wordWrapWidth:735,breakWords:true
  }});
  storyBody.position.set(48,253);
  storyOverlay.addChild(storyBody);
  const storyHelp=new Text({text:"轻触继续",style:{
    fontFamily:"sans-serif",fontSize:15,fill:0xa3acb8
  }});
  storyHelp.position.set(680,339);
  storyOverlay.addChild(storyHelp);
  stage.addChild(storyOverlay);
  const theatreView=new TheatreVectorView();
  stage.addChildAt(theatreView.root,stage.getChildIndex(storyOverlay));
  const stageImages=new StageImageRegistry();
  const stageView=new StageVectorView(asset=>stageImages.get(asset));
  stage.addChildAt(stageView.root,stage.getChildIndex(storyOverlay));
  let activeStage:StagePlayback|null=null;
  let stageFrame:ReturnType<typeof playback.advance>|null=null;
  let activeTheatre:TheatrePlayback|null=null;
  let theatreFrame:ReturnType<typeof playback.advance>|null=null;
  let theatreTime=Date.now();
  const updateStory=()=>{
    const frame=playback.current;
    if(frame?.kind==="stage"){
      if(stageFrame!==frame){
        activeStage=new StagePlayback(parseStage(frame.commands));
        stageFrame=frame;
        stageView.mount();
        activeStage.start();
        stageView.sync(activeStage);
        const missing=stageView.missingAssets();
        if(missing.length)console.warn("[SSNoir] stage assets pending:",missing);
      }
      if(activeTheatre){activeTheatre=null;theatreFrame=null;theatreView.reset();}
      storyOverlay.visible=Boolean(activeStage?.caption);
      if(activeStage?.caption){
        storySpeaker.text=activeStage.caption.speaker;
        storyBody.text=activeStage.caption.text.slice(0,activeStage.visibleCharacters);
        storyHelp.text=activeStage.requiresInput?"轻触继续":"自动播放";
      }
      return;
    }
    if(activeStage){activeStage=null;stageFrame=null;stageView.reset();}
    if(frame?.kind==="theatre"){
      if(theatreFrame!==frame){
        const program=parseTheatre(frame.commands);
        theatreFrame=frame;
        activeTheatre=new TheatrePlayback(program);
        theatreView.mount(program);
        activeTheatre.start();
        const missing=theatreView.missingAssets();
        if(missing.length)console.warn("[SSNoir] theatre assets pending:",missing.length);
        theatreTime=Date.now();
      }
      const caption=activeTheatre?.caption;
      storyOverlay.visible=Boolean(caption);
      if(caption){
        storySpeaker.text=caption.speaker;
        storyBody.text=caption.text.slice(0,activeTheatre!.visibleCharacters);
        storyHelp.text=activeTheatre!.waiting?"轻触继续":"自动播放";
      }
      return;
    }
    if(activeTheatre){
      activeTheatre=null;theatreFrame=null;theatreView.reset();
    }
    storyOverlay.visible=Boolean(frame);
    if(frame){
      storySpeaker.text=frame.speaker||"叙事";
      storyBody.text=frame.text;
      storyHelp.text="轻触继续";
    }
  };
  const receiveStory=()=>{
    if(!authoredWorld)return;
    playback.push(authoredWorld.state.takeStoryEvents());
    updateStory();
  };

  // This is the real LIPS runtime candidate, not an unrelated BiwaScheme probe.
  // Confirm isolated namespaces before any user action, in both Web and WeChat.
  const world = new LipsSession("SSNoir-foundation-world");
  const encounter = new LipsSession("SSNoir-foundation-encounter");
  await world.evaluate("(define foundation-local 17)");
  await encounter.evaluate("(define foundation-local 2)");
  if (await world.evaluateNumber("foundation-local") !== 17 ||
      await encounter.evaluateNumber("foundation-local") !== 2) {
    throw new Error("Scheme world/encounter interpreter isolation failed");
  }
  // Import the original Scheme source at build time; no second editable copy.
  // This is not the full game engine's native-function bridge yet.
  await world.evaluate(ssnoirStdlib);
  if (await world.evaluateNumber("(length (filter (lambda (x) (> x 2)) '(1 2 3 4)))") !== 2) {
    throw new Error("Original SSNoir stdlib.scm compatibility failed");
  }
  await world.evaluate("(define foundation-count 0)");
  await world.evaluate("(define (foundation-next!) (set! foundation-count (+ foundation-count 1)) foundation-count)");
  // Phase 2: run the SAME original engine.scm native inventory path on all hosts.
  // The independent legacy smoke counter remains for existing platform checks.
  const gameplay = await createHostScriptRuntime();
  let count = 0;
  let actionQueue = Promise.resolve();
  function increment(): Promise<void> {
    actionQueue = actionQueue.then(async () => {
      const next = await world.evaluateNumber("(foundation-next!)");
      // Initial counter display is not a paid action; start cash stays 15.
      if (next > 1) await gameplay.session.evaluate('(add-item! "金钱" 1)');
      count = next;
      debugCounter.text = "Scheme 计算结果：" + count +
        " · 金钱：" + gameplay.state.getItemCount("金钱");
    });
    return actionQueue;
  }
  await increment();

  let size = { width: options.width, height: options.height, scale: 1, left: 0, top: 0 };
  function resize(width: number, height: number): void {
    width = Math.max(1, width);
    height = Math.max(1, height);
    three.setSize(width, height, false);
    pixi.resize(width, height);
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
    // One fixed design height on every host; only the available width changes.
    const scale = height / VIRTUAL_HEIGHT;
    size = {
      width, height, scale,
      left: 0,
      top: (height - VIRTUAL_HEIGHT * scale) / 2
    };
    stage.scale.set(scale);
    stage.position.set(size.left, size.top);
  }
  resize(options.width, options.height);

  // For WeChat, where the SDK provides touch events rather than HTML pointer events.
  // Expose coordinates in native screen space, then map to the exact same Pixi button.
  function activateAt(x: number, y: number): boolean {
    const xx = (x - size.left) / size.scale;
    const yy = (y - size.top) / size.scale;
    if(playback.blocked){
      if(activeStage&&!activeStage.complete){
        activeStage.advance();updateStory();
        return true;
      }
      if(activeTheatre&&!activeTheatre.complete){
        activeTheatre.advance();updateStory();
        return true;
      }
      playback.advance();updateStory();
      return true;
    }
    const control=previewControls.find(c=>
      xx>=c.x&&xx<=c.x+105&&yy>=wy+424&&yy<=wy+466);
    if(control){
      worldQueue=worldQueue.then(async()=>{
        if(!authoredWorld)throw new Error("世界仍在加载");
        if(control.id==="save"){
          const data=await authoredWorld.save();
          saveLocal(data);
          worldStatus.text="游戏已保存 · 世界日 "+String(authoredWorld.state.getGlobal("世界日"));
        }else if(control.id==="load"){
          const data=loadLocal();
          if(!data){worldStatus.text="没有可读取的存档";return;}
          await authoredWorld.load(data);
          restoredGames++;
          dossierMode=null;selectedDossier=null;dossierEntries=[];
          pendingRoll=null;chosenDice=[];
          playback=new StoryPlaybackQueue();
          showRoot();
          receiveStory();
          worldStatus.text="游戏已恢复 · 世界日 "+String(authoredWorld.state.getGlobal("世界日"));
        }else if(authoredWorld.mode==="encounter"){
          // Use the original opponent rules, forced responses and composure tax.
          // This control advances the encounter, never the city calendar.
          const phases=await authoredWorld.director.endRound();
          await authoredWorld.refresh();
          pendingRoll=null;chosenDice=[];
          playback.push([
            ...phases.flatMap(phase=>phase.events),
            ...authoredWorld.state.takeStoryEvents()
          ]);
          updateStory();redrawCurrent();
          worldStatus.text="交锋回合结算 · "+phases.map(p=>p.phase).join(" → ");
        }else{
          const day=await authoredWorld.endDay();
          pendingRoll=null;chosenDice=[];
          playback.push([...day.events,...authoredWorld.state.takeStoryEvents()]);
          updateStory();redrawCurrent();
          worldStatus.text="世界日 "+String(day.after)+" · 骰子已更新";
        }
      }).catch(error=>{
        worldStatus.text="操作失败："+String(error).slice(0,54);
        console.error("[SSNoir] game control",error);
        options.onError?.(error);
      });
      return true;
    }
    const hit=worldButtons.find(b=>xx>=b.x&&xx<=b.x+b.width&&yy>=b.y&&yy<=b.y+b.height);
    if(hit){visitWorld(hit.name);return true;}
    if (qaMode && xx >= BUTTON.x && xx <= BUTTON.x + BUTTON.width &&
        yy >= BUTTON.y && yy <= BUTTON.y + BUTTON.height) {
      void increment().catch(error => options.onError?.(error));
      return true;
    }
    return false;
  }

  const loop = createFrameLoop(() => {
    const now=Date.now(),seconds=Math.max(0,Math.min(.1,(now-theatreTime)/1000));
    theatreTime=now;
    if(activeStage&&!activeStage.complete){
      activeStage.tick(seconds);
      stageView.sync(activeStage);
      if(activeStage.complete){playback.advance();updateStory();}
      else if(activeStage.caption){
        storyOverlay.visible=true;
        storySpeaker.text=activeStage.caption.speaker;
        storyBody.text=activeStage.caption.text.slice(0,activeStage.visibleCharacters);
      }
    }
    if(activeTheatre&&!activeTheatre.complete){
      activeTheatre.tick(seconds);
      theatreView.sync(activeTheatre);
      if(activeTheatre.complete){
        playback.advance();updateStory();
      }else if(activeTheatre.caption){
        storyBody.text=activeTheatre.caption.text.slice(0,activeTheatre.visibleCharacters);
      }
    }
    three.resetState();
    three.render(scene, camera);
    pixi.resetState();
    pixi.render({ container: stage, clear: false });
  }, options.animationFrame, options.cancelAnimationFrame);
  loop.resume();

  return {
    increment,
    activateAt,
    getCount: () => count,
    getMoney: () => gameplay.state.getItemCount("金钱"),
    getGamePreviewStatus:()=>({
      loaded:authoredWorld!==null,
      mode:authoredWorld?.mode??"loading",
      day:String(authoredWorld?.state.getGlobal("世界日")??""),
      entries:authoredWorld?.root.children.length??0,
      places:authoredWorld?.getPlaces().length??0,
      money:authoredWorld?.state.getItemCount("金钱")??0,
      restores:restoredGames,
      storyKind:playback.current?.kind??"",
      stageBeat:activeStage?.beatIndex??-1,
      view:currentContainer?.name??"",
      navigationMs:authoredWorld?.lastNavigation?.durationMs??null,
      materializations:authoredWorld?.lastNavigation?.materializations??null,
      busy:navigating
    }),
    getStageAssetDiagnostics:()=>stageImages.diagnostics(),
    getRenderDiagnostics: () => ({
      threeUsesHostCanvas: three.domElement === options.canvas,
      pixiViewUsesHostCanvas: pixi.canvas === options.canvas,
      pixiContextUsesHostCanvas: pixi.context.canvas === options.canvas,
      pixiTargetUsesHostCanvas: pixi.view.texture.source.resource === options.canvas,
      sharedWebGLContext: pixi.gl === three.getContext()
    }),
    resize,
    pause: loop.pause,
    resume: loop.resume,
    dispose() {
      loop.dispose();
      theatreView.destroy();
      stageView.destroy();
      stageImages.dispose();
      pixi.destroy();
      scene.traverse(obj => {
        if (obj instanceof THREE.Mesh) obj.geometry.dispose();
      });
      blocks.dispose();
      street.material.dispose();
      three.dispose();
    }
  };
}
