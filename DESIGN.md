---
version: alpha
name: YoWidgets
description: Small optical glass instruments above a Windows desktop; quiet local controls in Russian.
colors:
  background: "#11151D"
  surface: "#1A202C"
  field: "#131A25"
  text: "#F4F7FB"
  muted: "#B4C0D1"
  primary: "#78ADFF"
  border: "#303B4C"
  hover: "#2B374A"
  danger: "#FFB4AE"
typography:
  display:
    fontFamily: "Segoe UI Variable Display, Segoe UI, sans-serif"
  body:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
rounded:
  sm: "10px"
  md: "18px"
  lg: "28px"
spacing:
  control: "12px"
  panel: "24px"
  page: "34px"
components:
  button:
    borderRadius: "10px"
  panel:
    borderRadius: "18px"
  glass:
    borderRadius: "28px"
---
# YoWidgets design system

## Overview

Product register: a personal Windows utility, not a landing page. Russian UI; local system dates/timezone. Apple/MacShell video references define the optical material, while management uses restrained Windows controls. The signature is actual curved wallpaper refraction, RGB dispersion and a directed highlight. Avoid neon gradients, fake weather/media, decorative graphs and endless animation.

Runtime ownership model B: src/YoWidgets/Ui.cs owns semantic palette, typography and shared controls; Controls.xaml consumes the same resources for native WPF templates. This file mirrors those tokens and explains intent. Glass geometry/optics live only in shaders/Glass.hlsl. Mapping: background→Background, surface→Panel, field→Field, text→Text, muted→Muted, primary→Accent, border→Line, hover→Hover, danger→Danger.

## Colors

Dark management shell, quiet slate panels and blue active controls. Widget content uses white over adjustable optical tint. Increasing tint is available for difficult wallpapers; light glass is a material option, not a separate management theme. High Contrast must avoid relying on optics for meaning.

## Typography

System Segoe UI Variable fonts cover Cyrillic without network requests. Display heading 30px/semibold, navigation and body 13–15px, explanatory labels 11–12px. Clock type is 42–60px and scales inside constrained frames. Numbers and capability states are real data.

## Layout

210px navigation; natural vertical scrolling in the right content area. Minimum desktop window 860×600; 1100×790 default. Three-column widget catalog; settings have one scroll owner. Separate widget windows use S/M/L footprints and 20px optical padding; content is inset 18px. Screen bounds are physical pixels, sizes are DIP. This deliberate native mapping avoids pretending web viewport rules apply to HWND coordinates.

## Elevation & Depth

Quiet management panels have a single hairline border. Optical widget surfaces use a preblurred wallpaper source, SDF normal/refraction and rim lighting; only the background passes through the shader. No always-running decorative render loop. Static scenery redraws on geometry or source changes and pointer events.

## Shapes

10px controls, 18px panels, 28px glass radius (clamped on small widgets). Rounded controls keep visible focus and pressed/disabled states. Windows titlebar stays native with dark DWM appearance.

## Components

Ui.Button owns hover/focus/disabled/primary behavior. Ui.Toggle and Controls.xaml own keyboard-accessible switches. Ui.Input and PasswordBox own local fields; sensitive values masked. OS-owned file dialogs are intentional. Native scrollbars remain visible with shared colors. Status text is a stable live region at the bottom, not a disappearing critical error.

The calendar displays dates rather than editing them. Native context menus own widget size selection; authored dropdown geometry is not needed. Optical transitions respect SystemParameters.ClientAreaAnimation. Player expansion is 220ms and interrupted animations must settle at the latest requested size. Snooze removes surfaces and stops periodic work.

## Do's and Don'ts

- Do use one optical shader for previews and desktop widgets.
- Do distinguish disabled capability, missing permission and network failure.
- Do keep native keyboard access and non-drag positioning.
- Don't describe an untested virtual desktop mode as officially pinned.
- Don't substitute Acrylic and a white outline for verified refraction.
