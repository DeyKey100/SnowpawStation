using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace HeyHeyThere.PSXGasStationKitFree
{
    /// <summary>
    /// A machine: on a usable prop's prefab, what a use does to it, its state and the clips that show
    /// it, played by hand on its Animation (each clip keeps its own time, so a lever stays thrown while
    /// an engine runs). <see cref="kind"/> says what a use does:
    /// Switch: on and off, <see cref="clip"/> playing forward to on and back to off.
    /// Button: the same, the clip playing through at each press.
    /// Turn: <see cref="steps"/> uses open it, the clip a step each (forward opening, back closing); on while open.
    /// Slot: fitting what it <see cref="takes"/> switches it on, taking it back out off; with <see cref="keep"/>
    /// (a card) the item stays with the user, the clip plays through and each use switches it.
    /// Keypad: its keys (parts named *_key_&lt;key&gt;, each with a clip of its name) pressed: on when they
    /// spell <see cref="code"/>, or at any key without one; "C" clears, "on" and "off" switch it.
    /// Panel: its keys are switches, each up or down; on while they read the code, "1" up, "0" down.
    /// Item: picked up, <see cref="item"/> into the user's items, and gone.
    /// Powered: on while its needs are live, the clip playing forward as it comes on; with no needs a
    /// use switches it (a door).
    /// Gauge: reads its first need's output, <see cref="level"/> held at it (a needle, a gate a crank winds up).
    /// Anything else that takes an item has it fitted by its first use. It is powered while every
    /// machine in <see cref="needs"/> is live, and live while on and powered. Its <see cref="lit"/> parts
    /// show while it is live, its <see cref="idle"/> parts while powered and off, a panel's *_lamp_&lt;key&gt;
    /// while powered and that key is up, its <see cref="fitted"/> parts once what it takes is in (with
    /// keep, while it plays); <see cref="run"/> loops while it is live. Its output, what a gauge reads:
    /// a turn's share of its steps while powered, else 1 when live.
    /// </summary>
    public class Machine : MonoBehaviour
    {
        public enum Kind { Switch, Button, Turn, Slot, Keypad, Panel, Item, Powered, Gauge }
        [Serializable] public class BoolEvent : UnityEvent<bool> { }
        [Serializable] public class StringEvent : UnityEvent<string> { }

        public Kind kind;
        public bool on;
        public Machine[] needs = new Machine[0];
        public string clip = "", run = "", level = "";
        public int steps = 1;
        public string takes = "";
        public bool keep;
        public string item = "", code = "";
        public string[] lit = new string[0], idle = new string[0], fitted = new string[0];

        [Tooltip("A use did something: the part used")] public StringEvent onUsed = new StringEvent();
        public BoolEvent onSwitched = new BoolEvent(), onLive = new BoolEvent();
        [Tooltip("\"power\", \"code\", or the item it wants")] public StringEvent onRefused = new StringEvent();
        public StringEvent onKeyed = new StringEvent(), onPicked = new StringEvent();

        /// <summary>Its state or output moved: what a machine that needs it listens for.</summary>
        public event Action Changed;

        public bool Live { get; private set; }
        public bool Powered { get; private set; } = true;
        public int Count { get; private set; }  // a turn's steps open
        public bool HasItem { get; private set; }
        public string State { get; private set; } = "";  // a panel's keys, "1" up
        public List<string> Keys { get; } = new List<string>();

        Animation anim;
        readonly Dictionary<string, Renderer> parts = new Dictionary<string, Renderer>();
        readonly Dictionary<string, float> at = new Dictionary<string, float>();
        readonly Dictionary<string, float> onAt = new Dictionary<string, float>();
        readonly Dictionary<string, (float to, Action then)> moves = new Dictionary<string, (float, Action)>();
        string entry = "";
        float shown;
        bool opening = true, awake;

        void Awake()
        {
            anim = GetComponent<Animation>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                parts[r.name] = r;
                var k = r.name.LastIndexOf("_key_", StringComparison.Ordinal);
                if (k >= 0)
                    Keys.Add(r.name.Substring(k + 5));
            }
            Count = on ? steps : 0;
            opening = !on;
            HasItem = on && kind == Kind.Slot && !keep;
            State = on && kind == Kind.Panel ? code : new string('0', Keys.Count);
            if (Has(clip) && (kind == Kind.Switch || kind == Kind.Slot || kind == Kind.Powered) && !keep)
                Pose(clip, on ? OnAt(clip) : 0f);
            Show(fitted, HasItem);
            awake = true;
            Wire(needs);
        }

        /// <summary>
        /// Uses it, carrying <paramref name="items"/> (what it takes comes from there, what is picked up
        /// goes there), on <paramref name="part"/> where it has keys. Whether it did anything.
        /// </summary>
        public bool Use(List<string> items, string part = "")
        {
            items = items ?? new List<string>();
            if (kind == Kind.Item)
            {
                items.Add(item);
                onPicked.Invoke(item);
                onUsed.Invoke(part);
                gameObject.SetActive(false);
                return true;
            }
            if (kind == Kind.Gauge || moves.ContainsKey(clip) && (kind == Kind.Switch || kind == Kind.Turn || kind == Kind.Slot || kind == Kind.Powered))
                return false;
            if (takes != "" && kind != Kind.Slot && !HasItem)
            {
                if (!items.Remove(takes))
                {
                    onRefused.Invoke(takes);
                    return false;
                }
                HasItem = true;
                Show(fitted, true);
                onUsed.Invoke(part);
                return true;
            }
            switch (kind)
            {
                case Kind.Switch:
                case Kind.Button:
                    Set(!on);
                    break;
                case Kind.Powered:
                    if (needs.Length > 0)  // on with its power, by itself
                    {
                        if (!Powered)
                            onRefused.Invoke("power");
                        return false;
                    }
                    Set(!on);
                    break;
                case Kind.Turn:
                    if (!Powered)
                    {
                        onRefused.Invoke("power");
                        return false;
                    }
                    if (opening)
                    {
                        Count++;
                        Play(clip, Len(clip), () => Pose(clip, 0f));
                    }
                    else
                    {
                        Count--;
                        Pose(clip, Len(clip));
                        Play(clip, 0f);
                    }
                    if (Count == steps || Count == 0)
                        opening = Count == 0;
                    Flip(Count == steps);
                    break;
                case Kind.Slot:
                    if (keep)
                    {
                        if (!items.Contains(takes))
                        {
                            onRefused.Invoke(takes);
                            return false;
                        }
                        Show(fitted, true);
                        Play(clip, Len(clip), () => { Pose(clip, 0f); Show(fitted, false); });
                        Flip(!on);
                    }
                    else if (HasItem)
                    {
                        items.Add(takes);
                        HasItem = false;
                        Play(clip, 0f, () => Show(fitted, false));
                        Flip(false);
                    }
                    else if (items.Remove(takes))
                    {
                        HasItem = true;
                        Show(fitted, true);
                        Play(clip, OnAt(clip));
                        Flip(true);
                    }
                    else
                    {
                        onRefused.Invoke(takes);
                        return false;
                    }
                    break;
                case Kind.Keypad:
                {
                    var key = Key(part);
                    if (key == "")
                        return false;
                    Play(part, Len(part), () => Pose(part, 0f));
                    onKeyed.Invoke(key);
                    if (!Powered)
                        onRefused.Invoke("power");
                    else if (key == "C")
                        entry = "";
                    else if (key == "on" || key == "off")
                        Flip(key == "on");
                    else if (code == "")
                        Flip(true);
                    else
                    {
                        entry += key;
                        if (entry.Length >= code.Length)
                        {
                            if (entry == code) Flip(true); else onRefused.Invoke("code");
                            entry = "";
                        }
                    }
                    break;
                }
                case Kind.Panel:
                {
                    var k = Keys.IndexOf(Key(part));
                    if (k < 0)
                        return false;
                    var up = State[k] == '0';
                    State = State.Substring(0, k) + (up ? "1" : "0") + State.Substring(k + 1);
                    Play(part, up ? OnAt(part) : 0f);
                    if (!Powered)
                        onRefused.Invoke("power");
                    Flip(State == code);
                    break;
                }
            }
            onUsed.Invoke(part);
            return true;
        }

        /// <summary>
        /// Sets it on or off without a use: a turn all its steps, a keypad's code in, a panel's keys to
        /// it, a slot's item conjured in or out, an item picked up by nobody.
        /// </summary>
        public void SetOn(bool value)
        {
            switch (kind)
            {
                case Kind.Turn:
                    Count = value ? steps : 0;
                    opening = !value;
                    break;
                case Kind.Panel:
                    State = value ? code : new string('0', Keys.Count);
                    for (var k = 0; k < Keys.Count; k++)
                    {
                        var part = parts.Keys.FirstOrDefault(n => n.EndsWith("_key_" + Keys[k])) ?? "";
                        Play(part, State[k] == '1' ? OnAt(part) : 0f);
                    }
                    break;
                case Kind.Slot:
                    HasItem = value && !keep;
                    Show(fitted, HasItem);
                    break;
                case Kind.Item:
                    gameObject.SetActive(!value);
                    return;
            }
            if ((kind == Kind.Switch || kind == Kind.Slot || kind == Kind.Powered) && !keep)
                Play(clip, value ? OnAt(clip) : 0f);
            Flip(value);
        }

        /// <summary>Every clip where it is going, at once (a loop keeps running).</summary>
        public void Settle()
        {
            foreach (var c in moves.Keys.ToList())
            {
                var (to, then) = moves[c];
                if (float.IsPositiveInfinity(to))
                    continue;
                moves.Remove(c);
                Pose(c, to);
                then?.Invoke();
            }
        }

        /// <summary>Its needs, the machines it reads.</summary>
        public void Wire(Machine[] machines)
        {
            foreach (var m in needs.Where(m => m != null))
                m.Changed -= Refresh;
            needs = machines ?? new Machine[0];
            foreach (var m in needs.Where(m => m != null))
                m.Changed += Refresh;
            if (awake)
                Refresh();
        }

        public float Output() =>
            kind == Kind.Turn ? (Powered ? (float)Count / steps : 0f) : kind == Kind.Gauge ? shown : Live ? 1f : 0f;

        void Flip(bool value)
        {
            if (value != on)
            {
                on = value;
                onSwitched.Invoke(on);
                if (on && !Powered && kind != Kind.Powered)
                    onRefused.Invoke("power");
            }
            Refresh();
        }

        void Set(bool value)
        {
            if (kind == Kind.Button)
                Play(clip, Len(clip), () => Pose(clip, 0f));
            else
                Play(clip, value ? OnAt(clip) : 0f);
            Flip(value);
        }

        void Refresh()
        {
            var was = Live;
            Powered = needs.All(m => m != null && m.Live);
            if (kind == Kind.Powered && needs.Length > 0 && Powered != on)
            {
                on = Powered;
                Play(clip, on ? OnAt(clip) : 0f);
                onSwitched.Invoke(on);
            }
            if (kind == Kind.Gauge)
            {
                shown = needs.Length > 0 && needs[0] != null ? needs[0].Output() : 0f;
                on = shown > 0f;
            }
            Live = on && Powered;
            Show(lit, Live);
            Show(idle, Powered && !on);
            for (var k = 0; k < Keys.Count; k++)
                Show(parts.Keys.Where(n => n.EndsWith("_lamp_" + Keys[k])), Powered && State.Length > k && State[k] == '1');
            if (Has(run) && Live != moves.ContainsKey(run))
            {
                if (Live) moves[run] = (float.PositiveInfinity, null);
                else moves.Remove(run);
            }
            if (Has(level))
                Play(level, Output() * Len(level));
            if (Live != was)
                onLive.Invoke(Live);
            Changed?.Invoke();
        }

        void Update()
        {
            foreach (var c in moves.Keys.ToList())
            {
                var (to, then) = moves[c];
                at.TryGetValue(c, out var t);
                if (float.IsPositiveInfinity(to))
                {
                    Pose(c, (t + Time.deltaTime) % Len(c));
                    continue;
                }
                t = Mathf.MoveTowards(t, to, Time.deltaTime);
                Pose(c, t);
                if (t == to)
                {
                    moves.Remove(c);
                    then?.Invoke();
                }
            }
        }

        void Play(string c, float to, Action then = null)
        {
            if (Has(c))
                moves[c] = (to, then);
            else
                then?.Invoke();
        }

        /// <summary>The clip's curves at t seconds, sampled alone onto its parts.</summary>
        void Pose(string c, float t)
        {
            at[c] = t;
            var s = anim[c];
            s.enabled = true;
            s.weight = 1f;
            s.time = t;
            anim.Sample();
            s.enabled = false;
        }

        bool Has(string c) => !string.IsNullOrEmpty(c) && anim != null && anim[c] != null;

        float Len(string c) => Has(c) ? anim[c].length : 0f;

        /// <summary>
        /// Where the clip holds on: its end, or, when it ends where it starts (a recipe's track without keys
        /// plays there and back), its middle: else a lid opened by a switch shut itself again.
        /// </summary>
        float OnAt(string c)
        {
            if (!Has(c))
                return 0f;
            if (onAt.TryGetValue(c, out var held))
                return held;
            var had = at.TryGetValue(c, out var now);
            var all = GetComponentsInChildren<Transform>(true);
            Pose(c, 0f);
            var start = all.Select(x => (x.localPosition, x.localRotation, x.localScale)).ToArray();
            Pose(c, Len(c));
            var same = all.Select((x, i) => (x.localPosition - start[i].Item1).sqrMagnitude < 1e-8f
                && Quaternion.Angle(x.localRotation, start[i].Item2) < 0.01f && (x.localScale - start[i].Item3).sqrMagnitude < 1e-8f).All(b => b);
            Pose(c, had ? now : 0f);
            if (!had)
                at.Remove(c);
            return onAt[c] = same ? Len(c) / 2f : Len(c);
        }

        string Key(string part)
        {
            var k = part.LastIndexOf("_key_", StringComparison.Ordinal);
            return k >= 0 && parts.ContainsKey(part) ? part.Substring(k + 5) : "";
        }

        void Show(IEnumerable<string> names, bool value)
        {
            foreach (var n in names)
                if (parts.TryGetValue(n, out var r))
                    r.enabled = value;
        }
    }
}
