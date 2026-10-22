# Local custom collections

Use **Library → Import package** to choose a local ZIP. The native picker reads its metadata, then shows the widget names, collection IDs, authors and archive size. Review these and choose Import. Imported widgets appear in Library and can be shown, hidden and removed like catalog widgets. No URL in an imported manifest is fetched.

Community packages are not publisher verified. This version supports declarative styles and the existing native widget kinds; it does not execute community JavaScript, DLLs or arbitrary plugin code. Pets, taskbars and docks require future engines.

## Archive format

The outer ZIP contains `catalog.json` at its root and inner package ZIPs:

```text
catalog.json
packages/style.aurora.zip
packages/aurora.clock.zip
```

Use a unique collection ID, for example `aurora`. `glass` and `liquid` are reserved. Package IDs must belong to the widgets listed in this archive: `style.<collection>` and `<collection>.<kind>`. A shared style replacement must list every already installed widget of that collection. Unlisted dependencies and official item IDs are rejected.

Example catalog (replace `bytes` and `sha256` with the actual inner ZIP length and SHA-256):

```json
{
  "schemaVersion": 1,
  "items": [{
    "id": "aurora-clock", "title": "Aurora Clock", "type": "widget",
    "kind": "clock", "pack": "aurora", "version": "1.0.0",
    "author": "Your name", "packages": ["aurora.clock"]
  }],
  "packages": [
    { "id": "style.aurora", "version": "1.0.0", "bytes": 1,
      "sha256": "REPLACE_WITH_64_HEX_DIGITS", "dependencies": [] },
    { "id": "aurora.clock", "version": "1.0.0", "bytes": 1,
      "sha256": "REPLACE_WITH_64_HEX_DIGITS", "dependencies": ["style.aurora"] }
  ]
}
```

`style.aurora.zip` contains only `style.json`:

```json
{ "id": "aurora", "name": "Aurora", "blur": 2, "interactionBlur": 3, "tint": 0.1, "liquid": false }
```

`aurora.clock.zip` contains `widget.json`:

```json
{ "kind": "clock", "pack": "aurora", "defaultSize": "S" }
```

The native engine validates style ranges, supported kinds, resource file types, paths, dependency limits, checksums and extracted sizes. The outer archive is limited to 128 MiB and 200 items. The complete archive fingerprint is pinned at preview and checked again at install/retry. If a source file moved or changed, choose it again. Cancelling an import keeps previously installed resources intact; Clear finished clears only job history.
