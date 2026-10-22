// @vitest-environment jsdom
import { afterEach, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { LibraryView } from "./LibraryView";
import { previewState } from "./preview";
import type { Run, Snapshot } from "./types";
afterEach(cleanup);
const clock=previewState.catalog.items.find(i=>i.id==="glass-clock")!;
const photo=previewState.catalog.items.find(i=>i.id==="glass-photo")!;
function fixture():Snapshot{return {...previewState,installed:[clock,photo].map(item=>({item,packages:[],installedAt:""})),activeCount:99,widgets:[{id:"shown",contentId:clock.id,kind:"clock",pack:"glass",size:"M",enabled:true,visible:true,monitor:""},{id:"failed",contentId:photo.id,kind:"photo",pack:"glass",size:"M",enabled:true,visible:false,monitor:""},{id:"hidden",contentId:clock.id,kind:"clock",pack:"glass",size:"M",enabled:false,visible:false,monitor:""}]};}
function draw(tab:string,state=fixture()){render(<LibraryView state={state} run={vi.fn(async()=>{}) as Run} onBrowse={()=>{}} onDownloads={()=>{}} onOpen={()=>{}} preferences={{tab,query:"",pack:"all",sort:"recent"}} setPreferences={()=>{}}/>);}
it("On desktop counts and lists only windows actually displayed",()=>{draw("active");expect(screen.getByRole("button",{name:"On desktop 1"})).toBeTruthy();expect(screen.queryByText("Photos")).toBeNull();expect(screen.getAllByRole("button",{name:"Hide"})).toHaveLength(1);expect(screen.queryByRole("button",{name:"Show"})).toBeNull();});
it("Hidden instances have their own view and can be restored",()=>{draw("hidden");expect(screen.getAllByRole("button",{name:"Show"})).toHaveLength(2);expect(screen.queryByRole("button",{name:"Hide"})).toBeNull();});
it("paused widgets are not reported as visible and offer one Resume action",()=>{const state=fixture();state.snoozed=true;state.widgets=state.widgets.map(w=>({...w,visible:false}));draw("active",state);expect(screen.getByRole("button",{name:"On desktop 0"})).toBeTruthy();expect(screen.getByRole("button",{name:"Resume widgets"})).toBeTruthy();expect(screen.queryByRole("button",{name:"Hide"})).toBeNull();});
