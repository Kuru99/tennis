using System;
using System.IO;
using PrideCourt.Domain;
using PrideCourt.Input;
using UnityEngine;

namespace PrideCourt.Networking
{
    public enum LanPacketType : byte
    {
        Hello = 1,
        Welcome = 2,
        Command = 3,
        Snapshot = 4,
        Disconnect = 5,
        SettingsPause = 6
    }

    public readonly struct LanPacket
    {
        public LanPacket(LanPacketType type, byte[] payload)
        {
            Type = type;
            Payload = payload;
        }

        public LanPacketType Type { get; }
        public byte[] Payload { get; }
    }

    public static class LanBattleProtocol
    {
        public const int Version = 2;
        public const int GamePort = 47772;
        public const int DiscoveryPort = 47771;
        public const int MaximumPacketBytes = 64 * 1024;
        public const string Signature = "PRIDE_COURT_LAN";

        public static byte[] EncodeHello(AthleteIdentity identity, CardId[] deck)
        {
            return Write(writer =>
            {
                writer.Write(Version);
                writer.Write((byte)identity);
                WriteCards(writer, deck);
            });
        }

        public static bool TryDecodeHello(byte[] payload, out AthleteIdentity identity, out CardId[] deck)
        {
            identity = default;
            deck = Array.Empty<CardId>();
            try
            {
                using BinaryReader reader = Reader(payload);
                if (reader.ReadInt32() != Version) return false;
                identity = (AthleteIdentity)reader.ReadByte();
                deck = ReadCards(reader, 16);
                return deck.Length == 16;
            }
            catch { return false; }
        }

        public static byte[] EncodeCommand(TennisCommand command)
        {
            return Write(writer =>
            {
                WriteVector2(writer, command.Move);
                writer.Write(command.Dash);
                writer.Write(command.StrongPressed);
                writer.Write(command.SafePressed);
                writer.Write(command.TossPressed);
                writer.Write(command.SpecialPressed);
                writer.Write(command.DivePressed);
                WriteVector2(writer, command.DiveDirection);
                writer.Write(command.CardDown);
                writer.Write(command.CardHeld);
                writer.Write(command.CardReleased);
                WriteVector2(writer, command.CardPointer);
                writer.Write(command.ShotInputHeld);
                writer.Write(command.DirectCardPressed);
                writer.Write(command.DirectCardSlot);
                writer.Write(command.PreparationChoice);
            });
        }

        public static TennisCommand DecodeCommand(byte[] payload)
        {
            using BinaryReader reader = Reader(payload);
            return new TennisCommand(
                ReadVector2(reader), reader.ReadBoolean(), reader.ReadBoolean(), reader.ReadBoolean(),
                reader.ReadBoolean(), reader.ReadBoolean(), reader.ReadBoolean(), ReadVector2(reader),
                reader.ReadBoolean(), reader.ReadBoolean(), reader.ReadBoolean(), ReadVector2(reader),
                reader.ReadBoolean(), reader.ReadBoolean(), reader.ReadInt32(), reader.ReadInt32());
        }

        public static byte[] EncodeSettingsPause(bool paused)
        {
            return new[] { paused ? (byte)1 : (byte)0 };
        }

        public static bool DecodeSettingsPause(byte[] payload)
        {
            if (payload == null || payload.Length != 1)
                throw new InvalidDataException("Settings pause payload is invalid.");
            return payload[0] != 0;
        }

        public static byte[] EncodeSnapshot(LanMatchState state)
        {
            return Write(writer =>
            {
                writer.Write(state.Sequence);
                writer.Write(state.HasStarted);
                writer.Write((byte)state.Phase);
                writer.Write(state.NearPoints);
                writer.Write(state.FarPoints);
                writer.Write(state.CompletedPoints);
                writer.Write(state.PhaseTimeRemaining);
                writer.Write(state.ServeHasBeenStruck);
                writer.Write(state.IsGameplayPaused);
                writer.Write(state.StatusMessage ?? string.Empty);
                writer.Write(state.HasActiveCourtCard);
                writer.Write((byte)state.ActiveCourtCard);
                WriteAthlete(writer, state.NearAthlete);
                WriteAthlete(writer, state.FarAthlete);
                WriteBall(writer, state.Ball);
                WriteCardState(writer, state.NearCards);
                WriteCardState(writer, state.FarCards);
            });
        }

        public static LanMatchState DecodeSnapshot(byte[] payload)
        {
            using BinaryReader reader = Reader(payload);
            return new LanMatchState
            {
                Sequence = reader.ReadUInt32(),
                HasStarted = reader.ReadBoolean(),
                Phase = (MatchPhase)reader.ReadByte(),
                NearPoints = reader.ReadInt32(),
                FarPoints = reader.ReadInt32(),
                CompletedPoints = reader.ReadInt32(),
                PhaseTimeRemaining = reader.ReadSingle(),
                ServeHasBeenStruck = reader.ReadBoolean(),
                IsGameplayPaused = reader.ReadBoolean(),
                StatusMessage = reader.ReadString(),
                HasActiveCourtCard = reader.ReadBoolean(),
                ActiveCourtCard = (CardId)reader.ReadByte(),
                NearAthlete = ReadAthlete(reader),
                FarAthlete = ReadAthlete(reader),
                Ball = ReadBall(reader),
                NearCards = ReadCardState(reader),
                FarCards = ReadCardState(reader)
            };
        }

