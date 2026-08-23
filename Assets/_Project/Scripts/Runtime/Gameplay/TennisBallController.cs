using PrideCourt.Domain;
using PrideCourt.Presentation;
using UnityEngine;

namespace PrideCourt.Gameplay
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class TennisBallController : MonoBehaviour
    {
        private enum BallState
        {
            Idle,
            Tossed,
            ServeFlight,
            RallyFlight,
            Dead
        }

        [SerializeField] private TennisMatchController match;
        [SerializeField, Range(0.25f, 1.5f)] private float ballSpeedMultiplier = 0.6f;
        [SerializeField, Range(0.5f, 2.5f)] private float serveTossRelativeSpeedMultiplier = 1.5f;

        private Rigidbody body;
        private BallState state;
        private CourtSide lastHitter;
        private int bounceCount;
        private bool hasLegalBounce;
        private TennisAthleteController tossingAthlete;
        private bool specialBall;
        private AthleteIdentity specialOwner;
        private LineRenderer lobLandingMarker;
        private bool networkReplica;
        private bool hasReplicaTarget;
        private Vector3 replicaTargetPosition;
        private Quaternion replicaTargetRotation;
        private ShotPower activeServePower;
        private Vector3 activeFlightAcceleration;
        private bool touchedNetThisFlight;
        private bool serveReturnedBeforeBounce;
        private float lastNetContactTime = float.NegativeInfinity;

        public bool IsTossed => state == BallState.Tossed;
        public bool IsServeInFlight => state == BallState.ServeFlight;
        public bool WasServeReturnedBeforeBounce => serveReturnedBeforeBounce;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            CreateLobLandingMarker();
        }

        public void Configure(TennisMatchController configuredMatch)
        {
            match = configuredMatch;
        }

        private void Update()
        {
            if (networkReplica)
            {
                if (hasReplicaTarget)
                {
                    float blend = 1f - Mathf.Exp(-30f * Time.unscaledDeltaTime);
                    transform.position = Vector3.Lerp(transform.position, replicaTargetPosition, blend);
                    transform.rotation = Quaternion.Slerp(transform.rotation, replicaTargetRotation, blend);
                    if (body != null) body.position = transform.position;
                }
                return;
            }

            if (state == BallState.Tossed && (transform.position.y < 0.45f || Time.timeScale <= 0f))
            {
                if (transform.position.y < 0.45f)
                {
                    ResetForServe(tossingAthlete);
                }
            }

            if ((state == BallState.ServeFlight || state == BallState.RallyFlight) &&
                (transform.position.y < -2f || Mathf.Abs(transform.position.x) > 7.5f || Mathf.Abs(transform.position.z) > 14f))
            {
                ResolveOutOfBounds();
            }
        }

        public void SetNetworkReplica(bool value)
        {
            networkReplica = value;
            hasReplicaTarget = false;
            if (body == null) body = GetComponent<Rigidbody>();
            if (body == null) return;
            if (value)
            {
                body.useGravity = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.None;
            }
        }

        public LanBallState CaptureLanState()
        {
            return new LanBallState
            {
                Position = transform.position,
                Rotation = transform.rotation,
                Velocity = body == null ? Vector3.zero : body.linearVelocity,
                State = (int)state,
                LastHitter = lastHitter,
                BounceCount = bounceCount,
                SpecialBall = specialBall
            };
        }

        public void ApplyLanState(LanBallState snapshot)
        {
            if (!networkReplica) SetNetworkReplica(true);
            replicaTargetPosition = snapshot.Position;
            replicaTargetRotation = snapshot.Rotation;
            if (!hasReplicaTarget || Vector3.Distance(transform.position, snapshot.Position) > 6f)
            {
                transform.SetPositionAndRotation(replicaTargetPosition, replicaTargetRotation);
                if (body != null) body.position = snapshot.Position;
            }
            hasReplicaTarget = true;
            state = snapshot.State >= 0 && snapshot.State <= (int)BallState.Dead
                ? (BallState)snapshot.State
                : BallState.Dead;
            lastHitter = snapshot.LastHitter;
            bounceCount = snapshot.BounceCount;
            specialBall = snapshot.SpecialBall;
        }

        private void LateUpdate()
        {
            if (state == BallState.Idle && tossingAthlete != null && body != null && body.isKinematic)
            {
                SetHeldPosition(tossingAthlete);
            }
        }

        private void FixedUpdate()
        {
            if (body.isKinematic ||
                (state != BallState.Tossed && state != BallState.ServeFlight && state != BallState.RallyFlight))
            {
                return;
            }

            Vector3 velocity = body.linearVelocity;
            velocity += (state == BallState.Tossed ? ServeTossScaledGravity : activeFlightAcceleration) * Time.fixedDeltaTime;
            body.linearVelocity = velocity;
        }

        public void ResetForServe(TennisAthleteController server)
        {
            tossingAthlete = server;
            state = BallState.Idle;
            bounceCount = 0;
            hasLegalBounce = false;
            specialBall = false;
            activeFlightAcceleration = ScaledGravity;
            touchedNetThisFlight = false;
            serveReturnedBeforeBounce = false;
            lastNetContactTime = float.NegativeInfinity;
            SetLobMarkerVisible(false, Vector3.zero);
            body.useGravity = false;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
            body.interpolation = RigidbodyInterpolation.None;
            if (server != null)
            {
                SetHeldPosition(server);
            }
        }

        private static Vector3 ResolveHeldPosition(TennisAthleteController server)
        {
            Vector3 forward = server.Side == CourtSide.Near ? Vector3.forward : Vector3.back;
            return server.transform.position + forward * 0.45f + Vector3.up * 1.15f;
        }

        public void BeginServeToss(TennisAthleteController server)
        {
            if (match == null ||
                match.Phase != MatchPhase.Serving ||
                match.Server != server.Side ||
                state != BallState.Idle)
            {
                return;
            }

            tossingAthlete = server;
            Vector3 forward = server.Side == CourtSide.Near ? Vector3.forward : Vector3.back;
            Vector3 tossPosition = server.transform.position + forward * 0.45f + Vector3.up * 1.25f;
            body.position = tossPosition;
            transform.position = tossPosition;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.isKinematic = false;
            body.useGravity = false;
            body.linearVelocity = Vector3.up * (6.2f * ServeTossSpeedMultiplier);
            body.angularVelocity = Vector3.zero;
            state = BallState.Tossed;
        }

        private void SetHeldPosition(TennisAthleteController server)
        {
            Vector3 heldPosition = ResolveHeldPosition(server);
            body.position = heldPosition;
            transform.position = heldPosition;
        }

        public void StrikeServe(TennisAthleteController athlete, ShotPower power, AimZone aim)
        {
            if (state != BallState.Tossed || athlete != tossingAthlete || match.Server != athlete.Side)
            {
                return;
            }

            lastHitter = athlete.Side;
            bounceCount = 0;
            hasLegalBounce = false;
            touchedNetThisFlight = false;
            lastNetContactTime = float.NegativeInfinity;
            Vector3 target = ResolveServeTarget(athlete, aim);
            activeServePower = power;
            float duration = power == ShotPower.Strong ? 0.62f : 0.78f;
            float sliceDirection = target.x >= 0f ? 1f : -1f;
            Vector3 curveAcceleration = power == ShotPower.Safe
                ? Vector3.right * (sliceDirection * 2.4f)
                : Vector3.zero;
            float clearanceMargin = power == ShotPower.Safe ? 0.22f : 0.12f;
            LaunchTo(target, duration, curveAcceleration, ResolveMinimumNetCenterHeight(clearanceMargin));
            state = BallState.ServeFlight;
            match.NotifyServeStruck();
            PrideCourtAudio.PlayHitEffect(power == ShotPower.Strong);
        }

        public bool CanBeHitBy(CourtSide side)
        {
            if ((state != BallState.RallyFlight && state != BallState.ServeFlight) || side == lastHitter)
            {
                return false;
            }

            return side == CourtSide.Near ? transform.position.z < 0.8f : transform.position.z > -0.8f;
        }

        public bool TryRegisterDoubleHit(CourtSide side)
        {
            if (state != BallState.RallyFlight || side != lastHitter)
            {
                return false;
            }

            EnterDead();
            match.AwardPoint(side.Opposite(), "二度打ち");
            return true;
        }

        public void StopForPointEnd()
        {
            EnterDead();
        }

        public void StrikeRally(
            TennisAthleteController athlete,
            ShotKind kind,
            ShotPower power,
            AimZone aim,
            TimingGrade timing)
        {
            StrikeRally(athlete, kind, power, aim, timing, 1f, 1f, false);
        }

        public void StrikeRally(
            TennisAthleteController athlete,
            ShotKind kind,
            ShotPower power,
            AimZone aim,
            TimingGrade timing,
            float spinMultiplier,
            float accuracyMultiplier,
            bool isSpecial)
        {
            if (!CanBeHitBy(athlete.Side))
            {
                return;
            }

            bool returnedServe = state == BallState.ServeFlight;

            bool returnedBastionSpecial = specialBall && specialOwner == AthleteIdentity.Bastion && athlete.Identity != AthleteIdentity.Bastion;
            if (returnedBastionSpecial)
            {
                athlete.Stamina.TrySpend(25f);
            }

            lastHitter = athlete.Side;
            bounceCount = 0;
            hasLegalBounce = false;
            touchedNetThisFlight = false;
            lastNetContactTime = float.NegativeInfinity;
            specialBall = isSpecial;
            specialOwner = athlete.Identity;
            Vector3 target = ResolveRallyTarget(athlete.Side.Opposite(), kind, aim, timing, accuracyMultiplier);
            bool isLob = kind == ShotKind.AttackLob || kind == ShotKind.DefensiveLob;
            SetLobMarkerVisible(isLob, target);
            float duration = ResolveFlightDuration(kind, power, timing) / Mathf.Max(0.85f, spinMultiplier);
            Vector3 curveAcceleration = ResolveFlightCurve(kind, target, spinMultiplier);
            float clearanceMargin = ResolveNetClearanceMargin(kind, timing);
            LaunchTo(target, duration, curveAcceleration, ResolveMinimumNetCenterHeight(clearanceMargin));
            state = BallState.RallyFlight;
            if (returnedServe)
            {
                serveReturnedBeforeBounce = true;
                match.NotifyValidServe();
            }
            PrideCourtAudio.PlayHitEffect(power == ShotPower.Strong);
            match.NotifyRallyReturn();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (networkReplica)
            {
                return;
            }

            if (state != BallState.ServeFlight && state != BallState.RallyFlight)
            {
                return;
            }

            TennisNetSurface netSurface = collision.gameObject.GetComponent<TennisNetSurface>();
            if (netSurface != null)
            {
                HandleNetCollision(netSurface);
                return;
            }

            if (collision.gameObject.GetComponent<TennisCourtSurface>() == null)
            {
                return;
            }

            Vector3 bouncePosition = collision.GetContact(0).point;
            if (bounceCount == 0) SetLobMarkerVisible(false, Vector3.zero);
            float ballRadius = GetWorldBallRadius();
            bool insideCourt = TennisCourtGeometry.IsBallInSingles(bouncePosition.x, bouncePosition.z, ballRadius);
            float impactSpeed = Mathf.Max(Mathf.Abs(collision.relativeVelocity.y), collision.relativeVelocity.magnitude * 0.45f);
            float activeBounceMultiplier = match == null ? 1f : match.BounceMultiplier;
            BallBounceEffect.Play(bouncePosition, impactSpeed, specialBall, activeBounceMultiplier, insideCourt);
            PrideCourtAudio.PlayBounceEffect(impactSpeed, specialBall, activeBounceMultiplier, insideCourt);

            if (state == BallState.ServeFlight)
            {
                bool insideCorrectServiceBox = insideCourt &&
                                               IsInsideCorrectServiceBox(bouncePosition, lastHitter, tossingAthlete, ballRadius);
                if (touchedNetThisFlight && insideCorrectServiceBox)
                {
                    EnterDead();
                    match.RegisterServeLet();
                    return;
                }

                if (!insideCorrectServiceBox)
                {
                    EnterDead();
                    match.RegisterServeFault(touchedNetThisFlight ? "ネット" : "サービスエリア外");
                    return;
                }

                state = BallState.RallyFlight;
                bounceCount = 1;
                hasLegalBounce = true;
                match.NotifyValidServe();
                ApplyReadableBounce(activeServePower == ShotPower.Strong ? 1.22f : 1f);
                return;
            }

            if (!insideCourt && bounceCount == 0)
            {
                EnterDead();
                match.AwardPoint(lastHitter.Opposite(), "アウト");
                return;
            }

            if (insideCourt)
            {
                if (bounceCount == 0 && IsOnSide(bouncePosition, lastHitter))
                {
                    EnterDead();
                    match.AwardPoint(lastHitter.Opposite(), touchedNetThisFlight ? "ネット" : "自コートに落下");
                    return;
                }

                bounceCount++;
                hasLegalBounce = true;
                if (bounceCount >= 2)
                {
                    EnterDead();
                    match.AwardPoint(lastHitter, "ツーバウンド");
                    return;
                }

                ApplyReadableBounce();
                if (specialBall && specialOwner == AthleteIdentity.Lux && bounceCount == 1)
                {
                    Vector3 velocity = body.linearVelocity;
                    float direction = Mathf.Abs(velocity.x) < 0.2f * SpeedMultiplier
                        ? (lastHitter == CourtSide.Near ? 1f : -1f)
                        : -Mathf.Sign(velocity.x);
                    velocity.x += direction * (4.2f * SpeedMultiplier);
                    body.linearVelocity = velocity;
                }
            }
        }

        private void HandleNetCollision(TennisNetSurface netSurface)
        {
            if (Time.time - lastNetContactTime < 0.08f)
            {
                return;
            }

            lastNetContactTime = Time.time;
            touchedNetThisFlight = true;
            float ballRadius = GetWorldBallRadius();
            bool tippedOver = netSurface.DeflectBall(body, lastHitter, ballRadius, SpeedMultiplier);
            PrideCourtAudio.Instance?.PlayNet(tippedOver);
        }

        private static bool IsOnSide(Vector3 position, CourtSide side)
        {
            return side == CourtSide.Near ? position.z < 0f : position.z > 0f;
        }

        private void ApplyReadableBounce(float shotBounceMultiplier = 1f)
        {
            Vector3 velocity = body.linearVelocity;
            float bounceMultiplier = match == null ? 1f : match.BounceMultiplier;
            velocity.y = Mathf.Max(3.1f * SpeedMultiplier, Mathf.Abs(velocity.y) * 0.72f) * bounceMultiplier * shotBounceMultiplier;
            velocity.x *= 0.94f;
            velocity.z *= 0.94f;
            body.linearVelocity = velocity;
        }

        private void ResolveOutOfBounds()
        {
            if (state == BallState.Dead)
            {
                return;
            }

            EnterDead();
            CourtSide winner = hasLegalBounce ? lastHitter : lastHitter.Opposite();
            match.AwardPoint(winner, hasLegalBounce ? "返球不能" : "アウト");
        }

        private void EnterDead()
        {
            state = BallState.Dead;
            activeFlightAcceleration = Vector3.zero;
            SetLobMarkerVisible(false, Vector3.zero);

            if (body == null)
            {
                return;
            }

            body.useGravity = false;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
            body.interpolation = RigidbodyInterpolation.None;
        }

        private void LaunchTo(Vector3 target, float duration, Vector3 curveAcceleration, float minimumNetCenterHeight)
        {
            body.isKinematic = false;
            body.useGravity = false;
            Vector3 start = transform.position;
            BallTrajectoryPlan plan = BallTrajectoryPlanner.Create(
                start,
                target,
                duration,
                SpeedMultiplier,
                curveAcceleration,
                minimumNetCenterHeight);
            activeFlightAcceleration = plan.Acceleration;
            body.linearVelocity = plan.InitialVelocity;
            body.angularVelocity = new Vector3(8f, 3f, -6f) * SpeedMultiplier;
        }

        private float GetWorldBallRadius()
        {
            SphereCollider sphere = GetComponent<SphereCollider>();
            if (sphere == null) return 0f;
            return sphere.radius * Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y),
                Mathf.Abs(transform.lossyScale.z));
        }

        private float ResolveMinimumNetCenterHeight(float clearanceMargin)
        {
            return TennisCourtGeometry.NetHeight + GetWorldBallRadius() + clearanceMargin;
        }

        private Vector3 ResolveFlightCurve(ShotKind kind, Vector3 target, float spinMultiplier)
        {
            float spin = Mathf.Max(0.75f, spinMultiplier);
            float horizontalDirection = Mathf.Abs(target.x - transform.position.x) > 0.1f
                ? Mathf.Sign(target.x - transform.position.x)
                : (lastHitter == CourtSide.Near ? 1f : -1f);

            return kind switch
            {
                ShotKind.Topspin => Vector3.down * (3.2f * spin),
                ShotKind.Slice => Vector3.right * (horizontalDirection * 2.5f * spin),
                ShotKind.Drop => Vector3.down * (2.4f * spin),
                ShotKind.AttackLob => Vector3.right * (horizontalDirection * 0.8f * spin),
                ShotKind.DefensiveLob => Vector3.right * (horizontalDirection * 0.45f * spin),
                ShotKind.Smash => Vector3.down * (4.2f * spin),
                ShotKind.AngleVolley => Vector3.right * (horizontalDirection * 3.2f * spin),
                _ => Vector3.zero
            };
        }

        private static float ResolveNetClearanceMargin(ShotKind kind, TimingGrade timing)
        {
            float margin = kind switch
            {
                ShotKind.Control => 0.22f,
                ShotKind.Topspin => 0.2f,
                ShotKind.Slice => 0.14f,
                ShotKind.Flat => 0.1f,
                ShotKind.Drop => 0.24f,
                ShotKind.AttackLob => 1.25f,
                ShotKind.DefensiveLob => 1.65f,
                ShotKind.Smash => 0.06f,
                ShotKind.AngleVolley => 0.13f,
                _ => 0.16f
            };

            if (timing == TimingGrade.Just) margin += 0.05f;
            if (timing == TimingGrade.Mishit) margin -= 0.2f;
            return Mathf.Max(-0.08f, margin);
        }

        private float SpeedMultiplier => Mathf.Max(0.01f, ballSpeedMultiplier);
        private float SpeedSquared => SpeedMultiplier * SpeedMultiplier;
        private Vector3 ScaledGravity => Physics.gravity * SpeedSquared;
        private float ServeTossSpeedMultiplier => SpeedMultiplier * Mathf.Max(0.01f, serveTossRelativeSpeedMultiplier);
        private Vector3 ServeTossScaledGravity => Physics.gravity * ServeTossSpeedMultiplier * ServeTossSpeedMultiplier;

        private static Vector3 ResolveServeTarget(TennisAthleteController server, AimZone aim)
        {
            float magnitude = aim switch
            {
                AimZone.DeepLeft or AimZone.DeepRight => 3.95f * TennisCourtGeometry.Scale,
                AimZone.ShallowLeft or AimZone.ShallowRight => 2.75f * TennisCourtGeometry.Scale,
                _ => 1.1f * TennisCourtGeometry.Scale
            };

            float requiredSign = server.transform.position.x <= 0f ? 1f : -1f;
            float z = server.Side == CourtSide.Near ? 4.2f * TennisCourtGeometry.Scale : -4.2f * TennisCourtGeometry.Scale;
            return new Vector3(requiredSign * magnitude, 0.25f, z);
        }

        private static Vector3 ResolveRallyTarget(CourtSide targetSide, ShotKind kind, AimZone aim, TimingGrade timing, float accuracyMultiplier)
        {
            float x = aim switch
            {
                AimZone.DeepLeft => -5.05f * TennisCourtGeometry.Scale,
                AimZone.ShallowLeft => -3.15f * TennisCourtGeometry.Scale,
                AimZone.ShallowRight => 3.15f * TennisCourtGeometry.Scale,
                AimZone.DeepRight => 5.05f * TennisCourtGeometry.Scale,
                _ => 0f
            };

            float depth = ShotLandingProfile.ResolveDepth(kind);

            if (timing == TimingGrade.Mishit)
            {
                x += Random.Range(-1.15f, 1.15f) * accuracyMultiplier;
                depth += Random.Range(-1.1f, 1.1f) * accuracyMultiplier;
            }
            else if (timing == TimingGrade.Normal && Mathf.Abs((int)aim) == 2)
            {
                x += Random.Range(-0.45f, 0.45f) * accuracyMultiplier;
            }

            float z = targetSide == CourtSide.Near ? -depth : depth;
            return new Vector3(x, 0.25f, z);
        }

        private static float ResolveFlightDuration(ShotKind kind, ShotPower power, TimingGrade timing)
        {
            float duration = kind switch
            {
                ShotKind.Drop => 0.82f,
                ShotKind.AttackLob => 1.35f,
                ShotKind.DefensiveLob => 1.55f,
                ShotKind.Slice => 0.9f,
                ShotKind.Smash => 0.58f,
                ShotKind.AngleVolley => 0.7f,
                _ => power == ShotPower.Strong ? 0.72f : 0.9f
            };

            if (timing == TimingGrade.Just) duration *= 0.92f;
            if (timing == TimingGrade.Mishit) duration *= 1.15f;
            return duration;
        }

        private static bool IsInsideCorrectServiceBox(
            Vector3 point,
            CourtSide server,
            TennisAthleteController serverAthlete,
            float ballRadius)
        {
            if (serverAthlete == null)
            {
                return false;
            }

            float expectedSign = serverAthlete.transform.position.x <= 0f ? 1f : -1f;
            return TennisCourtGeometry.IsBallInServiceBox(
                point.x,
                point.z,
                server,
                expectedSign,
                ballRadius);
        }

        private void CreateLobLandingMarker()
        {
            GameObject markerObject = new GameObject("Lob Landing Circle");
            markerObject.transform.SetParent(null);
            lobLandingMarker = markerObject.AddComponent<LineRenderer>();
            lobLandingMarker.loop = true;
            lobLandingMarker.useWorldSpace = true;
            lobLandingMarker.positionCount = 48;
            lobLandingMarker.startWidth = 0.075f;
            lobLandingMarker.endWidth = 0.075f;
            lobLandingMarker.material = new Material(Shader.Find("Sprites/Default"));
            lobLandingMarker.startColor = new Color(0.15f, 0.95f, 1f, 0.85f);
            lobLandingMarker.endColor = new Color(1f, 0.18f, 0.55f, 0.85f);
            lobLandingMarker.enabled = false;
        }

        private void SetLobMarkerVisible(bool visible, Vector3 center)
        {
            if (lobLandingMarker == null) return;
            lobLandingMarker.enabled = visible;
            if (!visible) return;
            center.y = 0.045f;
            const float radius = 0.82f;
            for (int i = 0; i < lobLandingMarker.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / lobLandingMarker.positionCount;
                lobLandingMarker.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }

        private void OnDestroy()
        {
            if (lobLandingMarker != null && lobLandingMarker.material != null)
            {
                Destroy(lobLandingMarker.material);
            }
            if (lobLandingMarker != null)
            {
                Destroy(lobLandingMarker.gameObject);
            }
        }
    }
}
