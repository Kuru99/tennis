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
        [SerializeField] private Transform racketGrip;
        [SerializeField] private Transform racketHandle;
        [SerializeField] private Transform racketHand;

        private readonly List<BonePose> poses = new List<BonePose>();
        private TennisAthleteController athlete;
        private TennisBallController ball;
        private Vector3 rootBasePosition;
        private Quaternion rootBaseRotation;
        private float locomotionPhase;
        private float animationTime;
        private float strokeRemaining;
        private float strokeDuration;
        private float strokeSide = 1f;
        private StrokeMotion strokeMotion;
        private bool strongStroke;
        private float tossAccentRemaining;
        private Vector2 diveDirection;

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
        public bool HasCompleteMotionBinding =>
            HasRenderer(head) &&
            HasRenderer(leftWrist) &&
            HasRenderer(rightWrist) &&
            HasRenderer(leftAnkle) &&
            HasRenderer(rightAnkle) &&
            (identity != AthleteIdentity.Lux || HasRenderer(tailBase));

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
            Vector3 hipsPoint = identity == AthleteIdentity.Lux ? new Vector3(0f, 1.38f, 0f) : new Vector3(0f, 1.47f, 0f);
            Vector3 chestPoint = identity == AthleteIdentity.Lux ? new Vector3(0f, 1.78f, 0f) : new Vector3(0f, 2.02f, 0f);
            Vector3 headPoint = identity == AthleteIdentity.Lux ? new Vector3(0f, 2.24f, 0f) : new Vector3(0f, 2.39f, 0f);
            float shoulderX = identity == AthleteIdentity.Lux ? 0.45f : 0.67f;
            float shoulderY = identity == AthleteIdentity.Lux ? 1.92f : 2.24f;
            float elbowX = identity == AthleteIdentity.Lux ? 0.68f : 0.84f;
            float elbowY = identity == AthleteIdentity.Lux ? 1.48f : 1.65f;
            float wristX = identity == AthleteIdentity.Lux ? 0.77f : 0.88f;
            float wristY = identity == AthleteIdentity.Lux ? 1.08f : 1.18f;
            float legX = identity == AthleteIdentity.Lux ? 0.24f : 0.31f;
            float hipY = identity == AthleteIdentity.Lux ? 1.32f : 1.42f;
            float kneeY = identity == AthleteIdentity.Lux ? 0.83f : 0.98f;
            float ankleY = identity == AthleteIdentity.Lux ? 0.25f : 0.34f;

            hips = CreateBoneAt("Rig_Hips", hipsPoint, rigRoot);
            chest = CreateBoneAt("Rig_Chest", chestPoint, hips);
            head = CreateBoneAt("Rig_Head", headPoint, chest);
            leftShoulder = CreateBoneAt("Rig_LeftShoulder", new Vector3(-shoulderX, shoulderY, 0f), chest);
            leftElbow = CreateBoneAt("Rig_LeftElbow", new Vector3(-elbowX, elbowY, 0f), leftShoulder);
            leftWrist = CreateBoneAt("Rig_LeftWrist", new Vector3(-wristX, wristY, 0f), leftElbow);
            rightShoulder = CreateBoneAt("Rig_RightShoulder", new Vector3(shoulderX, shoulderY, 0f), chest);
            rightElbow = CreateBoneAt("Rig_RightElbow", new Vector3(elbowX, elbowY, 0f), rightShoulder);
            rightWrist = CreateBoneAt("Rig_RightWrist", new Vector3(wristX, wristY, 0f), rightElbow);
            leftHip = CreateBoneAt("Rig_LeftHip", new Vector3(-legX, hipY, 0f), hips);
            leftKnee = CreateBoneAt("Rig_LeftKnee", new Vector3(-legX, kneeY, 0f), leftHip);
            leftAnkle = CreateBoneAt("Rig_LeftAnkle", new Vector3(-legX, ankleY, 0f), leftKnee);
            rightHip = CreateBoneAt("Rig_RightHip", new Vector3(legX, hipY, 0f), hips);
            rightKnee = CreateBoneAt("Rig_RightKnee", new Vector3(legX, kneeY, 0f), rightHip);
            rightAnkle = CreateBoneAt("Rig_RightAnkle", new Vector3(legX, ankleY, 0f), rightKnee);

            Attach(parts, hips, "Shorts", "Waist_Dark", "Pelvis");
            Attach(parts, chest, "Torso", "Collar_Cyan", "Chest_Accent", "Chest_Accent_R", "Torso_Core", "Chest_Armor", "Upper_Chest", "Backpack", "Chest_Core", "Back_Spine_0", "Back_Spine_1", "Back_Spine_2");
            Attach(parts, head, "Head", "Muzzle", "Nose", "Left_Ear", "Right_Ear", "Left_Ear_Inner", "Right_Ear_Inner", "Left_Eye_Liner", "Right_Eye_Liner", "Left_Eye", "Right_Eye", "Left_Cheek_Tuft", "Right_Cheek_Tuft", "Hair_Tuft_0", "Hair_Tuft_1", "Hair_Tuft_2", "Hair_Tuft_3", "Hair_Tuft_4", "Helmet", "Visor", "Jaw");
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

            // The OBJ stores every part in model space, so pivot at the handle butt.
            Vector3 racketHandleButt = identity == AthleteIdentity.Lux
                ? new Vector3(1.25f, 0.48f, 0f)
                : new Vector3(1.48f, 0.53f, 0f);
            parts.TryGetValue("Racket_Handle", out racketHandle);
            parts.TryGetValue(identity == AthleteIdentity.Lux ? "R_Glove" : "R_Fist", out racketHand);
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
            racketGrip.localRotation = identity == AthleteIdentity.Lux
                ? Quaternion.Euler(8f, 8f, -24f)
                : Quaternion.Euler(7f, 6f, -20f);
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

        private Vector3 RacketGripPosition => identity == AthleteIdentity.Lux
            ? new Vector3(0f, -0.07f, 0.035f)
            : new Vector3(0f, -0.11f, 0.055f);

        public void PlayServeToss()
        {
            tossAccentRemaining = 0.38f;
        }

        public void PlayStroke(ShotPower power, float horizontalAim, StrokeMotion motion)
        {
            strongStroke = power == ShotPower.Strong;
            strokeMotion = motion;
            strokeSide = horizontalAim < -0.05f ? -1f : 1f;
            strokeDuration = motion == StrokeMotion.Serve ? 0.72f : strongStroke ? 0.52f : 0.42f;
            strokeRemaining = strokeDuration;
        }

        public void PlayDive(Vector2 direction)
        {
            diveDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.right;
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
            BlendTargets(deltaTime);
        }

        private void ApplyIdle()
        {
            float weight = identity == AthleteIdentity.Lux ? 1f : 0.42f;
            OffsetPosition(hips, new Vector3(0f, Mathf.Sin(animationTime * 2.1f) * 0.012f * weight, 0f));
            OffsetRotation(chest, new Vector3(Mathf.Sin(animationTime * 2.1f) * 1.2f * weight, 0f, 0f));
            OffsetRotation(leftShoulder, new Vector3(Mathf.Sin(animationTime * 1.7f) * 1.4f * weight, 0f, 1.5f));
            OffsetRotation(rightShoulder, new Vector3(-Mathf.Sin(animationTime * 1.7f) * 1.4f * weight, 0f, -1.5f));
        }

        private void ApplyLocomotion(float speed, bool dashing)
        {
            float characterWeight = identity == AthleteIdentity.Lux ? 1f : 0.72f;
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
            float progress = 1f - strokeRemaining / Mathf.Max(0.01f, strokeDuration);
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
            OffsetPosition(hips, new Vector3(0f, Mathf.Sin(windup * Mathf.PI) * 0.035f, 0f));
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
            float swing = Mathf.Lerp(76f, -108f, Mathf.SmoothStep(0f, 1f, impact));
            swing = Mathf.Lerp(swing, -38f, follow);
            float aimAccent = strokeSide * 4f;

            OffsetRotation(chest, new Vector3(6f, Mathf.Lerp(36f, -44f, impact) + aimAccent, Mathf.Lerp(-4f, 6f, impact)));
            OffsetRotation(rightShoulder, new Vector3(swing * strength, -24f, 54f * strength));
            OffsetRotation(rightElbow, new Vector3(Mathf.Lerp(-48f, -14f, impact), 0f, 12f));
            OffsetRotation(leftShoulder, new Vector3(swing * 0.72f * strength, -10f, 38f * strength));
            OffsetRotation(leftElbow, new Vector3(Mathf.Lerp(-36f, -10f, impact), 0f, -10f));
            OffsetRotation(hips, new Vector3(0f, Mathf.Lerp(14f, -18f, impact), 0f));
            OffsetRotation(rightWrist, new Vector3(0f, 0f, Mathf.Lerp(24f, -30f, impact)));
            OffsetRotation(leftWrist, new Vector3(0f, 0f, Mathf.Lerp(12f, -18f, impact)));
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
            if (identity != AthleteIdentity.Lux || tailBase == null)
            {
                return;
            }
            float rate = speed > 0.1f ? 5.5f : 2.2f;
            float amplitude = speed > 0.1f ? 18f : 8f;
            float sway = Mathf.Sin(animationTime * rate) * amplitude;
            OffsetRotation(tailBase, new Vector3(0f, sway, 4f));
            OffsetRotation(tailMiddle, new Vector3(0f, -sway * 0.75f, 0f));
            OffsetRotation(tailTip, new Vector3(0f, sway * 0.45f, 0f));
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
            float responsiveness = identity == AthleteIdentity.Lux ? 18f : 11f;
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
            Attach(parts, elbow, side + "_Elbow", side + "_Forearm", side + "_Forearm_Armor", side + "_Forearm_Panel");
            Attach(parts, wrist, side + "_Wrist_Band", side + "_Glove", side + "_Fist");
        }

        private static void AttachLeg(Dictionary<string, Transform> parts, string side, Transform hip, Transform knee, Transform ankle)
        {
            Attach(parts, hip, side + "_Thigh", side + "_Thigh_Armor");
            Attach(parts, knee, side + "_Knee", side + "_Knee_Pad", side + "_Knee_Cap", side + "_Shin", side + "_Shin_Armor");
            Attach(parts, ankle, side + "_Ankle", side + "_Foot", side + "_Toe_Armor", side + "_Shoe", side + "_Shoe_Accent");
        }
    }
}
