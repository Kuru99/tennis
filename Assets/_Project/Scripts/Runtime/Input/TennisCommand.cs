using UnityEngine;

namespace PrideCourt.Input
{
    public readonly struct TennisCommand
    {
        public TennisCommand(
            Vector2 move,
            bool dash,
            bool strongPressed,
            bool safePressed,
            bool tossPressed,
            bool specialPressed,
            bool divePressed = false,
            Vector2 diveDirection = default,
            bool cardDown = false,
            bool cardHeld = false,
            bool cardReleased = false,
            Vector2 cardPointer = default,
            bool shotInputHeld = false,
            bool directCardPressed = false,
            int directCardSlot = -1,
            int preparationChoice = -2)
        {
            Move = Vector2.ClampMagnitude(move, 1f);
            Dash = dash;
            StrongPressed = strongPressed;
            SafePressed = safePressed;
            TossPressed = tossPressed;
            SpecialPressed = specialPressed;
            DivePressed = divePressed;
            DiveDirection = Vector2.ClampMagnitude(diveDirection, 1f);
            CardDown = cardDown;
            CardHeld = cardHeld;
            CardReleased = cardReleased;
            CardPointer = cardPointer;
            ShotInputHeld = shotInputHeld;
            DirectCardPressed = directCardPressed;
            DirectCardSlot = directCardSlot;
            PreparationChoice = preparationChoice;
        }

        public Vector2 Move { get; }
        public bool Dash { get; }
        public bool StrongPressed { get; }
        public bool SafePressed { get; }
        public bool TossPressed { get; }
        public bool SpecialPressed { get; }
        public bool DivePressed { get; }
        public Vector2 DiveDirection { get; }
        public bool CardDown { get; }
        public bool CardHeld { get; }
        public bool CardReleased { get; }
        public Vector2 CardPointer { get; }
        public bool ShotInputHeld { get; }
        public bool DirectCardPressed { get; }
        public int DirectCardSlot { get; }
        public int PreparationChoice { get; }
    }

    public interface ITennisCommandSource
    {
        TennisCommand ReadCommand();
    }

    public static class LocalPreparationInputRouter
    {
        private static int queuedChoice = -2;

        public static void Queue(int choice)
        {
            queuedChoice = Mathf.Clamp(choice, -1, 2);
        }

        public static bool TryConsume(out int choice)
        {
            choice = queuedChoice;
            queuedChoice = -2;
            return choice >= -1;
        }
    }
}
