import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../catalog/v1');
const port=Number(process.argv[2]??8769);
http.createServer((request,response)=>{try{const url=new URL(request.url,'http://127.0.0.1');if(url.pathname==='/catalog.json'){const document=JSON.parse(fs.readFileSync(path.join(root,'catalog.json'),'utf8'));document.packages.forEach(p=>p.url=`http://127.0.0.1:${port}/packages/${p.id}.zip`);response.writeHead(200,{'Content-Type':'application/json','Cache-Control':'no-store'});response.end(JSON.stringify(document));return;}if(!/^\/packages\/[a-z0-9.-]+\.zip$/.test(url.pathname)){response.writeHead(404);response.end();return;}const file=path.resolve(root,'.'+url.pathname);if(!file.startsWith(root+path.sep)||!fs.existsSync(file)){response.writeHead(404);response.end();return;}response.writeHead(200,{'Content-Type':'application/zip','Content-Length':fs.statSync(file).size});fs.createReadStream(file).pipe(response);}catch{response.writeHead(500);response.end('Catalog unavailable');}}).listen(port,'127.0.0.1',()=>console.log(`Development catalog: http://127.0.0.1:${port}/catalog.json`));
