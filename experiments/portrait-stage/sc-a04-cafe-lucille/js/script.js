/* SC-A04 固定短戏：露西初识——今天又在哪里上工？
   节拍按“动作—对白—停顿”写，不把每句话变成一次运镜。
   台词来自场景稿，未增删；衣服观察句按玩家此前是否做过码头搬运分支。 */
(function () {
  const at = (ms, fn) => {
    const t = Show.current();
    setTimeout(() => {
      if (t === Show.current()) fn();
    }, ms);
  };

  const wobble = on => {
    const cup = document.getElementById('tray-cup-b');
    if (cup) cup.classList.toggle('wobble', on);
  };

  const takeCup = () => {
    const b = document.getElementById('tray-cup-b');
    if (b) b.style.opacity = 0;
    const fly = document.getElementById('cupFly');
    if (!fly) return;
    fly.style.transition = 'none';
    fly.style.opacity = 1;
    fly.style.transform = 'translate(920px,566px) scale(.8)';
    void fly.getBoundingClientRect();
    fly.style.transition = 'transform 520ms cubic-bezier(.2,.8,.3,1)';
    fly.style.transform = 'translate(1000px,584px) scale(.95)';
  };

  async function perform() {
    const workedDocks = !!window.__workedDocks;
    Show.showSlate(true);
    Audio.startAmbience();

    // 一、线把咖啡馆画出来
    await Show.buildStage();
    Show.showSlate(false);
    Audio.startRadio();
    Audio.doorBell();

    // 二、尼尔端着托盘从门口进来，经过桌边，停到柜台前
    Cast.show('nl', true, 520);
    Cast.pose('nl', 'nlTray');
    await Show.wait(240);
    Cast.move('nl', 704, 820, 1250);
    await Show.wait(1300);
    Cast.move('nl', 900, 820, 1050);
    await Show.wait(1050);

    // 三、露西在收银台后数零钱，没抬头
    Cast.show('lu', true, 440);
    Cast.pose('lu', 'luCountA');
    Audio.coin();
    await Show.wait(480);
    Cast.pose('lu', 'luCountB');
    Audio.coin();
    await Show.wait(560);
    Cast.pose('lu', 'luCountA');
    await Show.wait(420);

    Show.cameraTo('counter', 900);
    await Show.wait(700);

    // 四、她抬头，看见这个生面孔
    Cast.pose('lu', 'luLook');
    Cast.pose('nl', 'nlTrayFlat');
    await Show.say('lu', '你是新来的？');
    Cast.pose('nl', 'nlTrayNod');
    await Show.say('nl', '嗯。');

    Cast.pose('lu', 'luTalk');
    await Show.say('lu', '只干今天？');
    Cast.pose('nl', 'nlTrayWry');
    await Show.say('nl', '看老板明天还要不要我。');

    Cast.pose('lu', 'luDry');
    await Show.say('lu', '只要你没摔盘子就行。');
    await Show.wait(180);

    // 五、托盘上的杯子晃了一下；她伸手接过，放稳
    Cast.pose('nl', 'nlTrayShift');
    wobble(true);
    await Show.wait(260);
    Show.cameraTo('cup', 650);
    await Show.wait(540);
    Cast.pose('lu', 'luReach');
    await Show.wait(360);
    Audio.clink();
    wobble(false);
    takeCup();
    Cast.pose('lu', 'luSteady');
    await Show.wait(420);
    Cast.pose('lu', 'luPlace');
    await Show.wait(260);

    Cast.pose('lu', 'luSmile');
    await Show.say('lu', '先学会把杯子摆平。');

    // 六、她笑了；这一拍只给表情，不加动作
    Cast.pose('lu', 'luLaugh');
    await Show.wait(900);
    Show.cameraTo('counter', 750);
    await Show.wait(420);

    // 七、她看见他袖子上的痕迹——按玩家是否做过码头搬运分支
    if (workedDocks) {
      Cast.pose('lu', 'luNotice');
      Show.setCoal(true);
      await Show.say('lu', '你之前在码头干过活？袖子上还有煤灰。');
    } else {
      Cast.pose('lu', 'luNotice');
      await Show.say('lu', '你以前干什么的？看起来不像常端盘子的人。');
    }

    Cast.pose('nl', 'nlTrayWry');
    await Show.say('nl', '哪儿有活儿就去哪儿。');

    Cast.pose('lu', 'luSmile');
    await Show.say('lu', '那你挺忙的。明天要是还来，记得从后面拿托盘，别挡住客人。');

    // 八、收场：她拿起一只杯子继续擦，没有停下来等他回答
    Cast.pose('nl', 'nlTrayFlat');
    Cast.pose('lu', 'luTurn');
    Show.cameraTo('end', 2200);
    Audio.wipe();
    at(700, () => Cast.pose('lu', 'luWipeA'));
    at(1300, () => { Cast.pose('lu', 'luWipeB'); Audio.wipe(); });
    at(1950, () => { Cast.pose('lu', 'luWipeA'); Audio.wipe(); });
    Audio.fadeMusic(.1, 4);
    await Show.wait(2600);
    await Show.wait(900);
  }

  window.perform = perform;
})();
