---
title: Improve asset pipeline (tilesets) and avoid embedded PNGs in maps
labels: enhancement, roadmap, assets
---

## Summary

Provide a lightweight asset exporter and validator that can extract embedded base64 tile images from `maps/*.json` into a tileset folder, and update maps to reference files instead of embedding full PNG data URLs.

## Acceptance criteria
- [ ] Script/tool to extract embedded tiles into `assets/tilesets/<name>/` and rewrite map references.
- [ ] Validator that ensures referenced files exist and are not duplicated across maps.
- [ ] Documentation describing migration steps and recommended layout.

## Estimate
2–4 days
