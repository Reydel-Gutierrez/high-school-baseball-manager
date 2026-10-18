import asyncio, json, sys, subprocess, time, os
from playwright.async_api import async_playwright
ROOT='/home/claude/facekit'
async def main(specs, out):
    srv = subprocess.Popen([sys.executable,'-m','http.server','8765','--bind','127.0.0.1'],cwd=ROOT,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    time.sleep(0.6)
    try:
        async with async_playwright() as p:
            b = await p.chromium.launch(args=['--use-gl=swiftshader','--enable-unsafe-swiftshader','--ignore-gpu-blocklist'])
            pg = await b.new_page(viewport={'width':512,'height':640})
            pg.on('console', lambda m: print('console:', m.text))
            pg.on('pageerror', lambda e: print('pageerror:', e))
            await pg.goto('http://127.0.0.1:8765/web/index.html')
            await pg.wait_for_function('window.ready===true', timeout=60000)
            for i,(spec,look) in enumerate(specs):
                await pg.evaluate('([s,l])=>window.render(s,l)', [spec,look])
                await pg.locator('#c').screenshot(path=f'{out}_{i}.png')
            await b.close()
    finally:
        srv.terminate()
if __name__=='__main__':
    specs=json.loads(sys.argv[1]); asyncio.run(main(specs, sys.argv[2]))
