using System;
using System.Collections.Generic;
using PrideCourt.Domain;
using PrideCourt.Gameplay;
using UnityEngine;

namespace PrideCourt.Presentation
{
    public sealed class GeneratedCharacterRig : MonoBehaviour, ITennisAthleteMotionView
    {
        [Serializable]
        private sealed class BonePose
        {
            public Transform Transform;
            public Vector3 BasePosition;
            public Quaternion BaseRotation;
            public Vector3 TargetPosition;
            public Quaternion TargetRotation;
        }

        private readonly struct RigDimensions
        {
            public RigDimensions(Vector3 hips, Vector3 chest, Vector3 head, float shoulderX, float shoulderY,
                float elbowX, float elbowY, float wristX, float wristY, float legX, float hipY, float kneeY,
                float ankleY)
            {
                Hips = hips; Chest = chest; Head = head; ShoulderX = shoulderX; ShoulderY = shoulderY;
                ElbowX = elbowX; ElbowY = elbowY; WristX = wristX; WristY = wristY;
                LegX = legX; HipY = hipY; KneeY = kneeY; AnkleY = ankleY;
            }

            public Vector3 Hips { get; }
            public Vector3 Chest { get; }
            public Vector3 Head { get; }
            public float ShoulderX { get; }
            public float ShoulderY { get; }
            public float ElbowX { get; }
            public float ElbowY { get; }
            public float WristX { get; }
            public float WristY { get; }
            public float LegX { get; }
            public float HipY { get; }
            public float KneeY { get; }
            public float AnkleY { get; }
        }

        [SerializeField] private AthleteIdentity identity;
        [SerializeField] private bool rigBuilt;
        [SerializeField] private Transform rigRoot;
        [SerializeField] private Transform hips;
        [SerializeField] private Transform chest;
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftShoulder;
        [SerializeField] private Transform leftElbow;
        [SerializeField] private Transform leftWrist;
        [SerializeField] private Transform rightShoulder;
        [SerializeField] private Transform rightElbow;
        [SerializeField] private Transform rightWrist;
        [SerializeField] private Transform leftHip;
        [SerializeField] private Transform leftKnee;
        [SerializeField] private Transform leftAnkle;
        [SerializeField] private Transform rightHip;
        [SerializeField] private Transform rightKnee;
        [SerializeField] private Transform rightAnkle;
        [SerializeField] private Transform tailBase;
        [SerializeField] private Transform tailMiddle;
        [SerializeField] private Transform tailTip;
        [SerializeField] private Transform leftWing;
        [SerializeField] private Transform rightWing;
        [SerializeField] private Transform headLeaf;
        [SerializeField] private Transform racketGrip;
        [SerializeField] private Transform racketHandle;
        [SerializeField] private Transform racketHand;

        private readonly List<BonePose> poses = new List<BonePose>();
        private TennisAthleteController athlete;
        private TennisBallController ball;
        private Vector3 rootBasePosition;
        private Quaternion rootBaseRotation;
        private Vector3 rootBaseScale;
        private float locomotionPhase;
        private float animationTime;
        private float strokeRemaining;
        private float strokeDuration;
        private float strokeStartProgress;
        private float strokeSide = 1f;
        private StrokeMotion strokeMotion;
        private bool strongStroke;
        private bool strokePrepared;
        private bool preparedStrongStroke;
        private StrokeMotion preparedStrokeMotion;
        private float strokePreparationTime;
        private float tossAccentRemaining;
        private Vector2 diveDirection;
        private Vector3 restRacketVisualLocalPosition;
        private bool hasRestRacketVisualPosition;
        private bool awakened;
        private float signatureSpecialRemaining;
        private const float SignatureSpecialDuration = 0.78f;

        public AthleteIdentity Identity => identity;
        public bool IsRigBuilt => rigBuilt;
        public bool IsRacketHeld => racketGrip != null && rightWrist != null && racketGrip.parent == rightWrist &&
                                    racketHandle != null && racketHand != null && RacketVisualGripDistance <= 0.08f;
        public float RacketVisualGripDistance
        {
            get
            {
                Renderer handleRenderer = racketHandle == null ? null : racketHandle.GetComponent<Renderer>();
                Renderer handRenderer = racketHand == null ? null : racketHand.GetComponent<Renderer>();
                return handleRenderer == null || handRenderer == null
                    ? float.PositiveInfinity
                    : Vector3.Distance(handleRenderer.bounds.center, handRenderer.bounds.center);
            }
        }
        public int BoneCount => poses.Count;
        public float AnimationTime => animationTime;
        public StrokeMotion LastStrokeMotion => strokeMotion;
        public bool IsStrokePlaying => strokeRemaining > 0f;
        public bool IsPreparingStroke => strokePrepared && strokeRemaining <= 0f;
        public StrokeMotion PreparedStrokeMotion => preparedStrokeMotion;
        public Vector3 RacketVisualLocalPosition => GetRacketVisualLocalPosition();
        public float RacketVisualTravelDistance => hasRestRacketVisualPosition
            ? Vector3.Distance(restRacketVisualLocalPosition, RacketVisualLocalPosition)
            : 0f;
        public bool HasCompleteMotionBinding =>
            HasRenderer(head) &&
            HasRenderer(leftWrist) &&
            HasRenderer(rightWrist) &&
            HasRenderer(leftAnkle) &&
            HasRenderer(rightAnkle) &&
            (identity switch
            {
                AthleteIdentity.Lux => HasRenderer(tailBase),
                AthleteIdentity.Lucia => HasRenderer(leftWing) && HasRenderer(rightWing),
                AthleteIdentity.Charlotte => HasRenderer(tailBase),
                AthleteIdentity.Zephyr => HasRenderer(leftWing) && HasRenderer(rightWing),
                AthleteIdentity.Poko => HasRenderer(tailBase) && HasRenderer(headLeaf),
                _ => true
            });

