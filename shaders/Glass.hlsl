float4 geometry : register(c0); // content size, radius, transparent padding
float4 material : register(c1); // liquid package, neutral tint
float4 lighting : register(c2); // pointer position and hover
float motion : register(c3);
// Optical overlay only. The live background is a separate compositor surface.
// Deliberately no wallpaper texture or screen image sampler.
float4 main(float2 uv:TEXCOORD):COLOR {
 float2 size=geometry.xy, total=size+geometry.w*2;
 float2 p=uv*total-geometry.w-size*.5;
 float2 q=abs(p)-(size*.5-geometry.z);
 float dist=length(max(q,0))+min(max(q.x,q.y),0)-geometry.z;
 float coverage=saturate(-dist), depth=max(-dist,0);
 float2 signP=float2(p.x>=0?1:-1,p.y>=0?1:-1);
 float2 normal=length(max(q,0))>.001?normalize(max(q,0))*signP:(q.x>q.y?float2(signP.x,0):float2(0,signP.y));
 float liquid=material.x;
 float2 direction=normalize(float2(-.55,-.85)+(lighting.xy-.5)*lighting.z*.5);
 float facing=dot(normal,direction);
 float key=pow(saturate(facing),3), counter=pow(saturate(-facing),5);
 float hair=exp(-depth/.65), inner=exp(-abs(depth-1.6)/.85);
 float meniscus=pow(saturate(1-depth/(10+liquid*12+motion*5)),2.5);
 float pointerGlow=exp(-length(p-(lighting.xy-.5)*size)/100)*lighting.z;
 float tint=material.y+(.025+liquid*.018)*saturate(.5-p.y/max(size.y,1));
 float edge=hair*(.14+key*(.30+liquid*.38)+counter*.14);
 float glow=liquid*(inner*key*.055+meniscus*(.025+motion*.035)+pointerGlow*.018);
 float alpha=coverage*saturate(tint+edge+glow);
 float3 color=lerp(float3(.72,.77,.82),float3(1,1,1),saturate(.35+key*.35+hair*.3));
 return float4(color*alpha,alpha);
}
