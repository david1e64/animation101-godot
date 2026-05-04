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

    // Recording
    private int  _frameNum = 0;
    private bool _encDone  = false;
    private const string FRAMES_DIR    = "output/frames";
    private const double FIGHT_DURATION = 28.0;
    private const int    FPS            = 60;

    public override void _Ready()
    {
        _yaniv      = GetNode<Character>("WorldRoot/Yaniv");
        _or         = GetNode<Character>("WorldRoot/Or");
        _camera     = GetNode<Camera2D>("Camera2D");
        _worldRoot  = GetNode<Node2D>("WorldRoot");
        _flashRect  = GetNode<ColorRect>("FlashRect");
        _yanivHP    = GetNode<ProgressBar>("HUD/YanivHP");
        _orHP       = GetNode<ProgressBar>("HUD/OrHP");
        _hitSparks  = GetNode<CPUParticles2D>("WorldRoot/HitSparks");
        _afterimages = GetNode<Node2D>("WorldRoot/Afterimages");
        _speedLines  = GetNode<Node2D>("WorldRoot/SpeedLines");
        _groundLines = GetNode<Node2D>("WorldRoot/GroundLines");
        _stars       = GetNode<Node2D>("WorldRoot/Stars");

        _flashRect.Visible = false;
        _camera.Position   = _camTarget;

        _BuildStarfield();
        _ConfigureSparks();
        _BuildGroundGrid();

        _beats = FightScript.YanivVsOr();

        if (Recording)
        {
            DirAccess.MakeDirRecursiveAbsolute(FRAMES_DIR);
            Engine.MaxFps = FPS;
            GD.Print($"Recording {(int)(FIGHT_DURATION * FPS)} frames → {FRAMES_DIR}");
        }
    }

    public override void _Process(double delta)
    {
        if (_elapsed > FIGHT_DURATION)
        {
            if (Recording && !_encDone) { _encDone = true; _EncodeMP4(); }
            return;
        }

        _elapsed += delta;
        _tick++;

        // Fire scripted beats
        while (_cursor < _beats.Count && _beats[_cursor].Time <= _elapsed)
            _FireBeat(_beats[_cursor++]);

        // Dynamic facing — always look at each other
        if (Mathf.Abs(_yaniv.Position.X - _or.Position.X) > 8f)
        {
            _yaniv.FaceRight = _yaniv.Position.X < _or.Position.X;
            _or.FaceRight    = _or.Position.X    < _yaniv.Position.X;
        }

        // Flash fade-out
        if (_flashTimer > 0)
        {
            _flashTimer -= delta;
            float t = Mathf.Clamp((float)(_flashTimer / _flashDuration), 0f, 1f);
            _flashRect.Color = _flashColor with { A = _flashColor.A * t };
            if (_flashTimer <= 0) _flashRect.Visible = false;
        }

        // World shake (damped)
        if (_shakeTimer > 0)
        {
            _shakeTimer -= delta;
            float s = (float)(_shakeStrength * Mathf.Clamp(_shakeTimer / 0.3, 0, 1));
            _worldRoot.Position = new Vector2(
                (float)GD.RandRange(-s, s), (float)GD.RandRange(-s, s));
        }
        else _worldRoot.Position = Vector2.Zero;

        // Camera smooth follow + zoom
        _camera.Position = _camera.Position.Lerp(_camTarget, (float)(delta * 5.5f));
        float z = Mathf.Lerp(_camera.Zoom.X, _camZoomTarget, (float)(delta * 3.5f));
        _camera.Zoom = new Vector2(z, z);

        // Spawn afterimage ghosts every 2 ticks while dashing/specials
        if (_tick % 2 == 0)
        {
            if (_yanivGhosts > 0) { _yanivGhosts--; _SpawnGhost(_yaniv); }
            if (_orGhosts    > 0) { _orGhosts--;    _SpawnGhost(_or);    }
        }
        _TickGhosts(delta);

        // HUD
        _yanivHP.Value = _yaniv.HP;
        _orHP.Value    = _or.HP;

        if (Recording) _SaveFrame();
    }

    // ── Beat handler ────────────────────────────────────────────────────────

    private void _FireBeat(Beat beat)
    {
        var actor  = beat.Actor == "yaniv" ? _yaniv : _or;
        var target = beat.Actor == "yaniv" ? _or    : _yaniv;

        if (beat.Action != "move")
            actor.Play(beat.Action);

        if (beat.TargetHP >= 0)
            target.SetHP(beat.TargetHP);

        // Movement tween
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
                _camZoomTarget = 1.0f;
                _camTarget     = new Vector2(mid.X, 290f);
                break;

            case "idle":
                _camZoomTarget = Mathf.Lerp(_camZoomTarget, 1.0f, 0.15f);
                _camTarget     = _camTarget.Lerp(new Vector2(mid.X, 290f), 0.15f);
                break;

            case "dash":
                if (beat.Actor == "yaniv") _yanivGhosts = 10;
                else                       _orGhosts    = 10;
                _BuildSpeedLines(actor);
                _camTarget = new Vector2(mid.X, 290f);
                break;

            case "attack":
                _TriggerFlash(new Color(0.9f, 0.95f, 1.0f, 0.55f), 0.07);
                _TriggerShake(5, 0.15);
                _SpawnSparks(target.Position + new Vector2(0, -80));
                _camTarget     = new Vector2(mid.X, 290f);
                _camZoomTarget = 1.12f;
                break;

            case "hit":
                // Knockback — push target away from attacker
                float dir = beat.Actor == "yaniv" ? 1f : -1f;
                float kbX = Mathf.Clamp(target.Position.X + dir * 38, 120, 840);
                var   kb  = CreateTween();
                kb.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                kb.TweenProperty(target, "position:x", kbX, 0.10f);
                _camZoomTarget = 1.08f;
                break;

            case "special":
                if (beat.Actor == "yaniv") _yanivGhosts = 18;
                else                       _orGhosts    = 18;
                _TriggerFlash(new Color(0.35f, 0.70f, 1.0f, 0.75f), 0.15);
                _TriggerShake(15, 0.30);
                _SpawnSparks(target.Position + new Vector2(0, -80));
                _BuildSpeedLines(actor);
                _camTarget     = actor.Position with { Y = 290f };
                _camZoomTarget = 1.35f;
                break;

            case "death":
                _TriggerFlash(new Color(1f, 0.18f, 0.18f, 0.88f), 0.22);
                _TriggerShake(24, 0.5);
                _SpawnSparks(target.Position + new Vector2(0, -80));
                _camTarget     = target.Position with { Y = 290f };
                _camZoomTarget = 1.55f;
                break;

            case "victory":
                _camTarget     = actor.Position with { Y = 290f };
                _camZoomTarget = 1.28f;
                break;
        }
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

    private void _SpawnSparks(Vector2 pos)
    {
        _hitSparks.GlobalPosition = pos;
        _hitSparks.Restart();
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

        const double life = 0.24;
        _ghosts.Add((ghost, life, life));
    }

    private void _TickGhosts(double delta)
    {
        for (int i = _ghosts.Count - 1; i >= 0; i--)
        {
            var (s, life, max) = _ghosts[i];
            double nl = life - delta;
            if (nl <= 0) { s.QueueFree(); _ghosts.RemoveAt(i); continue; }
            s.Modulate = new Color(0.25f, 0.55f, 1.0f, (float)(nl / max) * 0.55f);
            _ghosts[i] = (s, nl, max);
        }
    }

    // ── Scene setup (done in code for reliability) ───────────────────────────

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
        }
    }

    private void _ConfigureSparks()
    {
        _hitSparks.Amount          = 30;
        _hitSparks.Lifetime        = 0.48f;
        _hitSparks.OneShot         = true;
        _hitSparks.Explosiveness   = 0.92f;
        _hitSparks.Randomness      = 0.55f;
        _hitSparks.EmissionShape   = CPUParticles2D.EmissionShapeEnum.Point;
        _hitSparks.Direction       = new Vector2(0, -1);
        _hitSparks.Spread          = 180f;
        _hitSparks.Gravity         = new Vector2(0, 320f);
        _hitSparks.InitialVelocityMin = 70f;
        _hitSparks.InitialVelocityMax = 240f;
        _hitSparks.ScaleAmountMin  = 3f;
        _hitSparks.ScaleAmountMax  = 8f;
        _hitSparks.Color           = new Color(1.0f, 0.82f, 0.22f, 1f);
        _hitSparks.Emitting        = false;
    }

    private void _BuildGroundGrid()
    {
        const int vx = 480, vy = 320;

        // Horizontal parallels — exponential spacing for perspective
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

        // Converging vertical lines
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
