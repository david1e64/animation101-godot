using Godot;
using System.Collections.Generic;

public partial class FightDirector : Node
{
    [Export] public bool Recording = false;

    // Scene nodes
    private Character      _yaniv, _or;
    private Camera2D       _camera;
    private Node2D         _worldRoot;
    private ColorRect      _flashRect;
    private ProgressBar    _yanivHP, _orHP;
    private CPUParticles2D _hitSparks;
    private Node2D         _afterimages, _speedLines, _groundLines, _stars;
    private ColorRect      _vigRect, _chromaRect;
    private Label          _fightText;

    // Shader materials
    private ShaderMaterial _vigMat, _chromaMat;

    // Fight state
    private double     _elapsed = 0.0;
    private int        _cursor  = 0;
    private List<Beat> _beats;

    // Flash
    private Color  _flashColor;
    private double _flashTimer = 0.0, _flashDuration = 0.1;

    // World shake
    private double _shakeStrength = 0.0, _shakeTimer = 0.0;

    // Camera
    private Vector2 _camTarget     = new(480, 290);
    private float   _camZoomTarget = 1.0f;

    // Afterimages
    private int _yanivGhosts = 0, _orGhosts = 0, _tick = 0;
    private readonly List<(Sprite2D s, double life, double max)> _ghosts = new();

    // Slow-mo & hit freeze
    private double _timeMult       = 1.0;
    private double _timeMultTarget = 1.0;
    private bool   _frozen         = false;
    private double _freezeTimer    = 0.0;

    // Vignette & chroma
    private float _vigTarget      = 0.8f;
    private float _vigStrength    = 0.8f;
    private float _chromaStrength = 0.0f;
    private float _chromaTarget   = 0.0f;

    // HP drain animation
    private float _yanivVisHP = 100f, _orVisHP = 100f;
    private int   _prevYanivHP = 100, _prevOrHP = 100;

    // Horizon glow
    private ColorRect _horizonGlow;

    // HP bar styling (color gradient)
    private StyleBoxFlat _yanivHPStyle, _orHPStyle;

    // Parallax layers
    private Polygon2D _mountainBack, _mountainFront;

    // Dust / landing particles
    private CPUParticles2D _dustParticles;

    // Critical HP flash tweens (stored to avoid double-create)
    private Tween _yanivCritTween, _orCritTween;
    private bool  _yanivInCrit = false, _orInCrit = false;

    // Ground shadows (drop circles under characters)
    private Polygon2D _yanivShadow, _orShadow;

    // Death debris second emitter
    private CPUParticles2D _deathDebris;

    // Twinkling stars (subset)
    private readonly List<(ColorRect rect, float bri, float freq, float phase)> _twinklers = new();

    // Recording
    private int  _frameNum = 0;
    private bool _encDone  = false;
    private const string FRAMES_DIR    = "output/frames";
    private const double FIGHT_DURATION = 40.0;
    private const int    FPS            = 60;

    public override void _Ready()
    {
        _yaniv       = GetNode<Character>("WorldRoot/Yaniv");
        _or          = GetNode<Character>("WorldRoot/Or");
        _camera      = GetNode<Camera2D>("Camera2D");
        _worldRoot   = GetNode<Node2D>("WorldRoot");
        _flashRect   = GetNode<ColorRect>("FlashRect");
        _yanivHP     = GetNode<ProgressBar>("HUD/YanivHP");
        _orHP        = GetNode<ProgressBar>("HUD/OrHP");
        _hitSparks   = GetNode<CPUParticles2D>("WorldRoot/HitSparks");
        _afterimages = GetNode<Node2D>("WorldRoot/Afterimages");
        _speedLines  = GetNode<Node2D>("WorldRoot/SpeedLines");
        _groundLines = GetNode<Node2D>("WorldRoot/GroundLines");
        _stars         = GetNode<Node2D>("WorldRoot/Stars");
        _vigRect       = GetNode<ColorRect>("VignetteLayer/Vignette");
        _chromaRect    = GetNode<ColorRect>("ChromaLayer/Chroma");
        _fightText     = GetNode<Label>("HUD/FightText");
        _horizonGlow   = GetNode<ColorRect>("WorldRoot/HorizonGlow");
        _mountainBack  = GetNode<Polygon2D>("WorldRoot/MountainBack");
        _mountainFront = GetNode<Polygon2D>("WorldRoot/MountainFront");

        _flashRect.Visible = false;
        _camera.Position   = _camTarget;

        _SetupVignette();
        _SetupChroma();
        _SetupHPBarStyles();
        _BuildStarfield();
        _ConfigureSparks();
        _ConfigureDebris();
        _ConfigureDust();
        _BuildGroundGrid();
        _BuildGroundShadows();
        _BuildHorizonPulse();

        _beats = FightScript.YanivVsOr();

        if (Recording)
        {
            DirAccess.MakeDirRecursiveAbsolute(FRAMES_DIR);
            Engine.MaxFps = FPS;
        }

        GetTree().CreateTimer(0.25).Timeout += () =>
            _ShowFightText("ROUND 1", new Color(0.85f, 0.88f, 1.0f), 1.4);
        GetTree().CreateTimer(2.0).Timeout += () =>
            _ShowFightText("FIGHT!", new Color(1f, 0.85f, 0.1f), 1.2);
    }

