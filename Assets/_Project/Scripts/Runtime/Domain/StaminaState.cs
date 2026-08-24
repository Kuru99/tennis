using System;

namespace PrideCourt.Domain
{
    [Serializable]
    public sealed class StaminaState
    {
        public const float Maximum = 80f;
        public const float ExhaustionRecoveryThreshold = Maximum * 0.8f;
        public const float RecoveryDelayAfterSpend = 0.4f;

        private float recoveryDelay;

        public StaminaState()
        {
            ResetForPoint();
        }

        public float Current { get; private set; }
        public bool IsExhausted { get; private set; }
        public float Normalized => Current / Maximum;

        public bool TrySpend(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (Current < amount)
            {
                Current = 0f;
                IsExhausted = true;
                recoveryDelay = RecoveryDelayAfterSpend;
                return false;
            }

            Current = Math.Max(0f, Current - amount);
            recoveryDelay = RecoveryDelayAfterSpend;
            if (Current <= 0f)
            {
                IsExhausted = true;
            }

            return true;
        }

        public bool TrySpendCommitted(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (Current <= 0f)
            {
                IsExhausted = true;
                return false;
            }

            Current = Math.Max(0f, Current - amount);
            recoveryDelay = RecoveryDelayAfterSpend;
            if (Current <= 0f)
            {
                IsExhausted = true;
            }

            return true;
        }

        public void TickRecovery(float deltaTime, bool isWalking, bool isStationary)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            if (recoveryDelay > 0f)
            {
                recoveryDelay = Math.Max(0f, recoveryDelay - deltaTime);
                return;
            }

            float rate = isStationary ? 14f : isWalking ? 8f : 0f;
            Current = Math.Min(Maximum, Current + rate * deltaTime);
            if (IsExhausted && Current >= ExhaustionRecoveryThreshold)
            {
                IsExhausted = false;
            }
        }

        public void Restore(float amount)
        {
            Current = Math.Min(Maximum, Current + Math.Max(0f, amount));
            if (IsExhausted && Current >= ExhaustionRecoveryThreshold)
            {
                IsExhausted = false;
            }
        }

        public void ResetForPoint()
        {
            Current = Maximum;
            IsExhausted = false;
            recoveryDelay = 0f;
        }

        public void Restore(float current, bool exhausted)
        {
            Current = Math.Max(0f, Math.Min(Maximum, current));
            IsExhausted = exhausted;
            recoveryDelay = 0f;
        }
    }
}
