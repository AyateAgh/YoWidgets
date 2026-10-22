# Security

Do not post credentials or private logs in public issues. Use GitHub private vulnerability reporting when available, or contact the repository owner privately before disclosing exploitation details.

The catalog accepts bounded declarative JSON resources for supported native widget kinds. Scripts, native binaries, linked archive entries and paths escaping the content directory are rejected. Content is staged, hash checked and registered locally. The production feed uses HTTPS and packages are restricted to this repository. Compromise of the publishing account remains a supply-chain risk; SHA-256 is not a publisher signature.

Management IPC is current-user scoped, asynchronous and bounded. Core operations never wait for JavaScript acknowledgements. Native code encrypts saved keys with DPAPI and omits them from management snapshots. Unsaved drafts remain in management memory until saved or discarded.

Before publication run `node scripts/audit-source.mjs --history`, inspect staged files and run `scripts/build.ps1`. The scanner is a useful check, not a guarantee that arbitrary private information is absent. Keep profiles, build outputs, signing keys and local configuration out of Git.
