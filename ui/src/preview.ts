import seed from "../../catalog/v1/catalog.json";
import type { Snapshot, Catalog } from "./types";
// Explicit ?preview=1 is a read-only design fixture. Native builds never use it.
export const previewState: Snapshot = {
  revision: 0,
  catalog: seed as Catalog,
  catalogStatus: "cached",
  catalogError: "",
  feedUrl: "",
  installed: [],
  widgets: [],
  jobs: [],
  snoozed: false,
  activeCount: 0,
  recovery: "",
  settings: {
    city: "Kyiv",
    analog: false,
    fahrenheit: false,
    seconds: false,
    hour24: true,
    topmost: false,
    interfaceSounds: false,
    interfaceVolume: 0.12,
    interfaceTheme: "dark",
    startup: false,
    lyricsEnabled: false,
    notificationsEnabled: false,
    notificationStatus: "Access not requested",
    aiAskBeforeControl: false,
    aiProcessText: "Thinking it through…",
    voice: {
      geminiModel: "gemini-3.8-live",
      openAIModel: "gpt-realtime-2.1",
      geminiVoice: "Aoede",
      openAIVoice: "marin",
      language: "auto",
      allowActions: true,
    },
    voiceChoices: {
      gemini: ["Aoede", "Kore", "Puck"],
      openai: ["marin", "cedar", "alloy"],
    },
    chatProvider: "Gemini",
    chatModel: "gemini-3.5-flash",
    chat: ["Gemini", "OpenAI", "Claude", "DeepSeek", "OpenRouter"].map(
      (provider) => ({
        provider,
        model:
          provider === "Gemini"
            ? "gemini-3.5-flash"
            : provider === "OpenAI"
              ? "gpt-5.4-mini"
              : provider === "Claude"
                ? "claude-sonnet-4-6"
                : provider === "DeepSeek"
                  ? "deepseek-chat"
                  : "openrouter/auto",
        connected: false,
      }),
    ),
    generation: [
      {
        id: "image",
        title: "Images",
        mode: "chat",
        provider: "Gemini",
        model: "",
        voice: "",
        keySaved: false,
        providers: ["Pollinations", "Gemini", "OpenAI", "OpenRouter"],
      },
      {
        id: "video",
        title: "Video",
        mode: "chat",
        provider: "Gemini",
        model: "",
        voice: "",
        keySaved: false,
        providers: ["Pollinations", "Gemini"],
      },
      {
        id: "model3d",
        title: "3D models",
        mode: "off",
        provider: "Pollinations",
        model: "",
        voice: "",
        keySaved: false,
        providers: ["Pollinations"],
      },
      {
        id: "audio",
        title: "Audio",
        mode: "off",
        provider: "Pollinations",
        model: "",
        voice: "",
        keySaved: false,
        providers: ["Pollinations"],
      },
    ],
    mediaSources: [],
    bridgeStatus: "Not connected",
    canUninstall: false,
    version: "0.6.0",
    platform: "Windows 11",
  },
};
