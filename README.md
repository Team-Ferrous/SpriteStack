# Ferrous Sprite Stack Builder

Copy `Assets/Ferrous/SpriteStack` into a Unity 2022.3 or Unity 6 project and
open **Tools → Ferrous → Sprite Stack Builder**. The code uses Unity's built-in
SpriteRenderer and SortingGroup. It has no external package dependencies.

## Input and preview

- Use separate transparent PNGs for **Top**, **Side**, **Front**, and optionally
  **Back**, or use one equal-cell 2×2 PNG with Top / Side above Front / Back.
- Draw straight orthographic views of the same asset at one pixel scale.
  The default expects the top to face down and the side to face left. Turn on
  the appropriate orientation option if your art uses the opposite direction.
  Leave **Mirror back view** on for a normal rear camera view. Turn it off if
  you already flipped the art to align with the front.
- Each view is trimmed to its visible pixels. The trimmed top width and depth
  set the footprint. The trimmed side determines height. Keep the front and
  back grounded at the same height as the side. Enable **Fit mismatched
  views** to resample the side and end views using nearest-neighbor sampling.
- Click **Build slice preview**. Use **Starting heading** to turn the preview.
  Use **Vertical squash** and **Slice rise** to tune the viewing angle and
  the visible height. Use **Pixels per unit** to set the scale in your scene.

The tool intersects the visible silhouettes at each height. It uses the top
art for exposed roofs and the side, front, and rear images for exposed edges.
Silhouettes leave hidden geometry ambiguous, so enclosed holes, overhangs,
and asymmetric side details may require hand-authored slices.

## Output

**Save stack prefab** creates a new folder under Assets with a prefab,
a PNG atlas, and a native Sprite collection containing the individual slices.
The atlas uses point filtering, transparent gutters, no mipmaps, and no
runtime CPU readback. The generated prefab uses a shared texture and one
SpriteRenderer per nonempty slice. Sprite sorting is grouped at the root.

The prefab faces its authored heading on a 2D XY plane. Use the runtime
component's `SetHeading(degrees)` to rotate the art while leaving its
vertical stack upright. Move the prefab by positioning its root. Adjust
**Sorting Layer** and **Order in Layer** on the root's Sorting Group for
ground placement in the level. Profile dense scenes on your mobile targets
because each visible layer still has its own renderer.

The builder supports footprints up to 256 × 256 pixels with up to 128
vertical levels, provided all nonempty slices fit one 2048 × 2048 atlas.
Output folders use unique names, so saving repeatedly preserves older builds.

Technique reference
[Sprite stacking for fake 3D pixel art cars and props](https://www.spritecook.ai/blog/sprite-stacking-pixel-art)
