// Reports only paths/rule names, never matched secret values. Also checks Git history.
import fs from 'node:fs';
import path from 'node:path';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const git=(args)=>execFileSync('git',args,{cwd:root,maxBuffer:64*1024*1024,encoding:'utf8',stdio:['ignore','pipe','pipe']});
const protectedPrefix=['AQAAANCM','nd8BFdERjHoAwE'].join('');
const rules=[['OpenAI/Anthropic key',/\bsk-(?:proj-|ant-api\d+-|or-v\d+-)?[A-Za-z0-9_-]{32,}/g],['Google API key',/\bAIza[A-Za-z0-9_-]{30,}/g],['GitHub token',/\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,})/g],['AWS access key',/\b(?:AKIA|ASIA)[A-Z0-9]{16}\b/g],['Private key',/-----BEGIN (?:RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----/g],['DPAPI credential',new RegExp(protectedPrefix+'.{40,}','g')],['Hardcoded credential',/(?:api[_-]?key|access[_-]?token|password|secret)\s*[=:]\s*["'][A-Za-z0-9+/_.=-]{28,}["']/gi]];
let count=0;const findings=[];
function scan(buffer,label){if(buffer.includes(0))return;const text=buffer.toString('utf8');for(const [rule,re]of rules){re.lastIndex=0;if(re.test(text))findings.push({path:label,rule});}count++;}
const files=[...new Set(git(['ls-files','--cached','--others','--exclude-standard','-z']).split('\0').filter(Boolean))];
for(const relative of files){const file=path.join(root,relative);if(!fs.existsSync(file)||!fs.statSync(file).isFile())continue;if(/(?:^|\/)(?:node_modules|artifacts|\.tools|\.git|Cache)(?:\/|$)/i.test(relative)||/\.env(?:\.|$)|(?:^|\/)settings\.json(?:\.|$)|\.pfx$|\.key$/i.test(relative)){findings.push({path:relative,rule:'Sensitive or generated path in source set'});continue;}const size=fs.statSync(file).size;if(size>8*1024*1024){findings.push({path:relative,rule:'Unexpected source file over 8 MiB'});continue;}scan(fs.readFileSync(file),relative);}
if(process.argv.includes('--history')){const objects=git(['rev-list','--objects','--all']).trim().split('\n');for(const line of objects){const space=line.indexOf(' ');if(space<0)continue;const oid=line.slice(0,space),name=line.slice(space+1);try{if(git(['cat-file','-t',oid]).trim()!=='blob')continue;scan(execFileSync('git',['cat-file','blob',oid],{cwd:root,maxBuffer:16*1024*1024,stdio:['ignore','pipe','pipe']}),'history:'+oid.slice(0,8)+':'+name);}catch{findings.push({path:name,rule:'Could not audit historical blob'});}}}
console.log(JSON.stringify({scannedTextFiles:count,findings},null,2));if(findings.length)process.exitCode=1;