        private void Awake()
        {
            if (!rigBuilt)
            {
                BuildRig(identity);
            }

            athlete = GetComponentInParent<TennisAthleteController>();
            ball = UnityEngine.Object.FindAnyObjectByType<TennisBallController>();
            ApplyRacketGripPose();
            CaptureRestPose();
            CaptureRacketRestPosition();
        }

        public void BuildRig(AthleteIdentity configuredIdentity)
        {
            identity = configuredIdentity;
            if (rigBuilt && rigRoot != null)
            {
                return;
            }

            Dictionary<string, Transform> parts = CollectParts();
            rigRoot = CreateBone("Rig_Root", Vector3.zero, transform);
            RigDimensions dimensions = GetRigDimensions(identity);

            hips = CreateBoneAt("Rig_Hips", dimensions.Hips, rigRoot);
            chest = CreateBoneAt("Rig_Chest", dimensions.Chest, hips);
            head = CreateBoneAt("Rig_Head", dimensions.Head, chest);
            leftShoulder = CreateBoneAt("Rig_LeftShoulder", new Vector3(-dimensions.ShoulderX, dimensions.ShoulderY, 0f), chest);
            leftElbow = CreateBoneAt("Rig_LeftElbow", new Vector3(-dimensions.ElbowX, dimensions.ElbowY, 0f), leftShoulder);
            leftWrist = CreateBoneAt("Rig_LeftWrist", new Vector3(-dimensions.WristX, dimensions.WristY, 0f), leftElbow);
            rightShoulder = CreateBoneAt("Rig_RightShoulder", new Vector3(dimensions.ShoulderX, dimensions.ShoulderY, 0f), chest);
            rightElbow = CreateBoneAt("Rig_RightElbow", new Vector3(dimensions.ElbowX, dimensions.ElbowY, 0f), rightShoulder);
            rightWrist = CreateBoneAt("Rig_RightWrist", new Vector3(dimensions.WristX, dimensions.WristY, 0f), rightElbow);
            leftHip = CreateBoneAt("Rig_LeftHip", new Vector3(-dimensions.LegX, dimensions.HipY, 0f), hips);
            leftKnee = CreateBoneAt("Rig_LeftKnee", new Vector3(-dimensions.LegX, dimensions.KneeY, 0f), leftHip);
            leftAnkle = CreateBoneAt("Rig_LeftAnkle", new Vector3(-dimensions.LegX, dimensions.AnkleY, 0f), leftKnee);
            rightHip = CreateBoneAt("Rig_RightHip", new Vector3(dimensions.LegX, dimensions.HipY, 0f), hips);
            rightKnee = CreateBoneAt("Rig_RightKnee", new Vector3(dimensions.LegX, dimensions.KneeY, 0f), rightHip);
            rightAnkle = CreateBoneAt("Rig_RightAnkle", new Vector3(dimensions.LegX, dimensions.AnkleY, 0f), rightKnee);

            Attach(parts, hips, "Shorts", "Waist_Dark", "Pelvis", "Haragake_Lower");
            Attach(parts, chest, "Torso", "Collar_Cyan", "Chest_Accent", "Chest_Accent_R", "Torso_Core", "Chest_Armor", "Upper_Chest", "Backpack", "Chest_Core", "Back_Spine_0", "Back_Spine_1", "Back_Spine_2", "Happi_Left", "Happi_Right", "Jet_Core");
            Attach(parts, head, "Head", "Muzzle", "Nose", "Left_Ear", "Right_Ear", "Left_Ear_Inner", "Right_Ear_Inner", "Left_Eye_Liner", "Right_Eye_Liner", "Left_Eye", "Right_Eye", "Left_Cheek_Tuft", "Right_Cheek_Tuft", "Hair_Tuft_0", "Hair_Tuft_1", "Hair_Tuft_2", "Hair_Tuft_3", "Hair_Tuft_4", "Helmet", "Visor", "Cockpit_Visor", "Jaw", "Left_Horn", "Right_Horn");
            AttachArm(parts, "L", leftShoulder, leftElbow, leftWrist);
            AttachArm(parts, "R", rightShoulder, rightElbow, rightWrist);
            AttachLeg(parts, "L", leftHip, leftKnee, leftAnkle);
            AttachLeg(parts, "R", rightHip, rightKnee, rightAnkle);

            if (identity == AthleteIdentity.Lux)
            {
                tailBase = CreateBoneAt("Rig_TailBase", new Vector3(0.05f, 1.30f, -0.18f), hips);
                tailMiddle = CreateBoneAt("Rig_TailMiddle", new Vector3(0.32f, 1.10f, -0.48f), tailBase);
                tailTip = CreateBoneAt("Rig_TailTip", new Vector3(0.55f, 0.92f, -0.62f), tailMiddle);
                Attach(parts, tailBase, "Tail_0", "Tail_Joint_0");
                Attach(parts, tailMiddle, "Tail_1", "Tail_Joint_1");
                Attach(parts, tailTip, "Tail_2", "Tail_Joint_2");
            }
            else if (identity == AthleteIdentity.Lucia)
            {
                leftWing = CreateBoneAt("Rig_LeftWing", new Vector3(-0.18f, 1.78f, -0.16f), chest);
                rightWing = CreateBoneAt("Rig_RightWing", new Vector3(0.18f, 1.78f, -0.16f), chest);
                Attach(parts, leftWing, "Left_Wing");
                Attach(parts, rightWing, "Right_Wing");
            }
            else if (identity == AthleteIdentity.Charlotte)
            {
                tailBase = CreateBoneAt("Rig_TailBase", new Vector3(0f, 1.32f, -0.16f), hips);
                tailMiddle = CreateBoneAt("Rig_TailMiddle", new Vector3(0.22f, 0.98f, -0.58f), tailBase);
                tailTip = CreateBoneAt("Rig_TailTip", new Vector3(0.46f, 0.58f, -0.82f), tailMiddle);
                Attach(parts, tailBase, "Tail_0");
                Attach(parts, tailMiddle, "Tail_1");
                Attach(parts, tailTip, "Tail_2", "Tail_3");
            }
            else if (identity == AthleteIdentity.Zephyr)
            {
                leftWing = CreateBoneAt("Rig_LeftWing", new Vector3(-0.28f, 2.02f, -0.18f), chest);
                rightWing = CreateBoneAt("Rig_RightWing", new Vector3(0.28f, 2.02f, -0.18f), chest);
                Attach(parts, leftWing, "Left_Wing", "Left_Wing_Tip");
                Attach(parts, rightWing, "Right_Wing", "Right_Wing_Tip");
            }
            else if (identity == AthleteIdentity.Poko)
            {
                tailBase = CreateBoneAt("Rig_TailBase", new Vector3(-0.12f, 1.04f, -0.2f), hips);
                tailMiddle = CreateBoneAt("Rig_TailMiddle", new Vector3(-0.55f, 1.12f, -0.42f), tailBase);
                tailTip = CreateBoneAt("Rig_TailTip", new Vector3(-0.9f, 1.34f, -0.48f), tailMiddle);
                Attach(parts, tailBase, "Tail_0");
                Attach(parts, tailMiddle, "Tail_1");
                Attach(parts, tailTip, "Tail_2", "Tail_3");
                headLeaf = CreateBoneAt("Rig_HeadLeaf", new Vector3(0f, 2.24f, 0.02f), head);
                Attach(parts, headLeaf, "Head_Leaf", "Leaf_Stem");
            }

            // The OBJ stores every part in model space, so pivot at the handle butt.
            Vector3 racketHandleButt = identity switch
            {
                AthleteIdentity.Lux => new Vector3(1.25f, 0.48f, 0f),
                AthleteIdentity.Bastion => new Vector3(1.48f, 0.53f, 0f),
                AthleteIdentity.Zephyr => new Vector3(1.08f, 0.56f, 0.12f),
                AthleteIdentity.Poko => new Vector3(0.88f, 0.48f, 0.12f),
                _ => new Vector3(1.03f, 0.57f, 0.12f)
            };
            parts.TryGetValue("Racket_Handle", out racketHandle);
            parts.TryGetValue(identity == AthleteIdentity.Bastion ? "R_Fist" : "R_Glove", out racketHand);
            racketGrip = CreateBoneAt("Rig_RacketGrip", racketHandleButt, rigRoot);
            foreach (KeyValuePair<string, Transform> pair in parts)
            {
                if (pair.Key.StartsWith("Racket_", StringComparison.Ordinal))
                {
                    pair.Value.SetParent(racketGrip, true);
                }
            }
            racketGrip.SetParent(rightWrist, true);
            ApplyRacketGripPose();

            rigBuilt = true;
            CaptureRestPose();
        }

