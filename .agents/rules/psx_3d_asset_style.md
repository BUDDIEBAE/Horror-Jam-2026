# 3D Asset Style Guide: Authentic Low-Poly PSX (PlayStation 1 Era)

This rule defines the exact art direction for all 3D props, interactables, and environment models (e.g. Signal Beacon, Oxygen Station, Terminals, Airlocks, Debris) approved by the user.

---

## 📌 Primary Visual Benchmark (Style Anchor)
- **Anchor Reference Image**: [Signal_Beacon_PSX_LowPoly.jpg](file:///d:/Horror%20Game%20Jam%20202/Assets/Concept_Art/Signal_Beacon_PSX_LowPoly.jpg)
- Every single 3D model, prop, and concept art in this game **MUST strictly match this benchmark**:
  1. **Geometry**: Minimalist, chunky low-poly primitives (8-sided cylinders, rectangular blocky rails, faceted angular domes, square protective frames). Zero smooth/curved subdivision.
  2. **Texturing**: Crunchy 128x128 pixel art with visible color dithering, industrial hazard stripes (yellow/black), painted-on analog dials/switches, and bold pixel fonts.
  3. **Atmosphere**: Utilitarian NASA-punk / Cassette Futurism deep space emergency equipment (Silent Hill 1, Iron Lung, Signalis style).
  4. **Shader Synergy**: Tailored for `PSX_Lit_URP.shader` (affine texture warping and vertex wobbling).

---

## 1. Geometry & Polycount Guidelines
- **Extreme Low-Poly Silhouette**: Target under **300 – 800 polygons** per prop/interactable.
- **Faceted Flat Shading**: Polygons should be clearly visible and angular. Avoid high-poly smoothing, subdivision, or modern rounded bevels.
- **Primitive Blocky Shapes**:
  - Cylinders should use 6 to 8 sides max (octagonal prism).
  - Handles, rails, and structural frames must be blocky rectangular bars.
  - Light domes, radar dishes, and caps should be faceted low-poly domes.
  - Wire cages should use 4-sided chunky square frames.

---

## 2. Texturing & Visual Fidelity
- **Resolution**: Crunchy **128x128** or **256x256** pixel art texture maps.
- **Dithering & Pixel Crunch**: Distinct pixel dithering pattern reminiscent of 1997–1999 PS1 / Sega Saturn hardware.
- **Painted-On Details**: Buttons, analog dials, switches, ventilation grilles, warning labels, and hazard stripes should be painted directly onto flat polygonal faces (no complex geometric detail for tiny parts).
- **Unity Shader Compatibility**: Must be paired with `PSX_Lit_URP.shader` to leverage:
  - Affine texture warping (lack of perspective correction)
  - Vertex snapping / wobble / jitter
  - Point-filtered textures (`Filter Mode: Point (no filter)`)

---

## 3. Mandatory AI Prompting Formula for 3D/Concept Generation
When generating future assets or prompts for external AI (Tripo3D, Meshy, Flux, Midjourney):
```text
Authentic 1997 PS1 retro 3D video game model, extreme low poly 3D asset of [ITEM_NAME]. 
Blocky angular geometry with visible flat-shaded polygonal facets, chunky 3D primitives (under 500 polygons). 
Crunchy 128x128 pixelated textures with visible pixels and dithering, painted-on buttons, dials, and hazard stripes. 
Retro PlayStation 1 graphics aesthetic, like Iron Lung, Signalis, and Silent Hill 1. 
Single isolated 3D model, isometric 3/4 view, centered on a pure solid white background, no floor, no shadows, game ready retro asset.
```
