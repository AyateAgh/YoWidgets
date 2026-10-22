import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const ui=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../ui');
const lock=JSON.parse(fs.readFileSync(path.join(ui,'package-lock.json'),'utf8'));
const sections=[];
for(const [relative,entry] of Object.entries(lock.packages)){
 if(!relative||entry.dev||!relative.startsWith('node_modules/'))continue;
 const directory=path.join(ui,relative);
 const metadata=JSON.parse(fs.readFileSync(path.join(directory,'package.json'),'utf8'));
 const license=fs.readdirSync(directory).find(name=>/^licen[sc]e(?:\.[a-z]+)?$/i.test(name));
 const source=license?path.join(directory,license):path.join(ui,'licenses',metadata.name.replaceAll('/','-')+'.txt');
 if(!fs.existsSync(source))throw new Error('Missing runtime license: '+relative);
 sections.push(`${metadata.name} ${metadata.version}\n${'='.repeat(64)}\n${fs.readFileSync(source,'utf8')}`);
}
fs.writeFileSync(path.join(ui,'THIRD-PARTY-LICENSES.txt'),sections.join('\n\n'));
console.log(`Collected ${sections.length} runtime licenses.`);
