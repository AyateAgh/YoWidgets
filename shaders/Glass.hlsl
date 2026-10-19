sampler2D backdrop : register(s0);
float4 geometry : register(c0); // width, height, corner radius, transparent padding
float4 optics : register(c1); // refraction, tint, light content, reserved
float4 lighting : register(c2); // normalized pointer, interaction, reserved

float4 main(float2 uv:TEXCOORD):COLOR {
 float2 size=geometry.xy, total=size+geometry.w*2;
 float2 p=uv*total-geometry.w-size*.5;
 float2 q=abs(p)-(size*.5-geometry.z);
 float dist=length(max(q,0))+min(max(q.x,q.y),0)-geometry.z;
 float coverage=saturate(-dist), depth=max(-dist,0);
 float2 s=float2(p.x>=0?1:-1,p.y>=0?1:-1);
 float2 g=length(max(q,0))>.001?normalize(max(q,0))*s:(q.x>q.y?float2(s.x,0):float2(0,s.y));
 // Broad convex meniscus; quiet center, concentrated lensing at the silhouette.
 float bend=pow(saturate(1-depth/30),2.4);
 float3 normal=normalize(float3(g*bend*2.8,1));
 float3 ray=refract(float3(0,0,-1),normal,1/1.47);
 float2 offset=ray.xy/max(-ray.z,.3)*26*optics.x/total;
 float2 lens=(uv-.5)*(1-.018*optics.x)+.5+offset;
 float3 c;
 c.r=tex2D(backdrop,saturate(lens+offset*.022)).r;
 c.g=tex2D(backdrop,saturate(lens)).g;
 c.b=tex2D(backdrop,saturate(lens-offset*.022)).b;
 float lum=dot(c,float3(.2126,.7152,.0722));
 // Avoid painting the wallpaper's saturated blue across unrelated foreground apps.
 c=lerp(lum.xxx,c,.64);
 float3 tint=lerp(float3(.10,.12,.15),float3(.92,.95,.98),optics.z);
 c=lerp(c,tint,.12+optics.y*.35);
 float2 light=normalize(float2(-.65,-.8)+(lighting.xy-.5)*lighting.z*.8);
 float facing=dot(g,light);
 float key=pow(saturate(facing),4), counter=pow(saturate(-facing),6);
 float hair=exp(-depth/.8), second=exp(-abs(depth-2.1)/.9);
 float glow=exp(-length(p-(lighting.xy-.5)*size)/100)*lighting.z;
 c+=hair*(.07+key*.58+counter*.22)+second*key*.055+glow*.05;
 c-=exp(-depth/8)*saturate(g.y)*.09;
 // Localized edge highlights carry depth; the interior remains truly see-through.
 float alpha=coverage*saturate(.18+optics.y*.23+bend*.04+hair*(.15+key*.25)+glow*.025);
 return float4(saturate(c)*alpha,alpha);
}
