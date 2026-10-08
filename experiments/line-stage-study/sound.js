import {soundEvents,performanceAt} from './story.js?v=rain-story-1';
// Claude's lamp soundtrack: filtered noise footsteps + 110 Hz lamp zaps.
export class StageSound{
 constructor(){this.enabled=true;this.position=0;this.sources=new Set()}
 async unlock(position){this.position=position;if(!this.context){this.context=new AudioContext();this.master=this.context.createGain();this.master.connect(this.context.destination);this.noise=this.context.createBuffer(1,this.context.sampleRate*2,this.context.sampleRate);const data=this.noise.getChannelData(0);for(let i=0;i<data.length;i++)data[i]=Math.random()*2-1}await this.context.resume();this.master.gain.value=this.enabled?1:0}
 silence(){for(const source of this.sources){try{source.stop()}catch{}}this.sources.clear()}
 seek(position){this.position=position;this.silence()}
 toggle(){this.enabled=!this.enabled;if(this.master)this.master.gain.value=this.enabled?1:0;if(!this.enabled)this.silence();return this.enabled}
 tone(f,d,type,v){const c=this.context,t=c.currentTime,o=c.createOscillator(),g=c.createGain();o.type=type;o.frequency.value=f;g.gain.setValueAtTime(0,t);g.gain.linearRampToValueAtTime(v,t+.01);g.gain.exponentialRampToValueAtTime(.0001,t+d);o.connect(g);g.connect(this.master);this.track(o);o.start(t);o.stop(t+d+.05)}
 burst(d,v,f){const c=this.context,t=c.currentTime,s=c.createBufferSource(),l=c.createBiquadFilter(),g=c.createGain();s.buffer=this.noise;l.type='lowpass';l.frequency.value=f;g.gain.setValueAtTime(v,t);g.gain.exponentialRampToValueAtTime(.0001,t+d);s.connect(l);l.connect(g);g.connect(this.master);this.track(s);s.start(t,Math.random(),d+.05)}
 track(source){this.sources.add(source);source.onended=()=>{this.sources.delete(source)}}
 sync(frame){
 if(this.context){if(!this.rainSource){this.rainSource=this.context.createBufferSource();this.rainSource.buffer=this.noise;this.rainSource.loop=true;this.rainFilter=this.context.createBiquadFilter();this.rainFilter.type='lowpass';this.rainFilter.frequency.value=3400;this.rainGain=this.context.createGain();this.rainGain.gain.value=0;this.rainSource.connect(this.rainFilter);this.rainFilter.connect(this.rainGain);this.rainGain.connect(this.master);this.rainSource.start()}this.rainGain.gain.setTargetAtTime(frame.playing&&this.enabled?performanceAt(frame.index,frame.elapsed).rain*.08:0,this.context.currentTime,.08)}
if(!frame.playing){this.seek(frame.position);return}if(this.context&&this.enabled&&frame.position>=this.position)for(const event of soundEvents)if(event.time>=this.position&&event.time<frame.position){if(event.type==='step')this.burst(.05,.2,700);else if(event.type==='bell'){this.tone(784,1.3,'sine',.16);this.tone(587,1.8,'sine',.16)}else if(event.type==='buzz'){this.tone(140,.9,'sawtooth',.08);this.tone(147,.9,'square',.04)}else{this.tone(110,.12,'square',.03);this.burst(.04,.15,3000)}}this.position=frame.position}
}
