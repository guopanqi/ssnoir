// 原创矢量半身剪影，用于布局实验；不冒充正式角色立绘。
export function portrait(index){
 const face=index===1?'#cbd8df':'#b2c4d0';
 const head=index===1?'<path d="M38 23 Q25 35 28 57 L45 69 68 58 Q76 31 61 22Z" fill="#263847"/><path d="M39 29 62 30 66 51 55 62 41 54Z" fill="'+face+'"/><path d="M29 26 Q50 7 67 25 L72 50 64 35 38 32 30 55Z" fill="#152532"/>':'<path d="M37 27 62 27 66 51 54 64 39 55Z" fill="'+face+'"/><path d="M40 29 59 31 59 52 52 59 45 49Z" fill="#819aab"/><path d="M26 27 74 28 72 33 25 32Z" fill="#d6e1e6"/><path d="M35 10 61 10 67 27 30 27Z" fill="#839cac"/><path d="M32 22 65 22 67 27 30 27Z" fill="#203241"/>';
 return '<svg viewBox="0 0 100 110" aria-hidden="true"><path d="M9 110 17 78 38 66 60 65 84 78 94 110Z" fill="#263d4d"/><path d="M38 66 50 81 61 65 69 72 59 110 37 110 30 72Z" fill="#b6c9d3"/><path d="M48 81 54 81 58 108 44 108Z" fill="#1c2c38"/>'+head+'<path d="M18 85 29 80 M72 79 84 88" fill="none" stroke="#91aab9" stroke-width="1"/><path d="M45 43 48 43 M57 43 60 43" stroke="#263642" stroke-width="1.5"/></svg>';
}
