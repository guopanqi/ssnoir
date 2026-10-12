import asyncio,os
from pathlib import Path
from playwright.async_api import async_playwright
BASE=os.environ.get('STAGE_URL','http://127.0.0.1:5190')
DEST=Path(__file__).resolve().parents[1]/'captures'
DEST.mkdir(exist_ok=True)
async def main():
 async with async_playwright() as p:
  browser=await p.chromium.launch(headless=True,args=['--no-sandbox','--disable-dev-shm-usage'])
  page=await browser.new_page(viewport={'width':1600,'height':1200},device_scale_factor=1)
  errors=[]
  page.on('pageerror',lambda e:errors.append(str(e)))
  await page.goto(BASE+'/?pause=1',wait_until='load')
  await page.wait_for_function('!!window.__cutout',timeout=10000)
  frames={'drunk':[1,4,5.5,8], 'brace':[1,4,6,8.5], 'run':[2,4,6,10.5]}
  for scene,times in frames.items():
   await page.evaluate('(v)=>window.__cutout.setScene(v)',scene)
   for t in times:
    await page.evaluate('(v)=>window.__cutout.seek(v)',t)
    await page.locator('#stage').screenshot(path=str(DEST/f'{scene}-{t}.png'))
   await page.evaluate('()=>window.__cutout.setView("compare")')
   await page.locator('#stage').screenshot(path=str(DEST/f'{scene}-compare.png'))
   await page.evaluate('()=>window.__cutout.setView("stage")')
  await page.evaluate('()=>window.__cutout.setView("exploded")')
  await page.locator('#stage').screenshot(path=str(DEST/'exploded.png'))
  if errors:raise RuntimeError('Browser errors: '+repr(errors))
  print('PASS - rendered',len(list(DEST.glob('*.png'))),'browser screenshots')
  await browser.close()
asyncio.run(main())
