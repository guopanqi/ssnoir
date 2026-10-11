/* rain */
const cv=$('#rain'),cx=cv.getContext('2d'),dr=Array.from({length:260},()=>({x:Math.random()*960,y:Math.random()*540,v:14+Math.random()*10,l:10+Math.random()*14}));cx.strokeStyle='rgba(150,185,235,.4)';
(function lp(){requestAnimationFrame(lp);cx.clearRect(0,0,960,540);if(!rainI)return;const n=Math.floor(dr.length*rainI);cx.beginPath();for(let i=0;i<n;i++){const d=dr[i];if(!paused){d.y+=d.v;d.x-=2;if(d.y>540){d.y=-20;d.x=Math.random()*980}}cx.moveTo(d.x,d.y);cx.lineTo(d.x-2,d.y+d.l)}cx.stroke()})();
/* grain texture */
(function(){const c=document.createElement('canvas');c.width=c.height=200;const x=c.getContext('2d'),d=x.createImageData(200,200);for(let i=0;i<d.data.length;i+=4){const v=Math.random()*255;d.data[i]=d.data[i+1]=d.data[i+2]=v;d.data[i+3]=255}x.putImageData(d,0,0);$('#grain').style.background=`url(${c.toDataURL()})`})();


