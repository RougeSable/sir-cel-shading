# Sir Cel Shading

Client-side plugin for Space Engineers, loaded by Pulsar. It does one thing:
**cel shading**, in three styles the player picks from.

- **Clear line** (the default): thin, even black outlines, bright plain
  colors, almost no shadows, in the spirit of Tintin or Moebius.
- **Animated film**: shadows in two or three soft tones, thin outlines in a
  darker shade of each object, a rim of light on silhouettes seen against the
  sun, and distant things fading into the sky color.
- **Comic book**: the original Sir Cel Shading rendering, unchanged. Black
  outlines around blocks, scenery and characters; flat colors.

Also:

- The player switches style, or turns the effect on and off, at will, without
  restarting the game.
- Turned off, the game draws with its own shaders: the image is exactly the
  game's.
- Everything happens on the player's machine. Nothing goes through the server,
  and a player without the plugin sees the game's rendering.
- The interface (HUD, menus, texts) stays sharp: it is drawn after the effect.

## Usage

- **Settings**: Pulsar, Sir Cel Shading, settings button. The first setting is
  the **Enable plugin** checkbox; right below it, **Style** offers Comic book,
  Animated film and Clear line. Below come the settings of the selected style,
  and only those: a setting of one style never changes another. **Style
  defaults** resets the selected style only. Every change shows on the next
  frame.
  - Comic book: tones per color, outline width, outline darkness, edge
    sensitivity, color vibrance (the settings of the previous versions).
  - Animated film: shadow tones (2 or 3), outline strength, rim light,
    distance haze.
  - Clear line: outline darkness, edge sensitivity, shadows kept, color
    vibrance.
- **Chat**: `/cel` turns the effect on or off; `/cel comic`, `/cel animated`
  and `/cel clearline` switch to that style (and turn the effect on);
  `/cel on`, `/cel off`; `/cel status` names the style in use. The command is
  not sent to other players.
- **Saved settings** in `%AppData%\SpaceEngineers\Storage\sir-cel-shading\settings.xml`,
  kept from one game to the next. A player who never picked a style gets Clear
  line. The file of earlier versions (`reglages.xml`) is read once, when
  `settings.xml` does not exist yet: the on/off switch and the Comic book
  settings are kept, and the style is Clear line.

## How it works

The game ships its image effects as source (`Content/Shaders`) and compiles
them itself when loading. Sir Cel Shading hooks the final colors step,
`MyToneMapping.Run`, which is a compute shader
(`Postprocess/Tonemapping/Main.hlsl`) in three variants: `m_cs`,
`m_csAlphaLuminance`, `m_csSkip`.

1. At startup, the plugin writes its variant of that shader,
   `Storage\sir-cel-shading\Shaders\CelShading.hlsl`. It is the game's body
   kept as is (grain, exposure, bloom, filmic curve, filters), followed by the
   style, right before the sRGB conversion. The three styles live in that one
   file; the `CEL_STYLE` macro picks one. It includes the game's headers
   between angle brackets, so those of the game's shader folder.
2. Enabled, a Harmony prefix compiles the three variants of the selected style
   with the game's compiler (`MyShaderCompiler.Compile`, which refuses without
   crashing, then `MyComputeShaders.Create`). Only the settings of that style
   are given to the compiler. The prefix then puts the current variant in the
   game's static field and binds the scene depth
   (`MyGBuffer.Main.ResolvedDepthStencil.SrvDepth`) in `t31`, and, for
   Animated film and Clear line, the scene albedo (`MyGBuffer.Main.GBuffer0`)
   in `t27`. The postfix gives the field back the game's shader and unbinds
   `t31` and `t27`. A finalizer does the same if the pass fails.
3. Turned off, the prefix touches nothing.

Switching style compiles the new style's variants on first use, then keeps
them: switching back is immediate.