        private void ApplyRacketGripPose()
        {
            if (racketGrip == null || rightWrist == null) return;
            if (racketGrip.parent != rightWrist) racketGrip.SetParent(rightWrist, true);

            // Put the lower handle inside the modeled glove/fist. Keeping the racket on the
            // wrist hierarchy is not enough visually if its handle misses the palm.
            racketGrip.localPosition = RacketGripPosition;
            racketGrip.localRotation = identity == AthleteIdentity.Bastion
                ? Quaternion.Euler(7f, 6f, -20f)
                : Quaternion.Euler(8f, 8f, -24f);
            AlignRacketHandleToHand();
        }

        private void AlignRacketHandleToHand()
        {
            Renderer handleRenderer = racketHandle == null ? null : racketHandle.GetComponent<Renderer>();
            Renderer handRenderer = racketHand == null ? null : racketHand.GetComponent<Renderer>();
            if (handleRenderer == null || handRenderer == null) return;

            // Imported OBJ submeshes retain model-space vertices, so their Transform origins do
            // not identify the visible handle or palm. Align the rendered geometry once, then
            // keep both under the same animated wrist hierarchy.
            racketGrip.position += handRenderer.bounds.center - handleRenderer.bounds.center;
        }

        private Vector3 RacketGripPosition => identity == AthleteIdentity.Bastion
            ? new Vector3(0f, -0.11f, 0.055f)
            : new Vector3(0f, -0.07f, 0.035f);

