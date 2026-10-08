import {Player as OriginalPlayer} from '../player.js';
export class Player extends OriginalPlayer{
 async load(story,start=0){
  if(this.story&&start===0){
   this.cancel();const token=this.token;this.paused=false;this.stage.pause(false);this.state='intro';this.update();
   this.stage.cover(550);await this.delay(550,token);if(token!==this.token)return;
  }
  return super.load(story,start);
 }

 restore(c){
  if(c.type==='title'||c.type==='flicker')return;
  if(c.type==='atmosphere')this.stage.atmosphere(c);
  else if(c.type==='pool')this.stage.pool(c.id,c.strength,true,c.duration);
  else if(c.type==='door')this.stage.door(c.action,true,c.duration);
  else if(c.type==='inspect')this.stage.inspect(c.id,c.show,true);
  else if(c.type==='tilt')this.stage.tilt(c.actor,c.angle,true);
  else super.restore(c);
 }
 async command(c,token){
  if(token!==this.token)return;
  if(c.type==='title'){
   this.state='intro';this.update();this.stage.title(c);
   await this.delay(c.fadeIn+c.hold,token);if(token!==this.token)return;
   this.stage.reveal(c.fadeOut);await this.delay(c.fadeOut,token);if(token!==this.token)return;
   this.stage.hideTitle();await this.delay(c.settle,token);if(token!==this.token)return;
   this.state='action';this.update();
  }else if(c.type==='flicker'){this.stage.flicker(c.id,c.levels,c.duration);await this.delay(c.duration,token);}
  else if(c.type==='pool'){this.stage.pool(c.id,c.strength,false,c.duration);await this.delay(c.duration,token);}
  else if(c.type==='door'){this.stage.door(c.action,false,c.duration);await this.delay(c.duration??650,token);}
  else if(c.type==='inspect'||c.type==='tilt'){
   c.type==='inspect'?this.stage.inspect(c.id,c.show,false,c.duration):this.stage.tilt(c.actor,c.angle,false,c.duration);await this.delay(c.duration??650,token);
  }else await super.command(c,token);
 }
}
