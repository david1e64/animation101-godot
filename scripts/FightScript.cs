using System.Collections.Generic;

// TargetX: world-space X to move actor to (NaN = don't move)
// MoveDur: seconds for the move tween
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
    // Yaniv home: 215   Or home: 745   Arena center: 480
    public static List<Beat> YanivVsOr() => new()
    {
        // ── Opening staredown — walk in from the edges ──────────────────
        new(0.0,  "yaniv", "idle"),
        new(0.0,  "or",    "idle"),
        new(1.2,  "yaniv", "walk",  TargetX: 345, MoveDur: 1.5),
        new(1.2,  "or",    "walk",  TargetX: 615, MoveDur: 1.5),
        new(2.8,  "yaniv", "idle"),
        new(2.8,  "or",    "idle"),

        // ── Yaniv opens: dash across, attack ────────────────────────────
        new(3.5,  "yaniv", "dash",   TargetX: 430, MoveDur: 0.17),
        new(4.0,  "yaniv", "attack"),
        new(4.3,  "or",    "hit",    TargetHP: 78, TargetX: 655, MoveDur: 0.11),
        new(5.2,  "yaniv", "idle"),
        new(5.2,  "or",    "idle"),

        // ── Or counter: dash in, attack ──────────────────────────────────
        new(5.8,  "or",    "dash",   TargetX: 545, MoveDur: 0.17),
        new(6.2,  "or",    "attack"),
        new(6.5,  "yaniv", "hit",    TargetHP: 82, TargetX: 305, MoveDur: 0.11),
        new(7.4,  "or",    "idle"),
        new(7.4,  "yaniv", "idle"),

        // ── Yaniv special: charge in, unleash ───────────────────────────
        new(8.0,  "yaniv", "dash",   TargetX: 410, MoveDur: 0.14),
        new(8.25, "yaniv", "special"),
        new(8.9,  "or",    "hit",    TargetHP: 55, TargetX: 675, MoveDur: 0.09),
        new(9.15, "or",    "hit",    TargetHP: 48, TargetX: 700, MoveDur: 0.09),
        new(9.8,  "yaniv", "idle"),
        new(9.8,  "or",    "walk",   TargetX: 620, MoveDur: 0.7),
        new(10.5, "or",    "idle"),

        // ── Or counter-special ───────────────────────────────────────────
        new(11.2, "or",    "dash",   TargetX: 555, MoveDur: 0.14),
        new(11.45,"or",    "special"),
        new(12.0, "yaniv", "hit",    TargetHP: 55, TargetX: 275, MoveDur: 0.09),
        new(12.4, "yaniv", "hit",    TargetHP: 40, TargetX: 248, MoveDur: 0.09),
        new(13.0, "or",    "idle"),
        new(13.0, "yaniv", "walk",   TargetX: 345, MoveDur: 0.7),
        new(13.6, "yaniv", "idle"),

        // ── Yaniv final combo ────────────────────────────────────────────
        new(14.2, "yaniv", "dash",   TargetX: 435, MoveDur: 0.12),
        new(14.65,"yaniv", "attack"),
        new(15.05,"or",    "hit",    TargetHP: 25, TargetX: 640, MoveDur: 0.1),
        new(15.6, "yaniv", "dash",   TargetX: 455, MoveDur: 0.09),
        new(15.85,"yaniv", "special"),
        new(16.6, "or",    "hit",    TargetHP: 6,  TargetX: 670, MoveDur: 0.09),

        // ── Finishing blow ────────────────────────────────────────────────
        new(17.2, "yaniv", "attack"),
        new(17.85,"or",    "death",  TargetHP: 0,  TargetX: 705, MoveDur: 0.22),

        // ── Victory ───────────────────────────────────────────────────────
        new(19.0, "yaniv", "idle",   TargetX: 390, MoveDur: 0.5),
        new(19.8, "yaniv", "victory"),
    };
}
