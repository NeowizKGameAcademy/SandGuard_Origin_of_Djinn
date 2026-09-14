Three desert ground material sets, 1024x1024.
01_Sand: fine sand. 02_ModerateGravel: moderate gravel. 03_DenseGravel: dense gravel.

Unity URP Lit:
BaseColor: sRGB on, Base Map.
Normal_Unity: import as Normal Map; Create from Grayscale OFF.
MetallicSmoothness: sRGB OFF; Metallic map, Smoothness Source=Metallic Alpha, Smoothness multiplier=1. Channels R=0, G=1 (neutral AO), B=0, A=smoothness.
Roughness: supplementary map, not directly assigned to URP smoothness.
Height_ESTIMATE: 8-bit estimated height, not measured displacement.
Repeat3x3 and RawGeneration are review images, not material maps.
Wrap mode: Repeat. Normals/height/roughness are RGB-derived estimates. Unity rendering is not yet verified.