        public static byte[] Frame(LanPacketType type, byte[] payload)
        {
            payload ??= Array.Empty<byte>();
            byte[] frame = new byte[payload.Length + 5];
            byte[] size = BitConverter.GetBytes(payload.Length + 1);
            Buffer.BlockCopy(size, 0, frame, 0, 4);
            frame[4] = (byte)type;
            if (payload.Length > 0) Buffer.BlockCopy(payload, 0, frame, 5, payload.Length);
            return frame;
        }

        public static bool TryParseFrame(byte[] frame, out LanPacket packet)
        {
            packet = default;
            if (frame == null || frame.Length < 5 || frame.Length > MaximumPacketBytes + 4) return false;
            int bodyLength = BitConverter.ToInt32(frame, 0);
            if (bodyLength != frame.Length - 4 || bodyLength < 1 || bodyLength > MaximumPacketBytes) return false;
            LanPacketType type = (LanPacketType)frame[4];
            if (!Enum.IsDefined(typeof(LanPacketType), type)) return false;
            byte[] payload = new byte[bodyLength - 1];
            if (payload.Length > 0) Buffer.BlockCopy(frame, 5, payload, 0, payload.Length);
            packet = new LanPacket(type, payload);
            return true;
        }

        private static byte[] Write(Action<BinaryWriter> action)
        {
            using MemoryStream stream = new MemoryStream(1024);
            using BinaryWriter writer = new BinaryWriter(stream);
            action(writer);
            writer.Flush();
            return stream.ToArray();
        }

        private static BinaryReader Reader(byte[] payload) => new BinaryReader(new MemoryStream(payload, false));
        private static void WriteVector2(BinaryWriter writer, Vector2 value) { writer.Write(value.x); writer.Write(value.y); }
        private static Vector2 ReadVector2(BinaryReader reader) => new Vector2(reader.ReadSingle(), reader.ReadSingle());
        private static void WriteVector3(BinaryWriter writer, Vector3 value) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }
        private static Vector3 ReadVector3(BinaryReader reader) => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        private static void WriteQuaternion(BinaryWriter writer, Quaternion value) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); writer.Write(value.w); }
        private static Quaternion ReadQuaternion(BinaryReader reader) => new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        private static void WriteCards(BinaryWriter writer, CardId[] cards)
        {
            int count = cards == null ? 0 : Math.Min(cards.Length, 32);
            writer.Write((byte)count);
            for (int i = 0; i < count; i++) writer.Write((byte)cards[i]);
        }

        private static CardId[] ReadCards(BinaryReader reader, int maximum)
        {
            int count = reader.ReadByte();
            if (count < 0 || count > maximum) throw new InvalidDataException("Invalid card count.");
            CardId[] cards = new CardId[count];
            for (int i = 0; i < count; i++) cards[i] = (CardId)reader.ReadByte();
            return cards;
        }

        private static void WriteAthlete(BinaryWriter writer, LanAthleteState state)
        {
            WriteVector3(writer, state.Position); writer.Write(state.RotationY); writer.Write((byte)state.Identity);
            writer.Write(state.Stamina); writer.Write(state.Exhausted); writer.Write(state.SpecialGauge);
            writer.Write(state.PlanarSpeed); writer.Write(state.IsDashing); writer.Write(state.IsDiving);
            writer.Write(state.IsSpecialReserved);
        }

        private static LanAthleteState ReadAthlete(BinaryReader reader) => new LanAthleteState
        {
            Position = ReadVector3(reader), RotationY = reader.ReadSingle(), Identity = (AthleteIdentity)reader.ReadByte(),
            Stamina = reader.ReadSingle(), Exhausted = reader.ReadBoolean(), SpecialGauge = reader.ReadSingle(),
            PlanarSpeed = reader.ReadSingle(), IsDashing = reader.ReadBoolean(), IsDiving = reader.ReadBoolean(),
            IsSpecialReserved = reader.ReadBoolean()
        };

        private static void WriteBall(BinaryWriter writer, LanBallState state)
        {
            WriteVector3(writer, state.Position); WriteQuaternion(writer, state.Rotation); WriteVector3(writer, state.Velocity);
            writer.Write(state.State); writer.Write((byte)state.LastHitter); writer.Write(state.BounceCount); writer.Write(state.SpecialBall);
        }

        private static LanBallState ReadBall(BinaryReader reader) => new LanBallState
        {
            Position = ReadVector3(reader), Rotation = ReadQuaternion(reader), Velocity = ReadVector3(reader),
            State = reader.ReadInt32(), LastHitter = (CourtSide)reader.ReadByte(), BounceCount = reader.ReadInt32(),
            SpecialBall = reader.ReadBoolean()
        };

        private static void WriteCardState(BinaryWriter writer, LanCardState state)
        {
            WriteCards(writer, state.Hand); writer.Write(state.ActiveIndex); writer.Write(state.UsedThisPoint);
            writer.Write(state.HasPending); writer.Write((byte)state.Pending);
        }

        private static LanCardState ReadCardState(BinaryReader reader) => new LanCardState
        {
            Hand = ReadCards(reader, 3), ActiveIndex = reader.ReadInt32(), UsedThisPoint = reader.ReadBoolean(),
            HasPending = reader.ReadBoolean(), Pending = (CardId)reader.ReadByte()
        };
    }
}