        public void SetAwakened(bool value)
        {
            awakened = value;
        }

        private static RigDimensions GetRigDimensions(AthleteIdentity identity)
        {
            return identity switch
            {
                AthleteIdentity.Lux => new RigDimensions(
                    new Vector3(0f, 1.38f, 0f), new Vector3(0f, 1.78f, 0f), new Vector3(0f, 2.24f, 0f),
                    0.45f, 1.92f, 0.68f, 1.48f, 0.77f, 1.08f, 0.24f, 1.32f, 0.83f, 0.25f),
                AthleteIdentity.Bastion => new RigDimensions(
                    new Vector3(0f, 1.47f, 0f), new Vector3(0f, 2.02f, 0f), new Vector3(0f, 2.39f, 0f),
                    0.67f, 2.24f, 0.84f, 1.65f, 0.88f, 1.18f, 0.31f, 1.42f, 0.98f, 0.34f),
                AthleteIdentity.Lucia => new RigDimensions(
                    new Vector3(0f, 1.22f, 0f), new Vector3(0f, 1.72f, 0f), new Vector3(0f, 2.28f, 0f),
                    0.43f, 1.92f, 0.65f, 1.54f, 0.76f, 1.15f, 0.22f, 1.2f, 0.69f, 0.2f),
                AthleteIdentity.Charlotte => new RigDimensions(
                    new Vector3(0f, 1.26f, 0f), new Vector3(0f, 1.76f, 0f), new Vector3(0f, 2.32f, 0f),
                    0.46f, 1.96f, 0.65f, 1.58f, 0.76f, 1.19f, 0.22f, 1.24f, 0.73f, 0.24f),
                AthleteIdentity.Zephyr => new RigDimensions(
                    new Vector3(0f, 1.34f, 0f), new Vector3(0f, 1.88f, 0f), new Vector3(0f, 2.43f, 0f),
                    0.5f, 2.08f, 0.7f, 1.63f, 0.79f, 1.2f, 0.24f, 1.3f, 0.77f, 0.22f),
                _ => new RigDimensions(
                    new Vector3(0f, 1.02f, 0f), new Vector3(0f, 1.43f, 0f), new Vector3(0f, 1.92f, 0f),
                    0.4f, 1.56f, 0.58f, 1.25f, 0.67f, 0.95f, 0.22f, 1f, 0.55f, 0.16f)
            };
        }

        public void PlayServeToss()
        {
            tossAccentRemaining = 0.38f;
        }

        public void PrepareStroke(ShotPower power, StrokeMotion motion)
        {
            if (motion == StrokeMotion.Serve || strokeRemaining > 0f)
            {
                return;
            }

            bool prepareStrong = power == ShotPower.Strong;
            if (strokePrepared && preparedStrokeMotion == motion && preparedStrongStroke == prepareStrong)
            {
                return;
            }

            preparedStrongStroke = prepareStrong;
            preparedStrokeMotion = motion;
            strokePrepared = true;
            strokePreparationTime = 0f;
        }

        public void CancelStrokePreparation()
        {
            strokePrepared = false;
            strokePreparationTime = 0f;
        }

        public void PlayStroke(ShotPower power, float horizontalAim, StrokeMotion motion)
        {
            bool continuePreparedMotion = strokePrepared &&
                                          preparedStrokeMotion == motion &&
                                          strokePreparationTime >= 0.04f;
            strongStroke = power == ShotPower.Strong;
            strokeMotion = motion;
            strokeSide = horizontalAim < -0.05f ? -1f : 1f;
            strokeDuration = motion == StrokeMotion.Serve ? 0.72f : strongStroke ? 0.56f : 0.48f;
            strokeStartProgress = continuePreparedMotion ? 0.18f : 0f;
            strokeRemaining = strokeDuration;
            CancelStrokePreparation();
        }

        public void PlayDive(Vector2 direction)
        {
            CancelStrokePreparation();
            diveDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.right;
        }

        public void PlaySignatureSpecial()
        {
            signatureSpecialRemaining = SignatureSpecialDuration;
        }

        private void LateUpdate()
        {
            if (!rigBuilt || athlete == null || poses.Count == 0)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            animationTime += deltaTime;
            strokeRemaining = Mathf.Max(0f, strokeRemaining - deltaTime);
            signatureSpecialRemaining = Mathf.Max(0f, signatureSpecialRemaining - deltaTime);
            if (strokePrepared)
            {
                strokePreparationTime += deltaTime;
            }
            tossAccentRemaining = Mathf.Max(0f, tossAccentRemaining - deltaTime);
            ResetTargets();

            float speed = athlete.PlanarSpeed;
            bool moving = speed > 0.12f;
            if (moving && !athlete.IsServeTossedMotion && !athlete.IsDivingMotion && strokeRemaining <= 0f)
            {
                ApplyLocomotion(speed, athlete.IsDashingMotion);
            }
            else
            {
                ApplyIdle();
            }

            if (athlete.IsServingMotion)
            {
                ApplyServeReady(athlete.IsServeTossedMotion);
            }
            if (strokePrepared && strokeRemaining <= 0f)
            {
                ApplyStrokePreparation();
            }
            if (strokeRemaining > 0f)
            {
                ApplyStroke();
            }
            if (athlete.IsDivingMotion)
            {
                ApplyDive();
            }
            ApplyHeadTracking();
            ApplyTailMotion(speed);
            ApplySignatureSpecial();
            BlendTargets(deltaTime);
        }

