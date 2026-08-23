using PrideCourt.Domain;
using PrideCourt.Input;
using PrideCourt.Cards;
using UnityEngine;

namespace PrideCourt.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class TennisAthleteController : MonoBehaviour
    {
        private const float StrongShotCost = 12f;
        private const float DashCostPerSecond = 15f;
        private const float DiveCost = 25f;
        private const float DiveStrongReturnExtraCost = 10f;
        private const float DiveDuration = 0.28f;
        private const float ShotBufferSeconds = 0.55f;

        [SerializeField] private CourtSide side;
        [SerializeField] private AthleteIdentity identity;
        [SerializeField] private MonoBehaviour commandSourceBehaviour;
        [SerializeField] private float walkSpeed = 5.5f;
        [SerializeField] private float dashMultiplier = 1.55f;
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float brakingAcceleration = 48f;
        [SerializeField] private float hitRadius = 2.25f;
        [SerializeField] private float diveSpeed = 12.5f;

        private CharacterController characterController;
        private ITennisCommandSource commandSource;
        [SerializeField] private TennisMatchController match;
        [SerializeField] private TennisBallController ball;
        private Vector3 planarVelocity;
        private ShotRequest? pendingShot;
        private TennisCommand currentCommand;
        private Vector3 diveVelocity;
        private float diveTimeRemaining;
        private float diveRecoveryRemaining;
        private bool diveSequenceActive;
        private CardLoadoutController cardLoadout;
        private float personalBuffRemaining;
        private float speedMultiplier = 1f;
        private float dashCostMultiplier = 1f;
        private float strongCostMultiplier = 1f;
        private float nextShotSpinMultiplier = 1f;
        private float nextShotAccuracyMultiplier = 1f;
        private bool specialReserved;
        private Renderer bodyRenderer;
        private bool isDashingMotion;
        private ITennisAthleteMotionView motionView;
        private bool networkReplica;
        private bool hasReplicaTarget;
        private Vector3 replicaTargetPosition;
        private Quaternion replicaTargetRotation;
        private bool serveStrikeInputReleased;

        public CourtSide Side => side;
        public AthleteIdentity Identity => identity;
        public StaminaState Stamina { get; } = new StaminaState();
        public SpecialGaugeState SpecialGauge { get; } = new SpecialGaugeState();
        public float HitRadius => hitRadius;
        public float DiveTravelDistance => diveSpeed * DiveDuration;
        public Vector2 CurrentAimInput => currentCommand.Move;
        public bool IsSpecialReserved => specialReserved;
        public float PlanarSpeed => planarVelocity.magnitude;
        public bool IsDashingMotion => isDashingMotion;
        public bool IsDivingMotion => diveTimeRemaining > 0f;
        public bool IsDiveSequenceActive => diveSequenceActive;
        public bool IsServingMotion => match != null && match.ServeRestrictionsActive && match.Server == side;
        public bool IsServeTossedMotion => IsServingMotion && ball != null && ball.IsTossed;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            commandSource = commandSourceBehaviour as ITennisCommandSource;
            cardLoadout = GetComponent<CardLoadoutController>();
            bodyRenderer = GetComponent<Renderer>();
            ApplyIdentityStats();
        }

        public void Configure(
            CourtSide configuredSide,
            AthleteIdentity configuredIdentity,
            MonoBehaviour configuredCommandSource,
            TennisMatchController configuredMatch,
            TennisBallController configuredBall)
        {
            side = configuredSide;
            identity = configuredIdentity;
            commandSourceBehaviour = configuredCommandSource;
            commandSource = configuredCommandSource as ITennisCommandSource;
            match = configuredMatch;
            ball = configuredBall;
            cardLoadout = GetComponent<CardLoadoutController>();
            ApplyIdentityStats();
        }

        public void BindMatchRuntime(TennisMatchController configuredMatch, TennisBallController configuredBall)
        {
            match = configuredMatch;
            ball = configuredBall;
            commandSource = commandSourceBehaviour as ITennisCommandSource;
            cardLoadout = GetComponent<CardLoadoutController>();
        }

        public void SetCommandSource(MonoBehaviour source)
        {
            commandSourceBehaviour = source;
            commandSource = source as ITennisCommandSource;
        }

        public void SetNetworkReplica(bool value)
        {
            networkReplica = value;
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (characterController != null) characterController.enabled = !value;
            pendingShot = null;
            planarVelocity = Vector3.zero;
            hasReplicaTarget = false;
        }

        public LanAthleteState CaptureLanState()
        {
            return new LanAthleteState
            {
                Position = transform.position,
                RotationY = transform.eulerAngles.y,
                Identity = identity,
                Stamina = Stamina.Current,
                Exhausted = Stamina.IsExhausted,
                SpecialGauge = SpecialGauge.Current,
                PlanarSpeed = PlanarSpeed,
                IsDashing = IsDashingMotion,
                IsDiving = IsDivingMotion,
                IsSpecialReserved = specialReserved
            };
        }

        public void ApplyLanState(LanAthleteState state)
        {
            if (!networkReplica) SetNetworkReplica(true);
            if (identity != state.Identity) SetIdentity(state.Identity);
            replicaTargetPosition = state.Position;
            replicaTargetRotation = Quaternion.Euler(0f, state.RotationY, 0f);
            if (!hasReplicaTarget || Vector3.Distance(transform.position, state.Position) > 4f)
                transform.SetPositionAndRotation(replicaTargetPosition, replicaTargetRotation);
            hasReplicaTarget = true;
            Stamina.Restore(state.Stamina, state.Exhausted);
            SpecialGauge.Restore(state.SpecialGauge);
            planarVelocity = transform.forward * state.PlanarSpeed;
            isDashingMotion = state.IsDashing;
            diveTimeRemaining = state.IsDiving ? 0.1f : 0f;
            specialReserved = state.IsSpecialReserved;
        }

        public void SetIdentity(AthleteIdentity value)
        {
            identity = value;
            ApplyIdentityStats();
        }

        private void ApplyIdentityStats()
        {
            if (identity == AthleteIdentity.Lux)
            {
                walkSpeed = 6.2f;
                dashMultiplier = 1.65f;
                acceleration = 22f;
                brakingAcceleration = 58f;
                hitRadius = 2.05f;
            }
            else
            {
                walkSpeed = 4.75f;
                dashMultiplier = 1.42f;
                acceleration = 13f;
                brakingAcceleration = 46f;
                hitRadius = 2.7f;
            }

            Color identityColor = identity == AthleteIdentity.Lux
                ? new Color(0.08f, 0.85f, 0.95f)
                : new Color(1f, 0.72f, 0.08f);
            Renderer rootRenderer = GetComponent<Renderer>();
            if (rootRenderer != null && rootRenderer.enabled)
            {
                rootRenderer.material.color = identityColor;
            }
            Transform luxVisual = transform.Find("Lux Silhouette");
            Transform bastionVisual = transform.Find("Bastion Silhouette");
            if (luxVisual != null) luxVisual.gameObject.SetActive(identity == AthleteIdentity.Lux);
            if (bastionVisual != null) bastionVisual.gameObject.SetActive(identity == AthleteIdentity.Bastion);
            ResolveMotionView();
            gameObject.name = (identity == AthleteIdentity.Lux ? "Lux" : "Bastion") +
                              (side == CourtSide.Near ? " - Player" : " - CPU");
        }

        private void Update()
        {
            if (networkReplica)
            {
                if (hasReplicaTarget)
                {
                    float blend = 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime);
                    transform.position = Vector3.Lerp(transform.position, replicaTargetPosition, blend);
                    transform.rotation = Quaternion.Slerp(transform.rotation, replicaTargetRotation, blend);
                }
                return;
            }

            if (commandSource == null || match == null || ball == null)
            {
                return;
            }

            if (match.IsGameplayPaused)
            {
                return;
            }

            currentCommand = commandSource.ReadCommand();
            TickTimedEffects();

            if (match.Phase == MatchPhase.CardSelection)
            {
                cardLoadout?.ProcessPreparationCommand(currentCommand);
                HoldPositionDuringSelection();
                return;
            }

            TickMovement(currentCommand);
            TickActions(currentCommand);
            cardLoadout?.ProcessCommand(currentCommand);
            TryResolveBufferedShot();
        }

        private void HoldPositionDuringSelection()
        {
            planarVelocity = Vector3.zero;
            isDashingMotion = false;
            pendingShot = null;
            diveTimeRemaining = 0f;
            diveRecoveryRemaining = 0f;
            diveSequenceActive = false;
            characterController.Move(Vector3.down * 2f * Time.deltaTime);
        }

        private void TickMovement(TennisCommand command)
        {
            bool isServingAthlete = match.ServeRestrictionsActive && match.Server == side;
            if (isServingAthlete && ball.IsTossed)
            {
                isDashingMotion = false;
                planarVelocity = Vector3.zero;
                characterController.Move(Vector3.down * 2f * Time.deltaTime);
                return;
            }

            if (diveRecoveryRemaining > 0f)
            {
                isDashingMotion = false;
                diveRecoveryRemaining -= Time.deltaTime;
                if (diveRecoveryRemaining <= 0f)
                {
                    diveSequenceActive = false;
                }
                characterController.Move(Vector3.down * 2f * Time.deltaTime);
                return;
            }

            if (diveTimeRemaining > 0f)
            {
                isDashingMotion = false;
                diveTimeRemaining -= Time.deltaTime;
                characterController.Move((diveVelocity + Vector3.down * 2f) * Time.deltaTime);
                ClampToCourtHalf();
                if (diveTimeRemaining <= 0f)
                {
                    diveRecoveryRemaining = 0.62f;
                }
                return;
            }

            if (!match.ServeRestrictionsActive && command.DivePressed && command.DiveDirection.sqrMagnitude > 0.1f &&
                Stamina.TrySpendCommitted(DiveCost))
            {
                Vector3 direction = new Vector3(
                    command.DiveDirection.x,
                    0f,
                    side == CourtSide.Near ? command.DiveDirection.y : -command.DiveDirection.y).normalized;
                diveVelocity = direction * diveSpeed;
                diveTimeRemaining = DiveDuration;
                diveSequenceActive = true;
                planarVelocity = Vector3.zero;
                motionView?.PlayDive(command.DiveDirection);
                return;
            }

            Vector2 input = command.Move;
            if (isServingAthlete) input.y = 0f;
            Vector3 desiredDirection = new Vector3(input.x, 0f, side == CourtSide.Near ? input.y : -input.y);
            bool servePhase = match.ServeRestrictionsActive;
            bool wantsDash = !servePhase && command.Dash && input.sqrMagnitude > 0.04f && !Stamina.IsExhausted;
            isDashingMotion = wantsDash;
            float speed = walkSpeed * speedMultiplier * (wantsDash ? dashMultiplier : 1f);
            if (Stamina.IsExhausted)
            {
                speed *= 0.7f;
            }

            if (wantsDash)
            {
                Stamina.TrySpend(DashCostPerSecond * dashCostMultiplier * Time.deltaTime);
            }
            else
            {
                bool stationary = input.sqrMagnitude < 0.01f;
                Stamina.TickRecovery(Time.deltaTime, !stationary, stationary);
            }

            Vector3 desiredVelocity = desiredDirection.normalized * speed * Mathf.Clamp01(input.magnitude);
            ApplyHitPointAssist(ref desiredVelocity);
            float courtAcceleration = match == null ? 1f : match.AccelerationMultiplier;
            bool isBraking = desiredVelocity.sqrMagnitude < planarVelocity.sqrMagnitude ||
                             Vector3.Dot(planarVelocity, desiredVelocity) < 0f;
            float movementResponse = isBraking ? brakingAcceleration : acceleration;
            planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVelocity, movementResponse * courtAcceleration * Time.deltaTime);
            characterController.Move((planarVelocity + Vector3.down * 2f) * Time.deltaTime);
            ClampToCourtHalf();
            if (isServingAthlete)
            {
                Vector3 servingPosition = transform.position;
                servingPosition.x = Mathf.Clamp(servingPosition.x, -TennisCourtGeometry.ServingLateralLimit, TennisCourtGeometry.ServingLateralLimit);
                servingPosition.z = side == CourtSide.Near ? -TennisCourtGeometry.ServingBaseline : TennisCourtGeometry.ServingBaseline;
                transform.position = servingPosition;
            }

            Vector3 lookDirection = side == CourtSide.Near ? Vector3.forward : Vector3.back;
            if (lookDirection.sqrMagnitude > 0f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDirection), 15f * Time.deltaTime);
            }
        }

        private void TickActions(TennisCommand command)
        {
            if (match.ServeRestrictionsActive && match.Server == side)
            {
                bool shotButtonPressed = command.StrongPressed || command.SafePressed;
                bool shotButtonHeld = command.ShotInputHeld || shotButtonPressed;
                if (!ball.IsTossed && (command.TossPressed || shotButtonPressed))
                {
                    ball.BeginServeToss(this);
                    if (ball.IsTossed)
                    {
                        // A serve always needs a separate second press. Proximity to the ball
                        // and the press that started the toss must never launch it.
                        serveStrikeInputReleased = false;
                        motionView?.PlayServeToss();
                    }
                    return;
                }

                if (ball.IsTossed && !shotButtonHeld)
                {
                    serveStrikeInputReleased = true;
                }

                if (ball.IsTossed && serveStrikeInputReleased && shotButtonPressed)
                {
                    ShotPower servePower = command.StrongPressed ? ShotPower.Strong : ShotPower.Safe;
                    ball.StrikeServe(this, servePower, ResolveAimZone(command.Move));
                    serveStrikeInputReleased = false;
                    motionView?.PlayStroke(servePower, command.Move.x, ResolveStrokeMotion(true));
                }

                return;
            }

            if (command.SpecialPressed && match.Phase == MatchPhase.Rally && SpecialGauge.IsReady)
            {
                specialReserved = !specialReserved;
            }

            if (match.Phase != MatchPhase.Rally && !match.IsServeAwaitingReturn)
            {
                pendingShot = null;
                return;
            }

            if (command.StrongPressed || command.SafePressed)
            {
                ShotPower power = command.StrongPressed ? ShotPower.Strong : ShotPower.Safe;
                pendingShot = new ShotRequest(power, ResolveShotKind(power, command.Move), Time.time, command.Move);
            }
        }

        private StrokeMotion ResolveStrokeMotion(bool isServe)
        {
            float ballX = ball == null ? transform.position.x : ball.transform.position.x;
            return StrokeMotionPolicy.Resolve(side, transform.position.x, ballX, isServe);
        }

        private void ResolveMotionView()
        {
            motionView = null;
            MonoBehaviour[] views = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] is ITennisAthleteMotionView candidate && views[i].gameObject.activeInHierarchy)
                {
                    motionView = candidate;
                    return;
                }
            }
        }

        private void TryResolveBufferedShot()
        {
            if (!pendingShot.HasValue ||
                (match.Phase != MatchPhase.Rally && !match.IsServeAwaitingReturn))
            {
                return;
            }

            ShotRequest request = pendingShot.Value;
            if (Time.time - request.RequestedAt > ShotBufferSeconds)
            {
                pendingShot = null;
                return;
            }

            float distance = Vector3.Distance(transform.position, ball.transform.position);
            if (distance > hitRadius)
            {
                return;
            }

            if (ball.TryRegisterDoubleHit(side))
            {
                pendingShot = null;
                return;
            }

            if (!ball.CanBeHitBy(side)) return;

            ShotPower resolvedPower = request.Power;
            float strongCost = (diveSequenceActive ? DiveStrongReturnExtraCost : StrongShotCost) * strongCostMultiplier;
            if (resolvedPower == ShotPower.Strong && !Stamina.TrySpend(strongCost))
            {
                resolvedPower = ShotPower.Safe;
            }

            TimingGrade timing = distance <= hitRadius * 0.35f
                ? TimingGrade.Just
                : distance <= hitRadius * 0.75f ? TimingGrade.Normal : TimingGrade.Mishit;
            match.NotifyShotTiming(side, timing);

            AimZone aim = ResolveAimZone(currentCommand.Move);
            float spin = nextShotSpinMultiplier;
            float accuracy = nextShotAccuracyMultiplier;
            nextShotSpinMultiplier = 1f;
            nextShotAccuracyMultiplier = 1f;

            if (specialReserved && SpecialGauge.IsReady)
            {
                ShotKind capturedKind = request.Kind;
                match.PlaySpecialCutIn(this, () =>
                {
                    if (!SpecialGauge.TryConsumeAll()) return;
                    ExecuteRallyStroke(request, capturedKind, resolvedPower, aim, timing, spin, accuracy, true);
                });
                specialReserved = false;
            }
            else
            {
                ExecuteRallyStroke(request, request.Kind, resolvedPower, aim, timing, spin, accuracy, false);
            }

            if (diveSequenceActive)
            {
                diveTimeRemaining = 0f;
                diveRecoveryRemaining = 0.22f;
            }
            SpecialGauge.Add(timing == TimingGrade.Just ? 5f : 3f);
            pendingShot = null;
        }

        private void ExecuteRallyStroke(
            ShotRequest request,
            ShotKind kind,
            ShotPower power,
            AimZone aim,
            TimingGrade timing,
            float spin,
            float accuracy,
            bool isSpecial)
        {
            StrokeMotion motion = ResolveStrokeMotion(false);
            motionView?.PlayStroke(power, request.InitialDirection.x, motion);
            ball.StrikeRally(this, kind, power, aim, timing, spin, accuracy, isSpecial);
        }

        private void ClampToCourtHalf()
        {
            Vector3 position = transform.position;
            position.x = Mathf.Clamp(position.x, -TennisCourtGeometry.MovementHalfWidth, TennisCourtGeometry.MovementHalfWidth);
            if (side == CourtSide.Near)
            {
                position.z = Mathf.Clamp(position.z, -TennisCourtGeometry.MovementBaseline, -0.8f);
            }
            else
            {
                position.z = Mathf.Clamp(position.z, 0.8f, TennisCourtGeometry.MovementBaseline);
            }

            transform.position = position;
        }

        private void ApplyHitPointAssist(ref Vector3 desiredVelocity)
        {
            if (!pendingShot.HasValue || ball == null || !ball.CanBeHitBy(side)) return;
            Vector3 offset = ball.transform.position - transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance <= hitRadius || distance > hitRadius + 1.15f) return;

            float assistSpeed = Mathf.Min(3.2f, (distance - hitRadius) * 5f);
            desiredVelocity += offset.normalized * assistSpeed;
            desiredVelocity = Vector3.ClampMagnitude(desiredVelocity, walkSpeed * speedMultiplier);
        }

        private ShotKind ResolveShotKind(ShotPower power, Vector2 direction)
        {
            if (power == ShotPower.Strong && ball != null && ball.transform.position.y >= 2.45f)
            {
                return ShotKind.Smash;
            }

            if (power == ShotPower.Safe && Mathf.Abs(transform.position.z) <= 4.5f && Mathf.Abs(direction.x) > 0.35f)
            {
                return ShotKind.AngleVolley;
            }

            if (direction.y > 0.45f)
            {
                return power == ShotPower.Strong ? ShotKind.Flat : ShotKind.Drop;
            }

            if (direction.y < -0.45f)
            {
                return power == ShotPower.Strong ? ShotKind.AttackLob : ShotKind.DefensiveLob;
            }

            if (Mathf.Abs(direction.x) > 0.35f)
            {
                return power == ShotPower.Strong ? ShotKind.Topspin : ShotKind.Slice;
            }

            return power == ShotPower.Strong ? ShotKind.Topspin : ShotKind.Control;
        }

        private static AimZone ResolveAimZone(Vector2 direction)
        {
            float horizontal = direction.x;
            if (horizontal <= -0.8f) return AimZone.DeepLeft;
            if (horizontal <= -0.2f) return AimZone.ShallowLeft;
            if (horizontal >= 0.8f) return AimZone.DeepRight;
            if (horizontal >= 0.2f) return AimZone.ShallowRight;
            return AimZone.Center;
        }

        public void ResetForPoint(Vector3 position)
        {
            characterController.enabled = false;
            transform.position = position;
            characterController.enabled = true;
            planarVelocity = Vector3.zero;
            isDashingMotion = false;
            pendingShot = null;
            Stamina.ResetForPoint();
            diveTimeRemaining = 0f;
            diveRecoveryRemaining = 0f;
            diveSequenceActive = false;
            specialReserved = false;
            serveStrikeInputReleased = false;
            ClearExpiredPointEffects();
        }

        public void ApplyPersonalBuff(float speed, float dashCost, float strongCost, float duration)
        {
            speedMultiplier = speed;
            dashCostMultiplier = dashCost;
            strongCostMultiplier = strongCost;
            personalBuffRemaining = duration;
        }

        public void QueueNextShotModifier(float spin, float accuracy)
        {
            nextShotSpinMultiplier = spin;
            nextShotAccuracyMultiplier = accuracy;
        }

        public void ClearExpiredPointEffects()
        {
            personalBuffRemaining = 0f;
            speedMultiplier = 1f;
            dashCostMultiplier = 1f;
            strongCostMultiplier = 1f;
            nextShotSpinMultiplier = 1f;
            nextShotAccuracyMultiplier = 1f;
        }

        private void TickTimedEffects()
        {
            if (personalBuffRemaining <= 0f || match == null || match.IsGameplayPaused) return;
            personalBuffRemaining -= Time.deltaTime;
            if (personalBuffRemaining <= 0f)
            {
                speedMultiplier = 1f;
                dashCostMultiplier = 1f;
                strongCostMultiplier = 1f;
            }
        }
    }
}
