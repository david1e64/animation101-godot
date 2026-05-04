using System.Collections.Generic;

// TargetX: world-space X to move actor to (NaN = don't move)
// MoveDur: seconds for the move tween
// TargetHP: for hit/death = actor's new HP; for attack/special = target's new HP
public record Beat(
    double Time,
    string Actor,
    string Action,
    int    TargetHP = -1,
    float  TargetX  = float.NaN,
    double MoveDur  = 0.18
);

public static class FightScript
{
    // Yaniv home: 215   Or home: 745   Mid: 480
    // Close clash: Yaniv ~440  Or ~540
    public static List<Beat> YanivVsOr() => new()
    {
        // ── Phase 1: Walk-in staredown (0–3s) ──────────────────────────────
        new(0.0,  "yaniv", "idle"),
        new(0.0,  "or",    "idle"),
        new(1.2,  "yaniv", "walk",  TargetX: 345, MoveDur: 1.5),
        new(1.2,  "or",    "walk",  TargetX: 615, MoveDur: 1.5),
        new(2.8,  "yaniv", "idle"),
        new(2.8,  "or",    "idle"),

        // ── Phase 2: First exchange — Yaniv opens (3–9s) ───────────────────
        new(3.6,  "yaniv", "dash",   TargetX: 432, MoveDur: 0.17),
        new(4.1,  "yaniv", "attack"),
        new(4.4,  "or",    "hit",    TargetHP: 78, TargetX: 658, MoveDur: 0.11),
        new(5.2,  "yaniv", "idle"),
        new(5.2,  "or",    "idle"),

        // Or counters — dashes in, attack, Yaniv gets pushed back
        new(5.9,  "or",    "dash",   TargetX: 548, MoveDur: 0.17),
        new(6.3,  "or",    "attack"),
        new(6.6,  "yaniv", "hit",    TargetHP: 82, TargetX: 302, MoveDur: 0.11),
        new(7.4,  "or",    "idle"),
        new(7.4,  "yaniv", "idle"),

        // ── Phase 3: Yaniv special charge (8–11s) ─────────────────────────
        new(8.1,  "yaniv", "dash",   TargetX: 418, MoveDur: 0.14),
        new(8.35, "yaniv", "special"),
        // Slow-mo kicks in; two rapid hits land
        new(9.0,  "or",    "hit",    TargetHP: 55, TargetX: 672, MoveDur: 0.09),
        new(9.3,  "or",    "hit",    TargetHP: 48, TargetX: 698, MoveDur: 0.09),
        new(9.9,  "yaniv", "idle"),
        new(9.9,  "or",    "walk",   TargetX: 624, MoveDur: 0.7),
        new(10.6, "or",    "idle"),

        // ── Phase 4: Or's counter-special (11–14s) ────────────────────────
        new(11.3, "or",    "dash",   TargetX: 558, MoveDur: 0.14),
        new(11.55,"or",    "special"),
        new(12.1, "yaniv", "hit",    TargetHP: 55, TargetX: 270, MoveDur: 0.09),
        new(12.5, "yaniv", "hit",    TargetHP: 40, TargetX: 242, MoveDur: 0.09),
        new(13.1, "or",    "idle"),
        new(13.1, "yaniv", "walk",   TargetX: 350, MoveDur: 0.7),
        new(13.8, "yaniv", "idle"),

        // ── Phase 5: Quick 3-hit exchange (14–18s) ────────────────────────
        new(14.4, "yaniv", "dash",   TargetX: 422, MoveDur: 0.12),
        new(14.85,"yaniv", "attack"),
        new(15.15,"or",    "hit",    TargetHP: 32, TargetX: 622, MoveDur: 0.10),
        new(15.6, "or",    "dash",   TargetX: 540, MoveDur: 0.12),
        new(15.95,"or",    "attack"),
        new(16.25,"yaniv", "hit",    TargetHP: 28, TargetX: 308, MoveDur: 0.10),
        new(16.7, "yaniv", "dash",   TargetX: 440, MoveDur: 0.10),
        new(17.0, "yaniv", "attack"),
        new(17.3, "or",    "hit",    TargetHP: 18, TargetX: 638, MoveDur: 0.09),
        new(17.9, "yaniv", "idle"),
        new(17.9, "or",    "idle"),

        // ── Phase 6: Tense staredown / buildup (18–22s) ───────────────────
        // Both limp toward each other slowly — camera widens
        new(18.6, "yaniv", "walk",   TargetX: 382, MoveDur: 1.1),
        new(18.6, "or",    "walk",   TargetX: 580, MoveDur: 1.1),
        new(19.7, "yaniv", "idle"),
        new(19.7, "or",    "idle"),
        // Silence — let the camera drift for 2s before the final burst
        new(21.4, "yaniv", "idle"),  // tension hold
        new(21.4, "or",    "idle"),

        // ── Phase 7: Yaniv's final combo (21.5–26s) ───────────────────────
        new(21.8, "yaniv", "dash",   TargetX: 452, MoveDur: 0.10),
        new(22.15,"yaniv", "attack"),
        new(22.45,"or",    "hit",    TargetHP: 10, TargetX: 598, MoveDur: 0.09),
        new(22.9, "yaniv", "dash",   TargetX: 464, MoveDur: 0.08),
        new(23.15,"yaniv", "special"),
        // Slow-mo; two hard hits
        new(23.95,"or",    "hit",    TargetHP: 4,  TargetX: 625, MoveDur: 0.09),
        new(24.4, "yaniv", "idle"),
        new(24.4, "or",    "walk",   TargetX: 590, MoveDur: 0.5),

        // ── Phase 8: Or's desperate last counter (25–27s) ─────────────────
        new(25.1, "or",    "dash",   TargetX: 530, MoveDur: 0.13),
        new(25.4, "or",    "attack"),
        new(25.7, "yaniv", "hit",    TargetHP: 20, TargetX: 338, MoveDur: 0.10),
        new(26.0, "or",    "idle"),
        new(26.3, "yaniv", "idle"),

        // ── Phase 9: Finishing blow — SLOW-MO DEATH (27–32s) ──────────────
        new(27.1, "yaniv", "dash",   TargetX: 460, MoveDur: 0.10),
        new(27.4, "yaniv", "special"),  // slow-mo 0.42x starts
        // Or staggers — barely standing
        new(28.2, "or",    "hit",    TargetHP: 4,  TargetX: 608, MoveDur: 0.09),
        // The killing blow
        new(29.2, "yaniv", "attack"),
        new(29.65,"or",    "death",  TargetHP: 0,  TargetX: 705, MoveDur: 0.22),
        // death triggers its own slow-mo 0.22x + K.O.! text

        // ── Phase 10: Victory (32–38s) ────────────────────────────────────
        new(32.5, "yaniv", "idle",   TargetX: 400, MoveDur: 0.7),
        new(34.0, "yaniv", "victory"),
    };
}
