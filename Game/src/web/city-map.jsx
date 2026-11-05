import React from "react";

/**
 * The detective's desk is strictly a view of authored world places.
 * Coordinates, districts and connecting thread are illustrative only:
 * no independent location flags, game clocks, unlocks or transitions.
 */
const LANDMARKS = {
  "家": [22, 68, "old"],
  "老街酒馆": [28, 42, "old"],
  "剧院": [57, 29, "theatre"],
  "码头": [77, 75, "port"],
  "诊所": [37, 20, "old"],
  "报社": [69, 39, "theatre"],
  "警察局": [49, 53, "theatre"],
  "咖啡馆": [43, 71, "old"],
  "街角咖啡馆": [43, 71, "old"],
  "居民区": [13, 50, "old"],
  "码头居民区": [12, 48, "old"],
  "格兰德酒店": [77, 15, "theatre"],
  "货运公司": [88, 52, "port"],
  "港务技术区": [88, 88, "port"],
  "试验工棚": [60, 84, "port"],
  "三号货栈工棚": [60, 84, "port"],
  "夜总会": [83, 32, "theatre"]
};
const DISTRICTS = {
  old: {title:"旧街区",en:"OLD QUARTER",x:27,y:51,hub:[27,51]},
  theatre: {title:"剧院街",en:"THEATRE QUARTER",x:60,y:37,hub:[60,37]},
  port: {title:"码头区",en:"HARBOR / DOCKS",x:76,y:69,hub:[76,69]}
};
function stableHash(text) {
  let hash=2166136261;
  for(const char of text)hash=Math.imul(hash^char.codePointAt(0),16777619);
  return hash>>>0;
}
function layoutFor(id) {
  if(LANDMARKS[id])return LANDMARKS[id];
  // Unknown future authored locations keep a position independent of list order.
  const hash=stableHash(id);
  const district=["old","theatre","port"][hash%3];
  const anchor=DISTRICTS[district].hub;
  const theta=((hash>>>4)%360)*Math.PI/180;
  const radius=11+((hash>>>13)%10);
  return [
    Math.max(11,Math.min(89,anchor[0]+radius*Math.cos(theta))),
    Math.max(17,Math.min(85,anchor[1]+radius*Math.sin(theta))),
    district
  ];
}
function connection(x,y,hub) {
  const mx=(x+hub[0])/2;
  return "M"+hub[0]+" "+hub[1]+" Q"+mx+" "+(Math.min(y,hub[1])-7)+" "+x+" "+y;
}
export function CityMap({places,activePlace=null,restReasons=[],onOpen,onBackground,locked}) {
  const urgent=new Set(restReasons.map(reason=>reason.location));
  const positioned=places.map(place=>{
    const [x,y,district]=layoutFor(place.id);
    return {place,x,y,district};
  });
  const current=positioned.find(({place})=>place.id===activePlace);
  const currentName=current?.place.title??activePlace;
  return <div className={"city-map detective-map"+(current?" has-focus":"")}
    role="region" aria-label="城市地图"
    onClick={event=>{
      if(event.target.closest(".map-location")||event.target.closest(".map-return"))return;
      if(!locked)onBackground?.();
    }}>
    <svg className="city-map-art" viewBox="0 0 100 100" preserveAspectRatio="none"
      aria-hidden="true">
      <defs>
        <pattern id="desk-blocks" width="8.5" height="10.5" patternUnits="userSpaceOnUse">
          <rect width="8.5" height="10.5" fill="#151920"/>
          <path d="M.4 1.2H3.8V4.7H.4Z M4.5 1H8V5.8H4.5Z M1 6H4V9.6H1Z M4.8 6.6H8V9.8H4.8Z"
            fill="#1b2027" stroke="#333235" strokeWidth=".08"/>
          <path d="M0 5.6H8.5M4.2 0V10.5" stroke="#272a30" strokeWidth=".17"/>
        </pattern>
        <pattern id="desk-water" width="4.6" height="3.3" patternUnits="userSpaceOnUse">
          <path d="M.2 1.3Q1 1 1.9 1.3T3.7 1.3" fill="none"
            stroke="#44606b" opacity=".5" strokeWidth=".13"/>
        </pattern>
        <radialGradient id="desk-vignette">
          <stop offset="0" stopColor="#333337" stopOpacity="0"/>
          <stop offset="1" stopColor="#07090d" stopOpacity=".8"/>
        </radialGradient>
      </defs>
      <rect width="100" height="100" fill="url(#desk-blocks)"/>
      <path d="M0 31Q18 34 36 29T66 34T100 27 M2 79Q25 71 45 61T100 43
        M40 -8Q44 28 49 45T64 104 M86 -6Q78 30 74 55T67 103"
        stroke="#13151a" strokeWidth="4" fill="none" opacity=".93"/>
      <path d="M0 31Q18 34 36 29T66 34T100 27 M2 79Q25 71 45 61T100 43
        M40 -8Q44 28 49 45T64 104 M86 -6Q78 30 74 55T67 103"
        stroke="#5e5544" strokeWidth=".19" strokeDasharray=".8 1.2" fill="none" opacity=".6"/>
      <path d="M96 38Q84 43 87 60T81 78L92 100H100V37Z
        M51 100Q66 87 75 89T100 83V100Z"
        fill="#10212a" stroke="#566878" strokeWidth=".3"/>
      <path d="M96 38Q84 43 87 60T81 78L92 100H100V37Z
        M51 100Q66 87 75 89T100 83V100Z"
        fill="url(#desk-water)"/>
      <path d="M6 55Q26 47 60 37T77 69 M60 37Q69 39 76 69"
        fill="none" stroke="#0b0f14" strokeWidth="2.2"/>
      <path d="M6 55Q26 47 60 37T77 69 M60 37Q69 39 76 69"
        fill="none" stroke="#a27d45" strokeDasharray=".7 1.35"
        strokeWidth=".19" opacity=".72"/>
      <g className="map-thread">
        {positioned.map(({place,x,y,district})=>
          <path key={place.id} d={connection(x,y,DISTRICTS[district].hub)}/>)}
      </g>
      {Object.entries(DISTRICTS).map(([id,entry])=>
        <g key={id} className="map-hub">
          <circle cx={entry.hub[0]} cy={entry.hub[1]} r=".85"/>
          <circle cx={entry.hub[0]} cy={entry.hub[1]} r="2.3" className="map-hub-ring"/>
        </g>)}
      {current&&<path className="map-selection-thread"
        d={"M"+current.x+" "+current.y+" C"+Math.min(current.x+17,92)+" "+
           current.y+" 91 48 100 48"}/>}
      <rect width="100" height="100" fill="url(#desk-vignette)" pointerEvents="none"/>
    </svg>
    <div className="map-paper-grain" aria-hidden="true"/>
    <div className="map-masthead" aria-hidden="true">
      <span>SSNOIR / CITY FILE</span>
      <strong>城市调查图</strong>
      <small>LOCATIONS &amp; LEADS</small>
    </div>
    {Object.entries(DISTRICTS).map(([key,district])=>
      <div key={key} className={"map-district-label map-district-"+key}
        style={{left:district.x+"%",top:district.y+"%"}} aria-hidden="true">
        <strong>{district.title}</strong><small>{district.en}</small>
      </div>)}
    {positioned.map(({place,x,y,district})=>{
      const isCurrent=place.id===activePlace;
      const needsVisit=urgent.has(place.id);
      const angle=(stableHash(place.id)%7-3)*.42;
      return <button key={place.id} type="button"
        className={"map-location map-paper-card"+(place.disabled?" unavailable":"")+
          (needsVisit?" urgent":"")+(isCurrent?" is-current":"")}
        style={{left:x+"%",top:y+"%","--paper-rotation":angle+"deg"}}
        disabled={locked||place.disabled}
        aria-current={isCurrent?"location":undefined}
        onClick={event=>{event.stopPropagation();onOpen(place.id);}}
        data-testid="map-place" data-node-id={place.id} data-map-district={district}
        title={place.subtitle?place.title+" — "+place.subtitle:place.title}>
        <span className="map-pin" aria-hidden="true"/>
        {needsVisit&&<span className="map-task-stamp">待办</span>}
        <span className="map-location-paper">
          <span className="map-location-eyebrow">
            {isCurrent?"当前位置":DISTRICTS[district].title}
          </span>
          <span className="map-location-name">{place.title}</span>
          {place.clocks?.length>0&&<span className="map-place-progress"
            title={place.clocks[0].displayLabel}>
            <span className="map-mini-clock" aria-hidden="true"/>
            {place.clocks[0].current}/{place.clocks[0].max}
          </span>}
        </span>
      </button>;
    })}
    <nav className="map-breadcrumb" aria-label="地图位置">
      <button type="button" className="map-return"
        disabled={locked} onClick={event=>{event.stopPropagation();onBackground?.();}}>
        城市
      </button>
      {current&&<><span aria-hidden="true">/</span>
        <strong>{currentName}</strong></>}
    </nav>
    <div className="map-compass" aria-hidden="true">N<span>✦</span></div>
    <div className="map-legend" aria-hidden="true">
      <span><i className="legend-place"/>地点</span>
      <span><i className="legend-now"/>当前</span>
      <span><i className="legend-urgent"/>待办</span>
    </div>
    <div className="map-caption" aria-live="polite">
      {current?"正在调查":"城市地图"}
      <span>{current?"其他纸卡可直接切换地点":"选取地点查看行动"}</span>
    </div>
  </div>;
}