    public override void _Process(double delta)
    {
        if (_elapsed > FIGHT_DURATION)
        {
            if (Recording && !_encDone) { _encDone = true; _EncodeMP4(); }
            return;
        }

        // Hit freeze — hold sprites, advance timer with real delta
        if (_frozen)
        {
            _freezeTimer -= delta;
            _yaniv.Sprite.SpeedScale = 0f;
            _or.Sprite.SpeedScale    = 0f;
            if (_freezeTimer <= 0)
            {
                _frozen = false;
                _yaniv.Sprite.SpeedScale = (float)_timeMult;
                _or.Sprite.SpeedScale    = (float)_timeMult;
            }
            else return;
        }

        _elapsed += delta;
        _tick++;

        // Lerp slow-mo
        _timeMult = Mathf.Lerp(_timeMult, _timeMultTarget, delta * 4.0);
        _yaniv.Sprite.SpeedScale = (float)_timeMult;
        _or.Sprite.SpeedScale    = (float)_timeMult;

        double eff = delta * _timeMult;

        // Fire scripted beats
        while (_cursor < _beats.Count && _beats[_cursor].Time <= _elapsed)
            _FireBeat(_beats[_cursor++]);

        // Dynamic facing
        if (Mathf.Abs(_yaniv.Position.X - _or.Position.X) > 8f)
        {
            _yaniv.FaceRight = _yaniv.Position.X < _or.Position.X;
            _or.FaceRight    = _or.Position.X    < _yaniv.Position.X;
        }

        // Flash fade
        if (_flashTimer > 0)
        {
            _flashTimer -= eff;
            float t = Mathf.Clamp((float)(_flashTimer / _flashDuration), 0f, 1f);
            _flashRect.Color = _flashColor with { A = _flashColor.A * t };
            if (_flashTimer <= 0) _flashRect.Visible = false;
        }

        // World shake
        if (_shakeTimer > 0)
        {
            _shakeTimer -= eff;
            float s = (float)(_shakeStrength * Mathf.Clamp(_shakeTimer / 0.3, 0, 1));
            _worldRoot.Position = new Vector2(
                (float)GD.RandRange(-s, s), (float)GD.RandRange(-s, s));
        }
        else _worldRoot.Position = Vector2.Zero;

        // Camera
        _camera.Position = _camera.Position.Lerp(_camTarget, (float)(delta * 5.5f));
        float z = Mathf.Lerp(_camera.Zoom.X, _camZoomTarget, (float)(delta * 3.5f));
        _camera.Zoom = new Vector2(z, z);

        // Parallax — mountains shift opposite to camera
        float camOff = _camera.Position.X - 480f;
        _mountainBack.Position  = new Vector2(-camOff * 0.12f, 0f);
        _mountainFront.Position = new Vector2(-camOff * 0.24f, 0f);

        // Ghosts
        if (_tick % 2 == 0)
        {
            if (_yanivGhosts > 0) { _yanivGhosts--; _SpawnGhost(_yaniv); }
            if (_orGhosts    > 0) { _orGhosts--;    _SpawnGhost(_or);    }
        }
        _TickGhosts(eff);

        // Ground shadow position tracking
        const float groundY = 315f;
        _yanivShadow.Position = new Vector2(_yaniv.Position.X, groundY);
        _orShadow.Position    = new Vector2(_or.Position.X,    groundY);

        // Star twinkle
        for (int i = 0; i < _twinklers.Count; i++)
        {
            var (rect, bri, freq, phase) = _twinklers[i];
            float a = bri * (0.65f + 0.35f * Mathf.Sin((float)_elapsed * freq + phase));
            rect.Color = new Color(bri, bri, bri * 0.92f, a);
        }

        // Distance-based staredown zoom — tighten when fighters face each other close
        float dist = Mathf.Abs(_yaniv.Position.X - _or.Position.X);
        if (dist < 260f && _camZoomTarget < 1.15f)
        {
            float zoomIn = Mathf.Lerp(1.0f, 1.18f, 1.0f - dist / 260f);
            _camZoomTarget = Mathf.Lerp(_camZoomTarget, zoomIn, (float)(delta * 0.6f));
        }

        // Vignette: base target + HP tension bonus
        float lowestHP = Mathf.Min(_yaniv.HP, _or.HP) / 100.0f;
        float hpBonus  = Mathf.Lerp(0.4f, 0.0f, lowestHP);
        float finalVig = Mathf.Max(_vigTarget, 0.78f + hpBonus);
        _vigStrength    = Mathf.Lerp(_vigStrength,    finalVig,     (float)(delta * 3.5f));
        _chromaStrength = Mathf.Lerp(_chromaStrength, _chromaTarget, (float)(delta * 5.0f));
        _vigMat.SetShaderParameter("strength", _vigStrength);
        _chromaMat.SetShaderParameter("strength", _chromaStrength);

        // HP drain animation + flash on damage
        if (_yaniv.HP < _prevYanivHP) _FlashHPBar(_yanivHP);
        if (_or.HP    < _prevOrHP)    _FlashHPBar(_orHP);
        _prevYanivHP = _yaniv.HP;
        _prevOrHP    = _or.HP;

        _yanivVisHP = Mathf.Lerp(_yanivVisHP, _yaniv.HP, (float)(delta * 2.8f));
        _orVisHP    = Mathf.Lerp(_orVisHP,    _or.HP,    (float)(delta * 2.8f));
        _yanivHP.Value = _yanivVisHP;
        _orHP.Value    = _orVisHP;

        // HP bar color gradient: green → yellow → red
        _yanivHPStyle.BgColor = _HPColor(_yanivVisHP / 100f);
        _orHPStyle.BgColor    = _HPColor(_orVisHP    / 100f);

        // Critical HP flash loop (< 25 HP)
        _UpdateCritFlash(_yaniv, _yanivHP, ref _yanivInCrit, ref _yanivCritTween);
        _UpdateCritFlash(_or,    _orHP,    ref _orInCrit,    ref _orCritTween);

        // HP-adaptive aura color (idle characters only)
        _UpdateIdleAura(_yaniv);
        _UpdateIdleAura(_or);

        if (Recording) _SaveFrame();
    }

