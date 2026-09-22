using UnityEngine;
using PrideCourt.Presentation;
using PrideCourt.Gameplay;

namespace PrideCourt.Input
{
    public sealed class KeyboardMouseCommandSource : MonoBehaviour, ITennisCommandSource
    {
        private const float StickRadius = 102f;
        private const int CircleTextureSize = 128;

        private readonly DirectionalFlickDetector gamepadDiveDetector = new DirectionalFlickDetector();
        private readonly DirectionalFlickDetector touchDiveDetector = new DirectionalFlickDetector();
        private int moveFingerId = -1;
        private int strongFingerId = -1;
        private int safeFingerId = -1;
        private int cardFingerId = -1;
        private Vector2 touchMove;
        private bool touchStrongDown;
        private bool touchStrongHeld;
        private bool touchSafeDown;
        private bool touchSafeHeld;
        private bool touchSpecialDown;
        private bool touchDirectCardPressed;
        private Vector2 touchDirectCardPointer;
        private bool touchCardDown;
        private bool touchCardHeld;
        private bool touchCardReleased;
        private Vector2 touchCardPointer;
        private TennisCommand injectedCommand;
        private float injectedCommandUntil;
        private bool injectedEdgeConsumed;
        private Texture2D circleTexture;
        private int queuedPreparationChoice = -2;

        public void QueuePreparationChoice(int choice)
        {
            queuedPreparationChoice = Mathf.Clamp(choice, -1, CardHandStateCapacityMinusOne);
        }

        private const int CardHandStateCapacityMinusOne = 2;

        public void InjectCommandForValidation(TennisCommand command)
        {
            injectedCommand = command;
            injectedCommandUntil = Time.unscaledTime + 0.3f;
            injectedEdgeConsumed = false;
        }