        private void ApplyIdle()
        {
            float weight = identity switch
            {
                AthleteIdentity.Lux => 1f,
                AthleteIdentity.Lucia => 1.08f,
                AthleteIdentity.Charlotte => 0.78f,
                AthleteIdentity.Zephyr => 0.62f,
                AthleteIdentity.Poko => 0.9f,
                _ => 0.42f
            };
            OffsetPosition(hips, new Vector3(0f, Mathf.Sin(animationTime * 2.1f) * 0.012f * weight, 0f));
            OffsetRotation(chest, new Vector3(Mathf.Sin(animationTime * 2.1f) * 1.2f * weight, 0f, 0f));
            OffsetRotation(leftShoulder, new Vector3(Mathf.Sin(animationTime * 1.7f) * 1.4f * weight, 0f, 1.5f));
            OffsetRotation(rightShoulder, new Vector3(-Mathf.Sin(animationTime * 1.7f) * 1.4f * weight, 0f, -1.5f));
        }

        private void ApplyLocomotion(float speed, bool dashing)
        {
            float characterWeight = identity switch
            {
                AthleteIdentity.Lucia => 1.08f,
                AthleteIdentity.Lux => 1f,
                AthleteIdentity.Charlotte => 0.88f,
                AthleteIdentity.Zephyr => 1.24f,
                AthleteIdentity.Poko => 1.02f,
                _ => 0.72f
            };
            locomotionPhase += Time.deltaTime * Mathf.Lerp(5.5f, 10.5f, Mathf.Clamp01(speed / 9f)) * characterWeight;
            float stride = Mathf.Sin(locomotionPhase) * (dashing ? 38f : 24f);
            float kneeLiftLeft = Mathf.Max(0f, -Mathf.Sin(locomotionPhase)) * (dashing ? 36f : 22f);
            float kneeLiftRight = Mathf.Max(0f, Mathf.Sin(locomotionPhase)) * (dashing ? 36f : 22f);
            OffsetRotation(leftHip, new Vector3(stride, 0f, 0f));
            OffsetRotation(rightHip, new Vector3(-stride, 0f, 0f));
            OffsetRotation(leftKnee, new Vector3(kneeLiftLeft, 0f, 0f));
            OffsetRotation(rightKnee, new Vector3(kneeLiftRight, 0f, 0f));
            OffsetRotation(leftShoulder, new Vector3(-stride * 0.72f, 0f, 4f));
            OffsetRotation(rightShoulder, new Vector3(stride * 0.72f, 0f, -4f));
            OffsetRotation(leftElbow, new Vector3(-Mathf.Abs(stride) * 0.25f, 0f, 0f));
            OffsetRotation(rightElbow, new Vector3(-Mathf.Abs(stride) * 0.25f, 0f, 0f));
            OffsetRotation(chest, new Vector3(dashing ? 10f : 4f, Mathf.Sin(locomotionPhase) * 2.5f, 0f));
            OffsetPosition(hips, new Vector3(0f, Mathf.Abs(Mathf.Cos(locomotionPhase)) * (dashing ? 0.055f : 0.03f), 0f));
        }

        private void ApplyServeReady(bool tossed)
        {
            float tossBlend = tossed ? 1f : tossAccentRemaining > 0f ? 0.75f : 0f;
            OffsetRotation(chest, new Vector3(-4f * tossBlend, -12f * tossBlend, 0f));
            OffsetRotation(leftShoulder, new Vector3(118f * tossBlend, 0f, -8f));
            OffsetRotation(leftElbow, new Vector3(-12f * tossBlend, 0f, 0f));
            OffsetRotation(rightShoulder, new Vector3(-58f * tossBlend, -18f * tossBlend, -58f * tossBlend));
            OffsetRotation(rightElbow, new Vector3(-72f * tossBlend, 0f, 0f));
            OffsetRotation(rightWrist, new Vector3(0f, 0f, -18f * tossBlend));
        }

        private void ApplyStroke()
        {
            float elapsed = 1f - strokeRemaining / Mathf.Max(0.01f, strokeDuration);
            float progress = Mathf.Lerp(strokeStartProgress, 1f, elapsed);
            float windup = Mathf.Clamp01(progress / 0.32f);
            float impact = Mathf.Clamp01((progress - 0.24f) / 0.34f);
            float follow = Mathf.Clamp01((progress - 0.58f) / 0.42f);
            float swing = Mathf.Lerp(-78f, 112f, Mathf.SmoothStep(0f, 1f, impact));
            swing = Mathf.Lerp(swing, 48f, follow);
            float strength = strongStroke ? 1f : 0.78f;
            if (strokeMotion == StrokeMotion.Serve)
            {
                ApplyServeStroke(swing, impact, strength);
            }
            else if (strokeMotion == StrokeMotion.Backhand)
            {
                ApplyBackhandStroke(impact, follow, strength);
            }
            else
            {
                ApplyForehandStroke(swing, impact, strength);
            }
            if (strokeMotion != StrokeMotion.Backhand)
            {
                OffsetPosition(hips, new Vector3(0f, Mathf.Sin(windup * Mathf.PI) * 0.035f, 0f));
            }
        }

