//----------------------------------------------------------------------------------------------------------------------+
// A cheat code done on the board, which unlocks the Retro theme: lean forward, forward, back, back, left, right, left,
// right, then twist left, twist right.
//
// Tuned on logged attempts (2026-10-19). A lean is 10 % or more off centre on its main axis; a twist is 9 % or more on
// one diagonal pair and clearly more than any corner lean would explain (people lean a little while twisting). A move
// counts once the same reading has lasted 0.05 s (two board updates); doing it again needs a different reading (or centre) in between.
//
// Real attempts are messy: a small bounce forward after leaning back, a flick on the way into the twist, a last
// "right" that is only a quick nudge. So the code only has to appear in order among the moves of the last 15 seconds,
// with up to 2 stray moves anywhere in between. Stray moves can't break it, but it still takes the deliberate sequence.
//----------------------------------------------------------------------------------------------------------------------+

using System;
using System.Collections.Generic;

namespace WiiBalanceWalker
{
    class SecretCode
    {
        enum Move { Forward, Back, Left, Right, TwistLeft, TwistRight }

        static readonly Move[] code =
        {
            Move.Forward, Move.Forward, Move.Back, Move.Back,
            Move.Left, Move.Right, Move.Left, Move.Right,
            Move.TwistLeft, Move.TwistRight,
        };

        const float  LeanMin = 10f, TwistMin = 9f, TwistOverCorner = 4f;
        const double HoldSeconds = 0.05, WindowSeconds = 15;
        const int    MaxStrayMoves = 2;

        readonly List<KeyValuePair<Move, DateTime>> history = new List<KeyValuePair<Move, DateTime>>();
        Move? reading;
        DateTime readingSince;
        bool readingCounted;

        // kg: weight on the board; brX / brY: share of the weight on the right / back half (50 = centred); twist:
        // back-right + front-left diagonal share minus 50 (positive turns right). Returns true once, when the code lands.
        public bool Feed(float kg, float brX, float brY, float twist, DateTime now)
        {
            Move? move = null;
            if (kg > 5f && !float.IsNaN(brX) && !float.IsNaN(brY) && !float.IsNaN(twist)) move = Classify(brX - 50, brY - 50, twist);

            if (move != reading) { reading = move; readingSince = now; readingCounted = false; }
            if (move == null || readingCounted || (now - readingSince).TotalSeconds < HoldSeconds) return false;
            readingCounted = true;

            history.Add(new KeyValuePair<Move, DateTime>(move.Value, now));
            history.RemoveAll(h => (now - h.Value).TotalSeconds > WindowSeconds);
            if (!Matches()) return false;
            history.Clear();
            return true;
        }

        static Move? Classify(float lx, float ly, float twist)
        {
            // A corner lean also loads one diagonal, by about the smaller of the two leans; a twist has to beat that.
            var corner = Math.Min(Math.Abs(lx), Math.Abs(ly));
            if (Math.Abs(twist) >= TwistMin && Math.Abs(twist) - corner >= TwistOverCorner) return twist > 0 ? Move.TwistRight : Move.TwistLeft;
            if (Math.Max(Math.Abs(lx), Math.Abs(ly)) < LeanMin) return null;
            return Math.Abs(lx) > Math.Abs(ly) ? (lx > 0 ? Move.Right : Move.Left) : (ly < 0 ? Move.Forward : Move.Back);
        }

        // Is the code a subsequence of the recent moves, ending with the newest one, with at most MaxStrayMoves extra moves
        // in between? Matching from the newest backwards takes the latest possible moves, which keeps the strays fewest.
        bool Matches()
        {
            int ci = code.Length - 1, stray = 0;
            for (int hi = history.Count - 1; hi >= 0 && ci >= 0; hi--)
            {
                if (history[hi].Key == code[ci]) ci--;
                else if (ci == code.Length - 1 || ++stray > MaxStrayMoves) return false;
            }
            return ci < 0;
        }
    }
}