        public TennisCommand ReadCommand()
        {
            if (Time.unscaledTime < injectedCommandUntil)
            {
                if (!injectedEdgeConsumed)
                {
                    injectedEdgeConsumed = true;
                    return injectedCommand;
                }

                return new TennisCommand(
                    injectedCommand.Move,
                    injectedCommand.Dash,
                    false,
                    false,
                    false,
                    false,
                    false,
                    Vector2.zero,
                    false,
                    injectedCommand.CardHeld,
                    false,
                    injectedCommand.CardPointer,
                    injectedCommand.ShotInputHeld);
            }
            ResetTouchFrameFlags();
            ReadTouches();
            Vector2 wasdMove = Vector2.zero;
            if (UnityEngine.Input.GetKey(KeyCode.A)) wasdMove.x -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.D)) wasdMove.x += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.S)) wasdMove.y -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.W)) wasdMove.y += 1f;
            Vector2 move = wasdMove;
            if (UnityEngine.Input.GetKey(KeyCode.LeftArrow)) move.x -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.RightArrow)) move.x += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.DownArrow)) move.y -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.UpArrow)) move.y += 1f;
            bool keyboardMoveHeld = IsKeyboardMoveHeld();
            Vector2 analog = new Vector2(UnityEngine.Input.GetAxisRaw("Horizontal"), UnityEngine.Input.GetAxisRaw("Vertical"));
            if (analog.sqrMagnitude > move.sqrMagnitude) move = Vector2.ClampMagnitude(analog, 1f);
            if (touchMove.sqrMagnitude > move.sqrMagnitude) move = touchMove;

            bool keyboardDiveChordPressed = UnityEngine.Input.GetKeyDown(KeyCode.Space) ||
                                            (UnityEngine.Input.GetKey(KeyCode.Space) && IsWasdKeyDown());
            bool divePressed = DiveInputPolicy.TryGetKeyboardDive(
                keyboardDiveChordPressed, wasdMove, out Vector2 diveDirection);
            if (!divePressed)
            {
                // Legacy input axes also mirror WASD/arrow keys. Neutralize them here so the
                // removed keyboard double-tap gesture cannot leak back in as a gamepad flick.
                Vector2 gamepadDiveInput = keyboardMoveHeld ? Vector2.zero : analog;
                divePressed = gamepadDiveDetector.Update(gamepadDiveInput, Time.unscaledTime, out diveDirection);
            }
            if (!divePressed)
            {
                divePressed = touchDiveDetector.Update(touchMove, Time.unscaledTime, out diveDirection);
            }
            bool cardDown = UnityEngine.Input.GetKeyDown(KeyCode.Q) || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton4) || touchCardDown;
            bool cardHeld = UnityEngine.Input.GetKey(KeyCode.Q) || UnityEngine.Input.GetKey(KeyCode.JoystickButton4) || touchCardHeld;
            bool cardReleased = UnityEngine.Input.GetKeyUp(KeyCode.Q) || UnityEngine.Input.GetKeyUp(KeyCode.JoystickButton4) || touchCardReleased;
            bool cardTouchActive = cardFingerId >= 0 || touchCardDown || touchCardHeld || touchCardReleased || touchDirectCardPressed;
            Vector2 pointer = cardTouchActive
                ? touchDirectCardPressed ? touchDirectCardPointer : touchCardPointer
                : UnityEngine.Input.GetKey(KeyCode.JoystickButton4)
                    ? new Vector2(Mathf.InverseLerp(-1f, 1f, move.x), -1f)
                    : new Vector2(UnityEngine.Input.mousePosition.x / Mathf.Max(1f, Screen.width), UnityEngine.Input.mousePosition.y / Mathf.Max(1f, Screen.height));
            DirectCardInputPolicy.TryGetKeyboardSlot(
                UnityEngine.Input.GetKeyDown(KeyCode.Alpha1),
                UnityEngine.Input.GetKeyDown(KeyCode.Alpha2),
                UnityEngine.Input.GetKeyDown(KeyCode.Alpha3),
                out int directCardSlot);
            // Android mirrors its first touch to the legacy mouse API. Ignoring that
            // emulation prevents stick/card taps from accidentally tossing or striking.
            bool mobileTouchActive = Application.isMobilePlatform && UnityEngine.Input.touchCount > 0;
            bool allowMouseInput = !Application.isMobilePlatform && UnityEngine.Input.touchCount == 0;
            bool allowHardwareShotInput = !mobileTouchActive;
            bool strongPressed = touchStrongDown || allowHardwareShotInput &&
                ((allowMouseInput && UnityEngine.Input.GetMouseButtonDown(0)) || UnityEngine.Input.GetKeyDown(KeyCode.J) ||
                 UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton2));
            bool safePressed = touchSafeDown || allowHardwareShotInput &&
                ((allowMouseInput && UnityEngine.Input.GetMouseButtonDown(1)) || UnityEngine.Input.GetKeyDown(KeyCode.K) ||
                 UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton0));
            bool shotInputHeld = touchStrongHeld || touchSafeHeld || allowHardwareShotInput &&
                ((allowMouseInput && UnityEngine.Input.GetMouseButton(0)) || UnityEngine.Input.GetKey(KeyCode.J) ||
                 UnityEngine.Input.GetKey(KeyCode.JoystickButton2) ||
                 (allowMouseInput && UnityEngine.Input.GetMouseButton(1)) || UnityEngine.Input.GetKey(KeyCode.K) ||
                 UnityEngine.Input.GetKey(KeyCode.JoystickButton0));
            bool tossPressed = allowHardwareShotInput &&
                (UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton1));
            int preparationChoice = queuedPreparationChoice;
            queuedPreparationChoice = -2;
            if (preparationChoice == -2 && LocalPreparationInputRouter.TryConsume(out int routedChoice))
            {
                preparationChoice = routedChoice;
            }
            if (preparationChoice == -2)
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1) || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton2)) preparationChoice = 0;
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2) || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton0)) preparationChoice = 1;
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3) || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton1)) preparationChoice = 2;
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton3)) preparationChoice = -1;
            }
            return new TennisCommand(
                move,
                DashInputPolicy.ShouldDash(
                    keyboardMoveHeld,
                    UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift),
                    analog.magnitude,
                    touchMove.magnitude),
                strongPressed,
                safePressed,
                tossPressed,
                UnityEngine.Input.GetKeyDown(KeyCode.E) || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton3) || touchSpecialDown,
                divePressed,
                diveDirection,
                cardDown,
                cardHeld,
                cardReleased,
                pointer,
                shotInputHeld,
                directCardSlot >= 0 || touchDirectCardPressed,
                directCardSlot,
                preparationChoice);
        }

        private static bool IsKeyboardMoveHeld()
        {
            return UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.D) ||
                   UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.W) ||
                   UnityEngine.Input.GetKey(KeyCode.LeftArrow) || UnityEngine.Input.GetKey(KeyCode.RightArrow) ||
                   UnityEngine.Input.GetKey(KeyCode.DownArrow) || UnityEngine.Input.GetKey(KeyCode.UpArrow);
        }

        private static bool IsWasdKeyDown()
        {
            return UnityEngine.Input.GetKeyDown(KeyCode.A) || UnityEngine.Input.GetKeyDown(KeyCode.D) ||
                   UnityEngine.Input.GetKeyDown(KeyCode.S) || UnityEngine.Input.GetKeyDown(KeyCode.W);
        }

        private void ResetTouchFrameFlags()
        {
            touchStrongDown = false;
            touchSafeDown = false;
            touchSpecialDown = false;
            touchDirectCardPressed = false;
            touchCardDown = false;
            touchCardReleased = false;
        }

        private void ReadTouches()
        {
            if (UnityEngine.Input.touchCount == 0)
            {
                if (moveFingerId >= 0) { moveFingerId = -1; touchMove = Vector2.zero; }
                if (strongFingerId >= 0) { strongFingerId = -1; touchStrongHeld = false; }
                if (safeFingerId >= 0) { safeFingerId = -1; touchSafeHeld = false; }
                if (cardFingerId >= 0) { cardFingerId = -1; touchCardHeld = false; touchCardReleased = true; }
                return;
            }

            for (int i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                Touch touch = UnityEngine.Input.GetTouch(i);
                Vector2 position = touch.position;
                if (touch.fingerId == moveFingerId)
                {
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        moveFingerId = -1;
                        touchMove = Vector2.zero;
                    }
                    else
                    {
                        touchMove = Vector2.ClampMagnitude((position - StickCenter) / ScaledStickRadius, 1f);
                    }
                    continue;
                }

                if (touch.fingerId == cardFingerId)
                {
                    touchCardPointer = new Vector2(position.x / Mathf.Max(1f, Screen.width), position.y / Mathf.Max(1f, Screen.height));
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        cardFingerId = -1;
                        touchCardHeld = false;
                        touchCardReleased = true;
                    }
                    continue;
                }

                if (touch.fingerId == strongFingerId)
                {
                    touchStrongHeld = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                    if (!touchStrongHeld) strongFingerId = -1;
                    continue;
                }

                if (touch.fingerId == safeFingerId)
                {
                    touchSafeHeld = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                    if (!touchSafeHeld) safeFingerId = -1;
                    continue;
                }

                if (touch.phase != TouchPhase.Began) continue;
                MobileTouchTarget target = MobileTouchTargetPolicy.Resolve(
                    position,
                    Screen.height,
                    Screen.width * 0.46f,
                    StrongRect,
                    SafeRect,
                    SpecialRect,
                    CardRect);
                if (target == MobileTouchTarget.Movement)
                {
                    moveFingerId = touch.fingerId;
                    touchMove = Vector2.ClampMagnitude((position - StickCenter) / ScaledStickRadius, 1f);
                }
                else if (target == MobileTouchTarget.Strong)
                {
                    strongFingerId = touch.fingerId;
                    touchStrongDown = true;
                    touchStrongHeld = true;
                }
                else if (target == MobileTouchTarget.Safe)
                {
                    safeFingerId = touch.fingerId;
                    touchSafeDown = true;
                    touchSafeHeld = true;
                }
                else if (target == MobileTouchTarget.Special) touchSpecialDown = true;
                else if (target == MobileTouchTarget.LegacyCard)
                {
                    cardFingerId = touch.fingerId;
                    touchCardDown = true;
                    touchCardHeld = true;
                    touchCardPointer = new Vector2(position.x / Mathf.Max(1f, Screen.width), position.y / Mathf.Max(1f, Screen.height));
                }
                else
                {
                    // The card controller owns the exact card rectangles. Forward an
                    // unconsumed right-side tap and let it reject taps outside the hand.
                    touchDirectCardPressed = true;
                    touchDirectCardPointer = new Vector2(
                        position.x / Mathf.Max(1f, Screen.width),
                        position.y / Mathf.Max(1f, Screen.height));
                }
            }
        }

        private void OnGUI()
        {
            if (HudSettingsController.IsAnyOpen) return;
            if (!Application.isMobilePlatform && UnityEngine.Input.touchCount == 0) return;
            EnsureCircleTexture();
            Color previous = GUI.color;
            Rect stickRect = new Rect(StickCenter.x - ScaledStickRadius, Screen.height - StickCenter.y - ScaledStickRadius,
                ScaledStickRadius * 2f, ScaledStickRadius * 2f);
            GUI.color = new Color(PrideCourtUiTheme.Ink.r, PrideCourtUiTheme.Ink.g, PrideCourtUiTheme.Ink.b, 0.86f);
            GUI.DrawTexture(stickRect, circleTexture, ScaleMode.StretchToFill, true);
            Rect innerRect = ScaleAroundCenter(stickRect, 0.82f);
            GUI.color = new Color(PrideCourtUiTheme.Cyan.r, PrideCourtUiTheme.Cyan.g, PrideCourtUiTheme.Cyan.b, 0.42f);
            GUI.DrawTexture(innerRect, circleTexture, ScaleMode.StretchToFill, true);
            Rect ringRect = ScaleAroundCenter(stickRect, 0.56f);
            GUI.color = new Color(PrideCourtUiTheme.Ink.r, PrideCourtUiTheme.Ink.g, PrideCourtUiTheme.Ink.b, 0.66f);
            GUI.DrawTexture(ringRect, circleTexture, ScaleMode.StretchToFill, true);
            Vector2 knobOffset = Vector2.ClampMagnitude(touchMove, 1f) * ScaledStickRadius * 0.46f;
            float knobSize = ScaledStickRadius * 0.72f;
            Rect knobRect = new Rect(stickRect.center.x + knobOffset.x - knobSize * 0.5f,
                stickRect.center.y - knobOffset.y - knobSize * 0.5f, knobSize, knobSize);
            GUI.color = new Color(PrideCourtUiTheme.Magenta.r, PrideCourtUiTheme.Magenta.g, PrideCourtUiTheme.Magenta.b, 0.92f);
            GUI.DrawTexture(knobRect, circleTexture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
            PrideCourtUiTheme.DrawTag(new Rect(stickRect.center.x - 45f * UiScale, stickRect.yMax + 2f,
                90f * UiScale, 22f * UiScale), "移動", PrideCourtUiTheme.Tone.Cyan, UiScale);

            PrideCourtUiTheme.DrawTouchButton(StrongRect, "A", "強打", PrideCourtUiTheme.Tone.Magenta, UiScale);
            PrideCourtUiTheme.DrawTouchButton(SafeRect, "B", "安定", PrideCourtUiTheme.Tone.Cyan, UiScale);
            PrideCourtUiTheme.DrawTouchButton(CardRect, "カード", "札をタップ", PrideCourtUiTheme.Tone.Violet, UiScale);
            PrideCourtUiTheme.DrawTouchButton(SpecialRect, "必殺", "発動", PrideCourtUiTheme.Tone.Yellow, UiScale);
        }

        private float UiScale => Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.72f, 1.25f);
        private float ScaledStickRadius => StickRadius * UiScale * VirtualStickPreferences.Scale;
        private Vector2 StickCenter => new Vector2(
            ScaledStickRadius + 33f * UiScale + Screen.safeArea.xMin,
            ScaledStickRadius + 33f * UiScale + Screen.safeArea.yMin);


        
        // 括弧の中：(横幅, 縦幅, 右からの隙間, 下からの隙間)
        private Rect StrongRect => BottomRightRect(125f, 125f, 32f, 55f);     // Aボタン：一回り大きく
        private Rect SafeRect => BottomRightRect(115f, 115f, 165f, 25f);    // Bボタン：一回り大きく、左に少し離す
        private Rect CardRect => BottomRightRect(110f, 65f, 295f, 155f);    // ボタン：文字が収まるように横幅・縦幅を拡大、位置調整
        private Rect SpecialRect => BottomRightRect(110f, 65f, 175f, 180f);   // 必殺ボタン：文字が収まるように横幅・縦幅を拡大、位置調整

        // private Rect StrongRect => BottomRightRect(112f, 112f, 32f, 48f);
        // private Rect SafeRect => BottomRightRect(100f, 100f, 150f, 25f);
        // private Rect CardRect => BottomRightRect(92f, 54f, 272f, 152f);
        // private Rect SpecialRect => BottomRightRect(98f, 54f, 164f, 172f);

        private void EnsureCircleTexture()
        {
            if (circleTexture != null) return;
            circleTexture = new Texture2D(CircleTextureSize, CircleTextureSize, TextureFormat.RGBA32, false)
            {
                name = "Runtime Mobile Stick Circle",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32 clear = new Color32(255, 255, 255, 0);
            Color32 solid = new Color32(255, 255, 255, 255);
            float center = (CircleTextureSize - 1) * 0.5f;
            float radiusSquared = center * center;
            Color32[] pixels = new Color32[CircleTextureSize * CircleTextureSize];
            for (int y = 0; y < CircleTextureSize; y++)
            {
                for (int x = 0; x < CircleTextureSize; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    pixels[y * CircleTextureSize + x] = dx * dx + dy * dy <= radiusSquared ? solid : clear;
                }
            }
            circleTexture.SetPixels32(pixels);
            circleTexture.Apply(false, true);
        }

        private static Rect ScaleAroundCenter(Rect rect, float scale)
        {
            float width = rect.width * scale;
            float height = rect.height * scale;
            return new Rect(rect.center.x - width * 0.5f, rect.center.y - height * 0.5f, width, height);
        }

        private void OnDestroy()
        {
            if (circleTexture != null) Destroy(circleTexture);
        }

        private Rect BottomRightRect(float width, float height, float right, float bottom)
        {
            float scale = UiScale;
            return new Rect(
                Screen.safeArea.xMax - (right + width) * scale,
                Screen.height - Screen.safeArea.yMin - (bottom + height) * scale,
                width * scale,
                height * scale);
        }

        private static Vector2 ToGuiPoint(Vector2 screenPoint)
        {
            return new Vector2(screenPoint.x, Screen.height - screenPoint.y);
        }

    }

    public static class DiveInputPolicy
    {
        public static bool TryGetKeyboardDive(bool spacePressed, Vector2 wasdDirection, out Vector2 diveDirection)
        {
            bool shouldDive = spacePressed && wasdDirection.sqrMagnitude > 0.1f;
            diveDirection = shouldDive ? wasdDirection.normalized : Vector2.zero;
            return shouldDive;
        }
    }

    public sealed class DirectionalFlickDetector
    {
        public const float FlickWindow = 0.28f;
        private const float NeutralThreshold = 0.25f;
        private const float FlickThreshold = 0.72f;
        private const float SameDirectionDotThreshold = 0.7f;

        private bool wasNeutral = true;
        private float returnedAt = float.NegativeInfinity;
        private Vector2 previousDirection;

        public bool Update(Vector2 input, float unscaledTime, out Vector2 direction)
        {
            float magnitude = input.magnitude;
            if (magnitude < NeutralThreshold)
            {
                if (!wasNeutral)
                {
                    returnedAt = unscaledTime;
                }

                wasNeutral = true;
                direction = Vector2.zero;
                return false;
            }

            Vector2 normalized = input.normalized;
            bool sameDirection = previousDirection.sqrMagnitude > 0.1f &&
                                 Vector2.Dot(previousDirection, normalized) >= SameDirectionDotThreshold;
            bool triggered = wasNeutral && magnitude > FlickThreshold &&
                             unscaledTime - returnedAt <= FlickWindow && sameDirection;
            wasNeutral = false;
            previousDirection = normalized;
            direction = triggered ? normalized : Vector2.zero;
            return triggered;
        }
    }

    public static class DashInputPolicy
    {
        private const float AnalogDashThreshold = 0.86f;

        public static bool ShouldDash(bool keyboardMoveHeld, bool shiftHeld, float analogMagnitude, float touchMagnitude)
        {
            if (shiftHeld)
            {
                return true;
            }

            // Legacy axes also report WASD and arrow input as full analog input.
            bool gamepadDash = !keyboardMoveHeld && analogMagnitude >= AnalogDashThreshold;
            return gamepadDash || touchMagnitude >= AnalogDashThreshold;
        }
    }

    public static class DirectCardInputPolicy
    {
        public static bool TryGetKeyboardSlot(bool firstPressed, bool secondPressed, bool thirdPressed, out int slot)
        {
            if (firstPressed) slot = 0;
            else if (secondPressed) slot = 1;
            else if (thirdPressed) slot = 2;
            else slot = -1;
            return slot >= 0;
        }
    }

    public enum MobileTouchTarget
    {
        Movement,
        Strong,
        Safe,
        Special,
        LegacyCard,
        DirectCard
    }

    public static class MobileTouchTargetPolicy
    {
        public static MobileTouchTarget Resolve(
            Vector2 screenPosition,
            float screenHeight,
            float movementRightEdge,
            Rect strongRect,
            Rect safeRect,
            Rect specialRect,
            Rect legacyCardRect)
        {
            if (screenPosition.x < movementRightEdge) return MobileTouchTarget.Movement;

            Vector2 guiPoint = new Vector2(screenPosition.x, screenHeight - screenPosition.y);
            if (strongRect.Contains(guiPoint)) return MobileTouchTarget.Strong;
            if (safeRect.Contains(guiPoint)) return MobileTouchTarget.Safe;
            if (specialRect.Contains(guiPoint)) return MobileTouchTarget.Special;
            if (legacyCardRect.Contains(guiPoint)) return MobileTouchTarget.LegacyCard;
            return MobileTouchTarget.DirectCard;
        }
    }
}