        private void ApplyStrokePreparation()
        {
            float strength = preparedStrongStroke ? 1f : 0.82f;
            if (preparedStrokeMotion == StrokeMotion.Backhand)
            {
                ApplyBackhandPreparation(strength);
            }
        }

        private void ApplyBackhandPreparation(float strength)
        {
            OffsetRotation(chest, new Vector3(8f, 48f, -6f));
            OffsetRotation(hips, new Vector3(0f, 20f, 0f));
            OffsetPosition(hips, new Vector3(-0.035f, -0.055f, 0f));

            // A right-handed one-handed backhand coils the racket across the body. The free
            // arm stays near the throat during preparation, then opens as a counterbalance.
            OffsetRotation(rightShoulder, new Vector3(48f * strength, -18f, -104f));
            OffsetRotation(rightElbow, new Vector3(-68f, 0f, -8f));
            OffsetRotation(rightWrist, new Vector3(6f, 0f, 28f));
            OffsetRotation(leftShoulder, new Vector3(-18f, -8f, -35f));
            OffsetRotation(leftElbow, new Vector3(-42f, 0f, 10f));
            OffsetRotation(leftWrist, new Vector3(0f, 0f, 16f));
            OffsetRotation(leftHip, new Vector3(-10f, 0f, -4f));
            OffsetRotation(rightHip, new Vector3(14f, 0f, 5f));
            OffsetRotation(leftKnee, new Vector3(13f, 0f, 0f));
            OffsetRotation(rightKnee, new Vector3(18f, 0f, 0f));
        }

        private void ApplyServeStroke(float swing, float impact, float strength)
        {
            OffsetRotation(chest, new Vector3(Mathf.Lerp(-12f, 16f, impact), Mathf.Lerp(-20f, 26f, impact), 0f));
            OffsetRotation(rightShoulder, new Vector3(swing * strength, 0f, -62f));
            OffsetRotation(rightElbow, new Vector3(Mathf.Lerp(-82f, -18f, impact), 0f, 0f));
            OffsetRotation(leftShoulder, new Vector3(Mathf.Lerp(112f, 24f, impact), 0f, -8f));
            OffsetRotation(rightWrist, new Vector3(0f, 0f, Mathf.Lerp(-20f, 28f, impact)));
        }

        private void ApplyForehandStroke(float swing, float impact, float strength)
        {
            OffsetRotation(chest, new Vector3(4f, strokeSide * Mathf.Lerp(-28f, 38f, impact), strokeSide * 4f));
            OffsetRotation(rightShoulder, new Vector3(swing * strength, strokeSide * 18f, -58f * strength));
            OffsetRotation(rightElbow, new Vector3(Mathf.Lerp(-65f, -20f, impact), 0f, 0f));
            OffsetRotation(leftShoulder, new Vector3(-swing * 0.2f, 0f, 14f));
            OffsetRotation(hips, new Vector3(0f, strokeSide * Mathf.Lerp(-10f, 14f, impact), 0f));
            OffsetRotation(rightWrist, new Vector3(0f, 0f, Mathf.Lerp(-20f, 28f, impact)));
        }

        private void ApplyBackhandStroke(float impact, float follow, float strength)
        {
            float contact = Mathf.SmoothStep(0f, 1f, impact);
            float finish = Mathf.SmoothStep(0f, 1f, follow);
            float aimAccent = strokeSide * 4f;

            Vector3 chestRotation = Vector3.Lerp(new Vector3(8f, 48f, -6f), new Vector3(4f, -8f, 3f), contact);
            chestRotation = Vector3.Lerp(chestRotation, new Vector3(-5f, -34f, 8f), finish);
            chestRotation.y += aimAccent;
            OffsetRotation(chest, chestRotation);

            Vector3 hipRotation = Vector3.Lerp(new Vector3(0f, 20f, 0f), new Vector3(0f, -8f, 0f), contact);
            hipRotation = Vector3.Lerp(hipRotation, new Vector3(0f, -20f, 0f), finish);
            OffsetRotation(hips, hipRotation);
            OffsetPosition(hips, Vector3.Lerp(new Vector3(-0.035f, -0.055f, 0f), new Vector3(0.05f, 0.015f, 0f), contact));

            Vector3 racketShoulder = Vector3.Lerp(new Vector3(48f * strength, -18f, -104f), new Vector3(-28f * strength, -10f, -72f), contact);
            racketShoulder = Vector3.Lerp(racketShoulder, new Vector3(-56f * strength, 8f, -18f), finish);
            OffsetRotation(rightShoulder, racketShoulder);
            OffsetRotation(rightElbow, Vector3.Lerp(Vector3.Lerp(new Vector3(-68f, 0f, -8f), new Vector3(-24f, 0f, 8f), contact), new Vector3(-12f, 0f, 22f), finish));
            OffsetRotation(rightWrist, Vector3.Lerp(Vector3.Lerp(new Vector3(6f, 0f, 28f), new Vector3(0f, 0f, -8f), contact), new Vector3(0f, 0f, -34f), finish));

            OffsetRotation(leftShoulder, Vector3.Lerp(Vector3.Lerp(new Vector3(-18f, -8f, -35f), new Vector3(8f, 2f, 48f), contact), new Vector3(18f, 8f, 62f), finish));
            OffsetRotation(leftElbow, Vector3.Lerp(Vector3.Lerp(new Vector3(-42f, 0f, 10f), new Vector3(-18f, 0f, -8f), contact), new Vector3(-10f, 0f, -18f), finish));
            OffsetRotation(leftWrist, new Vector3(0f, 0f, Mathf.Lerp(16f, -20f, contact)));

            OffsetRotation(leftHip, new Vector3(Mathf.Lerp(-10f, 9f, contact), 0f, Mathf.Lerp(-4f, 3f, contact)));
            OffsetRotation(rightHip, new Vector3(Mathf.Lerp(14f, -7f, contact), 0f, Mathf.Lerp(5f, -3f, contact)));
            OffsetRotation(leftKnee, new Vector3(Mathf.Lerp(13f, 4f, contact), 0f, 0f));
            OffsetRotation(rightKnee, new Vector3(Mathf.Lerp(18f, 3f, contact), 0f, 0f));
        }