    // ── Beat handler ────────────────────────────────────────────────────────

    private void _FireBeat(Beat beat)
    {
        var actor  = beat.Actor == "yaniv" ? _yaniv : _or;
        var target = beat.Actor == "yaniv" ? _or    : _yaniv;

        if (beat.Action != "move")
            actor.Play(beat.Action);

        // HP: "hit"/"death" damage the actor; everything else damages the target
        if (beat.TargetHP >= 0)
        {
            if (beat.Action == "hit" || beat.Action == "death")
                actor.SetHP(beat.TargetHP);
            else
                target.SetHP(beat.TargetHP);
        }

        // Movement tween (always moves actor)
        if (!float.IsNaN(beat.TargetX))
        {
            var tw = CreateTween();
            tw.SetTrans(beat.Action == "dash"
                ? Tween.TransitionType.Expo
                : Tween.TransitionType.Quad);
            tw.SetEase(Tween.EaseType.Out);
            tw.TweenProperty(actor, "position:x", beat.TargetX, (float)beat.MoveDur);
        }

        Vector2 mid = (_yaniv.Position + _or.Position) * 0.5f;

        switch (beat.Action)
        {
            case "walk":
                _camZoomTarget  = 1.0f;
                _camTarget      = new Vector2(mid.X, 290f);
                _vigTarget      = 0.8f;
                _timeMultTarget = 1.0;
                actor.StopChargeGlow();
                actor.SetAuraIntensity(0.0f, new Color(0.4f, 0.6f, 1.0f));
                break;

            case "idle":
                _camZoomTarget  = Mathf.Lerp(_camZoomTarget, 1.0f, 0.15f);
                _camTarget      = _camTarget.Lerp(new Vector2(mid.X, 290f), 0.15f);
                _vigTarget      = Mathf.Lerp(_vigTarget, 0.8f, 0.2f);
                _timeMultTarget = 1.0;
                actor.StopChargeGlow();
                actor.SetAuraIntensity(0.0f, new Color(0.4f, 0.6f, 1.0f));
                break;

            case "dash":
                if (beat.Actor == "yaniv") _yanivGhosts = 10;
                else                       _orGhosts    = 10;
                _BuildSpeedLines(actor);
                _SpawnDust(actor.Position + new Vector2(0, 10));
                _camTarget = new Vector2(mid.X, 290f);
                break;

            case "attack":
            {
                var pos = target.Position + new Vector2(0, -80);
                _TriggerFlash(new Color(0.9f, 0.95f, 1.0f, 0.50f), 0.07);
                _TriggerShake(5, 0.15);
                _TriggerFreeze(0.06);
                _SpawnSparks(pos);
                _SpawnImpactBurst(pos);
                actor.FlashAttack();
                _camTarget     = new Vector2(mid.X, 290f);
                _camZoomTarget = 1.12f;
                _vigTarget     = 1.0f;
                break;
            }

            case "hit":
                actor.FlashDamage();
                _SpawnDust(actor.Position + new Vector2(0, 10));
                _camZoomTarget = 1.08f;
                break;

            case "special":
            {
                var pos = target.Position + new Vector2(0, -80);
                if (beat.Actor == "yaniv") _yanivGhosts = 18;
                else                       _orGhosts    = 18;
                _TriggerFlash(new Color(0.35f, 0.70f, 1.0f, 0.75f), 0.15);
                _TriggerShake(14, 0.30);
                _TriggerFreeze(0.08);
                _SpawnSparks(pos);
                _SpawnImpactBurst(pos);
                _SpawnShockwave(actor.Position + new Vector2(0, -70));
                _BuildSpeedLines(actor);
                actor.FlashPower();
                actor.StartChargeGlow(new Color(1.4f, 1.2f, 0.3f));
                actor.SetAuraIntensity(0.85f, new Color(1.0f, 0.85f, 0.2f));
                _camTarget      = actor.Position with { Y = 290f };
                _camZoomTarget  = 1.35f;
                _vigTarget      = 1.5f;
                _timeMultTarget = 0.42;
                _ScheduleSlowMoEnd(1.1);
                break;
            }

            case "death":
            {
                var pos = actor.Position + new Vector2(0, -80);
                _TriggerFlash(new Color(1f, 0.18f, 0.18f, 0.88f), 0.22);
                _TriggerShake(24, 0.5);
                _TriggerFreeze(0.10);
                _SpawnSparks(pos);
                _SpawnDebris(pos);
                _SpawnImpactBurst(pos);
                _SpawnShockwave(pos);
                actor.FlashDamage();
                actor.StopChargeGlow();
                actor.SetAuraIntensity(1.0f, new Color(1f, 0.2f, 0.1f));
                _camTarget      = actor.Position with { Y = 290f };
                _camZoomTarget  = 1.55f;
                _vigTarget      = 2.0f;
                _chromaTarget   = 0.022f;
                _timeMultTarget = 0.22;
                _ScheduleSlowMoEnd(2.8);
                _ShowFightText("K.O.!", new Color(1f, 0.15f, 0.15f), 2.5);
                _DeathCinematic(actor.Position);
                break;
            }

            case "victory":
                _camTarget     = actor.Position with { Y = 290f };
                _camZoomTarget = 1.28f;
                _vigTarget     = 0.9f;
                _chromaTarget  = 0.0f;
                actor.FlashPower();
                actor.StartChargeGlow(new Color(1.3f, 1.1f, 0.25f));
                actor.SetAuraIntensity(0.6f, new Color(1.0f, 0.9f, 0.3f));
                break;
        }
    }

