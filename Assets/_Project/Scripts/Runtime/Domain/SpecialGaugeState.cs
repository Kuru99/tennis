using System;

namespace PrideCourt.Domain
{
    [Serializable]
    public sealed class SpecialGaugeState
    {
        public const float Maximum = 50f;
        public const float ActivationThreshold = Maximum;

        public float Current { get; private set; }
        public bool IsReady => Current >= ActivationThreshold;
        public float Normalized => Current / Maximum;

        public void Add(float amount)
        {
            Current = Math.Min(Maximum, Current + Math.Max(0f, amount));
        }

        public bool TryConsumeAll()
        {
            if (!IsReady)
            {
                return false;
            }

            Current = 0f;
            return true;
        }

        public void Reset()
        {
            Current = 0f;
        }

        public void Restore(float current)
        {
            Current = Math.Max(0f, Math.Min(Maximum, current));
        }
    }
}
