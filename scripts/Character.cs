using Godot;
using System.Collections.Generic;

/// <summary>
/// DBZ-format spritesheet (96x96 px per frame, 8 animation rows).
/// Row order: idle=0 walk=1 attack=2 hit=3 special=4 victory=5 death=6 dash=7
/// </summary>
public partial class Character : Node2D
{
    [Export] public string CharacterName = "yaniv";
    [Export] public int    FrameSize     = 96;
    [Export] public int    Scale2D       = 2;
    [Export] public bool   FaceRight     = true;

    public int HP { get; private set; } = 100;
    public AnimatedSprite2D Sprite { get; private set; }
    public CPUParticles2D   Aura   { get; private set; }

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
        new() { "attack", "hit", "special", "dash" };

    private string _currentAnim = "idle";
    public  string CurrentAnim  => _currentAnim;
    public  bool   AnimDone     { get; private set; }

    // Charge glow
    private Tween _chargeTween;

    // Idle breathing
    private float _breathTime = 0.0f;

    public override void _Ready()
    {
        Sprite = GetNode<AnimatedSprite2D>("Sprite");
        Sprite.Scale = new Vector2(FaceRight ? Scale2D : -Scale2D, Scale2D);
        _BuildFrames();
        _BuildAura();
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

    private void _BuildAura()
    {
        Aura = new CPUParticles2D();
        Aura.Position             = new Vector2(0, -(FrameSize * Scale2D * 0.55f));
        Aura.Amount               = 6;
        Aura.Lifetime             = 0.9f;
        Aura.OneShot              = false;
        Aura.Explosiveness        = 0.0f;
        Aura.Randomness           = 0.9f;
        Aura.EmissionShape        = CPUParticles2D.EmissionShapeEnum.Rectangle;
        Aura.EmissionRectExtents  = new Vector2(FrameSize * Scale2D * 0.35f, FrameSize * Scale2D * 0.45f);
        Aura.Direction            = new Vector2(0, -1);
        Aura.Spread               = 35f;
        Aura.Gravity              = Vector2.Zero;
        Aura.InitialVelocityMin   = 15f;
        Aura.InitialVelocityMax   = 45f;
        Aura.ScaleAmountMin       = 1.5f;
        Aura.ScaleAmountMax       = 4.0f;
        Aura.Color                = new Color(0.4f, 0.6f, 1.0f, 0.18f);
        Aura.Emitting             = true;
        AddChild(Aura);
    }

    // ── Playback ────────────────────────────────────────────────────────────

    public void Play(string anim)
    {
        if (_currentAnim == anim && Sprite.IsPlaying()) return;
        _currentAnim = anim;
        AnimDone     = false;
        Sprite.Play(anim);
    }

    public void SetHP(int hp) => HP = Mathf.Clamp(hp, 0, 100);

    // ── Visual feedback ──────────────────────────────────────────────────────

    public void FlashDamage()
    {
        Sprite.SelfModulate = new Color(2f, 0.2f, 0.2f, 1f);
        var tw = CreateTween();
        tw.TweenProperty(Sprite, "self_modulate", Colors.White, 0.22f);
    }

    public void FlashPower()
    {
        Sprite.SelfModulate = new Color(2f, 1.8f, 0.2f, 1f);
        var tw = CreateTween();
        tw.TweenProperty(Sprite, "self_modulate", Colors.White, 0.4f);
    }

    public void FlashAttack()
    {
        Sprite.SelfModulate = new Color(2f, 2f, 2f, 1f);
        var tw = CreateTween();
        tw.TweenProperty(Sprite, "self_modulate", Colors.White, 0.10f);
    }

    // Looping glow pulse used during charge / special
    public void StartChargeGlow(Color glowColor)
    {
        _chargeTween?.Kill();
        _chargeTween = CreateTween().SetLoops();
        _chargeTween.TweenProperty(Sprite, "self_modulate", glowColor, 0.28f)
            .SetTrans(Tween.TransitionType.Sine);
        _chargeTween.TweenProperty(Sprite, "self_modulate", Colors.White, 0.28f)
            .SetTrans(Tween.TransitionType.Sine);
    }

    public void StopChargeGlow()
    {
        _chargeTween?.Kill();
        _chargeTween = null;
        Sprite.SelfModulate = Colors.White;
    }

    // 0.0 = idle dim, 1.0 = full power
    public void SetAuraIntensity(float t, Color baseColor)
    {
        Aura.Amount  = Mathf.RoundToInt(Mathf.Lerp(5f, 28f, t));
        Aura.Color   = baseColor with { A = Mathf.Lerp(0.15f, 0.65f, t) };
        Aura.InitialVelocityMax = Mathf.Lerp(45f, 110f, t);
    }

    private void _OnAnimFinished()
    {
        AnimDone = true;
        if (OneShot.Contains(_currentAnim))
            Play("idle");
    }

    public override void _Process(double delta)
    {
        Sprite.Scale = new Vector2(FaceRight ? Scale2D : -Scale2D, Scale2D);

        // Idle breathing — subtle vertical oscillation
        if (_currentAnim is "idle" or "victory")
        {
            _breathTime += (float)delta * 1.6f;
            Sprite.Position = new Vector2(0, Mathf.Sin(_breathTime) * 2.5f);
        }
        else
        {
            _breathTime = 0.0f;
            Sprite.Position = Vector2.Zero;
        }
    }
}