    private async void _ScheduleSlowMoEnd(double delay)
    {
        await ToSignal(GetTree().CreateTimer(delay), SceneTreeTimer.SignalName.Timeout);
        _timeMultTarget = 1.0;
    }

    // ── Visual effects ───────────────────────────────────────────────────────

    private void _TriggerFlash(Color color, double dur)
    {
        _flashColor        = color;
        _flashRect.Color   = color;
        _flashRect.Visible = true;
        _flashTimer        = dur;
        _flashDuration     = dur;
    }

    private void _TriggerShake(double strength, double dur)
    {
        _shakeStrength = strength;
        _shakeTimer    = dur;
    }

    private void _TriggerFreeze(double dur)
    {
        _frozen      = true;
        _freezeTimer = dur;
    }

    private void _SpawnSparks(Vector2 pos)
    {
        _hitSparks.GlobalPosition = pos;
        _hitSparks.Restart();
    }

    private void _SpawnImpactBurst(Vector2 pos)
    {
        const int spikes = 12;
        var burst = new Node2D();
        burst.Position = pos;
        _worldRoot.AddChild(burst);

        for (int i = 0; i < spikes; i++)
        {
            float angle = i * Mathf.Tau / spikes + (float)GD.RandRange(-0.22, 0.22);
            float len   = (float)GD.RandRange(16, 52);
            var   line  = new Line2D();
            line.AddPoint(Vector2.Zero);
            line.AddPoint(new Vector2(Mathf.Cos(angle) * len, Mathf.Sin(angle) * len));
            line.Width        = (float)GD.RandRange(1.5, 4.0);
            line.DefaultColor = GD.Randf() > 0.5f
                ? new Color(1.0f, 0.80f, 0.2f, 0.95f)
                : new Color(1.0f, 1.0f,  1.0f, 0.95f);
            burst.AddChild(line);
        }

        var tw = CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(burst, "scale", new Vector2(2.4f, 2.4f), 0.18f)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(burst, "modulate:a", 0.0f, 0.18f);
        tw.Chain().TweenCallback(Callable.From(() => burst.QueueFree()));
    }

