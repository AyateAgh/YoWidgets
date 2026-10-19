sampler2D backdrop : register(s0);
float4 geometry : register(c0); // visible width/height, radius, padded margin
float4 optics : register(c1); // strength, tint, light, relative height
float4 lighting : register(c2); // mouse x/y, interaction, unused

float4 main(float2 uv:TEXCOORD):COLOR {
 float2 size=geometry.xy;
 float pad=geometry.w;
 float2 total=size+pad*2;
 float2 p=uv*total-pad-size*.5;
 float2 d=abs(p)-(size*.5-geometry.z);
 float dist=length(max(d,0))+min(max(d.x,d.y),0)-geometry.z;
 float coverage=saturate(.7-dist);
 float edge=max(-dist,0);
 float2 signP=float2(p.x>=0?1:-1,p.y>=0?1:-1);
 float2 g=length(max(d,0))>.001?normalize(max(d,0))*signP:(d.x>d.y?float2(signP.x,0):float2(0,signP.y));
 float u=saturate(edge/25);
 float slope=min(pow(1-u,3)/pow(max(1-pow(1-u,4),.002),.75),7);
 float3 n=normalize(float3(g*slope,1));
 float3 rd=refract(float3(0,0,-1),n,1/1.5);
 float2 shift=rd.xy/max(-rd.z,.2)*29*optics.x/total;
 float2 lens=uv;
 lens.y=(uv.y-.5)*(1-.07*optics.x)+.5;
 float2 a=lens+shift;
 float3 col;
 col.r=tex2D(backdrop,saturate(a+shift*.035)).r;
 col.g=tex2D(backdrop,saturate(a)).g;
 col.b=tex2D(backdrop,saturate(a-shift*.035)).b;
 float lum=dot(col,float3(.2126,.7152,.0722));
 col=lerp(lum.xxx,col,1.15);
 col=lerp(col,lerp(float3(.045,.065,.09),float3(.98,.985,1),optics.z),optics.y);
 float facing=dot(g,normalize(float2(-.55,-.83)));
 float spec=pow(max(facing,0),1.6)+.5*pow(max(-facing,0),2.2);
 col+=(exp(-edge/1.15)*(.13+.57*spec)+exp(-edge/5)*.045)*(.7+.3*optics.x);
 col-=.035*exp(-edge/7)*max(g.y,0);
 float2 mouse=lighting.xy*size-size*.5;
 col+=exp(-length(p-mouse)/90)*lighting.z*.035;
 float alpha=coverage;
 return float4(saturate(col)*alpha,alpha);
}
