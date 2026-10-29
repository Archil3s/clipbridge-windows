"use strict";
const assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const fs = require('node:fs/promises');
const path = require('node:path');
const { chromium } = require('../../cloudflare-dashboard/node_modules/playwright');
const root = path.resolve(__dirname, '../..');
function fixture(mode) {
  const rich = ['rich', 'delayed'].includes(mode);
  const controls = '<input type="file" multiple accept="video/mp4"><div id="chips"></div><button type="button" ' +
    (rich ? 'id="composer-submit-button"' : 'data-testid="send-button"') + ' disabled>Send</button>';
  const markup = rich ? '<textarea id="prompt-textarea" hidden></textarea><div id="composer-background"><div><div id="prompt-textarea" contenteditable="true" class="ProseMirror" role="textbox"><p><br></p></div></div>' + controls + '</div>' :
    '<form><div data-type="unified-composer"><textarea id="prompt-textarea"></textarea></div>' + controls + '</form>';
  return `<!doctype html><html><body><main>${markup}<div id="turns"></div></main><script>
    const mode=${JSON.stringify(mode)};
    const input=document.querySelector('input'),editor=document.querySelector('[contenteditable="true"]')||document.querySelector('textarea'),send=document.querySelector('button'),chips=document.querySelector('#chips');
    const getText=()=>editor.isContentEditable?editor.innerText:editor.value;
    const setText=value=>{if(editor.isContentEditable)editor.textContent=value;else editor.value=value;};
    window.changes=[];window.sends=[];window.payloads=[];window.premature=false;
    if(mode==='delayed'){const root=document.querySelector('#composer-background');root.remove();setTimeout(()=>document.querySelector('main').prepend(root),3500);}
    if(mode==='draft')setText('My unfinished reel instructions');
    input.onchange=async()=>{
      const files=Array.from(input.files);window.changes.push(files.map(file=>file.name));window.currentFiles=files;
      send.disabled=true;chips.replaceChildren();
      for(const file of (mode==='partial'?files.slice(0,2):files)){const chip=document.createElement('span');chip.textContent=file.name;chips.append(chip);}
      const progress=document.createElement('span');progress.setAttribute('role','progressbar');progress.textContent='Uploading';chips.append(progress);
      window.payloads.push(await Promise.all(files.map(async file=>({name:file.name,size:file.size,bytes:Array.from(new Uint8Array(await file.arrayBuffer()))}))));
      if(mode==='error')setTimeout(()=>{const alert=document.createElement('div');alert.setAttribute('role','alert');alert.textContent='Upload limit reached';document.body.append(alert);},100);
      if(mode==='edit')setTimeout(()=>{setText('My own instructions, keep this text');editor.dispatchEvent(new Event('input',{bubbles:true}));},300);
      if(mode!=='pending')setTimeout(()=>{progress.remove();send.disabled=mode==='disabled';},450);
    };
    send.onclick=()=>{
      if(chips.querySelector('[role="progressbar"]'))window.premature=true;
      window.sends.push({names:window.currentFiles.map(file=>file.name),message:getText()});
      const turn=document.createElement('div');turn.setAttribute('data-message-author-role','user');turn.textContent=getText();document.querySelector('#turns').append(turn);
      chips.replaceChildren();input.value='';setText('');send.disabled=true;
      const stop=document.createElement('button');stop.setAttribute('data-testid','stop-button');stop.textContent='Stop';document.body.append(stop);
      setTimeout(()=>{const reply=document.createElement('div');reply.setAttribute('data-message-author-role','assistant');reply.textContent='Batch received';document.querySelector('#turns').append(reply);stop.remove();},500);
    };
  </script></body></html>`;
}
async function run(mode) {
  const server = spawn(process.env.DOTNET_EXE || 'dotnet', [path.join(root,
    'tests/ClipBridge.Core.Tests/bin/Release/net10.0-windows/ClipBridge.Core.Tests.dll'), '--serve'], { cwd: root, windowsHide: true });
  let output='';
  const started = new Promise((resolve,reject)=>{
    const timeout=setTimeout(()=>reject(new Error('Fixture startup: '+output)),20000);
    server.stdout.on('data',data=>{output+=data;if(output.includes('BRIDGE_TEST_READY')){clearTimeout(timeout);resolve();}});
    server.stderr.on('data',data=>{output+=data;});
    server.once('error',error=>{clearTimeout(timeout);reject(error);});
    server.once('exit',code=>{clearTimeout(timeout);reject(new Error('Fixture exited '+code+' '+output));});
  });
  let context;
  try {
    await started;
    const directory=await fs.mkdtemp(path.join(root,'work','batch-browser-'));
    const extension=path.join(directory,'extension');
    await fs.cp(path.join(root,'brave-extension'),extension,{recursive:true});
    const workerPath=path.join(extension,'background.js');
    await fs.writeFile(workerPath,(await fs.readFile(workerPath,'utf8')).replace('http://127.0.0.1:38419','http://127.0.0.1:38429'));
    const manifestPath=path.join(extension,'manifest.json');
    await fs.writeFile(manifestPath,(await fs.readFile(manifestPath,'utf8')).replace('http://127.0.0.1:38419','http://127.0.0.1:38429'));
    context=await chromium.launchPersistentContext(path.join(directory,'profile'),{
      channel:'chromium',headless:true,chromiumSandbox:true,
      ...(process.env.CHROMIUM_EXE?{executablePath:process.env.CHROMIUM_EXE}:{}),
      args:[`--disable-extensions-except=${extension}`,`--load-extension=${extension}`]
    });
    const worker=context.serviceWorkers()[0]||await context.waitForEvent('serviceworker');
    const page=await context.newPage();
    await page.route('https://chatgpt.com/**',route=>route.fulfill({contentType:'text/html',body:fixture(mode)}));
    await page.goto('https://chatgpt.com/c/synthetic-batch');
    await page.bringToFront();
    await worker.evaluate(async mode=>{
      await chrome.storage.local.set({pairingKey:'A'.repeat(64),enabled:true,batchSize:3,
        ...(mode==='manual'?{}:{autoSend:true}),batchPrompt:'Gardening clips for reels. Wait for my instructions.'});
      await handle({type:'arm'},{id:chrome.runtime.id,url:chrome.runtime.getURL('popup.html')});
    },mode);
    if(mode==='delayed') {
      await page.waitForTimeout(1900);
      assert.equal((await worker.evaluate(()=>chrome.storage.session.get('batch'))).batch,undefined);
      assert.match((await worker.evaluate(()=>chrome.storage.local.get('lastStatus'))).lastStatus,/Waiting for the ChatGPT message box/);
    }
    if(mode==='draft') {
      await page.waitForFunction(async()=>window.changes.length===0&&document.querySelector('textarea').value.length>0);
      for(let i=0;i<30;i++){
        const status=await worker.evaluate(()=>chrome.storage.local.get('lastStatus'));
        if(status.lastStatus?.includes('already contains'))break;
        await new Promise(resolve=>setTimeout(resolve,100));
      }
      assert.match((await worker.evaluate(()=>chrome.storage.local.get('lastStatus'))).lastStatus,/already contains/);
      assert.equal((await worker.evaluate(()=>chrome.storage.session.get('batch'))).batch,undefined);
      assert.equal(await page.inputValue('textarea'),'My unfinished reel instructions');
      assert.equal(await page.evaluate(()=>window.sends.length),0);
    } else {
      await page.waitForFunction(()=>window.changes.length>0,null,{timeout:15000});
      if(['auto','rich','delayed'].includes(mode)) {
        await page.waitForFunction(()=>window.sends.length===2,null,{timeout:25000});
        const result=await page.evaluate(()=>({sends:window.sends,changes:window.changes,premature:window.premature,payloads:window.payloads}));
        assert.deepEqual(result.changes.map(files=>files.length),[3,1]);
        assert.equal(new Set(result.sends.flatMap(batch=>batch.names)).size,4);
        assert.equal(result.premature,false);
        assert.ok(result.sends.every(batch=>batch.message.includes('Gardening clips for reels.')));
        assert.equal(result.payloads.flat().length,4);
        for(const payload of result.payloads.flat()){
          const number=Number(payload.name.match(/-(\d+)\.mp4$/)[1]);
          assert.equal(payload.size,700000);
          assert.ok(payload.bytes.every((byte,i)=>byte===(i+number)%251));
        }
      } else {
        if(mode==='navigation') await page.evaluate(()=>history.pushState({},'', '/c/another-conversation'));
        await page.waitForTimeout(2400);
        assert.equal(await page.evaluate(()=>window.sends.length),0);
        assert.equal(await page.evaluate(()=>window.changes[0].length),3);
        if(mode==='edit') assert.equal(await page.inputValue('textarea'),'My own instructions, keep this text');
        const state=await worker.evaluate(()=>chrome.storage.session.get(['armed','batch']));
        if(['manual','error','edit','navigation'].includes(mode)) assert.equal(state.armed,undefined);
        else assert.equal(state.batch.sendReserved,false);
      }
    }
    await worker.evaluate(()=>handle({type:'disable'},{id:chrome.runtime.id,url:chrome.runtime.getURL('popup.html')}));
    console.log('PASS real sandboxed Chromium batch scenario: '+mode);
  } finally {
    await context?.close();
    const exited=new Promise(resolve=>server.once('exit',resolve));
    if(server.exitCode===null){server.kill();await exited;}
  }
}
(async()=>{
  for(const mode of ['manual','auto','rich','delayed','pending','disabled','partial','error','edit','navigation','draft']) await run(mode);
  console.log('Synthetic pages/files only. Normal Brave, real Google sign-in and live ChatGPT acceptance are not tested.');
})().catch(error=>{console.error(error);process.exitCode=1;});
