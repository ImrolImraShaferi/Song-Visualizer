# Song Visualizer

A .NET 8 command-line audio visualizer using SkiaSharp and FFmpeg. Input is mono or stereo **16-bit PCM WAV**. Convert other formats with FFmpeg first.

```sh
dotnet build Visualizer.sln
dotnet test Visualizer.sln
ffmpeg -i song.mp3 -c:a pcm_s16le song.wav
dotnet run --project Visualizer.Cli -- song.wav pipe 30 1920 1080 0 song.mp4 \
  --scene night-drive --title "Phases" --artist "Imra"
```

FFmpeg must be available on `PATH`. The Linux Skia native library is included in the project dependencies. On Linux, text uses DejaVu Sans or Liberation Sans when installed; install one of those font families if your machine has no fonts. Other platforms use the system sans-serif font.

The positional arguments remain compatible with the original CLI:

```text
Visualizer.Cli input.wav [pipe|png] [fps] [width] [height] [frames] [output]
```

`frames=0` renders the entire song. `output` is an MP4 filename in pipe mode or a directory in PNG mode. MP4 dimensions must be even.

## Scenes

| Option | Scene |
| --- | --- |
| `--scene night-drive` | Travel through a layered moonlit city with varied building sizes, street lamps, wet-road reflections, rain, and clouds. Occasional exit ramps and flyovers pass in perspective. |
| `--scene skyline` | Stationary city skyline across animated water, with rain and cloud drift. |
| `--scene desert-highway` | A dusk drive past cacti, scrub, rocks, mesas, utility poles, and occasional small towns with a gas station, diner, motel, and water tower. |
| `--scene bars` | Original green bar on black; the default for existing commands. |

For the desert highway, use the same command with `--scene desert-highway`. Settlements are spaced one minute of travel apart, with open country between them. Bass gently moves the mesa heights and cacti, mids control window lights and road reflectors, and highs animate dust, wind in the plants, and sky wisps. This scene is dry and does not use rain; `--no-rain` only changes the city scenes. The strength controls and top-center song text work for the desert too.

```sh
dotnet run --project Visualizer.Cli -- song.wav pipe 30 1920 1080 0 desert.mp4 --scene desert-highway --title "Phases" --artist "Imra"
```

Night-drive places three rows of buildings on each side of the road. Low broad blocks, mid-rise buildings, and narrow towers have independent heights, widths, setbacks, and roof details. Their profiles remain stable while driving past. Exits curve through cleared space between blocks, with guardrails and signs; flyovers have decks, railings, and piers outside the driving lanes. Each type occurs every 42 seconds, with exits alternating sides. Passing landmarks and city blocks share one perspective camera; bass still adds a gentle building-height response.

`--title` and `--artist` place text at the top center. The middle stays free for a future lyrics layer; lyrics are not implemented yet.

Night-drive and skyline use three frequency bands rather than the original overall RMS:

| Band | Range | Animation |
| --- | --- | --- |
| Bass | 20–250 Hz | Building heights change gently (up to 3.5% at default strength). |
| Mid | 250–4000 Hz | Window brightness and road or water reflections. |
| High | 4000 Hz–Nyquist | Rain streak length/intensity and cloud movement. |

FFT windows use a Hann taper. Stereo channel powers are averaged to preserve opposite-phase signals. Each band is normalized against the track's 95th percentile, with a floor relative to overall energy to avoid magnifying spectral leakage. Attack/release smoothing limits flicker. Travel uses elapsed seconds, so motion is independent of render FPS and reproducible for the same time and audio controls.

`--no-rain` turns off rain. `--bass-strength 0..3` and `--high-strength 0..3` adjust the corresponding animation; zero disables that band’s influence. These controls do not alter the audio. At low sample rates, bands above Nyquist receive no energy.

For PNG frames:

```sh
dotnet run --project Visualizer.Cli -- song.wav png 30 1280 720 300 frames \
  --scene skyline --title "Phases" --artist "Imra" --no-rain
```

## Validation

`dotnet test Visualizer.sln` includes synthetic-tone band separation, opposite-phase stereo, deterministic scene rendering, per-band animation, metadata, city block variation and travel, ramp clearance, landmark transitions, and FFmpeg encoding tests. FFmpeg is required for integration tests.

After a Debug build, `python3 tests/cli_smoke_test.py` checks the CLI, fractional FPS, quoted paths, frame limits, and output decoding.
