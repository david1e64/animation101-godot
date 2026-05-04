using Godot;
using System.Collections.Generic;

/// <summary>
/// Drives an AnimatedSprite2D from a horizontal sprite sheet.
/// Rows = animation states: idle=0, walk=1, attack=2, hit=3, special=4, victory=5, death=6, dash=7
/// </summary>
public partial class Character : Node2D
{
	[Export] public string CharacterName = "yaniv";
	[Export] public int    FrameSize     = 96;   // px per frame in source sheet
	[Export] public int    Scale2D       = 2;
	[Export] public bool   FaceRight     = true;

	// HP
	public int HP { get; private set; } = 100;

	public AnimatedSprite2D Sprite { get; private set; }

	// Animation definitions: name -> (row, frameCount, fps)
	private static readonly Dictionary<string, (int row, int frames, float fps)> AnimDefs = new()
	{
		{ "idle",    (0, 8,  8f)  },
		{ "walk",    (1, 8,  12f) },
		{ "attack",  (2, 8,  14f) },
		{ "hit",     (3, 4,  12f) },
		{ "special", (4, 10, 14f) },
		{ "victory", (5, 8,  8f)  },
		{ "death",   (6, 8,  10f) },
		{ "dash",    (7, 6,  18f) },
	};

	private static readonly HashSet<string> OneShot =
		new() { "attack", "hit", "special", "victory", "dash" };

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
		var tex = GD.Load<Texture2D>($"res://assets/sheets/{CharacterName}.png");
		int sheetW = tex.GetWidth();

		foreach (var kv in AnimDefs)
		{
			string animName = kv.Key;
			var (row, frameCount, fps) = kv.Value;
			frames.AddAnimation(animName);
			frames.SetAnimationSpeed(animName, fps);
			frames.SetAnimationLoop(animName, !OneShot.Contains(animName));

			for (int f = 0; f < frameCount; f++)
			{
				var region = new Rect2(f * FrameSize, row * FrameSize, FrameSize, FrameSize);
				var atlasT = new AtlasTexture();
				atlasT.Atlas  = tex;
				atlasT.Region = region;
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

	public void SetHP(int hp)
	{
		HP = Mathf.Clamp(hp, 0, 100);
	}

	private void _OnAnimFinished()
	{
		AnimDone = true;
		if (OneShot.Contains(_currentAnim) && _currentAnim != "death" && _currentAnim != "victory")
			Play("idle");
	}

	public override void _Process(double delta)
	{
		// Flip sprite based on facing (set by FightDirector)
		Sprite.Scale = new Vector2(FaceRight ? Scale2D : -Scale2D, Scale2D);
	}
}
