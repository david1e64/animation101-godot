using Godot;
using System.Collections.Generic;

/// <summary>
/// Reads a Universal LPC spritesheet (64x64 px per frame).
///
/// Standard LPC layout — 4 direction sub-rows per animation block (N/W/S/E):
///   Block 0  rows  0- 3  Spellcast   7 frames
///   Block 1  rows  4- 7  Thrust      8 frames
///   Block 2  rows  8-11  Walk        9 frames
///   Block 3  rows 12-15  Slash       6 frames
///   Block 4  rows 16-19  Shoot      13 frames
///   Row 20              Hurt         6 frames  (South-only)
///   Row 21              Death        6 frames  (South-only)
///
/// East direction = sub-row 3 (character faces right, profile view).
/// We use East for all directional anims; FaceRight flips the sprite for left.
/// If your sheet has different row counts, adjust AnimDefs below.
/// </summary>
public partial class Character : Node2D
{
	[Export] public string CharacterName = "yaniv";
	[Export] public int    FrameSize     = 64;   // LPC standard
	[Export] public int    Scale2D       = 3;    // 64×3 = 192px on screen
	[Export] public bool   FaceRight     = true;

	public int HP { get; private set; } = 100;
	public AnimatedSprite2D Sprite { get; private set; }

	// ── LPC animation rows ────────────────────────────────────────────────
	// East-facing (base_row + 3) for combat anims; Hurt/Death are South-only.
	// Tweak row numbers here if your downloaded sheet differs.
	private static readonly Dictionary<string, (int row, int frames, float fps)> AnimDefs = new()
	{
		{ "idle",    (10, 1,  4f)  }, // Walk South row 10, 1 frame — static stand
		{ "walk",    (11, 9,  12f) }, // Walk East  row 11, 9 frames
		{ "attack",  (15, 6,  14f) }, // Slash East row 15, 6 frames
		{ "hit",     (20, 6,  12f) }, // Hurt       row 20, 6 frames (South-only)
		{ "special", (3,  7,  13f) }, // Spellcast East row 3, 7 frames
		{ "dash",    (7,  8,  20f) }, // Thrust East row 7, 8 frames
		{ "death",   (21, 6,  10f) }, // Death      row 21, 6 frames (South-only)
		{ "victory", (3,  7,  5f)  }, // Spellcast East slow — triumphant hold
	};

	private static readonly HashSet<string> OneShot =
		new() { "attack", "hit", "special", "dash" };

	private string _currentAnim = "idle";
	public  string CurrentAnim => _currentAnim;
	public  bool   AnimDone    { get; private set; }

	public override void _Ready()
	{
		Sprite = GetNode<AnimatedSprite2D>("Sprite");
		Sprite.Scale = new Vector2(FaceRight ? Scale2D : -Scale2D, Scale2D);
		_BuildFrames();
		Play("idle");
		Sprite.AnimationFinished += _OnAnimFinished;
	}

	private void _BuildFrames()
	{
		var frames = new SpriteFrames();
		var tex    = GD.Load<Texture2D>($"res://assets/sheets/{CharacterName}.png");

		foreach (var kv in AnimDefs)
		{
			string animName            = kv.Key;
			var (row, frameCount, fps) = kv.Value;

			frames.AddAnimation(animName);
			frames.SetAnimationSpeed(animName, fps);
			frames.SetAnimationLoop(animName,
				!OneShot.Contains(animName) && animName != "death" && animName != "victory");

			for (int f = 0; f < frameCount; f++)
			{
				var atlasT = new AtlasTexture();
				atlasT.Atlas  = tex;
				atlasT.Region = new Rect2(f * FrameSize, row * FrameSize, FrameSize, FrameSize);
				frames.AddFrame(animName, atlasT);
			}
		}

		Sprite.SpriteFrames = frames;
	}

	public void Play(string anim)
	{
		if (_currentAnim == anim && Sprite.IsPlaying()) return;
		_currentAnim = anim;
		AnimDone     = false;
		Sprite.Play(anim);
	}

	public void SetHP(int hp) => HP = Mathf.Clamp(hp, 0, 100);

	private void _OnAnimFinished()
	{
		AnimDone = true;
		if (OneShot.Contains(_currentAnim))
			Play("idle");
	}

	public override void _Process(double delta)
	{
		Sprite.Scale = new Vector2(FaceRight ? Scale2D : -Scale2D, Scale2D);
	}
}