    private void _SpawnShockwave(Vector2 pos)
    {
        const int seg = 20;
        var ring = new Node2D();
        ring.Position = pos;
        _worldRoot.AddChild(ring);

        var line = new Line2D { Width = 3.0f, DefaultColor = new Color(0.6f, 0.85f, 1.0f, 0.85f) };
        for (int i = 0; i <= seg; i++)
        {
            float a = i * Mathf.Tau / seg;
            line.AddPoint(new Vector2(Mathf.Cos(a) * 12f, Mathf.Sin(a) * 12f));
        }
        ring.AddChild(line);

        var tw = CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(ring, "scale", new Vector2(5.5f, 5.5f), 0.28f)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(ring, "modulate:a", 0.0f, 0.28f);
        tw.Chain().TweenCallback(Callable.From(() => ring.QueueFree()));
    }

    private void _ShowFightText(string text, Color color, double duration)
    {
        if (!IsInstanceValid(_fightText)) return;
        _fightText.Text     = text;
        _fightText.Scale    = new Vector2(1.8f, 1.8f);
        _fightText.Modulate = color with { A = 0f };
        _fightText.Visible  = true;

        var tw = CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(_fightText, "modulate:a", 1.0f, 0.14f);
        tw.TweenProperty(_fightText, "scale", Vector2.One, 0.22f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

        GetTree().CreateTimer(duration).Timeout += () =>
        {
            if (!IsInstanceValid(_fightText)) return;
            var fadeTw = CreateTween();
            fadeTw.TweenProperty(_fightText, "modulate:a", 0.0f, 0.35f);
        };
    }

    private void _BuildSpeedLines(Character actor)
    {
        foreach (Node c in _speedLines.GetChildren()) c.QueueFree();
        _speedLines.Modulate = Colors.White;

        bool  right = actor.FaceRight;
        float cx    = actor.Position.X;
        float cy    = actor.Position.Y - 55;
        float fdir  = right ? 1f : -1f;

        for (int i = 0; i < 16; i++)
        {
            float len   = (float)GD.RandRange(55, 160);
            float oy    = (float)GD.RandRange(-80, 80);
            float ox    = (float)GD.RandRange(0, 25) * -fdir;
            float angle = (float)GD.RandRange(-0.18, 0.18);
            float alpha = (float)GD.RandRange(0.35, 0.85);

            var line = new Line2D();
            line.AddPoint(new Vector2(cx + ox, cy + oy));
            line.AddPoint(new Vector2(
                cx + ox + fdir * len * Mathf.Cos(angle),
                cy + oy          + len * Mathf.Sin(angle)));
            line.Width        = (float)GD.RandRange(1.5, 4.0);
            line.DefaultColor = new Color(1f, 1f, 1f, alpha);
            _speedLines.AddChild(line);
        }

        var tw = CreateTween();
        tw.TweenProperty(_speedLines, "modulate:a", 0.0f, 0.20f);
        tw.TweenCallback(Callable.From(() =>
        {
            foreach (Node c in _speedLines.GetChildren()) c.QueueFree();
            _speedLines.Modulate = Colors.White;
        }));
    }

    private void _SpawnGhost(Character actor)
    {
        var frame = actor.Sprite.SpriteFrames?.GetFrameTexture(
            actor.CurrentAnim, actor.Sprite.Frame);
        if (frame == null) return;

        var ghost = new Sprite2D();
        ghost.Texture  = frame;
        ghost.Position = actor.Position;
        ghost.Scale    = actor.Sprite.Scale;
        ghost.Modulate = new Color(0.25f, 0.55f, 1.0f, 0.55f);
        _afterimages.AddChild(ghost);
        _ghosts.Add((ghost, 0.24, 0.24));
    }

    private void _TickGhosts(double eff)
    {
        for (int i = _ghosts.Count - 1; i >= 0; i--)
        {
            var (s, life, max) = _ghosts[i];
            double nl = life - eff;
            if (nl <= 0) { s.QueueFree(); _ghosts.RemoveAt(i); continue; }
            s.Modulate = new Color(0.25f, 0.55f, 1.0f, (float)(nl / max) * 0.55f);
            _ghosts[i] = (s, nl, max);
        }
    }

    // ── Shader setup ────────────────────────────────────────────────────────

    private void _SetupVignette()
    {
        var shader = new Shader();
        shader.Code =
@"shader_type canvas_item;
uniform float strength : hint_range(0.0, 3.0) = 0.8;
void fragment() {
    vec2 uv = UV - 0.5;
    float d = dot(uv, uv) * 4.0;
    COLOR = vec4(0.0, 0.0, 0.0, clamp(d * strength, 0.0, 0.92));
}";
        _vigMat = new ShaderMaterial();
        _vigMat.Shader = shader;
        _vigMat.SetShaderParameter("strength", 0.8f);
        _vigRect.Material = _vigMat;
    }

    private void _SetupChroma()
    {
        var shader = new Shader();
        shader.Code =
@"shader_type canvas_item;
uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;
uniform float strength : hint_range(0.0, 0.05) = 0.0;
void fragment() {
    vec2 uv = SCREEN_UV;
    float r = texture(SCREEN_TEXTURE, uv + vec2(strength, 0.0)).r;
    float g = texture(SCREEN_TEXTURE, uv).g;
    float b = texture(SCREEN_TEXTURE, uv - vec2(strength, 0.0)).b;
    COLOR = vec4(r, g, b, 1.0);
}";
        _chromaMat = new ShaderMaterial();
        _chromaMat.Shader = shader;
        _chromaMat.SetShaderParameter("strength", 0.0f);
        _chromaRect.Material = _chromaMat;
    }

    private void _SpawnDust(Vector2 pos)
    {
        _dustParticles.GlobalPosition = pos;
        _dustParticles.Restart();
    }

    private void _UpdateCritFlash(Character ch, ProgressBar bar, ref bool inCrit, ref Tween critTw)
    {
        bool nowCrit = ch.HP > 0 && ch.HP < 25;
        if (nowCrit && !inCrit)
        {
            inCrit = true;
            critTw?.Kill();
            critTw = CreateTween().SetLoops();
            critTw.TweenProperty(bar, "modulate:a", 0.45f, 0.35f)
                .SetTrans(Tween.TransitionType.Sine);
            critTw.TweenProperty(bar, "modulate:a", 1.0f, 0.35f)
                .SetTrans(Tween.TransitionType.Sine);
        }
        else if (!nowCrit && inCrit)
        {
            inCrit = false;
            critTw?.Kill();
            bar.Modulate = Colors.White;
        }
    }

    private static void _UpdateIdleAura(Character ch)
    {
        if (ch.CurrentAnim != "idle") return;
        float ratio = ch.HP / 100.0f;
        var   color = Color.FromHsv(ratio * 0.55f, 0.85f, 1.0f);
        ch.SetAuraIntensity(Mathf.Lerp(0.05f, 0.0f, ratio), color);
    }

    private void _SetupHPBarStyles()
    {
        _yanivHPStyle = new StyleBoxFlat { BgColor = new Color(0.2f, 0.85f, 0.25f) };
        _orHPStyle    = new StyleBoxFlat { BgColor = new Color(0.2f, 0.85f, 0.25f) };
        _yanivHP.AddThemeStyleboxOverride("fill", _yanivHPStyle);
        _orHP.AddThemeStyleboxOverride("fill", _orHPStyle);
    }

    private static Color _HPColor(float ratio) =>
        Color.FromHsv(ratio * 0.33f, 0.88f, 0.92f);

    private async void _DeathCinematic(Vector2 dyingPos)
    {
        // Already at 1.55x. Push tighter after 0.9s
        await ToSignal(GetTree().CreateTimer(0.9), SceneTreeTimer.SignalName.Timeout);
        _camZoomTarget = 1.88f;
        _camTarget     = dyingPos with { Y = 290f };
        // Hold for dramatic pause, then pull back for victory
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);
        _camZoomTarget = 1.0f;
        _camTarget     = new Vector2(480f, 290f);
    }

