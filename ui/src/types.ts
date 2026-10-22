export interface CatalogItem {
  id: string;
  title: string;
  description: string;
  category: string;
  type: "widget" | "pack";
  kind: string;
  pack: string;
  collectionName?: string;
  author: string;
  version: string;
  tags: string[];
  packages: string[];
  includes: string[];
  permissions: string[];
  publishedAt: string;
}
export interface ContentPackage {
  id: string;
  version: string;
  url: string;
  sha256: string;
  bytes: number;
  dependencies: string[];
}
export interface Catalog {
  schemaVersion: number;
  version: string;
  items: CatalogItem[];
  packages: ContentPackage[];
}
export interface Installed {
  item: CatalogItem;
  packages: { id: string; version: string; sha256: string }[];
  installedAt: string;
  needsRepair?: boolean;
}
export interface Widget {
  id: string;
  contentId?: string;
  kind: string;
  pack: string;
  size: string;
  enabled: boolean;
  monitor: string;
}
export interface Job {
  id: string;
  itemId: string;
  title: string;
  stage: string;
  downloaded: number;
  total: number;
  error: string;
  startedAt: string;
}
export interface ChatConnection {
  provider: string;
  model: string;
  connected: boolean;
}
export interface GenerationConnection {
  id: string;
  title: string;
  mode: string;
  provider: string;
  model: string;
  voice: string;
  keySaved: boolean;
  providers: string[];
}
export interface Voice {
  geminiModel: string;
  openAIModel: string;
  geminiVoice: string;
  openAIVoice: string;
  language: string;
  allowActions: boolean;
}
export interface Settings {
  city: string;
  analog: boolean;
  fahrenheit: boolean;
  seconds: boolean;
  hour24: boolean;
  topmost: boolean;
  interfaceSounds: boolean;
  interfaceVolume: number;
  interfaceTheme: string;
  startup: boolean;
  lyricsEnabled: boolean;
  notificationsEnabled: boolean;
  notificationStatus: string;
  aiAskBeforeControl: boolean;
  aiProcessText: string;
  voice: Voice;
  voiceChoices: { gemini: string[]; openai: string[] };
  chatProvider: string;
  chatModel: string;
  chat: ChatConnection[];
  generation: GenerationConnection[];
  mediaSources: string[];
  mediaSource?: string;
  bridgeStatus: string;
  canUninstall: boolean;
  version: string;
  platform: string;
}
export interface Snapshot {
  revision: number;
  catalog: Catalog;
  catalogStatus: string;
  catalogError: string;
  feedUrl: string;
  installed: Installed[];
  widgets: Widget[];
  jobs: Job[];
  settings: Settings;
  snoozed: boolean;
  activeCount: number;
  requestedPage?: string;
  requestedProvider?: string;
  notice?: string;
  recovery: string;
}
export interface Filters {
  query: string;
  category: string;
  kind: string;
  pack: string;
  status: string;
  sort: string;
}
export interface LibraryPreferences {
  tab: string;
  query: string;
  pack: string;
  sort: string;
}
export const defaults: Filters = {
  query: "",
  category: "all",
  kind: "all",
  pack: "all",
  status: "all",
  sort: "recent",
};
export type Run = <T = Record<string, unknown>>(
  method: string,
  args?: Record<string, unknown>,
  success?: string,
) => Promise<T>;
