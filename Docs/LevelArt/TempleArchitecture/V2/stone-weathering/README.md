# Stone weathering pass

Updated Courtyard Stone shading:
- Shared vertical joint boundaries vary per stone, retaining continuous rows.
- Broader per-stone warm/cool and brightness variation within the sandstone palette.
- Irregular edge wear, sparse angled hairline cracks and dust in horizontal joints.
- Surface detail is shader-only; mesh positions, collision, scene layout, and rail materials unchanged.
- Masonry-specific additions are masked by _Masonry.

Verification: Unity shader compilation and three-view preview rendering completed. See after-entry.png, after-paving.png, after-wall.png and after-validation.txt.

## Ornament pass
Turquoise is brighter with faded glaze patches. Bronze has a warmer gold base, patchy green patina and varying polish. Explicit _OrnamentWeathering enables this only on Temple_Turquoise and Temple_Bronze; other material defaults remain zero. Builder settings preserve the changes on regeneration.

Verified in the running editor using an isolated preview scene. See ornaments-entry.png, ornaments-paving.png, ornaments-wall.png and ornaments-validation.txt. No scene or geometry was saved by the preview.
