// Local synthetic HTTP trial. Never reads credentials or contacts external hosts.
const http = require('node:http');
const assert = require('node:assert/strict');
const { performance } = require('node:perf_hooks');
const body = Buffer.from(JSON.stringify({model:'fixture-only',messages:[{role:'user',content:'Keep AC-9, not approved, 12 tests, 9 discovered.'}]}));
function listen(server) { return new Promise(resolve => server.listen(0, '127.0.0.1', () => resolve(server.address().port))); }
function request(port, data) {
  return new Promise((resolve,reject) => {
    const call = http.request({hostname:'127.0.0.1',port,path:'/',method:'POST',headers:{'content-type':'application/json','content-length':data.length}}, response => {
      const chunks=[];response.on('data',chunk=>chunks.push(chunk));response.on('end',()=>resolve({status:response.statusCode,body:Buffer.concat(chunks)}));
    });
    call.on('error',reject);call.setTimeout(5000,()=>call.destroy(new Error('Fixture timeout')));call.end(data);
  });
}
(async()=>{
  const origin=http.createServer((req,res)=>{const chunks=[];req.on('data',chunk=>chunks.push(chunk));req.on('end',()=>{res.writeHead(200,{'content-type':'application/json'});res.end(Buffer.concat(chunks));});});
  let proxy;
  try {
    const originPort=await listen(origin);
    proxy=http.createServer((req,res)=>{const chunks=[];req.on('data',chunk=>chunks.push(chunk));req.on('end',async()=>{try{const answer=await request(originPort,Buffer.concat(chunks));res.writeHead(answer.status,{'content-type':'application/json'});res.end(answer.body);}catch{res.writeHead(502);res.end('Fixture upstream failed');}});});
    const proxyPort=await listen(proxy);
    const rows=[];
    for(let i=0;i<20;i++) {
      // Alternate order to reduce warmup bias. This remains a local transport microbenchmark.
      for(const [name,port] of (i%2 ? [['proxy',proxyPort],['direct',originPort]] : [['direct',originPort],['proxy',proxyPort]])) {
        const start=performance.now();const response=await request(port,body);
        assert.equal(response.status,200);assert.deepEqual(response.body,body);
        rows.push({name,milliseconds:performance.now()-start});
      }
    }
    const mean=name=>rows.filter(x=>x.name===name).reduce((sum,x)=>sum+x.milliseconds,0)/20;
    process.stdout.write(JSON.stringify({passed:true,requests:40,exactResponseBytes:true,directMeanMs:mean('direct'),proxyMeanMs:mean('proxy'),samples:rows,adopted:false,reason:'Loopback transport only; no model streaming, cost, or production benefit proof.'}));
  } finally {
    if(proxy) await new Promise(resolve=>proxy.close(resolve));
    await new Promise(resolve=>origin.close(resolve));
  }
})().catch(error=>{process.stderr.write(error.stack);process.exitCode=1;});
