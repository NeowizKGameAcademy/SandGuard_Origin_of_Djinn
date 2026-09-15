# Entry rail comparison

Target: RAILS_REBUILT_ENTRY_NE_T1
Scene was opened for diagnostics only and was not saved.

- Captured entry/reverse views, opposing isolated lit views, and unlit front/back/top/side views.
- Left and Right texture SHA256 hashes match. Material settings match except names and texture asset references.
- World geometry mirrors across x=z within 0.00003815 m.
- Both meshes: 40 vertices, 24 triangles, non-degenerate UV triangles.
- Signed volume: Left +2.19677633 m3; Right -2.19680402 m3.
- Both transforms have positive scale; the reversed orientation is in the mesh data.
- Stored normals agree with triangle winding on both sides, so recalculating normals alone will not fix the right mesh. Its winding and normals need to be reversed together.

Conclusion: the right rail is inside-out. Lighting adds contrast, but is not the underlying defect. No level geometry, materials, or colliders were changed in this comparison.
