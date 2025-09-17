# Blasphemous TikTok Crowd Control Mod

This mod enables **TikTok crowd control gameplay** in *Blasphemous*.

Using TikFinity, TikTok interactions (likes, follows, shares, gifts) can trigger key presses, which the mod translates into **enemy spawns** inside the game.

This allows streamers to create highly interactive content, where the audience actively shapes the challenge — and helps make livestreams more engaging and fun.

---

## 📌 Credits

This mod is based on and would not be possible without:

- [Blasphemous.ModdingAPI](https://github.com/BrandenEK/Blasphemous.ModdingAPI)
- [Blasphemous.Randomizer](https://github.com/BrandenEK/Blasphemous.Randomizer)

A huge thanks to **BrandenEK** and contributors for their work.
This mod reuses and builds upon some of their code — all credit to the original authors.

---

## ⚙️ Requirements

1. Install the [Blasphemous Modding Installer](https://github.com/BrandenEK/Blasphemous.Modding.Installer).
2. Install the [Blasphemous.Randomizer](https://github.com/BrandenEK/Blasphemous.Randomizer).
3. Set up [TikFinity](https://tikfinity.zerody.one/) so that TikTok events (likes, gifts, follows, etc.) map to **keyboard key presses (0–9)**.

---

## 🕹️ Installation

1. Download the `.dll` file from this repository's **[Releases](../../releases)** section.
2. Place the `.dll` inside your game's: `Blasphemous/Modding/plugins/`
3. Launch the game using the **mod loader**.
4. Verify the mod is active and ready to receive key inputs.

---

## 🎮 How It Works

Each numeric key (0–9) is bound to the spawning of a specific enemy.
You can configure how many presses are needed to trigger each spawn.

Default configuration:

| Key | Enemy Code | Description |
|-----|------------|-------------|
| 1   | EV11       | Spawn enemy EV11 |
| 2   | EN22       | Spawn enemy EN22 |
| 3   | EN20       | Spawn enemy EN20 |
| 4   | EN01       | Spawn enemy EN01 |
| 5   | EN26       | Spawn enemy EN26 |
| 6   | EV03       | Spawn enemy EV03 |
| 7   | EN27       | Spawn enemy EN27 |
| 8   | EN16       | Spawn enemy EN16 |
| 9   | EN03       | Spawn enemy EN03 |
| 0   | EV26       | Spawn enemy EV26 |
| ¿   | *Game Over* | Force a game over (safety fallback) |

---

## 🛠️ Customization

- You can change the **enemy codes** (e.g. replace `"EV11"` with any valid tag) to fit your content.
- Stronger enemies can be mapped to **higher-tier TikTok gifts** for better engagement.
- The number of presses required per spawn can also be configured.

---

## 🚀 Usage

1. Install the mod following the steps above.
2. Map TikTok events in **TikFinity** to number key presses (`0–9`).
3. Start your stream — viewers will now be able to spawn enemies in-game through TikTok interactions.
4. Use the `¿` key to force a game over if things go wrong.

---

## 🎃 Final Note

This is an experimental, fun mod — use it to spice up your TikTok livestreams, let your viewers make your run chaotic, and maybe… become a millionaire. 😉