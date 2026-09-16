# Rail sandstone palette alignment

Applied Courtyard Rail Stone to the existing Left/Right materials (44 courtyard rails).
The shader preserves original UV sampling and trim luminance detail while replacing orange color with the courtyard wall sandstone tint (0.76, 0.645, 0.47), with reduced detail contrast (0.7).
Original texture files, separate UVs, geometry, transforms, and collision were not modified.
TempleCourtyardSurfaces reapplies the same palette during rebuilding.

Validation: Unity batch preview succeeded; no shader compilation errors; all 44 materials verified. before.png and after.png share the same camera, scene, and lighting. The preview scene was closed without saving or replacing the live scene.

Editor menu: Tools / Temple Art / Compare Rail Sandstone Color.
