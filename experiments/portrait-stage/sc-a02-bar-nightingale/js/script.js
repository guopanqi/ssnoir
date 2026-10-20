/* SC-A02 固定开场：酒保与夜莺初次亮相
   节拍按“动作—对白—停顿”写，不把每句话变成一次运镜。
   台词来自用户给的场景稿，未增删。 */
(function () {
  const at = (ms, fn) => {
    const t = Show.current();
    setTimeout(() => {
      if (t === Show.current()) fn();
    }, ms);
  };

  const glassTo = (x, y, ms = 420, opacity = 1) => {
    const g = document.getElementById('glass');
    if (!g) return;
    g.style.transitionDuration = ms + 'ms';
    g.style.transform = `translate(${x}px,${y}px) scale(1.18)`;
    g.style.opacity = opacity;
  };

  async function perform() {
    Show.showSlate(true);
    Audio.startAmbience();

    // 一、线自己把酒馆画出来
    await Show.buildStage();
    Show.showSlate(false);

    Cast.show('bm', true, 520);
    Cast.show('nl', true, 520);
    Cast.pose('bm', 'bmWipeA');
    Cast.pose('nl', 'nlLean');
    Audio.wipe();

    await Show.wait(320);
    Show.cameraTo('bar', 900);
    await Show.wait(520);

    // 二、吧台对话：酒保一直在擦杯子，尼尔一直没坐直
    await Show.say('bm', '又没活儿了？');
    Cast.pose('bm', 'bmGlance');
    await Show.wait(180);

    Cast.pose('nl', 'nlWry');
    await Show.say('nl', '有就不来问你了。');
    Cast.pose('nl', 'nlTalk');
    await Show.wait(160);

    Cast.pose('bm', 'bmTalk');
    Audio.wipe();
    await Show.say('bm', '码头，咖啡馆都有零工可做。');
    Cast.pose('bm', 'bmShrug');
    await Show.wait(180);

    Cast.pose('nl', 'nlAsk');
    await Show.say('nl', '你这儿呢？');
    await Show.wait(160);

    Cast.pose('bm', 'bmLever');
    at(2100, () => Cast.pose('bm', 'bmShrug'));
    await Show.say('bm', '还是和以前一样，看场子，有人闹事就你来处理。至于平常的工钱，你知道。');
    await Show.wait(280);

    // 三、麦克风噪声把两个人的头一起拧向舞台
    Show.clearCaption();
    Audio.micTest();
    Cast.pose('bm', 'bmGlance');
    Cast.pose('nl', 'nlTurnStage');
    Show.cameraTo('stage', 900);
    Show.opacity('ny', .9, 500);
    Show.silhouette('ny', true);
    await Show.wait(780);

    // 追光落下，舞台才真正出现
    Show.stageLight(true);
    Show.silhouette('ny', false);
    Show.opacity('ny', 1, 700);
    Cast.pose('ny', 'nySettle');
    Show.audience(true, 1100);
    Audio.startMusic();
    Show.cameraTo('stage', 900);
    await Show.wait(900);

    Cast.pose('ny', 'nyListen');
    await Show.wait(900);
    Cast.pose('ny', 'nySettle');
    await Show.wait(700);

    // 两小节伴奏过去，她开口
    await Show.wait(1900);

    // 四、舞台全景保持住：先看舞台和观众，不急着靠近
    Cast.pose('ny', 'nySingA');
    await Show.wait(2600);
    Cast.pose('ny', 'nySingB');
    await Show.wait(2600);
    Cast.pose('ny', 'nySingA');
    await Show.wait(2600);
    Cast.pose('ny', 'nySingB');
    await Show.wait(2600);

    // 五、掌声；唯一一次近景
    Audio.applause();
    Show.showHands(true);
    Show.cameraTo('close', 780);
    Cast.pose('ny', 'nySmile');
    await Show.wait(2400);
    Show.showHands(false);

    // 六、回到吧台。她点头，继续下一段
    Show.audience(false, 700);
    Show.cameraTo('bar', 900);
    Cast.pose('ny', 'nyNod');
    at(850, () => Cast.pose('ny', 'nySingA'));

    Cast.pose('nl', 'nlWatch');
    await Show.wait(900);
    await Show.say('nl', '她是谁？');
    await Show.wait(200);

    Cast.pose('bm', 'bmTalk');
    await Show.say('bm', '夜莺。就快去大剧院了。');
    await Show.wait(220);

    Cast.pose('nl', 'nlDoubt');
    await Show.say('nl', '那里会要我们这儿的人？');
    await Show.wait(240);

    // 酒保沿吧台走两步，收走空杯
    Cast.move('bm', 600, 790, 900);
    await Show.wait(720);
    Cast.pose('bm', 'bmCollect');
    Audio.clink();
    glassTo(632, 546, 380);
    at(420, () => glassTo(606, 578, 520));

    await Show.say('bm', '也许是她唱得好吧。');

    glassTo(596, 606, 600, 0);
    await Show.wait(320);

    // 七、收场：她在台上继续唱，镜头退开
    Cast.pose('bm', 'bmListen');
    Cast.pose('nl', 'nlWatch');
    at(1200, () => Cast.pose('ny', 'nySingB'));
    Show.cameraTo('end', 2400);
    Audio.fadeMusic(.08, 3.4);
    await Show.wait(2500);
    await Show.wait(1200);
  }

  window.perform = perform;
})();
