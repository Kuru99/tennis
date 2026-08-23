using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using PrideCourt.Domain;
using PrideCourt.Gameplay;
using PrideCourt.Cards;
using PrideCourt.AI;
using PrideCourt.Presentation;
using PrideCourt.Input;
using PrideCourt.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PrideCourt.Editor
{
    public static class TennisPrototypeValidation
    {
        [MenuItem("Pride Court/Validate MVP Prototype")]
        public static void ValidateAll()
        {
            ValidateScoring();
            ValidateStamina();
            ValidateCards();
            ValidateDifficulty();
            ValidateDashInput();
            ValidateDiveInput();
            ValidateStrokeMotion();
            ValidateDirectCardAndStickInput();
            ValidateBounceFeedback();
            ValidateLineTouchAndTrajectory();
            ValidateUiTheme();
            ValidateFrontEndFlow();
            ValidateSettingsPolicy();
            ValidateLanProtocol();
            ValidateLanLoopbackTransport();
            ValidateScene();
            ValidateNetDeflection();
            Debug.Log("PRIDE_COURT_STATIC_VALIDATION_PASSED");
        }

        private static void ValidateUiTheme()
        {
            Require(PrideCourtUiTheme.FontsAvailable,
                "The Pride Court UI display/body fonts are missing from Resources/Fonts.");
            Require(PrideCourtUiTheme.DisplayFont != PrideCourtUiTheme.BodyFont,
                "The UI display and body roles must use distinct typefaces.");
        }

        private static void ValidateFrontEndFlow()
        {
            PrideCourtFrontEndFlow flow = new PrideCourtFrontEndFlow();
            Require(flow.Screen == FrontEndScreen.Opening, "The front end must begin with the opening movie.");
            Require(flow.Apply(FrontEndAction.FinishOpening) && flow.Screen == FrontEndScreen.Title,
                "The opening movie must lead to the title screen.");
            Require(flow.Apply(FrontEndAction.OpenModeSelect) && flow.Screen == FrontEndScreen.ModeSelect,
                "The title screen must lead to game-mode selection.");
            Require(flow.Apply(FrontEndAction.SelectMultiplayer) && flow.Screen == FrontEndScreen.MultiplayerSelect,
                "Multiplayer must lead to its local/network submenu.");
            Require(flow.Apply(FrontEndAction.SelectLocal) && flow.Screen == FrontEndScreen.LocalNetworkLobby &&
                    flow.PendingMultiplayerEntry == MultiplayerEntry.Local,
                "Local multiplayer must lead to the LAN host/join lobby.");
            Require(flow.Apply(FrontEndAction.Back) && flow.Screen == FrontEndScreen.MultiplayerSelect,
                "The LAN lobby must return to multiplayer selection.");
            Require(!PrideCourtFrontEndFlow.NetworkBattleAvailable &&
                    !flow.Apply(FrontEndAction.SelectNetwork) &&
                    flow.Screen == FrontEndScreen.MultiplayerSelect &&
                    flow.PendingMultiplayerEntry == MultiplayerEntry.None,
                "Network multiplayer must remain unavailable while marked as not implemented.");
            Require(flow.Apply(FrontEndAction.Back) && flow.Screen == FrontEndScreen.ModeSelect,
                "The multiplayer submenu must return to game-mode selection.");
            Require(flow.Apply(FrontEndAction.SelectSolo) && flow.Screen == FrontEndScreen.Setup,
                "Solo play must lead to the existing character and deck setup.");
            Require(flow.Apply(FrontEndAction.ReturnToTitle) && flow.Screen == FrontEndScreen.Title,
                "The pause settings must be able to return directly to the title screen.");
        }

        private static void ValidateSettingsPolicy()
        {
            Require(SettingsAvailabilityPolicy.CanOpen(false, true, MatchPhase.Rally, false),
                "Solo play settings must remain available during a rally.");
            Require(SettingsAvailabilityPolicy.CanOpen(true, true, MatchPhase.Serving, true),
                "Multiplayer settings must be available while waiting for the serve.");
            Require(SettingsAvailabilityPolicy.CanOpen(true, true, MatchPhase.CardSelection, false),
                "Multiplayer settings must be available during card selection.");
            Require(!SettingsAvailabilityPolicy.CanOpen(true, true, MatchPhase.Serving, false),
                "Multiplayer settings must be locked after the serve has entered play.");
            Require(!SettingsAvailabilityPolicy.CanOpen(true, true, MatchPhase.Rally, false),
                "Multiplayer settings must be locked during a rally.");
            Require(!SettingsAvailabilityPolicy.CanOpen(true, true, MatchPhase.PointResult, false),
                "Multiplayer settings must stay locked during the point-result transition.");
        }

        private static void ValidateLanProtocol()
        {
            TennisCommand source = new TennisCommand(new Vector2(0.4f, -0.8f), true, true, false, false, true,
                true, Vector2.left, true, true, false, new Vector2(0.75f, 0.25f), true, true, 2, -1);
            TennisCommand restored = LanBattleProtocol.DecodeCommand(LanBattleProtocol.EncodeCommand(source));
            Require(Vector2.Distance(source.Move, restored.Move) < 0.001f && restored.Dash && restored.StrongPressed &&
                    restored.SpecialPressed && restored.DivePressed && restored.DirectCardSlot == 2 &&
                    restored.PreparationChoice == -1,
                "LAN command serialization must preserve PC/mobile gameplay input.");

            CardId[] deck = CardCatalog.BuildPreset(AthleteIdentity.Lux).ToArray();
            Require(LanBattleProtocol.TryDecodeHello(LanBattleProtocol.EncodeHello(AthleteIdentity.Lux, deck),
                        out AthleteIdentity identity, out CardId[] restoredDeck) && identity == AthleteIdentity.Lux &&
                    restoredDeck.Length == 16,
                "LAN handshake must preserve character and 16-card deck selection.");
            Require(LanBattleProtocol.DecodeSettingsPause(LanBattleProtocol.EncodeSettingsPause(true)),
                "LAN settings-pause requests must preserve the requested state.");
            Require(!LanBattleProtocol.DecodeSettingsPause(LanBattleProtocol.EncodeSettingsPause(false)),
                "LAN settings-resume requests must preserve the requested state.");
            byte[] framed = LanBattleProtocol.Frame(LanPacketType.Hello, new byte[] { 3, 7, 11 });
            Require(LanBattleProtocol.TryParseFrame(framed, out LanPacket packet) &&
                    packet.Type == LanPacketType.Hello && packet.Payload.SequenceEqual(new byte[] { 3, 7, 11 }),
                "The shared LAN/EOS transport frame must round-trip without changing the battle protocol.");
            Require(EosRoomCode.Normalize("ab-01 z9") == "ABOIZ9" && EosRoomCode.IsValid("ABOIZ9") &&
                    !EosRoomCode.IsValid("SHORT"),
                "EOS room codes must normalize ambiguous mobile input into a six-character code.");
            byte[] eosSnapshot = LanBattleProtocol.Frame(LanPacketType.Snapshot,
                LanBattleProtocol.EncodeSnapshot(new LanMatchState { StatusMessage = new string('x', 128) }));
            Require(eosSnapshot.Length <= EosP2PTransport.MaximumPacketSize,
                "The authoritative snapshot must fit in one EOS P2P packet.");
        }

        private static void ValidateLanLoopbackTransport()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            LanPeerConnection host = null;
            LanPeerConnection guest = null;
            try
            {
                listener.Start(1);
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                var accept = listener.AcceptTcpClientAsync();
                TcpClient guestSocket = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
                guestSocket.Connect(IPAddress.Loopback, port);
                host = new LanPeerConnection(accept.GetAwaiter().GetResult());
                guest = new LanPeerConnection(guestSocket);
                TennisCommand sent = new TennisCommand(Vector2.right, true, false, true, false, false);
                guest.Send(LanPacketType.Command, LanBattleProtocol.EncodeCommand(sent));
                DateTime deadline = DateTime.UtcNow.AddSeconds(2);
                LanPacket received = default;
                bool arrived = false;
                while (DateTime.UtcNow < deadline && !(arrived = host.TryReceive(out received))) Thread.Sleep(5);
                Require(arrived && received.Type == LanPacketType.Command,
                    "LAN framed transport must deliver a command over a real loopback socket.");
                TennisCommand restored = LanBattleProtocol.DecodeCommand(received.Payload);
                Require(restored.SafePressed && restored.Dash && Vector2.Distance(restored.Move, Vector2.right) < 0.001f,
                    "LAN loopback transport must preserve the command payload.");
            }
            finally
            {
                guest?.Dispose();
                host?.Dispose();
                listener.Stop();
            }
        }

        public static void ValidateFromCommandLine()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MVP_Prototype.unity");
            ValidateAll();
            EditorApplication.Exit(0);
        }

        private static void ValidateScoring()
        {
            MatchScore score = new MatchScore();
            for (int i = 0; i < 5; i++) score.AwardPoint(CourtSide.Near);
            for (int i = 0; i < 5; i++) score.AwardPoint(CourtSide.Far);
            Require(!score.IsMatchOver, "5-5 must not end the match.");
            score.AwardPoint(CourtSide.Near);
            Require(!score.IsMatchOver, "6-5 must not end the match.");
            score.AwardPoint(CourtSide.Near);
            Require(score.TryGetWinner(out CourtSide winner) && winner == CourtSide.Near, "7-5 must be a Near win.");

            MatchScore serverScore = new MatchScore();
            Require(serverScore.Server == CourtSide.Near, "Near must serve first.");
            serverScore.AwardPoint(CourtSide.Far);
            Require(serverScore.Server == CourtSide.Far, "Serve must alternate every point.");
        }

        private static void ValidateStamina()
        {
            StaminaState stamina = new StaminaState();
            Require(stamina.TrySpend(100f), "Full stamina spend must succeed.");
            Require(stamina.IsExhausted, "Zero stamina must enter exhaustion.");
            stamina.TickRecovery(StaminaState.RecoveryDelayAfterSpend, false, true);
            stamina.Restore(79f);
            Require(stamina.IsExhausted, "Exhaustion must remain below 80 percent.");
            stamina.Restore(1f);
            Require(!stamina.IsExhausted, "Exhaustion must clear at 80 percent.");
            stamina.ResetForPoint();
            Require(Math.Abs(stamina.Current - StaminaState.Maximum) < 0.001f, "Point reset must fully restore stamina.");

            Require(stamina.TrySpend(85f), "Stamina setup spend must succeed.");
            Require(stamina.TrySpendCommitted(25f), "A committed dive must still start with partial stamina.");
            Require(stamina.IsExhausted && Math.Abs(stamina.Current) < 0.001f,
                "A partial-stamina dive must consume the remainder and enter exhaustion.");
            Require(!stamina.TrySpendCommitted(25f), "A committed dive must not start from zero stamina.");
        }

        private static void ValidateLineTouchAndTrajectory()
        {
            const float ballRadius = 0.16f;
            float lineTouchLimit = TennisCourtGeometry.HalfWidth + TennisCourtGeometry.LineWidth * 0.5f + ballRadius;
            Require(TennisCourtGeometry.IsBallInSingles(lineTouchLimit - 0.001f, 0f, ballRadius),
                "A ball grazing the singles sideline must be in.");
            Require(!TennisCourtGeometry.IsBallInSingles(lineTouchLimit + 0.001f, 0f, ballRadius),
                "A ball fully outside the singles sideline must be out.");
            Require(TennisCourtGeometry.IsBallInServiceBox(-ballRadius * 0.8f, 3f, CourtSide.Near, 1f, ballRadius),
                "A serve grazing the center service line must be in.");

            Vector3 start = new Vector3(-1.2f, 0.52f, -8f);
            Vector3 target = new Vector3(3.8f, 0.25f, 8.2f);
            const float minimumNetHeight = 1.28f;
            BallTrajectoryPlan safePlan = BallTrajectoryPlanner.Create(
                start, target, 0.72f, 0.6f, Vector3.zero, minimumNetHeight);
            Require(safePlan.NetCrossingHeight >= minimumNetHeight - 0.001f,
                "A normal planned shot must clear its minimum net height.");
            Require(Vector3.Distance(safePlan.EvaluatePosition(start, safePlan.Duration), target) <= 0.001f,
                "A planned shot must still land on its requested target.");

            BallTrajectoryPlan slicePlan = BallTrajectoryPlanner.Create(
                start, target, 0.9f, 0.6f, Vector3.right * 2.5f, minimumNetHeight);
            Vector3 sliceMidpoint = slicePlan.EvaluatePosition(start, slicePlan.Duration * 0.5f);
            float linearMidpointX = Mathf.Lerp(start.x, target.x, 0.5f);
            Require(Mathf.Abs(sliceMidpoint.x - linearMidpointX) >= 0.05f,
                "A slice trajectory must visibly curve instead of remaining linear.");

            Require(ShotLandingProfile.ResolveBaselineInset(ShotKind.Control) >= 2.5f &&
                    ShotLandingProfile.ResolveBaselineInset(ShotKind.Control) <= 3f,
                "A control shot must land deep enough without crowding the baseline.");
            Require(ShotLandingProfile.ResolveDepth(ShotKind.Topspin) > ShotLandingProfile.ResolveDepth(ShotKind.Control),
                "Topspin must land deeper than a control shot.");
            Require(ShotLandingProfile.ResolveDepth(ShotKind.Flat) > ShotLandingProfile.ResolveDepth(ShotKind.Topspin),
                "A flat drive must land deeper than topspin.");
            Require(ShotLandingProfile.ResolveDepth(ShotKind.Smash) >= ShotLandingProfile.ResolveDepth(ShotKind.Flat),
                "A smash must retain the deepest attacking target.");
            Require(ShotLandingProfile.ResolveBaselineInset(ShotKind.AttackLob) >= 1f &&
                    ShotLandingProfile.ResolveBaselineInset(ShotKind.AttackLob) <= 1.3f,
                "An attack lob must pressure the baseline without targeting outside it.");
            Require(Mathf.Approximately(ShotLandingProfile.ResolveDepth(ShotKind.Drop), 2.35f * TennisCourtGeometry.Scale),
                "Drop-shot depth must stay short.");
            Require(Mathf.Approximately(ShotLandingProfile.ResolveDepth(ShotKind.AngleVolley), 3.8f * TennisCourtGeometry.Scale),
                "Angle-volley depth must stay short.");
        }

        private static void ValidateScene()
        {
            Require(Resources.Load<Texture2D>(MvpFrontEndController.OpeningArtworkResourcePath) != null,
                "Opening/title artwork is missing.");
            Require(Resources.Load<Texture2D>("Stadium/CrowdMosaic") != null,
                "Stadium crowd mosaic is missing.");
            Require(Resources.Load<Texture2D>("Stadium/PunkBanner") != null,
                "Stadium punk banner is missing.");
            StadiumEnvironment stadium = UnityEngine.Object.FindAnyObjectByType<StadiumEnvironment>();
            Require(stadium != null, "Pop-punk fantasy stadium environment is missing.");
            Require(stadium.GetComponentsInChildren<Collider>(true).Length == 0,
                "Stadium presentation objects must not affect gameplay collisions.");
            Require(UnityEngine.Object.FindAnyObjectByType<TennisMatchController>() != null, "Match controller is missing.");
            Require(UnityEngine.Object.FindAnyObjectByType<TennisBallController>() != null, "Ball controller is missing.");
            Require(UnityEngine.Object.FindAnyObjectByType<TennisNetSurface>() != null, "Gameplay tennis net surface is missing.");
            Require(UnityEngine.Object.FindAnyObjectByType<TennisNetVisual>() != null, "Renderable tennis net is missing.");
            Require(UnityEngine.Object.FindObjectsByType<TennisAthleteController>(FindObjectsInactive.Exclude).Length == 2, "Exactly two athletes are required.");
            Require(Camera.main != null, "Main camera is missing.");
            Require(UnityEngine.Object.FindObjectsByType<CardLoadoutController>(FindObjectsInactive.Exclude).Length == 2, "Exactly two card loadouts are required.");
            Require(UnityEngine.Object.FindAnyObjectByType<MvpFrontEndController>() != null, "MVP front end is missing.");
            PrideCourtAudio audio = UnityEngine.Object.FindAnyObjectByType<PrideCourtAudio>();
            Require(audio != null, "Procedural audio controller is missing.");
            AudioSource audioSource = audio.GetComponent<AudioSource>();
            Require(audioSource != null && !audioSource.playOnAwake && !audioSource.mute &&
                    Mathf.Approximately(audioSource.volume, 1f) && Mathf.Approximately(audioSource.spatialBlend, 0f),
                "Ball effect audio source must be an unmuted 2D source with play-on-awake disabled.");
            Require(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude).Length == 1,
                "The MVP scene must have exactly one active audio listener.");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(TennisGeneratedModelIntegrator.LuxRiggedPrefabPath) != null,
                "Lux rigged prefab is missing.");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(TennisGeneratedModelIntegrator.BastionRiggedPrefabPath) != null,
                "Bastion rigged prefab is missing.");
            foreach (TennisAthleteController athlete in UnityEngine.Object.FindObjectsByType<TennisAthleteController>(FindObjectsInactive.Exclude))
            {
                Require(athlete.transform.Find("Lux Silhouette") != null, "Athlete is missing the Lux generated visual.");
                Require(athlete.transform.Find("Bastion Silhouette") != null, "Athlete is missing the Bastion generated visual.");
                GeneratedCharacterRig[] rigs = athlete.GetComponentsInChildren<GeneratedCharacterRig>(true);
                Require(rigs.Length == 2, "Athlete must retain both generated character rigs for identity switching.");
                foreach (GeneratedCharacterRig rig in rigs)
                {
                    Require(rig.IsRigBuilt, "Generated character rig was not built.");
                    Require(rig.HasCompleteMotionBinding,
                        "Generated character meshes are not bound to the animated rig bones.");
                    Require(rig.IsRacketHeld, "Generated character racket is not attached at the wrist grip.");
                }
                Renderer rootRenderer = athlete.GetComponent<Renderer>();
                Require(rootRenderer == null || !rootRenderer.enabled, "Legacy capsule renderer must be disabled.");
            }
            TennisCourtSurface court = UnityEngine.Object.FindAnyObjectByType<TennisCourtSurface>();
            Require(court != null, "Court surface is missing.");
            Require(Mathf.Approximately(court.transform.localScale.x, TennisCourtGeometry.VisualWidth) &&
                    Mathf.Approximately(court.transform.localScale.z, TennisCourtGeometry.VisualLength),
                "Court visual and gameplay geometry must use the shared enlarged dimensions.");
        }

        private static void ValidateNetDeflection()
        {
            TennisNetSurface netSurface = UnityEngine.Object.FindAnyObjectByType<TennisNetSurface>();
            Require(netSurface != null, "Gameplay tennis net surface is missing.");
            BoxCollider netCollider = netSurface.GetComponent<BoxCollider>();
            Require(netCollider != null, "Gameplay tennis net collider is missing.");

            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            probe.name = "Net Deflection Validation Probe";
            probe.hideFlags = HideFlags.HideAndDontSave;
            Rigidbody probeBody = probe.AddComponent<Rigidbody>();
            probeBody.useGravity = false;
            const float probeRadius = 0.16f;

            try
            {
                Bounds netBounds = netCollider.bounds;
                probeBody.position = new Vector3(0f, netBounds.max.y - 0.08f, netBounds.min.z - probeRadius);
                probeBody.linearVelocity = new Vector3(1f, -0.5f, 5f);
                bool tippedOver = netSurface.DeflectBall(probeBody, CourtSide.Near, probeRadius, 0.6f);
                Require(tippedOver && probeBody.position.z > netBounds.max.z && probeBody.linearVelocity.z > 0f,
                    "A top-cord hit must lose speed but continue into the opponent court.");

                probeBody.position = new Vector3(0f, netBounds.center.y, netBounds.min.z - probeRadius);
                probeBody.linearVelocity = new Vector3(1f, -0.5f, 5f);
                bool blocked = netSurface.DeflectBall(probeBody, CourtSide.Near, probeRadius, 0.6f);
                Require(!blocked && probeBody.linearVelocity.z < 0f && probeBody.linearVelocity.y < 0f,
                    "A low net hit must be blocked and drop back into the hitter court.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        private static void ValidateDashInput()
        {
            Require(!DashInputPolicy.ShouldDash(true, false, 1f, 0f),
                "Keyboard movement without Shift must not be mistaken for analog dash.");
            Require(DashInputPolicy.ShouldDash(true, true, 1f, 0f),
                "Keyboard movement with Shift must dash.");
            Require(DashInputPolicy.ShouldDash(false, false, 0.9f, 0f),
                "A deliberate full gamepad tilt must still dash.");
        }

        private static void ValidateDiveInput()
        {
            Require(DiveInputPolicy.TryGetKeyboardDive(true, Vector2.right, out Vector2 keyboardDirection) &&
                    Vector2.Dot(keyboardDirection, Vector2.right) > 0.99f,
                "Space plus WASD must trigger a keyboard dive in the held direction.");
            Require(!DiveInputPolicy.TryGetKeyboardDive(false, Vector2.right, out _),
                "WASD without Space must not trigger a keyboard dive.");

            DirectionalFlickDetector flick = new DirectionalFlickDetector();
            Require(!flick.Update(Vector2.right, 0f, out _), "The first stick direction must prime, not trigger, a dive.");
            Require(!flick.Update(Vector2.zero, 0.1f, out _), "Returning the stick to neutral must not trigger a dive.");
            Require(flick.Update(Vector2.right, 0.2f, out Vector2 flickDirection) &&
                    Vector2.Dot(flickDirection, Vector2.right) > 0.99f,
                "A same-direction stick flick inside the window must trigger a dive.");
            Require(!flick.Update(Vector2.zero, 0.3f, out _), "A second neutral input must remain inert.");
            Require(!flick.Update(Vector2.left, 0.4f, out _),
                "A reverse-direction flick must not trigger the same-direction dive gesture.");

            Require(CpuDivePolicy.ShouldDive(CpuDifficulty.Normal, true, 3f, 2f, 5f, 25f, 0f),
                "CPU must be allowed to dive for a reachable ball outside standing reach.");
            Require(!CpuDivePolicy.ShouldDive(CpuDifficulty.Hard, true, 3f, 2f, 5f, 0f, 0f),
                "CPU must not dive from zero stamina.");
        }

        private static void ValidateStrokeMotion()
        {
            Require(StrokeMotionPolicy.Resolve(CourtSide.Near, 0f, -1f, false) == StrokeMotion.Backhand,
                "A near-side right-handed athlete must use a backhand for a ball on the body's left side.");
            Require(StrokeMotionPolicy.Resolve(CourtSide.Near, 0f, 1f, false) == StrokeMotion.Forehand,
                "A near-side right-handed athlete must use a forehand for a ball on the body's right side.");
            Require(StrokeMotionPolicy.Resolve(CourtSide.Far, 0f, 1f, false) == StrokeMotion.Backhand,
                "A far-side athlete's backhand side must mirror with court facing.");
            Require(StrokeMotionPolicy.Resolve(CourtSide.Far, 0f, -1f, false) == StrokeMotion.Forehand,
                "A far-side athlete's forehand side must mirror with court facing.");
            Require(StrokeMotionPolicy.Resolve(CourtSide.Near, 0f, -1f, true) == StrokeMotion.Serve,
                "Serve motion must not be replaced by a backhand based on ball position.");
        }

        private static void ValidateDirectCardAndStickInput()
        {
            Require(DirectCardInputPolicy.TryGetKeyboardSlot(true, false, false, out int firstSlot) && firstSlot == 0,
                "Keyboard 1 must map to the first hand card.");
            Require(DirectCardInputPolicy.TryGetKeyboardSlot(false, true, false, out int secondSlot) && secondSlot == 1,
                "Keyboard 2 must map to the second hand card.");
            Require(DirectCardInputPolicy.TryGetKeyboardSlot(false, false, true, out int thirdSlot) && thirdSlot == 2,
                "Keyboard 3 must map to the third hand card.");
            Require(VirtualStickPreferences.ScaleFor(VirtualStickSize.Small) < VirtualStickPreferences.ScaleFor(VirtualStickSize.Medium) &&
                    VirtualStickPreferences.ScaleFor(VirtualStickSize.Medium) < VirtualStickPreferences.ScaleFor(VirtualStickSize.Large),
                "Virtual stick Small, Medium, and Large sizes must remain strictly ordered.");

            Rect strong = new Rect(800f, 700f, 120f, 120f);
            Rect safe = new Rect(650f, 760f, 100f, 100f);
            Rect special = new Rect(780f, 850f, 100f, 60f);
            Rect card = new Rect(620f, 850f, 100f, 60f);
            Require(MobileTouchTargetPolicy.Resolve(new Vector2(860f, 240f), 1000f, 460f,
                        strong, safe, special, card) == MobileTouchTarget.Strong,
                "Only the Strong button rectangle must resolve to Strong touch input.");
            Require(MobileTouchTargetPolicy.Resolve(new Vector2(700f, 190f), 1000f, 460f,
                        strong, safe, special, card) == MobileTouchTarget.Safe,
                "Only the Safe button rectangle must resolve to Safe touch input.");
            MobileTouchTarget emptyArea = MobileTouchTargetPolicy.Resolve(new Vector2(560f, 500f), 1000f, 460f,
                strong, safe, special, card);
            Require(emptyArea != MobileTouchTarget.Strong && emptyArea != MobileTouchTarget.Safe,
                "A tap outside the shot buttons must never resolve to a racket swing.");
        }

        private static void ValidateBounceFeedback()
        {
            BounceEffectProfile normal = BallBounceEffect.GetProfile(3f, false, 1f, true);
            BounceEffectProfile hard = BallBounceEffect.GetProfile(7f, false, 1f, true);
            BounceEffectProfile special = BallBounceEffect.GetProfile(3f, true, 1f, true);
            BounceEffectProfile highBounce = BallBounceEffect.GetProfile(3f, false, 1.2f, true);
            BounceEffectProfile outside = BallBounceEffect.GetProfile(3f, false, 1f, false);
            Require(normal.Strength == BounceEffectStrength.Normal && normal.ParticleCount >= 10,
                "A normal ball bounce must have visible lightweight feedback.");
            Require(hard.Strength == BounceEffectStrength.Hard && hard.Radius > normal.Radius,
                "A hard ball bounce must read stronger than a normal bounce.");
            Require(special.Strength == BounceEffectStrength.Special && special.ParticleCount > hard.ParticleCount,
                "A special-ball bounce must have its dedicated feedback profile.");
            Require(highBounce.Strength == BounceEffectStrength.Hard,
                "The High Bounce court effect must also strengthen bounce feedback.");
            Require(Vector4.Distance(outside.Accent, PrideCourtUiTheme.Magenta) < 0.001f,
                "An outside-court bounce must use the out accent color.");
        }

        private static void ValidateCards()
        {
            foreach (CardId card in CardCatalog.All)
            {
                Require(Resources.Load<Texture2D>("CardArt/" + card) != null,
                    "Card art is missing for " + card + ".");
                CardEffectProfile effectProfile = CardActivationEffect.GetProfile(card);
                Require(effectProfile.Duration >= 0.8f && effectProfile.ParticleCount >= 20 && effectProfile.Radius > 0f,
                    "Card activation effect profile is incomplete for " + card + ".");
                CardEffectStyle expectedStyle = CardCatalog.Get(card).Category switch
                {
                    CardCategory.Instant => CardEffectStyle.Pulse,
                    CardCategory.NextShot => CardEffectStyle.Spiral,
                    CardCategory.Court => CardEffectStyle.CourtWave,
                    _ => CardEffectStyle.Aura
                };
                Require(effectProfile.Style == expectedStyle,
                    "Card activation effect style does not match its category for " + card + ".");
            }

            var preset = CardCatalog.BuildPreset(AthleteIdentity.Lux);
            Require(preset.Count == 16, "Preset deck must contain exactly 16 cards.");
            var bastionPreset = CardCatalog.BuildPreset(AthleteIdentity.Bastion);
            Require(bastionPreset.Count == 16, "Bastion preset deck must contain exactly 16 cards.");
            for (int i = 0; i < preset.Count; i++) Require(CardCatalog.IsAllowedFor(preset[i], AthleteIdentity.Lux), "Lux preset contains an illegal card.");
            for (int i = 0; i < bastionPreset.Count; i++) Require(CardCatalog.IsAllowedFor(bastionPreset[i], AthleteIdentity.Bastion), "Bastion preset contains an illegal card.");
            DeckState deck = new DeckState(preset, 101);
            CardHandState hand = new CardHandState();
            for (int i = 0; i < 3; i++) Require(hand.TryAdd(deck.Draw()), "Opening hand must accept three cards.");
            Require(!hand.TryAdd(deck.Draw()), "Hand must reject a fourth card.");
            CardId used = hand.ConsumeActive(out bool rewardOrigin);
            Require(!rewardOrigin, "Normal deck card must retain its origin.");
            deck.Discard(used);
            Require(hand.Count == 2, "Using a card must remove it from hand.");

            CardHandState rewardHand = new CardHandState();
            rewardHand.TryAdd(CardId.FlashStep, true);
            rewardHand.ConsumeActive(out bool fromReward);
            Require(fromReward, "Reward card origin must survive hand storage.");
        }

        private static void ValidateDifficulty()
        {
            CpuDifficulty previous = CpuDifficultyPreferences.Current;
            CpuDifficultyPreferences.Current = CpuDifficulty.Easy;
            Require(CpuDifficultyPreferences.Current == CpuDifficulty.Easy, "CPU difficulty preference did not save Easy.");
            CpuDifficultyPreferences.Current = CpuDifficulty.Hard;
            Require(CpuDifficultyPreferences.Current == CpuDifficulty.Hard, "CPU difficulty preference did not save Hard.");
            CpuDifficultyPreferences.Current = previous;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("PRIDE_COURT_VALIDATION_FAILED: " + message);
            }
        }
    }
}
