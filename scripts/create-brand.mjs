import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {deflateSync} from 'node:zlib';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
function crc(buffer){let c=0xffffffff;for(const b of buffer){c^=b;for(let i=0;i<8;i++)c=(c>>>1)^((c&1)?0xedb88320:0);}return(c^0xffffffff)>>>0;}
function chunk(type,data){const label=Buffer.from(type);const output=Buffer.alloc(data.length+12);output.writeUInt32BE(data.length);label.copy(output,4);data.copy(output,8);output.writeUInt32BE(crc(Buffer.concat([label,data])),data.length+8);return output;}
const size=256, pixels=Buffer.alloc((size*4+1)*size);
for(let y=0;y<size;y++)for(let x=0;x<size;x++){let coverage=0;for(let sy=0;sy<4;sy++)for(let sx=0;sx<4;sx++){const radius=Math.hypot(x+(sx+.5)/4-128,y+(sy+.5)/4-128);if(radius>=67&&radius<=105)coverage++;}const index=y*(size*4+1)+1+x*4;const light=Math.round(247-55*y/256);pixels[index]=pixels[index+1]=pixels[index+2]=light;pixels[index+3]=Math.round(coverage/16*255);}
const header=Buffer.alloc(13);header.writeUInt32BE(size);header.writeUInt32BE(size,4);header[8]=8;header[9]=6;
const png=Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',header),chunk('IDAT',deflateSync(pixels)),chunk('IEND',Buffer.alloc(0))]);
const icon=Buffer.alloc(22);icon.writeUInt16LE(1,2);icon.writeUInt16LE(1,4);icon.writeUInt16LE(1,10);icon.writeUInt16LE(32,12);icon.writeUInt32LE(png.length,14);icon.writeUInt32LE(22,18);
const svg='<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256"><defs><linearGradient id="ink" x2="0" y2="1"><stop stop-color="#f7f7f7"/><stop offset="1" stop-color="#c0c0c0"/></linearGradient></defs><path fill="url(#ink)" fill-rule="evenodd" d="M128 23a105 105 0 1 1 0 210 105 105 0 0 1 0-210Zm0 38a67 67 0 1 0 0 134 67 67 0 0 0 0-134Z"/></svg>\n';
fs.writeFileSync(path.join(root,'assets/Logo.png'),png);fs.writeFileSync(path.join(root,'assets/YoWidgets.ico'),Buffer.concat([icon,png]));fs.writeFileSync(path.join(root,'assets/Mark.svg'),svg);console.log('Created original empty-ring SVG, PNG and ICO.');