    private void _FlashHPBar(ProgressBar bar)
    {
        bar.Modulate = new Color(2.2f, 2.2f, 2.2f);
        var tw = CreateTween();
        tw.TweenProperty(bar, "modulate", Colors.White, 0.35f)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
    }

    // ── Scene setup ─────────────────────────────────────────────────────────

    private void _SpawnDebris(Vector2 pos)
    {
        _deathDebris.GlobalPosition = pos;
        _deathDebris.Restart();
    }

    private void _BuildGroundShadows()
    {
        _yanivShadow = _MakeShadowEllipse();
        _orShadow    = _MakeShadowEllipse();
        _worldRoot.AddChild(_yanivShadow);
        _worldRoot.AddChild(_orShadow);
    }

    private static Polygon2D _MakeShadowEllipse()
    {
        const int pts = 20;
        const float rx = 48f, ry = 9f;
        var points = new Vector2[pts];
        for (int i = 0; i < pts; i++)
        {
            float a = i * Mathf.Tau / pts;
            points[i] = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
        }
        return new Polygon2D { Polygon = points, Color = new Color(0f, 0f, 0f, 0.28f) };
    }

    private void _BuildHorizonPulse()
    {
        var tw = CreateTween().SetLoops();
        tw.TweenProperty(_horizonGlow, "color:a", 0.88f, 1.6f)
            .SetTrans(Tween.TransitionType.Sine);
        tw.TweenProperty(_horizonGlow, "color:a", 0.42f, 1.6f)
            .SetTrans(Tween.TransitionType.Sine);
    }

