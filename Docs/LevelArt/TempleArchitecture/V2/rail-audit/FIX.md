# Rail face orientation repair

Corrected 22 inward-facing meshes among 44 separate rails, including the entry Right rail.

- Fixed the shared RailsV2 mesh assets referenced by the trim prefab and the courtyard prefab/scene. Asset GUIDs and references remain unchanged.
- Reversed triangle winding and normals; adjusted tangent handedness.
- Vertex positions, bounds, UVs, materials, and collision assets unchanged.
- Fixed the editable Blender source and exported FBX; added orientation correction after UV assignment to build_rails_v2.py to retain UV coordinates when regenerating.
- Unity entry volumes after correction: Left +2.19677633; Right +2.19680402 (centered signed-volume diagnostic).
- Traversal: 17 decks, 22 passages, 92 wall pushes; 0 failures.
- Collision: 1356 probes, 0 missing, maximum source height error 0.00000763 m.
- Same-angle scene render: after/01-entry.png. Other eight-angle comparison captures in after/.