        private void ApplyDive()
        {
            float side = Mathf.Abs(diveDirection.x) > 0.1f ? Mathf.Sign(diveDirection.x) : 1f;
            SetRootTarget(new Vector3(0f, 0.16f, 0f), new Vector3(diveDirection.y * 28f, 0f, -side * 62f));
            OffsetRotation(leftShoulder, new Vector3(35f, 0f, -28f));
            OffsetRotation(rightShoulder, new Vector3(72f, 0f, -58f));
            OffsetRotation(leftHip, new Vector3(-20f, 0f, 12f));
            OffsetRotation(rightHip, new Vector3(28f, 0f, -12f));
        }

        private void ApplyHeadTracking()
        {
            if (ball == null || head == null)
            {
                return;
            }
            Vector3 localBall = transform.InverseTransformPoint(ball.transform.position);
            float yaw = Mathf.Clamp(Mathf.Atan2(localBall.x, Mathf.Max(0.25f, localBall.z)) * Mathf.Rad2Deg, -24f, 24f);
            float pitch = Mathf.Clamp(-Mathf.Atan2(localBall.y - 2.3f, Mathf.Max(0.5f, Mathf.Abs(localBall.z))) * Mathf.Rad2Deg, -16f, 18f);
            OffsetRotation(head, new Vector3(pitch, yaw, 0f));
        }

        private void ApplyTailMotion(float speed)
        {
            if (identity == AthleteIdentity.Zephyr && leftWing != null && rightWing != null)
            {
                bool deployed = athlete != null && athlete.IsDashingMotion || signatureSpecialRemaining > 0f;
                float deploy = deployed ? 1f : 0f;
                float flutter = Mathf.Sin(animationTime * 15f) * (deployed ? 2.5f : 0.8f);
                OffsetRotation(leftWing, new Vector3(-4f * deploy, -42f * deploy + flutter, 18f * deploy));
                OffsetRotation(rightWing, new Vector3(-4f * deploy, 42f * deploy - flutter, -18f * deploy));
                return;
            }
            if (identity == AthleteIdentity.Lucia && leftWing != null && rightWing != null)
            {
                float flutterRate = speed > 0.1f ? 13f : 5.5f;
                float flutter = Mathf.Sin(animationTime * flutterRate) * (speed > 0.1f ? 18f : 8f);
                OffsetRotation(leftWing, new Vector3(0f, -18f + flutter, -8f));
                OffsetRotation(rightWing, new Vector3(0f, 18f - flutter, 8f));
                return;
            }
            if (tailBase == null)
            {
                return;
            }
            float rate = speed > 0.1f ? 5.5f : 2.2f;
            float amplitude = (speed > 0.1f ? 18f : 8f) * (awakened ? 1.55f : 1f);
            float sway = Mathf.Sin(animationTime * rate) * amplitude;
            OffsetRotation(tailBase, new Vector3(awakened ? -26f : 0f, sway, 4f));
            OffsetRotation(tailMiddle, new Vector3(0f, -sway * 0.75f, 0f));
            OffsetRotation(tailTip, new Vector3(0f, sway * 0.45f, 0f));
        }

        private void ApplySignatureSpecial()
        {
            float normalized = signatureSpecialRemaining <= 0f
                ? 0f
                : Mathf.Clamp01(signatureSpecialRemaining / SignatureSpecialDuration);
            float pulse = Mathf.Sin((1f - normalized) * Mathf.PI);

            if (identity == AthleteIdentity.Poko)
            {
                float scale = 1f + pulse * 0.72f;
                if (rigRoot != null)
                    rigRoot.localScale = Vector3.Lerp(rigRoot.localScale, rootBaseScale * scale, 0.42f);
                OffsetRotation(chest, new Vector3(-10f * pulse, 0f, 0f));
                OffsetRotation(headLeaf, new Vector3(0f, 0f, 34f * pulse));
                return;
            }

            if (rigRoot != null)
                rigRoot.localScale = Vector3.Lerp(rigRoot.localScale, rootBaseScale, 0.34f);
            if (identity == AthleteIdentity.Zephyr && pulse > 0f)
            {
                OffsetRotation(chest, new Vector3(18f * pulse, 0f, 0f));
                OffsetPosition(hips, new Vector3(0f, 0.05f * pulse, 0.16f * pulse));
            }
        }

