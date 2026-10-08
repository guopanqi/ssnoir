export class StoryPlayer{
 constructor(story,onFrame){this.story=story;this.onFrame=onFrame;this.index=0;this.elapsed=0;this.playing=false;this.speed=1;this.last=0;this.frame=this.frame.bind(this);this.emit();requestAnimationFrame(this.frame)}
 get total(){return this.story.reduce((sum,cue)=>sum+cue.duration,0)}
 get position(){return this.story.slice(0,this.index).reduce((sum,cue)=>sum+cue.duration,0)+this.elapsed}
 emit(){this.onFrame({index:this.index,elapsed:this.elapsed,position:this.position,total:this.total,playing:this.playing})}
 frame(now){const delta=this.last?Math.max(0,now-this.last):0;this.last=now;if(this.playing){this.elapsed+=delta*this.speed;while(this.elapsed>=this.story[this.index].duration){this.elapsed-=this.story[this.index].duration;if(this.index===this.story.length-1){this.elapsed=this.story[this.index].duration;this.playing=false;break}this.index++}this.emit()}requestAnimationFrame(this.frame)}
 toggle(){if(!this.playing&&this.position>=this.total)this.seek(0);this.playing=!this.playing;this.emit()}
 restart(){this.index=0;this.elapsed=0;this.playing=true;this.emit()}
 seek(position){let remaining=Math.max(0,Math.min(this.total,position));this.index=0;while(this.index<this.story.length-1&&remaining>=this.story[this.index].duration){remaining-=this.story[this.index].duration;this.index++}this.elapsed=remaining;this.emit()}
 step(delta){this.index=Math.max(0,Math.min(this.story.length-1,this.index+delta));this.elapsed=0;this.emit()}
}
