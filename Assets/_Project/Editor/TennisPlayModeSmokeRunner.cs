using System;
using System.IO;
using PrideCourt.AI;
using PrideCourt.Domain;
using PrideCourt.Gameplay;
using PrideCourt.Cards;
using PrideCourt.Input;
using PrideCourt.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PrideCourt.Editor
{
    [InitializeOnLoad]
    public static class TennisPlayModeSmokeRunner
    {
        private const string ScenePath = "Assets/_Project/Scenes/MVP_Prototype.unity";
        private const string RunningKey = "PrideCourt.PlayModeSmoke.Running";
        private const string StartedAtKey = "PrideCourt.PlayModeSmoke.StartedAt";
        private const string VerifiedKey = "PrideCourt.PlayModeSmoke.Verified";
        private const string StageKey = "PrideCourt.PlayModeSmoke.Stage";
        private const string PositionKey = "PrideCourt.PlayModeSmoke.PositionZ";
        private const string BallPositionKey = "PrideCourt.PlayModeSmoke.BallPositionX";
        private const string BallPointEndYKey = "PrideCourt.PlayModeSmoke.BallPointEndY";
        private const string HandCountKey = "PrideCourt.PlayModeSmoke.HandCount";
        private const string SkipPresentationValidationKey = "PrideCourt.PlayModeSmoke.SkipPresentationValidation";

        private static bool specialCallbackFired;

        static TennisPlayModeSmokeRunner()
        {
            if (SessionState.GetBool(RunningKey, false))
            {
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
        }

        public static void RunFromCommandLine()
        {
            SessionState.SetBool(RunningKey, true);
            SessionState.SetBool(VerifiedKey, false);
            SessionState.SetFloat(StartedAtKey, 0f);
            SessionState.SetInt(StageKey, -1);
            specialCallbackFired = false;
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        public static void RunFeedbackFromCommandLine()
        {
            SessionState.SetBool(SkipPresentationValidationKey, true);
            RunFromCommandLine();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(RunningKey, false))
            {
                EditorApplication.update -= Tick;
                return;
            }

            try
            {
                if (EditorApplication.isPlaying)
                {
                    float startedAt = SessionState.GetFloat(StartedAtKey, 0f);
                    if (startedAt <= 0f)
                    {
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        TennisMatchController openingMatch = UnityEngine.Object.FindAnyObjectByType<TennisMatchController>();
                        Require(openingMatch != null, "Match controller was not alive before smoke start.");
                        Require(!openingMatch.HasStarted, "The front end was skipped before smoke start.");
                        MvpFrontEndController openingFrontEnd = UnityEngine.Object.FindAnyObjectByType<MvpFrontEndController>();
                        Require(openingFrontEnd != null && openingFrontEnd.CurrentScreen == FrontEndScreen.Opening,
                            "The opening movie was not the first front-end screen.");
                        CaptureUiFrame("UI_Opening.png");
                        return;
                    }

                    TennisMatchController match = UnityEngine.Object.FindAnyObjectByType<TennisMatchController>();
                    TennisBallController ball = UnityEngine.Object.FindAnyObjectByType<TennisBallController>();
                    TennisAthleteController[] athletes = UnityEngine.Object.FindObjectsByType<TennisAthleteController>(FindObjectsInactive.Exclude);
                    Require(match != null, "Match controller was not alive in Play Mode.");
                    Require(ball != null, "Ball was not alive in Play Mode.");
                    Require(athletes.Length == 2, "Two athletes were not alive in Play Mode.");
                    int stage = SessionState.GetInt(StageKey, 0);
                    double elapsed = EditorApplication.timeSinceStartup - startedAt;
                    MvpFrontEndController frontEnd = UnityEngine.Object.FindAnyObjectByType<MvpFrontEndController>();
                    Require(frontEnd != null, "The front-end controller was not alive in Play Mode.");
                    if (stage == -1 && elapsed >= 0.2d)
                    {
                        Require(frontEnd.ApplyFrontEndAction(FrontEndAction.FinishOpening) &&
                                frontEnd.CurrentScreen == FrontEndScreen.Title,
                            "The opening movie did not transition to the title screen.");
                        CaptureUiFrame("UI_Title.png");
                        SessionState.SetInt(StageKey, -10);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == -10 && elapsed >= 0.2d)
                    {
                        Require(frontEnd.ApplyFrontEndAction(FrontEndAction.OpenModeSelect) &&
                                frontEnd.CurrentScreen == FrontEndScreen.ModeSelect,
                            "The title screen did not transition to mode selection.");
                        CaptureUiFrame("UI_ModeSelect.png");
                        SessionState.SetInt(StageKey, -11);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == -11 && elapsed >= 0.2d)
                    {
                        Require(frontEnd.ApplyFrontEndAction(FrontEndAction.SelectMultiplayer) &&
                                frontEnd.CurrentScreen == FrontEndScreen.MultiplayerSelect,
                            "Multiplayer did not transition to local/network selection.");
                        CaptureUiFrame("UI_MultiplayerSelect.png");
                        SessionState.SetInt(StageKey, -12);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == -12 && elapsed >= 0.2d)
                    {
                        Require(frontEnd.ApplyFrontEndAction(FrontEndAction.SelectLocal) &&
                                frontEnd.CurrentScreen == FrontEndScreen.LocalNetworkLobby &&
                                frontEnd.PendingMultiplayerEntry == MultiplayerEntry.Local,
                            "Local battle did not reach the LAN host/join lobby.");
                        CaptureUiFrame("UI_LocalNetworkLobby.png");
                        SessionState.SetInt(StageKey, -13);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == -13 && elapsed >= 0.2d)
                    {
                        Require(frontEnd.ApplyFrontEndAction(FrontEndAction.Back) &&
                                !frontEnd.ApplyFrontEndAction(FrontEndAction.SelectNetwork) &&
                                frontEnd.CurrentScreen == FrontEndScreen.MultiplayerSelect &&
                                frontEnd.PendingMultiplayerEntry == MultiplayerEntry.None,
                            "Network battle must remain unavailable while marked as not implemented.");
                        CaptureUiFrame("UI_MultiplayerNetworkUnavailable.png");
                        SessionState.SetInt(StageKey, -14);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == -14 && elapsed >= 0.2d)
                    {
                        Require(frontEnd.ApplyFrontEndAction(FrontEndAction.Back) &&
                                frontEnd.ApplyFrontEndAction(FrontEndAction.SelectSolo) &&
                                frontEnd.CurrentScreen == FrontEndScreen.Setup,
                            "Solo play did not reach character and deck setup.");
                        CaptureUiFrame("UI_Setup.png");
                        Debug.Log("PRIDE_COURT_FRONT_END_FLOW_PASSED");
                        SessionState.SetInt(StageKey, -15);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == -15 && elapsed >= 0.2d)
                    {
                        match.StartNewMatch();
                        SessionState.SetInt(StageKey, -2);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == -2 && match.Phase == MatchPhase.Serving)
                    {
                        CaptureUiFrame("UI_GameplayHud.png");
                        HudSettingsController settings = UnityEngine.Object.FindAnyObjectByType<HudSettingsController>();
                        Require(settings != null, "The settings/pause controller was not alive in Play Mode.");
                        settings.SendMessage("SetOpen", true, SendMessageOptions.RequireReceiver);
                        Require(settings.IsOpen && match.IsGameplayPaused && Mathf.Approximately(Time.timeScale, 0f),
                            "Opening solo settings did not pause gameplay and time.");
                        CaptureUiFrame("UI_SettingsPause.png");
                        SessionState.SetInt(StageKey, -20);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == -2)
                    {
                        Require(elapsed < 5d, "The opening point did not reach Serving phase within five seconds.");
                        return;
                    }

                    if (stage == -20 && elapsed >= 0.2d)
                    {
                        HudSettingsController settings = UnityEngine.Object.FindAnyObjectByType<HudSettingsController>();
                        Require(settings != null && settings.IsOpen && match.Phase == MatchPhase.Serving,
                            "The settings overlay did not hold the match in its serve-wait state.");
                        CaptureUiFrame("UI_SettingsPauseReady.png");
                        SessionState.SetInt(StageKey, -21);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == -21 && elapsed >= 0.25d)
                    {
                        HudSettingsController settings = UnityEngine.Object.FindAnyObjectByType<HudSettingsController>();
                        Require(settings != null && settings.IsOpen && match.IsGameplayPaused,
                            "The settings overlay closed before its visible frame was captured.");
                        settings.SendMessage("SetOpen", false, SendMessageOptions.RequireReceiver);
                        Require(!settings.IsOpen && !match.IsGameplayPaused && Mathf.Approximately(Time.timeScale, 1f),
                            "Closing settings did not resume gameplay and time.");
                        Debug.Log("PRIDE_COURT_SETTINGS_PAUSE_PLAY_MODE_PASSED");
                        SessionState.SetInt(StageKey, 0);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 0 && match.Phase != MatchPhase.Serving)
                    {
                        Require(elapsed < 5d, "The opening point did not reach Serving phase within five seconds.");
                        return;
                    }

                    if (stage == 0 && elapsed >= 0.5d)
                    {
                        TennisAthleteController player = FindAthlete(athletes, CourtSide.Near);
                        if (!SessionState.GetBool(SkipPresentationValidationKey, false))
                        {
                            foreach (TennisAthleteController athlete in athletes)
                            {
                                GeneratedCharacterRig rig = athlete.GetComponentInChildren<GeneratedCharacterRig>();
                                Require(rig != null && rig.IsRigBuilt && rig.BoneCount >= 16,
                                    "Active generated character rig was not initialized.");
                                Require(rig.HasCompleteMotionBinding,
                                    "Active generated character meshes were not bound to the animated rig bones.");
                                Require(rig.IsRacketHeld && rig.RacketVisualGripDistance <= 0.08f,
                                    "The visible racket handle was not aligned with the character hand.");
                            }
                        }
                        KeyboardMouseCommandSource input = player.GetComponent<KeyboardMouseCommandSource>();
                        PrideCourtAudio audio = PrideCourtAudio.Instance;
                        Require(audio != null && audio.HasPlayableBallEffects,
                            "Racket-hit and court-bounce audio clips were not initialized.");
                        PrepareRigPreviewCamera(player);
                        CaptureRigFrame("Rig_Idle.png");
                        SessionState.SetFloat(PositionKey, player.transform.position.x);
                        SessionState.SetFloat(BallPositionKey, ball.transform.position.x);
                        input.InjectCommandForValidation(new TennisCommand(
                            Vector2.right, true, false, false, false, false, true, Vector2.right));
                        SessionState.SetInt(StageKey, 10);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 10 && elapsed >= 0.12d)
                    {
                        TennisAthleteController player = FindAthlete(athletes, CourtSide.Near);
                        Require(player.transform.position.x > SessionState.GetFloat(PositionKey, player.transform.position.x) + 0.001f,
                            "Injected player movement did not move the athlete.");
                        Require(Mathf.Approximately(player.Stamina.Current, StaminaState.Maximum),
                            "Serving movement incorrectly consumed stamina through dash or dive.");
                        Require(!player.IsDashingMotion, "An athlete entered dash motion during the serving phase.");
                        float playerDelta = player.transform.position.x - SessionState.GetFloat(PositionKey, player.transform.position.x);
                        float ballDelta = ball.transform.position.x - SessionState.GetFloat(BallPositionKey, ball.transform.position.x);
                        Require(Mathf.Abs(playerDelta - ballDelta) <= 0.03f,
                            "The held serve ball did not follow the moving server.");
                        CaptureRigFrame("Rig_BaselineMove.png");
                        player.GetComponent<KeyboardMouseCommandSource>().InjectCommandForValidation(default);
                        SessionState.SetInt(StageKey, 101);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 101 && elapsed >= 0.15d)
                    {
                        TennisAthleteController player = FindAthlete(athletes, CourtSide.Near);
                        Require(player.PlanarSpeed <= 0.15f,
                            "Player movement retained too much inertia after movement input was released.");
                        player.GetComponent<KeyboardMouseCommandSource>().InjectCommandForValidation(
                            new TennisCommand(Vector2.zero, false, true, false, false, false,
                                shotInputHeld: true));
                        SessionState.SetInt(StageKey, 11);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 11 && elapsed >= 0.12d)
                    {
                        Require(ball.IsTossed && !ball.IsServeInFlight,
                            "The held first A press did not remain a toss-only serve input.");
                        CaptureRigFrame("Rig_ServeToss.png");
                        SessionState.SetInt(StageKey, 111);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 111 && elapsed >= 0.24d)
                    {
                        Require(ball.IsTossed && !ball.IsServeInFlight,
                            "The serve launched without a separate explicit shot-button press.");
                        FindAthlete(athletes, CourtSide.Near).GetComponent<KeyboardMouseCommandSource>().InjectCommandForValidation(
                            new TennisCommand(Vector2.zero, false, true, false, false, false));
                        SessionState.SetInt(StageKey, 12);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 12 && elapsed >= 0.2d)
                    {
                        Require(ball.IsServeInFlight, "Injected strong-shot input did not launch the serve.");
                        Require(match.IsServeAwaitingReturn && !match.ServeRestrictionsActive,
                            "Serve restrictions remained active after racket contact.");
                        Require(Mathf.Approximately(match.ServeTimeRemaining, 0f),
                            "The ten-second serve timer continued after racket contact.");
                        Require(PrideCourtAudio.Instance != null && PrideCourtAudio.Instance.HitPlayCount > 0,
                            "The real serve-strike path did not play a racket-hit sound.");
                        TennisAthleteController player = FindAthlete(athletes, CourtSide.Near);
                        SessionState.SetFloat(PositionKey, player.transform.position.x);
                        player.GetComponent<KeyboardMouseCommandSource>().InjectCommandForValidation(
                            new TennisCommand(Vector2.left, true, false, false, false, false));
                        SessionState.SetInt(StageKey, 121);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 121 && elapsed >= 0.12d)
                    {
                        TennisAthleteController player = FindAthlete(athletes, CourtSide.Near);
                        Require(Mathf.Abs(player.transform.position.x -
                                          SessionState.GetFloat(PositionKey, player.transform.position.x)) > 0.001f,
                            "The server could not move after striking the serve.");
                        Require(player.IsDashingMotion && player.Stamina.Current < StaminaState.Maximum,
                            "Dash did not unlock after striking the serve.");
                        player.GetComponent<KeyboardMouseCommandSource>()
                            .InjectCommandForValidation(new TennisCommand(Vector2.zero, false, true, false, false, false));
                        SessionState.SetInt(StageKey, 122);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 122 && elapsed >= 0.12d)
                    {
                        Require(ball.IsServeInFlight,
                            "A second shot-button press restarted the toss while the serve was already in flight.");
                        CaptureRigFrame("Rig_ServeSwing.png");
                        FindAthlete(athletes, CourtSide.Near).GetComponent<KeyboardMouseCommandSource>()
                            .InjectCommandForValidation(default);
                        SessionState.SetInt(StageKey, 123);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 123)
                    {
                        if (!ball.CanBeHitBy(CourtSide.Far))
                        {
                            Require(elapsed < 3d,
                                "The serve did not become hittable before its first bounce.");
                            return;
                        }

                        TennisAthleteController cpu = FindAthlete(athletes, CourtSide.Far);
                        Vector3 receivePosition = ball.transform.position;
                        receivePosition.y = cpu.transform.position.y;
                        cpu.transform.position = receivePosition;
                        cpu.GetComponent<CpuCommandSource>().InjectCommandForValidation(
                            new TennisCommand(Vector2.zero, false, false, true, false, false));
                        SessionState.SetInt(StageKey, 129);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 129 && elapsed >= 0.14d)
                    {
                        Require(ball.WasServeReturnedBeforeBounce,
                            "The CPU receiver input did not return the serve before its first bounce.");

                        Require(match.Phase == MatchPhase.Rally,
                            "A no-bounce serve return did not enter the rally phase.");
                        Require(!ball.IsServeInFlight && match.CardsAllowed,
                            "A no-bounce serve return did not unlock normal rally systems.");
                        Debug.Log("PRIDE_COURT_NO_BOUNCE_SERVE_RETURN_PASSED");
                        if (!SessionState.GetBool(SkipPresentationValidationKey, false))
                        {
                            TennisAthleteController cpu = FindAthlete(athletes, CourtSide.Far);
                            GeneratedCharacterRig rig = cpu.GetComponentInChildren<GeneratedCharacterRig>();
                            Require(rig != null && rig.IsStrokePlaying,
                                "The CPU no-bounce serve return did not play a visible stroke.");
                            PrepareRigPreviewCamera(cpu);
                            CaptureRigFrame("Rig_NoBounceServeReturn.png");
                        }
                        SessionState.SetInt(StageKey, 128);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 128)
                    {
                        if (BallBounceEffect.PlayCount <= 0)
                        {
                            Require(elapsed < 3d,
                                "The no-bounce serve return did not reach the opposite court.");
                            return;
                        }

                        Require(PrideCourtAudio.Instance != null && PrideCourtAudio.Instance.BouncePlayCount > 0,
                            "The returned serve did not play court-bounce audio.");
                        CompleteFirstPointSmoke(match, ball);
                        SessionState.SetInt(StageKey, 1);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 124)
                    {
                        TennisAthleteController player = FindAthlete(athletes, CourtSide.Near);
                        if (!ball.CanBeHitBy(CourtSide.Near) ||
                            Vector3.Distance(player.transform.position, ball.transform.position) > player.HitRadius * 0.82f)
                        {
                            Require(elapsed < 4d, "The planned backhand feed did not enter the player's hitting radius.");
                            return;
                        }

                        player.GetComponent<KeyboardMouseCommandSource>().InjectCommandForValidation(
                            new TennisCommand(Vector2.zero, false, false, true, false, false));
                        SessionState.SetInt(StageKey, 125);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 125)
                    {
                        if (elapsed < 0.06d)
                        {
                            return;
                        }
                        FindAthlete(athletes, CourtSide.Near).GetComponent<KeyboardMouseCommandSource>()
                            .InjectCommandForValidation(default);
                        SessionState.SetInt(StageKey, 126);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 126 && elapsed >= 0.18d)
                    {
                        TennisAthleteController player = FindAthlete(athletes, CourtSide.Near);
                        GeneratedCharacterRig rig = player.GetComponentInChildren<GeneratedCharacterRig>();
                        Require(rig != null && rig.LastStrokeMotion == StrokeMotion.Backhand,
                            "A left-side rally ball did not select the backhand motion through player input.");
                        Require(rig.IsStrokePlaying,
                            "The backhand motion ended before the buffered rally return reached the ball.");
                        CaptureRigFrame("Rig_BackhandSwing.png");
                        SessionState.SetInt(StageKey, 127);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 127)
                    {
                        if (!ball.CanBeHitBy(CourtSide.Far))
                        {
                            Require(elapsed < 3d,
                                "The backhand input did not complete the rally return.");
                            return;
                        }

                        CompleteFirstPointSmoke(match, ball);
                        SessionState.SetInt(StageKey, 1);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 126)
                    {
                        return;
                    }

                    if (stage == 1 && elapsed >= 1.6d)
                    {
                        if (match.Phase == MatchPhase.PointResult && elapsed < 4d)
                        {
                            return;
                        }
                        Require(match.Phase == MatchPhase.CardSelection, "Point result did not enter card selection.");
                        Require(Mathf.Abs(ball.transform.position.y -
                                          SessionState.GetFloat(BallPointEndYKey, ball.transform.position.y)) <= 0.001f,
                            "A point-ending ball drifted vertically before the next point reset.");
                        CardLoadoutController nearCards = FindCards(athletes, CourtSide.Near);
                        CardLoadoutController farCards = FindCards(athletes, CourtSide.Far);
                        Require(nearCards != null && farCards != null, "Card loadouts were not alive in Play Mode.");
                        Require(nearCards.PendingCard.HasValue, "Player did not draw a card between points.");
                        Require(farCards.IsPreparationReady, "CPU did not resolve its card draw.");
                        TennisAthleteController player = FindAthlete(athletes, CourtSide.Near);
                        SessionState.SetFloat(PositionKey, player.transform.position.x);
                        player.GetComponent<KeyboardMouseCommandSource>().InjectCommandForValidation(
                            new TennisCommand(Vector2.right, true, true, false, false, true, true, Vector2.right));
                        SessionState.SetInt(StageKey, 15);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 15 && elapsed >= 0.18d)
                    {
                        TennisAthleteController player = FindAthlete(athletes, CourtSide.Near);
                        Require(Mathf.Abs(player.transform.position.x - SessionState.GetFloat(PositionKey, player.transform.position.x)) <= 0.001f,
                            "Character input moved the athlete during card selection.");
                        FindCards(athletes, CourtSide.Near).ForcePreparationReady();
                        SessionState.SetInt(StageKey, 2);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 2 && elapsed >= 0.25d)
                    {
                        Require(match.Phase == MatchPhase.Serving, "Ready card selections did not begin the next point.");
                        match.NotifyValidServe();
                        Require(match.CardsAllowed,
                            "Cards were not enabled immediately after a valid serve entered the rally phase.");
                        CardLoadoutController nearCards = FindCards(athletes, CourtSide.Near);
                        SessionState.SetInt(HandCountKey, nearCards.Hand.Count);
                        FindAthlete(athletes, CourtSide.Near).GetComponent<KeyboardMouseCommandSource>()
                            .InjectCommandForValidation(new TennisCommand(
                                Vector2.zero, false, false, false, false, false,
                                directCardPressed: true, directCardSlot: 0));
                        SessionState.SetInt(StageKey, 16);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 16 && elapsed >= 0.12d)
                    {
                        CardLoadoutController nearCards = FindCards(athletes, CourtSide.Near);
                        Require(nearCards.Hand.Count == SessionState.GetInt(HandCountKey, nearCards.Hand.Count) - 1,
                            "Keyboard direct-card input did not use card slot 1.");
                        Require(CardActivationEffect.PlayCount > 0,
                            "Using a card did not create its activation effect.");
                        Require(PrideCourtAudio.Instance != null &&
                                PrideCourtAudio.Instance.GetComponent<AudioSource>().isPlaying,
                            "Using a card did not play its activation sound.");
                        SessionState.SetInt(HandCountKey, nearCards.Hand.Count);
                        Vector2 cardPointer = nearCards.GetHandCardPointerForValidation(0);
                        FindAthlete(athletes, CourtSide.Near).GetComponent<KeyboardMouseCommandSource>()
                            .InjectCommandForValidation(new TennisCommand(
                                Vector2.zero, false, false, false, false, false,
                                cardPointer: cardPointer, directCardPressed: true));
                        SessionState.SetInt(StageKey, 17);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 17 && elapsed >= 0.12d)
                    {
                        CardLoadoutController nearCards = FindCards(athletes, CourtSide.Near);
                        Require(nearCards.Hand.Count == SessionState.GetInt(HandCountKey, nearCards.Hand.Count) - 1,
                            "Android-style card-image tap did not use the touched card.");
                        SessionState.SetInt(HandCountKey, nearCards.Hand.Count);
                        Vector2 cardPointer = nearCards.GetHandCardPointerForValidation(0);
                        FindAthlete(athletes, CourtSide.Near).GetComponent<KeyboardMouseCommandSource>()
                            .InjectCommandForValidation(new TennisCommand(
                                Vector2.zero, false, false, false, false, false,
                                cardDown: true, cardHeld: true, cardPointer: cardPointer));
                        SessionState.SetInt(StageKey, 20);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 20 && elapsed >= 0.34d)
                    {
                        Vector2 cardPointer = FindCards(athletes, CourtSide.Near).GetHandCardPointerForValidation(0);
                        FindAthlete(athletes, CourtSide.Near).GetComponent<KeyboardMouseCommandSource>()
                            .InjectCommandForValidation(new TennisCommand(
                                Vector2.zero, false, false, false, false, false,
                                cardHeld: true, cardPointer: cardPointer));
                        SessionState.SetInt(StageKey, 21);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 21 && elapsed >= 0.08d)
                    {
                        Vector2 cardPointer = FindCards(athletes, CourtSide.Near).GetHandCardPointerForValidation(0);
                        FindAthlete(athletes, CourtSide.Near).GetComponent<KeyboardMouseCommandSource>()
                            .InjectCommandForValidation(new TennisCommand(
                                Vector2.zero, false, false, false, false, false,
                                cardReleased: true, cardPointer: cardPointer));
                        SessionState.SetInt(StageKey, 22);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 22 && elapsed >= 0.12d)
                    {
                        CardLoadoutController nearCards = FindCards(athletes, CourtSide.Near);
                        Require(nearCards.Hand.Count == SessionState.GetInt(HandCountKey, nearCards.Hand.Count) - 1,
                            "Card-button hold, slide, and release did not use the selected card.");
                        match.PlaySpecialCutIn(athletes[0], () => specialCallbackFired = true);
                        Require(match.IsGameplayPaused, "Special cut-in did not pause gameplay.");
                        Require(match.HasSpecialCutInTexture,
                            "Special cut-in image was not loaded from Resources.");
                        CaptureUiFrame("SpecialCutIn.png");
                        SessionState.SetInt(StageKey, 3);
                        SessionState.SetFloat(StartedAtKey, (float)EditorApplication.timeSinceStartup);
                        return;
                    }

                    if (stage == 3 && elapsed >= 1.7d)
                    {
                        Require(specialCallbackFired, "Special cut-in did not invoke the valid-shot callback.");
                        Require(!match.IsGameplayPaused, "Special cut-in did not restore gameplay.");
                        Require(Mathf.Approximately(Time.timeScale, 1f), "Special cut-in did not restore time scale.");
                        Debug.Log(SessionState.GetBool(SkipPresentationValidationKey, false)
                            ? "PRIDE_COURT_FEEDBACK_PLAY_MODE_SMOKE_PASSED"
                            : "PRIDE_COURT_PLAY_MODE_SMOKE_PASSED");
                        SessionState.SetBool(VerifiedKey, true);
                        EditorApplication.ExitPlaymode();
                        return;
                    }
                }

                if (!EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(VerifiedKey, false))
                {
                    SessionState.EraseBool(RunningKey);
                    SessionState.EraseBool(VerifiedKey);
                    SessionState.EraseFloat(StartedAtKey);
                    SessionState.EraseInt(StageKey);
                    SessionState.EraseBool(SkipPresentationValidationKey);
                    EditorApplication.update -= Tick;
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.EraseBool(RunningKey);
                SessionState.EraseBool(VerifiedKey);
                SessionState.EraseFloat(StartedAtKey);
                SessionState.EraseInt(StageKey);
                SessionState.EraseBool(SkipPresentationValidationKey);
                EditorApplication.update -= Tick;
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.ExitPlaymode();
                }
                EditorApplication.Exit(1);
            }
        }

        private static CardLoadoutController FindCards(TennisAthleteController[] athletes, CourtSide side)
        {
            for (int i = 0; i < athletes.Length; i++)
            {
                if (athletes[i].Side == side) return athletes[i].GetComponent<CardLoadoutController>();
            }
            return null;
        }

        private static void CompleteFirstPointSmoke(TennisMatchController match, TennisBallController ball)
        {
            match.AwardPoint(CourtSide.Near, "PLAY_MODE_SMOKE");
            Require(match.Score.NearPoints == 1, "Runtime point award did not update the score.");
            Require(match.Phase == MatchPhase.PointResult, "Runtime point award did not enter PointResult.");
            Rigidbody ballBody = ball.GetComponent<Rigidbody>();
            Require(ballBody != null && ballBody.isKinematic,
                "A point-ending ball remained dynamic after entering PointResult.");
            Require(ballBody.linearVelocity.sqrMagnitude <= 0.000001f,
                "A point-ending ball retained velocity after entering PointResult.");
            SessionState.SetFloat(BallPointEndYKey, ball.transform.position.y);
        }

        private static TennisAthleteController FindAthlete(TennisAthleteController[] athletes, CourtSide side)
        {
            for (int i = 0; i < athletes.Length; i++) if (athletes[i].Side == side) return athletes[i];
            return null;
        }

        private static void PrepareRigPreviewCamera(TennisAthleteController player)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            TennisCameraController controller = camera.GetComponent<TennisCameraController>();
            if (controller != null) controller.enabled = false;
            camera.transform.position = player.transform.position + new Vector3(3.8f, 2.6f, -4.6f);
            camera.transform.rotation = Quaternion.LookRotation(player.transform.position + Vector3.up * 0.9f - camera.transform.position, Vector3.up);
        }

        private static void CaptureRigFrame(string fileName)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            const int width = 960;
            const int height = 720;
            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                RenderTexture.active = target;
                camera.Render();
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply();
                string outputDirectory = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Logs", "RigPreview");
                Directory.CreateDirectory(outputDirectory);
                File.WriteAllBytes(Path.Combine(outputDirectory, fileName), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(image);
            }
        }

        private static void CaptureUiFrame(string fileName)
        {
            string outputDirectory = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Logs", "UiPreview");
            Directory.CreateDirectory(outputDirectory);
            ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, fileName), 1);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("PRIDE_COURT_PLAY_MODE_SMOKE_FAILED: " + message);
            }
        }
    }
}
