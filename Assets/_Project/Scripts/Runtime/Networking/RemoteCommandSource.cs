using System.Collections.Generic;
using PrideCourt.Input;
using UnityEngine;

namespace PrideCourt.Networking
{
    public sealed class RemoteCommandSource : MonoBehaviour, ITennisCommandSource
    {
        private readonly Queue<TennisCommand> pending = new Queue<TennisCommand>();
        private TennisCommand continuous;

        public void Push(TennisCommand command)
        {
            continuous = command;
            if (pending.Count >= 4) pending.Dequeue();
            pending.Enqueue(command);
        }

        public TennisCommand ReadCommand()
        {
            if (pending.Count > 0) return pending.Dequeue();
            return new TennisCommand(continuous.Move, continuous.Dash, false, false, false, false,
                false, Vector2.zero, false, continuous.CardHeld, false, continuous.CardPointer,
                continuous.ShotInputHeld);
        }

        public void Clear()
        {
            pending.Clear();
            continuous = default;
        }
    }
}