    private void _BuildStarfield()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        for (int i = 0; i < 140; i++)
        {
            float size = rng.RandfRange(1f, 3.2f);
            float bri  = rng.RandfRange(0.5f, 1.0f);
            var   star = new ColorRect();
            star.Size     = new Vector2(size, size);
            star.Position = new Vector2(rng.RandfRange(0, 960), rng.RandfRange(0, 308));
            star.Color    = new Color(bri, bri, bri * 0.92f, bri);
            _stars.AddChild(star);

            // Register ~40 bright stars as twinklers
            if (i < 40 && bri > 0.6f)
            {
                _twinklers.Add((
                    star,
                    bri,
                    rng.RandfRange(0.6f, 2.2f),  // twinkle frequency
                    rng.RandfRange(0f, Mathf.Tau) // phase offset
                ));
            }
        }
    }

    private void _ConfigureDust()
    {
        _dustParticles = new CPUParticles2D();
        _dustParticles.Amount               = 12;
        _dustParticles.Lifetime             = 0.5f;
        _dustParticles.OneShot              = true;
        _dustParticles.Explosiveness        = 0.7f;
        _dustParticles.Randomness           = 0.85f;
        _dustParticles.EmissionShape        = CPUParticles2D.EmissionShapeEnum.Sphere;
        _dustParticles.EmissionSphereRadius = 18f;
        _dustParticles.Direction            = new Vector2(0, -1);
        _dustParticles.Spread               = 75f;
        _dustParticles.Gravity              = new Vector2(0, 180f);
        _dustParticles.InitialVelocityMin   = 15f;
        _dustParticles.InitialVelocityMax   = 55f;
        _dustParticles.ScaleAmountMin       = 2f;
        _dustParticles.ScaleAmountMax       = 7f;
        _dustParticles.Color                = new Color(0.72f, 0.68f, 0.78f, 0.55f);
        _dustParticles.Emitting             = false;
        _worldRoot.AddChild(_dustParticles);
    }

    private void _ConfigureDebris()
    {
        _deathDebris = new CPUParticles2D();
        _deathDebris.Amount               = 22;
        _deathDebris.Lifetime             = 1.0f;
        _deathDebris.OneShot              = true;
        _deathDebris.Explosiveness        = 0.85f;
        _deathDebris.Randomness           = 0.7f;
        _deathDebris.EmissionShape        = CPUParticles2D.EmissionShapeEnum.Point;
        _deathDebris.Direction            = new Vector2(0, -1);
        _deathDebris.Spread               = 160f;
        _deathDebris.Gravity              = new Vector2(0, 400f);
        _deathDebris.InitialVelocityMin   = 40f;
        _deathDebris.InitialVelocityMax   = 160f;
        _deathDebris.ScaleAmountMin       = 5f;
        _deathDebris.ScaleAmountMax       = 16f;
        _deathDebris.Color                = new Color(0.95f, 0.35f, 0.1f, 1f);
        _deathDebris.Emitting             = false;
        _worldRoot.AddChild(_deathDebris);
    }

    private void _ConfigureSparks()
    {
        _hitSparks.Amount             = 35;
        _hitSparks.Lifetime           = 0.50f;
        _hitSparks.OneShot            = true;
        _hitSparks.Explosiveness      = 0.92f;
        _hitSparks.Randomness         = 0.55f;
        _hitSparks.EmissionShape      = CPUParticles2D.EmissionShapeEnum.Point;
        _hitSparks.Direction          = new Vector2(0, -1);
        _hitSparks.Spread             = 180f;
        _hitSparks.Gravity            = new Vector2(0, 320f);
        _hitSparks.InitialVelocityMin = 80f;
        _hitSparks.InitialVelocityMax = 260f;
        _hitSparks.ScaleAmountMin     = 3f;
        _hitSparks.ScaleAmountMax     = 9f;
        _hitSparks.Color              = new Color(1.0f, 0.82f, 0.22f, 1f);
        _hitSparks.Emitting           = false;
    }

    private void _BuildGroundGrid()
    {
        const int vx = 480, vy = 320;

        for (int i = 1; i <= 8; i++)
        {
            float t = (float)i / 8f;
            float y = vy + (540 - vy) * (t * t);
            var   h = new Line2D();
            h.AddPoint(new Vector2(0, y));
            h.AddPoint(new Vector2(960, y));
            h.Width        = 1f;
            h.DefaultColor = new Color(0.25f, 0.78f, 0.92f, t * 0.30f);
            _groundLines.AddChild(h);
        }

        for (int i = -10; i <= 10; i++)
        {
            float gx = vx + i * 52f;
            var   v  = new Line2D();
            v.AddPoint(new Vector2(vx, vy));
            v.AddPoint(new Vector2(gx, 540f));
            v.Width        = 1f;
            v.DefaultColor = new Color(0.25f, 0.78f, 0.92f, 0.11f);
            _groundLines.AddChild(v);
        }
    }

    // ── Recording ────────────────────────────────────────────────────────────

    private void _SaveFrame()
    {
        GetViewport().GetTexture().GetImage()
            .SavePng($"{FRAMES_DIR}/frame_{_frameNum++:D5}.png");
    }

    private void _EncodeMP4()
    {
        GD.Print("Encoding MP4...");
        OS.Execute("ffmpeg", new[]
        {
            "-y", "-framerate", FPS.ToString(),
            "-i", $"{FRAMES_DIR}/frame_%05d.png",
            "-c:v", "libx264", "-preset", "slow", "-crf", "18",
            "-pix_fmt", "yuv420p", "output/yaniv_vs_or_godot.mp4"
        });
        GD.Print("Done → output/yaniv_vs_or_godot.mp4");
    }
}
