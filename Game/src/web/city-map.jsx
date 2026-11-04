import React from "react";

/**
 * Illustrated, presentation-only city board. The authoritative list of places
 * comes from the current Scheme node tree; coordinates are visual landmarks.
 * This component never unlocks places or applies game rules.
 */
const landmarks = {
  "家": [24, 68],
  "老街酒馆": [35, 43],
  "剧院": [57, 28],
  "码头": [77, 75],
  "诊所": [32, 22],
  "报社": [70, 35],
  "警察局": [52, 53],
  "咖啡馆": [42, 66],
  "居民区": [20, 33],
  "格兰德酒店": [64, 16],
  "货运公司": [84, 51],
  "港务技术区": [87, 83],
  "试验工棚": [70, 86]
};
function position(id,index) {
  if (landmarks[id]) return landmarks[id];
  const angle = index * 2.399963229728653;
  return [50 + 33*Math.cos(angle),50 + 30*Math.sin(angle)];
}
export function CityMap({places,activePlace=null,restReasons,onOpen,onBackground,locked}) {
  const urgent=new Set(restReasons.map(reason => reason.location));
  return <div className="city-map"
    onClick={event=>{
      if(event.target.closest(".map-location"))return;
      if(!locked)onBackground?.();
    }} title="点击地图空白处返回城市总览">
    <svg className="city-map-art" viewBox="0 0 1000 700" preserveAspectRatio="xMidYMid slice" aria-hidden="true">
      <defs>
        <pattern id="blocks" width="83" height="71" patternUnits="userSpaceOnUse" patternTransform="rotate(-13)">
          <path d="M0 0H83V71H0Z M8 12H33V31H8Z M43 9H74V37H43Z M9 43H37V64H9Z M45 46H74V65H45Z" fill="none" stroke="#9a9582" strokeWidth="1.8" opacity=".24"/>
        </pattern>
        <pattern id="riverlines" width="22" height="22" patternUnits="userSpaceOnUse">
          <path d="M0 12 Q5 7 11 12 T22 12" stroke="#e6dfc9" strokeWidth="1" fill="none" opacity=".27"/>
        </pattern>
      </defs>
      <rect width="1000" height="700" fill="#1b2527"/>
      <path d="M0 0H1000V700H0Z" fill="url(#blocks)"/>
      <path d="M-35 568 C170 510 230 605 405 536 S680 340 798 414 S937 605 1030 558" fill="none" stroke="#6f7771" strokeWidth="94" opacity=".32"/>
      <path d="M-35 568 C170 510 230 605 405 536 S680 340 798 414 S937 605 1030 558" fill="none" stroke="#a3a18b" strokeWidth="6" strokeDasharray="8 18" opacity=".44"/>
      <path d="M-60 665 C175 618 261 720 435 645 S675 450 821 523 S950 690 1040 652 L1040 750H-60Z" fill="#304b50" stroke="#829798" strokeWidth="3"/>
      <path d="M-60 665 C175 618 261 720 435 645 S675 450 821 523 S950 690 1040 652 L1040 750H-60Z" fill="url(#riverlines)"/>
      <g fill="none" stroke="#e5d5ad" strokeWidth="2" opacity=".3">
        <path d="M87 135L300 110L485 219L780 165L954 90"/>
        <path d="M104 388L307 301L482 407L719 321L915 381"/>
        <path d="M185 30L163 525M492 32L504 621M809 10L760 585"/>
      </g>
      <g fill="#e7dcc3" opacity=".14">
        <path d="M516 131l38 -27 39 18 10 43 -31 22 -51 -15z"/>
        <path d="M285 231l53 -16 28 29 -6 51 -66 7 -19 -35z"/>
        <path d="M700 487l66 -28 41 39 -24 45 -73 -4z"/>
      </g>
      <text x="108" y="95" className="map-district">旧街区 / OLD QUARTER</text>
      <text x="645" y="93" className="map-district">剧院区 / THEATRE</text>
      <text x="755" y="642" className="map-district">第七码头 / DOCKS</text>
      <path d="M48 44H92 M70 22V66" stroke="#dbc49a" strokeWidth="2"/>
      <text x="70" y="18" textAnchor="middle" fill="#dbc49a" fontSize="14">N</text>
    </svg>
    <div className="map-paper-grain" aria-hidden="true"/>
    {places.map((place,index)=>{
      const [x,y]=position(place.id,index);
      return <button key={place.id} type="button"
        className={"map-location"+(place.disabled?" unavailable":"")+(urgent.has(place.id)?" urgent":"")+(activePlace===place.id?" is-current":"")}
        style={{left:x+"%",top:y+"%"}}
        disabled={locked||place.disabled}
        aria-current={activePlace===place.id?"location":undefined}
        onClick={event=>{event.stopPropagation();onOpen(place.id);}}
        data-testid="map-place" data-node-id={place.id}
        title={place.subtitle ? place.title+" — "+place.subtitle : place.title}>
        <span className="map-pin" aria-hidden="true"/>
        <span className="map-location-name">{place.title}</span>
      </button>;
    })}
    <div className="map-caption">BELLEVILLE <span>{activePlace
      ? "当前位置 · "+(places.find(x=>x.id===activePlace)?.title??activePlace)
      : "城市地图 · 选择地点"}</span></div>
  </div>;
}