        private void CaptureRestPose()
        {
            poses.Clear();
            if (rigRoot == null)
            {
                return;
            }
            foreach (Transform bone in rigRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!bone.name.StartsWith("Rig_", StringComparison.Ordinal))
                {
                    continue;
                }
                poses.Add(new BonePose
                {
                    Transform = bone,
                    BasePosition = bone.localPosition,
                    BaseRotation = bone.localRotation,
                    TargetPosition = bone.localPosition,
                    TargetRotation = bone.localRotation
                });
            }
            rootBasePosition = transform.localPosition;
            rootBaseRotation = transform.localRotation;
            rootBaseScale = rigRoot == null ? Vector3.one : rigRoot.localScale;
        }

        private void CaptureRacketRestPosition()
        {
            Renderer handleRenderer = racketHandle == null ? null : racketHandle.GetComponent<Renderer>();
            hasRestRacketVisualPosition = handleRenderer != null;
            if (hasRestRacketVisualPosition)
            {
                restRacketVisualLocalPosition = transform.InverseTransformPoint(handleRenderer.bounds.center);
            }
        }

        private Vector3 GetRacketVisualLocalPosition()
        {
            Renderer handleRenderer = racketHandle == null ? null : racketHandle.GetComponent<Renderer>();
            return handleRenderer == null
                ? Vector3.zero
                : transform.InverseTransformPoint(handleRenderer.bounds.center);
        }

        private void ResetTargets()
        {
            foreach (BonePose pose in poses)
            {
                pose.TargetPosition = pose.BasePosition;
                pose.TargetRotation = pose.BaseRotation;
            }
            SetRootTarget(Vector3.zero, Vector3.zero);
        }

        private void BlendTargets(float deltaTime)
        {
            float responsiveness = identity switch
            {
                AthleteIdentity.Lucia => 20f,
                AthleteIdentity.Lux => 18f,
                AthleteIdentity.Charlotte => 14f,
                AthleteIdentity.Zephyr => 24f,
                AthleteIdentity.Poko => 16f,
                _ => 11f
            };
            float blend = 1f - Mathf.Exp(-responsiveness * deltaTime);
            foreach (BonePose pose in poses)
            {
                pose.Transform.localPosition = Vector3.Lerp(pose.Transform.localPosition, pose.TargetPosition, blend);
                pose.Transform.localRotation = Quaternion.Slerp(pose.Transform.localRotation, pose.TargetRotation, blend);
            }
        }

        private void OffsetPosition(Transform bone, Vector3 offset)
        {
            BonePose pose = FindPose(bone);
            if (pose != null) pose.TargetPosition = pose.BasePosition + offset;
        }

        private void OffsetRotation(Transform bone, Vector3 euler)
        {
            BonePose pose = FindPose(bone);
            if (pose != null) pose.TargetRotation = pose.BaseRotation * Quaternion.Euler(euler);
        }

        private void SetRootTarget(Vector3 positionOffset, Vector3 rotationEuler)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, rootBasePosition + positionOffset, 0.32f);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, rootBaseRotation * Quaternion.Euler(rotationEuler), 0.32f);
        }

        private BonePose FindPose(Transform bone)
        {
            if (bone == null) return null;
            for (int i = 0; i < poses.Count; i++) if (poses[i].Transform == bone) return poses[i];
            return null;
        }

        private static bool HasRenderer(Transform bone)
        {
            return bone != null && bone.GetComponentInChildren<Renderer>(true) != null;
        }

        private Dictionary<string, Transform> CollectParts()
        {
            var parts = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform || child.name.StartsWith("Rig_", StringComparison.Ordinal)) continue;
                if (!parts.ContainsKey(child.name)) parts.Add(child.name, child);
            }
            return parts;
        }

        private Transform CreateBone(string name, Vector3 localPosition, Transform parent)
        {
            GameObject bone = new GameObject(name);
            bone.transform.SetParent(parent, false);
            bone.transform.localPosition = localPosition;
            return bone.transform;
        }

        private Transform CreateBoneAt(string name, Vector3 modelPoint, Transform parent)
        {
            Transform bone = CreateBone(name, Vector3.zero, parent);
            bone.position = transform.TransformPoint(modelPoint);
            return bone;
        }

        private static void Attach(Dictionary<string, Transform> parts, Transform bone, params string[] names)
        {
            foreach (string name in names)
            {
                if (parts.TryGetValue(name, out Transform part)) part.SetParent(bone, true);
            }
        }

        private static void AttachArm(Dictionary<string, Transform> parts, string side, Transform shoulder, Transform elbow, Transform wrist)
        {
            Attach(parts, shoulder, side + "_Shoulder", side + "_Shoulder_Joint", side + "_Shoulder_Armor", side + "_Shoulder_Navy", side + "_Upper_Arm");
            Attach(parts, elbow, side + "_Elbow", side + "_Forearm", side + "_Forearm_Armor", side + "_Forearm_Panel", side + "_Forearm_Nozzle");
            Attach(parts, wrist, side + "_Wrist_Band", side + "_Glove", side + "_Fist");
        }

        private static void AttachLeg(Dictionary<string, Transform> parts, string side, Transform hip, Transform knee, Transform ankle)
        {
            Attach(parts, hip, side + "_Thigh", side + "_Thigh_Armor");
            Attach(parts, knee, side + "_Knee", side + "_Knee_Pad", side + "_Knee_Cap", side + "_Shin", side + "_Shin_Armor", side + "_Calf_Nozzle");
            Attach(parts, ankle, side + "_Ankle", side + "_Foot", side + "_Toe_Armor", side + "_Shoe", side + "_Shoe_Accent");
        }
    }
}
