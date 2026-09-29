# Sir Cel Shading

Client-side plugin for Space Engineers, loaded by Pulsar. It does one thing:
**Animated film cel shading**, in the spirit of Breath of the Wild or Ghibli.

- Shadows in two or three soft tones, never black.
- Thin outlines, in a darker shade of each object's own color.
- A rim of light on silhouettes seen against the sun.
- Aerial perspective: the farther a thing is, the more it fades into the sky
  color.

Also:

- The player turns the effect on and off at will, without restarting the game.
- Turned off, the game draws with its own shaders: the image is exactly the
  game's.
- Everything happens on the player's machine. Nothing goes through the server,
  and a player without the plugin sees the game's rendering.
- The interface (HUD, menus, texts) stays sharp: it is drawn after the effect.

## Usage

- **Settings**: Pulsar, Sir Cel Shading, settings button. The first setting is
  the **Enable plugin** checkbox. Below it: shadow tones (2 or 3), outline
  strength, rim light, distance haze. **Defaults** resets them. Every change
  shows on the next frame.
- **Chat**: `/cel` turns the effect on or off; `/cel on`, `/cel off`;
  `/cel status` says whether the effect is on. The command is not sent to
  other players.
- **Saved settings** in `%AppData%\SpaceEngineers\Storage\sir-cel-shading\settings.xml`,
  kept from one game to the next.
- **Earlier versions**: the plugin has a single rendering. A settings file
  written by an earlier version is read without error: the on/off switch and
  the rendering settings that still exist are kept, everything else is
  ignored, and the player gets Animated film. The file of the first version
  (`reglages.xml`) is read once, when `settings.xml` does not exist yet: only
  its on/off switch is kept.

## How it works

The game ships its image effects as source (`Content/Shaders`) and compiles
them itself when loading. Sir Cel Shading hooks the final colors step,
`MyToneMapping.Run`, which is a compute shader
(`Postprocess/Tonemapping/Main.hlsl`) in three variants: `m_cs`,
`m_csAlphaLuminance`, `m_csSkip`.

1. At startup, the plugin writes its variant of that shader,
   `Storage\sir-cel-shading\Shaders\CelShading.hlsl`. It is the game's body
   kept as is (grain, exposure, bloom, filmic curve, filters), followed by the
   effect, right before the sRGB conversion. It includes the game's headers
   between angle brackets, so those of the game's shader folder.
2. Enabled, a Harmony prefix compiles the three variants with the game's
   compiler (`MyShaderCompiler.Compile`, which refuses without crashing, then
   `MyComputeShaders.Create`), once for each set of settings. The prefix then
   puts the current variant in the game's static field, binds the scene depth
   (`MyGBuffer.Main.ResolvedDepthStencil.SrvDepth`) in `t31` and the scene
   albedo (`MyGBuffer.Main.GBuffer0`) in `t27`. The postfix gives the field
   back the game's shader and unbinds both slots. A finalizer does the same if
   the pass fails.
3. Turned off, the prefix touches nothing.

**Outlines.** They come from the second derivative of the inverse of the
distance: it is zero on a flat surface, and lights up at silhouettes and
edges. The measure is divided by the nearest distance of the neighborhood and
by the angle of a pixel. It therefore works in ratios of distances, never in
meters: an edge is drawn the same at one meter or at ten kilometers. The sky
is recognized by the game's clear depth. The game's edge detection
(`Postprocess/EdgeDetection.hlsl`) is not used: it only marks the coverage of
multisample antialiasing, which the game no longer enables. The outlines are
one pixel wide, in a darker, deeper shade of the object's own color.

**Light and color.** The final color divided by the albedo of the object gives
the light it receives. The plugin brings that light onto two or three soft
tones, the darkest never black. Light above the object's own color
(highlights, lamps, flames) is left alone. Where the albedo cannot be read
(the sky, an almost black object, multisampling), the game's lighting is kept.

**Rim light.** A pixel within a few pixels of something at least a third
farther, or of the sky, is on a silhouette. It receives a thin edge of
sunlight, in the sun's color, the more so as the camera faces the sun.

**Distance haze.** The haze grows with the distance measured in octaves of the
near plane distance, a ratio: it starts around fifty meters and is at its
strongest around twenty-five kilometers with the game's usual near plane, and
it never hides a thing completely. The sky color comes from the image itself:
each group of 8x8 pixels reads one point of a fixed 8x8 grid over the screen,
and every pixel blends the sky points near it, those above weighing more.
Under a black sky (space) there is no haze.

**Slots t31 and t27.** No file of `Content/Shaders` declares `t31` or `t27`.
The game's pass only uses `t0` to `t3`, `u0` and `s0` to `s3`, and the engine
handles 32 slots per stage.

**Cost.** Fifteen depth reads, one albedo read, one scene color read for the
sky grid and, for distant pixels only, a blend of 64 shared values. All of it
in a pass the game runs anyway: no full-screen pass is added.

## Stops and coexistence

Every internal name of the engine is resolved by reflection at startup.
Several cases stop the effect for the whole session:

- a missing name;
- a missing game header;
- a variant refused by the game's compiler;
- a fault on the render thread.

All go through the same path (`SessionStop`). The game keeps its rendering, a
`[sir-cel-shading]` line goes to the game log, and the player gets a
notification as soon as a game is open. `/cel status` then gives the reason.

Two plugins never fight over the same step. Before hooking in, then every ten
seconds, Sir Cel Shading looks at who is hooked on `MyToneMapping.Run`
(`Harmony.GetPatchInfo`). If it finds another owner, it steps aside and tells
the player.

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

The tests cover the pure logic (`Source/Logic`): settings and their loading
from earlier files, the chat command, session stops, coexistence, shader
variants and the shader source. They need neither the game nor Pulsar.