**Outlines.** They come from the second derivative of the inverse of the
distance: it is zero on a flat surface, and lights up at silhouettes and
edges. The measure is divided by the nearest distance of the neighborhood and
by the angle of a pixel. It therefore works in ratios of distances, never in
meters: an edge is drawn the same at one meter or at ten kilometers. The sky
is recognized by the game's clear depth. The game's edge detection
(`Postprocess/EdgeDetection.hlsl`) is not used: it only marks the coverage of
multisample antialiasing, which the game no longer enables. Comic book draws
them black and as wide as set; Clear line draws them one pixel wide with a
crisp ramp, so that they stay even; Animated film draws them one pixel wide in
a darker, deeper shade of the object's own color.

**Comic book flat colors.** The value of each color (its strongest channel, in
sRGB) falls on an adjustable number of steps; the hue is kept. Under half of
the first step the image stays the game's: the black of space stays black.
This style compiles to exactly the same GPU instructions as before the styles
existed.

**Light and color (Animated film, Clear line).** The final color divided by
the albedo of the object gives the light it receives. Animated film brings
that light onto two or three soft tones, the darkest never black; Clear line
lifts the shadows until only the set share of them remains. Light above the
object's own color (highlights, lamps, flames) is left alone. Where the albedo
cannot be read (the sky, an almost black object, multisampling), the game's
lighting is kept.

**Rim light (Animated film).** A pixel within a few pixels of something at
least a third farther, or of the sky, is on a silhouette. It receives a thin
edge of sunlight, in the sun's color, the more so as the camera faces the sun.

**Distance haze (Animated film).** The haze grows with the distance measured
in octaves of the near plane distance, a ratio: it starts around fifty meters
and is at its strongest around twenty-five kilometers with the game's usual
near plane, and it never hides a thing completely. The sky color comes from
the image itself: each group of 8x8 pixels reads one point of a fixed 8x8 grid
over the screen, and every pixel blends the sky points near it, those above
weighing more. Under a black sky (space) there is no haze.

**Slots t31 and t27.** No file of `Content/Shaders` declares `t31` or `t27`.
The game's pass only uses `t0` to `t3`, `u0` and `s0` to `s3`, and the engine
handles 32 slots per stage.

**Cost.** Comic book: nine depth reads and a few operations per pixel.
Clear line: the same plus one depth read and one albedo read. Animated film:
fifteen depth reads, one albedo read, one scene color read for the sky grid
and, for distant pixels only, a blend of 64 shared values. All of it in a pass the game runs anyway: no full-screen
pass is added.

## Stops and coexistence

Every internal name of the engine is resolved by reflection at startup.
Several cases stop the effect for the whole session:

- a missing name;
- a missing game header;
- a variant refused by the game's compiler;
- a fault on the render thread.

All go through the same path (`SessionStop`). The game keeps its rendering, a
`[sir-cel-shading]` line goes to the game log, and the player gets a
notification as soon as a game is open. `/cel status` then gives the reason,
and the selected style.

Two plugins never fight over the same step. Before hooking in, then every ten
seconds, Sir Cel Shading looks at who is hooked on `MyToneMapping.Run`
(`Harmony.GetPatchInfo`). If it finds another owner, it steps aside, whatever
the style, and tells the player.

## Name shown in Pulsar

Pulsar reads the displayed name from the plugin's sheet in the Sirius catalog,
not from this repository. The studio copies `PluginHub/sir-cel-shading.xml`
there at every release, with `FriendlyName` set to "Sir Cel Shading" and the
published commit in `Commit`. `SourceDirectories` limits the compilation to
the `Source` folder.

## Build and test

    dotnet build sir-cel-shading.csproj
    dotnet test tests/tests.csproj

The build looks for the game in the `Bin64` property, then in the `SE_BIN64`
environment variable, then in the most common Steam libraries. For another
location, see `Directory.Build.props.example`.

The tests cover the pure logic (`Source/Logic`): settings and their legacy
import, the chat command, session stops, coexistence, shader variants and the
shader source. They need neither the game nor Pulsar.
